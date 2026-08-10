using RgbFx.LampArray.Protocol;
using RgbFx.Service.Client;
using RgbFx.Service.Mapping;

namespace RgbFx.Service.Sinks;

public sealed class HttpApiSink : ILightingSink
{
    private readonly MysticLightApiClient _client;
    private readonly ZoneMapper _mapper;
    private readonly string _mode;
    private DateTime _lastSent = DateTime.MinValue;
    private readonly TimeSpan _minInterval;

    public HttpApiSink(MysticLightApiClient client, ZoneMapper mapper, int maxFrameHz = 15, string mode = "Direct")
    {
        _client = client;
        _mapper = mapper;
        _mode = mode;
        var hz = Math.Clamp(maxFrameHz, 1, 60);
        _minInterval = TimeSpan.FromMilliseconds(1000.0 / hz);
    }

    public string? LastError { get; private set; }
    public long FramesSent { get; private set; }

    public async Task ApplyAsync(LightingFrame frame, CancellationToken ct = default)
    {
        if (frame.AutonomousMode)
        {
            // Autonomous: turn lights off on remote (policy)
            try
            {
                await _client.PostFrameAsync(
                    new Dictionary<string, ZoneColorSpec>(),
                    _mode,
                    master: false,
                    ct).ConfigureAwait(false);
                LastError = null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _lastSent < _minInterval)
            return;

        var zones = _mapper.MapFrame(frame);
        if (zones.Count == 0)
            return;

        try
        {
            await _client.PostFrameAsync(zones, _mode, master: true, ct).ConfigureAwait(false);
            _lastSent = now;
            FramesSent++;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            throw;
        }
    }
}
