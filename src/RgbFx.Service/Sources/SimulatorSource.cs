using RgbFx.LampArray.Protocol;

namespace RgbFx.Service.Sources;

/// <summary>
/// Software fallback that synthesizes a slow rainbow across lamps.
/// Used when VHF driver is not installed — validates HTTP path end-to-end.
/// Main product path is <see cref="NamedPipeLampSource"/> / driver.
/// </summary>
public sealed class SimulatorSource : ILightingSource
{
    private readonly int _lampCount;
    private readonly int _intervalMs;

    public SimulatorSource(int lampCount = 4, int intervalMs = 100)
    {
        _lampCount = lampCount;
        _intervalMs = intervalMs;
    }

    public async IAsyncEnumerable<LightingFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var t = 0.0;
        while (!ct.IsCancellationRequested)
        {
            var updates = new List<LampColorUpdate>(_lampCount);
            for (var i = 0; i < _lampCount; i++)
            {
                var hue = (t + i * (360.0 / _lampCount)) % 360.0;
                updates.Add(new LampColorUpdate
                {
                    LampId = i,
                    Color = HsvToRgb(hue, 1.0, 1.0),
                });
            }

            yield return new LightingFrame { Updates = updates };
            t += 8;
            try
            {
                await Task.Delay(_intervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // fall through; loop condition / final check exits cleanly
            }

            if (ct.IsCancellationRequested)
                yield break;
        }
    }

    private static RgbColor HsvToRgb(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
        var m = v - c;
        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }
        return new RgbColor(
            (byte)Math.Clamp((r + m) * 255, 0, 255),
            (byte)Math.Clamp((g + m) * 255, 0, 255),
            (byte)Math.Clamp((b + m) * 255, 0, 255));
    }
}
