using System.Net.Http.Json;
using System.Text.Json;

namespace RgbFx.AacHal;

/// <summary>Forwards packed LED colors to msi-mystic-light-web /api/v1/frame.</summary>
public sealed class MsiFrameSink : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly HttpClient _http;
    private readonly string[] _zones;
    private DateTime _last = DateTime.MinValue;
    private readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(40);

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

        var z = Environment.GetEnvironmentVariable("RGBFX_ZONES");
        _zones = string.IsNullOrWhiteSpace(z)
            ? new[] { "JRGB1", "JRAINBOW1", "JRAINBOW2", "ONBOARD" }
            : z.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public void ApplyColors(uint[] colors)
    {
        if (colors.Length == 0) return;
        var now = DateTime.UtcNow;
        if (now - _last < _minInterval) return;

        try
        {
            var zones = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _zones.Length; i++)
            {
                var c = colors[i % colors.Length];
                zones[_zones[i]] = new { color = ToHexRgb(c), brightness = 100 };
            }

            var payload = new { zones, mode = "Static", master = true };
            using var res = _http.PostAsJsonAsync("api/v1/frame", payload, JsonOpts).GetAwaiter().GetResult();
            var body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode)
            {
                LastError = $"HTTP {(int)res.StatusCode}: {body}";
                Log($"frame fail {LastError}");
                return;
            }

            _last = now;
            FramesSent++;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log($"frame exception: {ex.Message}");
        }
    }

    public void ApplySolid(uint color) => ApplyColors(new[] { color });

    public void Off()
    {
        var zeros = new uint[_zones.Length];
        // still send black
        try
        {
            var zones = new Dictionary<string, object>();
            foreach (var z in _zones)
                zones[z] = new { color = "000000", brightness = 0 };
            var payload = new { zones, mode = "Static", master = false };
            _http.PostAsJsonAsync("api/v1/frame", payload, JsonOpts).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    /// <summary>ASUS UI4 color packing — try 0x00BBGGRR then 0x00RRGGBB.</summary>
    public static string ToHexRgb(uint c)
    {
        // Prefer low 24 bits as RRGGBB if high byte is 0
        var r = (c >> 16) & 0xFF;
        var g = (c >> 8) & 0xFF;
        var b = c & 0xFF;
        // Heuristic: many Aura paths use 0x00BBGGRR
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
