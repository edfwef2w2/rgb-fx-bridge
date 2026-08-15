using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RgbFx.AacHal;

public sealed class ZoneEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display")]
    public string? Display { get; set; }

    [JsonPropertyName("async")]
    public bool Async { get; set; }

    [JsonIgnore]
    public string Label =>
        !string.IsNullOrWhiteSpace(Display) ? Display! : Name;

    [JsonIgnore]
    public bool IsAsync =>
        Async || Name.Contains("RAINBOW", StringComparison.OrdinalIgnoreCase);
}

public sealed class LightingContract
{
    [JsonPropertyName("aura_leds")]
    public int AuraLeds { get; set; } = CapabilityBuilder.DefaultLedCount;

    [JsonPropertyName("hw_leds")]
    public int HwLeds { get; set; } = CapabilityBuilder.DefaultHwLeds;

    [JsonPropertyName("clip")]
    public string Clip { get; set; } = "even";

    [JsonPropertyName("partition_slots")]
    public List<string>? PartitionSlots { get; set; }

    [JsonPropertyName("partition_count")]
    public int PartitionCount { get; set; }
}

/// <summary>Board UI zones from GET /api/v1/zones. Shared file so HAL never invents names.</summary>
public static class ZoneCatalog
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "RgbFx", "AacHal", "zones.json");

    public static string LightingPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "RgbFx", "AacHal", "lighting.json");

    public static IReadOnlyList<ZoneEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return Array.Empty<ZoneEntry>();
            var list = JsonSerializer.Deserialize<List<ZoneEntry>>(File.ReadAllText(FilePath), JsonOpts);
            return Clean(list);
        }
        catch
        {
            return Array.Empty<ZoneEntry>();
        }
    }

    public static void Save(IEnumerable<ZoneEntry> zones)
    {
        var list = Clean(zones);
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(list, JsonOpts));
    }

    public static string? ReadSavedUrl()
    {
        try
        {
            var urlFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "RgbFx", "AacHal", "msi-url.txt");
            if (File.Exists(urlFile))
            {
                var s = File.ReadAllText(urlFile).Trim();
                if (s.Length > 0)
                    return s;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    public static IReadOnlyList<ZoneEntry> FetchAlways(string? baseUrl = null)
    {
        baseUrl = baseUrl
                  ?? Environment.GetEnvironmentVariable("RGBFX_MSI_URL")
                  ?? Environment.GetEnvironmentVariable("MSI_RGB_URL")
                  ?? ReadSavedUrl();
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            try
            {
                var root = baseUrl.Trim().TrimEnd('/') + "/";
                if (!root.Contains("://", StringComparison.Ordinal))
                    root = "http://" + root;
                using var http = new HttpClient { BaseAddress = new Uri(root), Timeout = TimeSpan.FromSeconds(3) };
                using var res = http.GetAsync("api/v1/zones").GetAwaiter().GetResult();
                var body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var parsed = JsonSerializer.Deserialize<ZonesDto>(body, JsonOpts);
                var list = Clean(parsed?.Zones);
                var lighting = parsed?.Lighting;
                if (lighting is null)
                    lighting = FetchCapabilities(http)?.Lighting;
                if (lighting is not null)
                {
                    SaveLighting(lighting);
                    lighting = LoadLighting();
                    Log($"handshake aura_leds={lighting.AuraLeds} hw_leds={lighting.HwLeds} clip={lighting.Clip} slots={lighting.PartitionCount}");
                }
                if (list.Count > 0)
                {
                    Save(list);
                    var host = parsed?.HostName;
                    var board = parsed?.BoardId;
                    if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(board))
                    {
                        var health = FetchHealth(http);
                        host ??= health?.HostName;
                        board ??= health?.BoardId;
                    }
                    PersistPrefix(host, board);
                    return list;
                }
            }
            catch
            {
                // fall back to last file
            }
        }

        return Load();
    }

    public static IReadOnlyList<ZoneEntry> LoadOrFetch(string? baseUrl)
        => FetchAlways(baseUrl);

    static IReadOnlyList<ZoneEntry> Clean(IEnumerable<ZoneEntry>? src)
    {
        if (src is null)
            return Array.Empty<ZoneEntry>();
        return src
            .Where(z => !string.IsNullOrWhiteSpace(z.Name))
            .GroupBy(z => z.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new ZoneEntry
            {
                Name = g.Key,
                Display = g.First().Display,
                Async = g.First().Async || g.Key.Contains("RAINBOW", StringComparison.OrdinalIgnoreCase),
            })
            .ToList();
    }

    public static LightingContract LoadLighting()
    {
        try
        {
            if (File.Exists(LightingPath))
            {
                var c = JsonSerializer.Deserialize<LightingContract>(File.ReadAllText(LightingPath), JsonOpts);
                if (c is not null)
                    return Sanitize(c);
            }
        }
        catch
        {
            // fall through
        }

        return Sanitize(new LightingContract());
    }

    public static void SaveLighting(LightingContract lighting)
    {
        var c = Sanitize(lighting);
        var dir = Path.GetDirectoryName(LightingPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(LightingPath, JsonSerializer.Serialize(c, JsonOpts));
    }

    public static LightingContract Sanitize(LightingContract? raw)
    {
        var c = raw ?? new LightingContract();
        if (c.AuraLeds < 1)
            c.AuraLeds = CapabilityBuilder.DefaultLedCount;
        if (c.AuraLeds > CapabilityBuilder.MaxReportedLeds)
            c.AuraLeds = CapabilityBuilder.MaxReportedLeds;
        if (c.HwLeds < 1)
            c.HwLeds = CapabilityBuilder.DefaultHwLeds;
        if (c.HwLeds > CapabilityBuilder.MaxReportedLeds)
            c.HwLeds = CapabilityBuilder.MaxReportedLeds;
        if (string.IsNullOrWhiteSpace(c.Clip))
            c.Clip = "even";
        if (c.PartitionSlots is null || c.PartitionSlots.Count == 0)
        {
            c.PartitionSlots = new List<string>
            {
                "JRGB1", "JRAINBOW1", "JCORSAIR", "JCOROUT", "ONBOARD",
                "ONBRD1", "ONBRD2", "ONBRD3", "ONBRD4", "ONBRD5",
                "ONBRD6", "ONBRD7", "ONBRD8", "ONBRD9", "ONBRD10", "JRGB2",
            };
        }
        c.PartitionCount = c.PartitionSlots.Count;
        return c;
    }

    public static string[] EvenResample(string[] src, int dest)
    {
        dest = Math.Max(1, dest);
        if (src.Length == dest)
            return src;
        if (src.Length == 0)
        {
            var blank = new string[dest];
            Array.Fill(blank, "000000");
            return blank;
        }

        if (src.Length < dest)
        {
            var padded = new string[dest];
            for (int i = 0; i < dest; i++)
                padded[i] = i < src.Length ? src[i] : src[^1];
            return padded;
        }

        var nIn = src.Length;
        var clipped = new string[dest];
        for (int i = 0; i < dest; i++)
            clipped[i] = src[(int)((long)i * nIn / dest)];
        return clipped;
    }

    public static string ComposePrefix(string? hostName, string? boardId)
    {
        var host = Token(hostName, firstLabel: true);
        var board = Token(boardId, firstLabel: false);
        if (string.IsNullOrEmpty(host) && string.IsNullOrEmpty(board))
            return "device";
        if (string.IsNullOrEmpty(board))
            return host!;
        if (string.IsNullOrEmpty(host))
            return board;
        return host + "-" + board;
    }

    static ZonesDto? FetchHealth(HttpClient http)
    {
        try
        {
            using var res = http.GetAsync("api/v1/health").GetAwaiter().GetResult();
            var body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<ZonesDto>(body, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    static CapsDto? FetchCapabilities(HttpClient http)
    {
        try
        {
            using var res = http.GetAsync("api/v1/capabilities").GetAwaiter().GetResult();
            var body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<CapsDto>(body, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    static void Log(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "aachal.log"),
                $"[{DateTime.Now:O}] {msg}{Environment.NewLine}");
        }
        catch { /* ignore */ }
    }

    static void PersistPrefix(string? hostName, string? boardId)
    {
        try
        {
            var prefix = ComposePrefix(hostName, boardId);
            if (string.IsNullOrEmpty(prefix))
                return;
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "display-name.txt"), prefix);
        }
        catch { /* ignore */ }
    }

    static string? Token(string? raw, bool firstLabel)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var s = raw.Trim();
        if (firstLabel)
        {
            var dot = s.IndexOf('.');
            if (dot > 0)
                s = s[..dot];
        }

        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_')
                sb.Append(c);
            else if (c is ' ' or '.')
                sb.Append('-');
        }

        var t = sb.ToString().Trim('-', '_');
        if (t.Length > 48)
            t = t[..48];
        return t.Length > 0 ? t : null;
    }

    sealed class ZonesDto
    {
        public List<ZoneEntry>? Zones { get; set; }

        [JsonPropertyName("board_id")]
        public string? BoardId { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("hostname")]
        public string? HostName { get; set; }

        [JsonPropertyName("identity")]
        public string? Identity { get; set; }

        [JsonPropertyName("lighting")]
        public LightingContract? Lighting { get; set; }
    }

    sealed class CapsDto
    {
        [JsonPropertyName("lighting")]
        public LightingContract? Lighting { get; set; }
    }
}
