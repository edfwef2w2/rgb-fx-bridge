using RgbFx.LampArray.Protocol;
using RgbFx.Service.Client;
using RgbFx.Service.Config;
using RgbFx.Service.Mapping;
using RgbFx.Service.Sinks;
using RgbFx.Service.Sources;

namespace RgbFx.Service;

public sealed class BridgeHost
{
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public BridgeConfig Config { get; private set; } = BridgeConfig.Load();
    public string Status { get; private set; } = "idle";
    public string? LastError { get; private set; }
    public long FramesForwarded { get; private set; }

    public event Action? StateChanged;

    public void ReloadConfig()
    {
        Config = BridgeConfig.Load();
        StateChanged?.Invoke();
    }

    public async Task<string> ProbeAsync(RemoteTarget target, CancellationToken ct = default)
    {
        using var client = new MysticLightApiClient(target.BaseUrl, target.ApiToken);
        var health = await client.GetHealthAsync(ct).ConfigureAwait(false);
        if (health == null)
            return "no response";
        var zones = await client.GetZonesAsync(ct).ConfigureAwait(false);
        var zc = zones?.Zones?.Count ?? 0;
        return $"ok={health.Ok} connected={health.Connected} board={health.BoardId} zones={zc} api={health.ApiVersion}";
    }

    public void Start()
    {
        Stop();
        Config = BridgeConfig.Load();
        if (!Config.ForwardingEnabled)
        {
            Status = "forwarding disabled";
            StateChanged?.Invoke();
            return;
        }

        var target = Config.Targets.FirstOrDefault(t => t.Id == Config.ActiveTargetId)
                     ?? Config.Targets.FirstOrDefault();
        if (target == null)
        {
            Status = "no target configured";
            StateChanged?.Invoke();
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(target, _cts.Token));
        Status = $"starting → {target.BaseUrl}";
        StateChanged?.Invoke();
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _loop?.Wait(1000); } catch { /* ignore */ }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
        Status = "stopped";
        StateChanged?.Invoke();
    }

    private async Task RunAsync(RemoteTarget target, CancellationToken ct)
    {
        try
        {
            using var client = new MysticLightApiClient(target.BaseUrl, target.ApiToken);
            var zonesResp = await client.GetZonesAsync(ct).ConfigureAwait(false);
            var zoneInfos = zonesResp?.Zones?
                                .Where(z => !string.IsNullOrWhiteSpace(z.Name))
                                .ToList()
                            ?? new List<ZoneInfo>
                            {
                                new() { Name = "JRGB1" },
                                new() { Name = "JRAINBOW1", Async = true },
                                new() { Name = "ONBOARD" },
                            };
            var zoneNames = zoneInfos.Select(z => z.Name!).ToList();

            var zoneCount = Math.Max(zoneNames.Count, 1);
            var device = LampArrayDeviceDescription.CreateDefaultChassis(zoneCount);
            var hw = zonesResp?.Lighting?.HwLeds ?? 72;
            var mapper = new ZoneMapper(zoneInfos, hw, device);

            var caps = await client.GetCapabilitiesAsync(ct).ConfigureAwait(false);
            var hz = Config.MaxFrameHz;
            if (caps?.Limits?.MaxFrameHz > 0)
                hz = Math.Min(hz, caps.Limits.MaxFrameHz);

            var sink = new HttpApiSink(client, mapper, hz, mode: "Direct");
            var source = LightingSourceFactory.Create(Config.SourceMode, zoneCount);
            var zoneLabel = string.Join(",", zoneNames!);
            Status = $"forwarding ({Config.SourceMode}) zones=[{zoneLabel}] → {target.BaseUrl}";
            StateChanged?.Invoke();

            var lastUi = DateTime.MinValue;
            await foreach (var frame in source.ReadFramesAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    await sink.ApplyAsync(frame, ct).ConfigureAwait(false);
                    FramesForwarded = sink.FramesSent;
                    LastError = sink.LastError;
                    // Refresh UI ~2x/sec (not every 30 frames — that looked like "jumps of 30")
                    if ((DateTime.UtcNow - lastUi).TotalMilliseconds >= 500)
                    {
                        lastUi = DateTime.UtcNow;
                        var backend = source is PipeWithSimulatorFallbackSource fb
                            ? fb.ActiveBackend
                            : Config.SourceMode;
                        Status =
                            $"fwd {backend} z={sink.LastZoneCount}/{zoneCount} → {target.BaseUrl}";
                        StateChanged?.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    Status = $"error → {target.BaseUrl}";
                    StateChanged?.Invoke();
                    await Task.Delay(500, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Status = "stopped";
        }
        catch (Exception ex)
        {
            Status = "error";
            LastError = ex.Message;
        }
        finally
        {
            StateChanged?.Invoke();
        }
    }
}
