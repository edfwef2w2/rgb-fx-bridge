using System.IO.Pipes;
using RgbFx.AuraAddressable.Protocol;
using RgbFx.LampArray.Protocol;

namespace RgbFx.Service.Sources;

/// <summary>
/// Aura Addressable device-side simulation.
/// 1) Reads raw 65-byte host reports from named pipe <see cref="DefaultPipeName"/> (future VHF/driver).
/// 2) If pipe unavailable, injects demo SetColors reports through the same protocol parser
///    so the path can be tested without HID (still not "real" Aura enumeration).
/// </summary>
public sealed class AuraAddressableSimSource : ILightingSource
{
    public const string DefaultPipeName = "RgbFxAuraAddressable";

    private readonly AddressableDevice _device;
    private readonly int _demoIntervalMs;
    private readonly bool _allowDemoFallback;

    public AuraAddressableSimSource(int ledCount = 8, int demoIntervalMs = 120, bool allowDemoFallback = true)
    {
        _device = new AddressableDevice(ledCount);
        _demoIntervalMs = demoIntervalMs;
        _allowDemoFallback = allowDemoFallback;
    }

    public AddressableDevice Device => _device;

    public async IAsyncEnumerable<LightingFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Prefer pipe from virtual HID driver (L3)
        while (!ct.IsCancellationRequested)
        {
            var gotPipeFrame = false;
            await foreach (var frame in ReadFromPipeAsync(ct).ConfigureAwait(false))
            {
                gotPipeFrame = true;
                yield return frame;
            }

            if (gotPipeFrame || !_allowDemoFallback || ct.IsCancellationRequested)
            {
                if (ct.IsCancellationRequested)
                    yield break;
                // Pipe ended; brief pause then retry pipe
                try { await Task.Delay(1000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { yield break; }
                continue;
            }

            // Demo fallback: drive the protocol with synthetic host SetColors (not real Aura)
            await foreach (var frame in ReadDemoThroughProtocolAsync(ct).ConfigureAwait(false))
                yield return frame;
            yield break;
        }
    }

    private async IAsyncEnumerable<LightingFrame> ReadFromPipeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        NamedPipeClientStream? pipe = null;
        var connected = false;
        try
        {
            pipe = new NamedPipeClientStream(".", DefaultPipeName, PipeDirection.In, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(800, ct).ConfigureAwait(false);
            connected = true;
        }
        catch (OperationCanceledException)
        {
            if (pipe != null) await pipe.DisposeAsync().ConfigureAwait(false);
            yield break;
        }
        catch
        {
            if (pipe != null)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                pipe = null;
            }
        }

        if (!connected || pipe is null)
            yield break;

        try
        {
            var buffer = new byte[HidConstants.ReportSize];
            while (!ct.IsCancellationRequested)
            {
                int n;
                var failed = false;
                try
                {
                    n = await pipe.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch
                {
                    failed = true;
                    n = 0;
                }

                if (failed || n <= 0)
                    break;

                var result = _device.ApplyHostReport(buffer.AsSpan(0, Math.Min(n, buffer.Length)));
                if (result.ColorsChanged)
                    yield return _device.ToLightingFrame();
            }
        }
        finally
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async IAsyncEnumerable<LightingFrame> ReadDemoThroughProtocolAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        _device.ApplyHostReport(AddressableDevice.BuildStartUpdateReport());
        var t = 0.0;
        while (!ct.IsCancellationRequested)
        {
            var colors = new RgbColor[_device.LedCount];
            for (var i = 0; i < colors.Length; i++)
            {
                var hue = (t + i * (360.0 / colors.Length)) % 360.0;
                colors[i] = Hsv(hue);
            }

            var report = AddressableDevice.BuildSetColorsReport(colors);
            var result = _device.ApplyHostReport(report);
            if (result.ColorsChanged)
                yield return _device.ToLightingFrame();

            t += 10;
            try
            {
                await Task.Delay(_demoIntervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            if (ct.IsCancellationRequested)
                yield break;
        }
    }

    private static RgbColor Hsv(double h)
    {
        var c = 1.0;
        var x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }
        return new RgbColor(
            (byte)(r * 255),
            (byte)(g * 255),
            (byte)(b * 255));
    }
}
