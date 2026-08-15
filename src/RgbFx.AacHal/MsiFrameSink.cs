using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RgbFx.AacHal;

/// <summary>Latest-frame POST to /api/v1/frame. SetEffect must not wait on this.</summary>
public sealed class MsiFrameSink : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    private readonly HttpClient _http;
    private readonly object _gate = new();
    private Dictionary<string, FrameZoneSpec>? _pending;
    private uint _pendingAuraId;
    private bool _sending;

    public string? LastError { get; private set; }
    public long FramesSent { get; private set; }

    public MsiFrameSink()
    {
        var baseUrl = Environment.GetEnvironmentVariable("RGBFX_MSI_URL")
                      ?? Environment.GetEnvironmentVariable("MSI_RGB_URL")
                      ?? ReadUrlFile()
                      ?? "http://127.0.0.1:17700";
        if (!baseUrl.Contains("://", StringComparison.Ordinal))
            baseUrl = "http://" + baseUrl.TrimEnd('/');
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(3),
        };
        var token = Environment.GetEnvironmentVariable("MSI_RGB_API_TOKEN")
                    ?? Environment.GetEnvironmentVariable("RGBFX_API_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + token);
        LogAuraTempo();
    }

    public void ApplySolid(uint color)
    {
        var zone = ZoneCatalog.Load().FirstOrDefault()?.Name;
        if (string.IsNullOrWhiteSpace(zone))
            return;
        ApplyColors(new[] { zone }, new[] { color });
    }

    public void ApplyColors(IReadOnlyList<string> zones, uint[] colors, uint auraId = 0)
    {
        if (zones.Count == 0 || colors.Length == 0)
            return;

        var lighting = ZoneCatalog.LoadLighting();
        var catalog = ZoneCatalog.Load();
        var hex = new string[colors.Length];
        for (int i = 0; i < hex.Length; i++)
            hex[i] = ToHexRgb(colors[i]);

        var names = zones.Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0)
            return;

        var clipped = hex;
        var sync = clipped.Length > 0 ? clipped[0] : "000000";
        var map = new Dictionary<string, FrameZoneSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var entry = catalog.FirstOrDefault(z =>
                z.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var argb = entry is { IsAsync: true }
                       || name.Contains("RAINBOW", StringComparison.OrdinalIgnoreCase);
            var spec = new FrameZoneSpec
            {
                Color = sync,
                Brightness = 100,
                AuraId = (int)auraId,
            };
            if (argb)
            {
                spec.LedCount = clipped.Length;
                spec.Leds = clipped;
            }
            map[name] = spec;
        }

        if (map.Count == 0)
            return;

        bool startPump;
        lock (_gate)
        {
            _pending = map;
            _pendingAuraId = auraId;
            startPump = !_sending;
            if (startPump)
                _sending = true;
        }

        if (startPump)
            ThreadPool.QueueUserWorkItem(static s => ((MsiFrameSink)s!).Pump(), this);
    }

    public void Off(IReadOnlyList<string> zones)
    {
        if (zones.Count == 0)
            return;
        var black = new uint[zones.Count];
        ApplyColors(zones, black);
    }

    void Pump()
    {
        while (true)
        {
            Dictionary<string, FrameZoneSpec>? frame;
            uint auraId;
            lock (_gate)
            {
                frame = _pending;
                auraId = _pendingAuraId;
                _pending = null;
                if (frame is null)
                {
                    _sending = false;
                    return;
                }
            }

            PostFrame(frame, auraId);
        }
    }

    void PostFrame(Dictionary<string, FrameZoneSpec> zones, uint auraId)
    {
        try
        {
            var leds = zones.Values.FirstOrDefault(z => z.Leds is { Length: > 0 })?.Leds;
            var mcu = McuMap.Resolve(auraId, leds);
            var payload = new { zones, mode = mcu, master = true };
            Log($"post MCU={mcu} aura={auraId} n={zones.Count} leds={leds?.Length ?? 0}");
            using var res = _http.PostAsJsonAsync("api/v1/frame", payload, JsonOpts).GetAwaiter().GetResult();
            var body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode)
            {
                LastError = $"HTTP {(int)res.StatusCode}: {body}";
                Log("frame fail " + LastError);
                return;
            }

            FramesSent++;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log("frame exception: " + ex.Message);
        }
    }

    /// <summary>ASUS UI4 packing. RGBFX_COLOR_ORDER=bgr (default) or rgb.</summary>
    public static string ToHexRgb(uint c)
    {
        var r = (c >> 16) & 0xFF;
        var g = (c >> 8) & 0xFF;
        var b = c & 0xFF;
        var mode = Environment.GetEnvironmentVariable("RGBFX_COLOR_ORDER") ?? "bgr";
        if (string.Equals(mode, "bgr", StringComparison.OrdinalIgnoreCase))
        {
            b = (c >> 16) & 0xFF;
            g = (c >> 8) & 0xFF;
            r = c & 0xFF;
        }
        return $"{r:X2}{g:X2}{b:X2}";
    }

    static string? ReadUrlFile()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "RgbFx", "AacHal", "msi-url.txt");
            if (File.Exists(path))
            {
                var s = File.ReadAllText(path).Trim();
                if (s.Length > 0)
                    return s;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    static void LogAuraTempo()
    {
        try
        {
            var mode = "";
            var level = "";
            var status = "";
            var ini = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ASUS", "RogAura30", "Plugin_Status.ini");
            if (File.Exists(ini))
            {
                foreach (var raw in File.ReadAllLines(ini))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("PerformanceMode=", StringComparison.OrdinalIgnoreCase))
                        mode = line["PerformanceMode=".Length..];
                    else if (line.StartsWith("PerformanceLevel=", StringComparison.OrdinalIgnoreCase))
                        level = line["PerformanceLevel=".Length..];
                    else if (line.StartsWith("AuraPerformanceModeStatus=", StringComparison.OrdinalIgnoreCase))
                        status = line["AuraPerformanceModeStatus=".Length..];
                }
            }

            var rec = "";
            try
            {
                using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\WOW6432Node\ASUS\LS_performance");
                rec = Convert.ToString(k?.GetValue("PerformanceRecLevel") ?? "");
            }
            catch { /* ignore */ }

            Log($"aura tempo mode={mode} level={level} status={status} rec={rec}");
        }
        catch { /* ignore */ }
    }

    public void Dispose() => _http.Dispose();

    static void Log(string msg)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "RgbFx", "AacHal");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "aachal.log"),
                $"[{DateTime.Now:O}] {msg}{Environment.NewLine}");
        }
        catch { /* ignore */ }
    }
}

sealed class FrameZoneSpec
{
    [JsonPropertyName("color")]
    public string Color { get; set; } = "000000";

    [JsonPropertyName("brightness")]
    public int Brightness { get; set; } = 100;

    [JsonPropertyName("led_count")]
    public int? LedCount { get; set; }

    [JsonPropertyName("leds")]
    public string[]? Leds { get; set; }

    [JsonPropertyName("aura_id")]
    public int? AuraId { get; set; }
}

static class McuMap
{
    public static string Resolve(uint auraId, string[]? leds)
    {
        switch (auraId)
        {
            case 101: return "Static";
            case 1: return "Static";
            case 2: return "Breathing";
            case 3: return "Flashing";
            case 4: return "Color shift";
            case 5: return "Rainbow wave";
            case 8: return "Meteor";
            case 10: return "Lightning";
            case 11: return "Color wave";
            case 12: return "Color pulse";
            case 13: return "Fire";
        }

        if (leds is { Length: > 2 })
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in leds)
            {
                if (!string.IsNullOrEmpty(c))
                    seen.Add(c);
                if (seen.Count >= 3)
                    return "Rainbow wave";
            }
        }

        return "Static";
    }
}
