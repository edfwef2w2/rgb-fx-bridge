using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using RgbFx.Service.Client;

namespace RgbFx.Service.Discovery;

public sealed class DiscoveredDevice
{
    public required string BaseUrl { get; init; }
    public required string DisplayName { get; init; }
    public string? HostName { get; init; }
    public string? BoardId { get; init; }
    public bool Connected { get; init; }
    public bool Ok { get; init; }
    public string? ApiVersion { get; init; }
}

public sealed class ScanProgress
{
    public int Done { get; init; }
    public int Total { get; init; }
    public DiscoveredDevice? Found { get; init; }
}

/// <summary>
/// Probes connected IPv4 /24 subnets on port 17700 for msi-mystic-light-web
/// via GET /api/v1/health. Hue SSDP on :80 is intentionally ignored.
/// </summary>
public static class LanScanner
{
    const int Concurrency = 64;
    static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(350);
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static async Task<IReadOnlyList<DiscoveredDevice>> ScanAsync(
        IEnumerable<string>? extraUrls = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var candidates = CollectCandidates(extraUrls);
        var found = new ConcurrentDictionary<string, DiscoveredDevice>(StringComparer.OrdinalIgnoreCase);
        var done = 0;
        var total = candidates.Count;

        using var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(250),
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            MaxConnectionsPerServer = Concurrency,
            AutomaticDecompression = DecompressionMethods.None,
        };
        using var http = new HttpClient(handler) { Timeout = ProbeTimeout };
        using var gate = new SemaphoreSlim(Concurrency);

        var tasks = candidates.Select(async url =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                var device = await ProbeAsync(http, url, ct).ConfigureAwait(false);
                if (device is not null)
                    found[device.BaseUrl] = device;
                var n = Interlocked.Increment(ref done);
                progress?.Report(new ScanProgress { Done = n, Total = total, Found = device });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                var n = Interlocked.Increment(ref done);
                progress?.Report(new ScanProgress { Done = n, Total = total });
            }
            finally
            {
                gate.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // return whatever was found
        }

        return found.Values
            .OrderByDescending(d => d.Connected)
            .ThenBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static async Task<DiscoveredDevice?> ProbeAsync(string baseUrl, CancellationToken ct = default)
    {
        if (!RemoteUrl.TryNormalize(baseUrl, out var url, out _))
            return null;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        return await ProbeAsync(http, url, ct).ConfigureAwait(false);
    }

    static async Task<DiscoveredDevice?> ProbeAsync(HttpClient http, string baseUrl, CancellationToken ct)
    {
        var root = baseUrl.TrimEnd('/') + "/";
        using var req = new HttpRequestMessage(HttpMethod.Get, root + "api/v1/health");
        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!LooksLikeMystic(res, body, out var health) || health is null)
            return null;

        var hostName = health.HostName;
        var board = health.BoardId;
        if (string.IsNullOrWhiteSpace(board) || DeviceIdentity.IsPlaceholder(hostName))
        {
            var extra = await TryFetchIdentity(http, root, ct).ConfigureAwait(false);
            hostName ??= extra.hostName;
            board ??= extra.boardId;
        }

        string host;
        try { host = new Uri(root).Host; }
        catch { host = root; }

        var label = DeviceIdentity.ComposePrefix(hostName, board, host);
        return new DiscoveredDevice
        {
            BaseUrl = root.TrimEnd('/'),
            DisplayName = label,
            HostName = hostName,
            BoardId = board,
            Connected = health.Connected || health.Ok,
            Ok = health.Ok,
            ApiVersion = health.ApiVersion,
        };
    }

    public static string FormatName(string? hostName, string? boardId, string fallbackHost)
        => DeviceIdentity.ComposePrefix(hostName, boardId, fallbackHost);

    static async Task<(string? hostName, string? boardId)> TryFetchIdentity(
        HttpClient http, string root, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, root + "api/v1/zones");
            using var res = await http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var z = JsonSerializer.Deserialize<ZonesResponse>(body, JsonOpts);
            return (z?.HostName, z?.BoardId);
        }
        catch
        {
            return (null, null);
        }
    }

    static bool LooksLikeMystic(HttpResponseMessage res, string body, out HealthResponse? health)
    {
        health = null;
        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            health = JsonSerializer.Deserialize<HealthResponse>(body, JsonOpts);
        }
        catch
        {
            return false;
        }

        if (health is null)
            return false;

        if (res.Headers.TryGetValues("X-MSI-RGB-API-Version", out var vs))
        {
            var ver = vs.FirstOrDefault();
            if (!string.IsNullOrEmpty(ver))
                health.ApiVersion ??= ver;
        }

        if (!string.IsNullOrEmpty(health.ApiVersion) || !string.IsNullOrEmpty(health.BoardId) ||
            !string.IsNullOrEmpty(health.HostName))
            return true;

        // hardware-offline still returns connected/ok keys
        return body.Contains("\"connected\"", StringComparison.OrdinalIgnoreCase)
               || body.Contains("\"board_id\"", StringComparison.OrdinalIgnoreCase)
               || body.Contains("\"api_version\"", StringComparison.OrdinalIgnoreCase);
    }

    static List<string> CollectCandidates(IEnumerable<string>? extraUrls)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var priority = new List<string>();

        void Add(string url, bool first)
        {
            if (!RemoteUrl.TryNormalize(url, out var n, out _))
                return;
            if (urls.Add(n) && first)
                priority.Add(n);
        }

        if (extraUrls is not null)
        {
            foreach (var u in extraUrls)
                Add(u, first: true);
        }

        Add($"http://127.0.0.1:{RemoteUrl.DefaultPort}", first: true);

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                var b = ua.Address.GetAddressBytes();
                if (b[0] == 127 || (b[0] == 169 && b[1] == 254))
                    continue;

                Add($"http://{b[0]}.{b[1]}.{b[2]}.{b[3]}:{RemoteUrl.DefaultPort}", first: true);
                for (var i = 1; i <= 254; i++)
                    Add($"http://{b[0]}.{b[1]}.{b[2]}.{i}:{RemoteUrl.DefaultPort}", first: false);
            }
        }

        var rest = urls.Except(priority, StringComparer.OrdinalIgnoreCase);
        return priority.Concat(rest).ToList();
    }
}

public static class LanScanCache
{
    public static IReadOnlyList<DiscoveredDevice> Devices { get; private set; } = Array.Empty<DiscoveredDevice>();
    public static string Summary { get; private set; } = "";
    public static DateTime LastUtc { get; private set; }

    public static void Replace(IReadOnlyList<DiscoveredDevice> devices, string summary)
    {
        Devices = devices;
        Summary = summary;
        LastUtc = DateTime.UtcNow;
    }
}
