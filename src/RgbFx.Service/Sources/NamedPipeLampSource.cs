using System.IO.Pipes;
using RgbFx.LampArray.Protocol;

namespace RgbFx.Service.Sources;

/// <summary>
/// Reads raw HID-style reports from the VHF driver user-mode channel.
/// Pipe name must match driver/service contract: \\.\pipe\RgbFxLampArray
/// </summary>
public sealed class NamedPipeLampSource : ILightingSource
{
    public const string DefaultPipeName = "RgbFxLampArray";

    private readonly string _pipeName;

    public NamedPipeLampSource(string pipeName = DefaultPipeName)
    {
        _pipeName = pipeName;
    }

    public async IAsyncEnumerable<LightingFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeClientStream? pipe = null;
            try
            {
                pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.In, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(2000, ct).ConfigureAwait(false);
                var buffer = new byte[256];
                while (!ct.IsCancellationRequested)
                {
                    var n = await pipe.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                    if (n <= 0)
                        break;
                    if (ReportCodec.TryParseMultiUpdate(buffer.AsSpan(0, n), out var frame) && frame != null)
                        yield return frame;
                    else if (ReportCodec.TryParseRangeUpdate(buffer.AsSpan(0, n), out frame) && frame != null)
                        yield return frame;
                    else if (ReportCodec.TryParseControl(buffer.AsSpan(0, n), out var auto))
                        yield return new LightingFrame
                        {
                            Updates = Array.Empty<LampColorUpdate>(),
                            AutonomousMode = auto,
                        };
                }
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch
            {
                // Driver not present — wait and retry
                try { await Task.Delay(2000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { yield break; }
            }
            finally
            {
                if (pipe != null)
                    await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
