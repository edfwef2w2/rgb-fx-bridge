using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RgbFx.Service.Client;

public sealed class MysticLightApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;

    public MysticLightApiClient(string baseUrl, string? apiToken = null, TimeSpan? timeout = null)
    {
        // Accept "192.168.x.x:17700" without scheme — Uri("host:port") throws invalid scheme.
        var root = RemoteUrl.Normalize(baseUrl);
        _http = new HttpClient { BaseAddress = new Uri(root), Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        if (!string.IsNullOrWhiteSpace(apiToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
    }

    public async Task<HealthResponse?> GetHealthAsync(CancellationToken ct = default)
    {
        using var res = await _http.GetAsync("api/v1/health", ct).ConfigureAwait(false);
        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<HealthResponse>(body, JsonOpts);
    }

    public async Task<CapabilitiesResponse?> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        using var res = await _http.GetAsync("api/v1/capabilities", ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CapabilitiesResponse>(JsonOpts, ct).ConfigureAwait(false);
    }

    public async Task<ZonesResponse?> GetZonesAsync(CancellationToken ct = default)
    {
        using var res = await _http.GetAsync("api/v1/zones", ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<ZonesResponse>(JsonOpts, ct).ConfigureAwait(false);
    }

    public async Task<FrameResponse?> PostFrameAsync(
        IReadOnlyDictionary<string, ZoneColorSpec> zones,
        string mode = "Direct",
        bool master = true,
        CancellationToken ct = default)
    {
        var payload = new
        {
            zones,
            mode = "Direct",
            master,
        };
        _ = mode;
        using var res = await _http.PostAsJsonAsync("api/v1/frame", payload, JsonOpts, ct).ConfigureAwait(false);
        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize<FrameResponse>(body, JsonOpts);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"frame failed HTTP {(int)res.StatusCode}: {body}");
        return parsed;
    }

    public void Dispose() => _http.Dispose();
}

public sealed class HealthResponse
{
    public bool Ok { get; set; }
    public bool Connected { get; set; }
    [JsonPropertyName("board_id")]
    public string? BoardId { get; set; }
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
    [JsonPropertyName("hostname")]
    public string? HostName { get; set; }
    [JsonPropertyName("identity")]
    public string? Identity { get; set; }
    [JsonPropertyName("uptime_sec")]
    public double UptimeSec { get; set; }
    public string? Error { get; set; }
    [JsonPropertyName("api_version")]
    public string? ApiVersion { get; set; }
}

public sealed class CapabilitiesResponse
{
    public string? ApiVersion { get; set; }
    public FeatureFlags? Features { get; set; }
    public LimitFlags? Limits { get; set; }
    public LightingInfo? Lighting { get; set; }
}

public sealed class LightingInfo
{
    [JsonPropertyName("aura_leds")]
    public int AuraLeds { get; set; } = 120;
    [JsonPropertyName("hw_leds")]
    public int HwLeds { get; set; } = 72;
    public string? Clip { get; set; } = "even";
}

public sealed class FeatureFlags
{
    public bool Frame { get; set; }
    public bool DirectMode { get; set; }
    [JsonPropertyName("per_led")]
    public bool PerLed { get; set; }
}

public sealed class LimitFlags
{
    public int MaxFrameHz { get; set; } = 20;
    public int MaxZonesPerFrame { get; set; } = 16;
}

public sealed class ZonesResponse
{
    public List<ZoneInfo>? Zones { get; set; }
    public LightingInfo? Lighting { get; set; }
    [JsonPropertyName("board_id")]
    public string? BoardId { get; set; }
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
    [JsonPropertyName("hostname")]
    public string? HostName { get; set; }
    [JsonPropertyName("identity")]
    public string? Identity { get; set; }
}

public sealed class ZoneInfo
{
    public string? Name { get; set; }
    public string? Display { get; set; }
    public bool Async { get; set; }
}

public sealed class ZoneColorSpec
{
    public string Color { get; set; } = "FFFFFF";
    public int Brightness { get; set; } = 100;
    [JsonPropertyName("led_count")]
    public int? LedCount { get; set; }
    public string[]? Leds { get; set; }
}

public sealed class FrameResponse
{
    public bool Success { get; set; }
    public List<string>? Applied { get; set; }
    public double LatencyMs { get; set; }
    public string? Error { get; set; }
}
