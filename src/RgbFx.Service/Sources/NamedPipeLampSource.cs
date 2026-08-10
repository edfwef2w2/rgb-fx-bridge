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
        // Note: C# forbids yield return inside try blocks that have a catch clause.
        // Connect/read errors are handled without yielding inside those try/catch regions.
        while (!ct.IsCancellationRequested)
        {
            NamedPipeClientStream? pipe = null;
            var connected = false;

            try
            {
                pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.In, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(2000, ct).ConfigureAwait(false);
                connected = true;
            }
            catch (OperationCanceledException)
            {
                if (pipe != null)
                    await pipe.DisposeAsync().ConfigureAwait(false);
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
            {
                try
                {
                    await Task.Delay(2000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }

                continue;
            }

            // try/finally is allowed with yield; no catch on this block
            try
            {
                var buffer = new byte[256];
                while (!ct.IsCancellationRequested)
                {
                    int n;
                    var readFailed = false;
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
                        readFailed = true;
                        n = 0;
                    }

                    if (readFailed || n <= 0)
                        break;

                    LightingFrame? frame = null;
                    if (ReportCodec.TryParseMultiUpdate(buffer.AsSpan(0, n), out frame) && frame != null)
                    {
                        yield return frame;
                    }
                    else if (ReportCodec.TryParseRangeUpdate(buffer.AsSpan(0, n), out frame) && frame != null)
                    {
                        yield return frame;
                    }
                    else if (ReportCodec.TryParseControl(buffer.AsSpan(0, n), out var auto))
                    {
                        yield return new LightingFrame
                        {
                            Updates = Array.Empty<LampColorUpdate>(),
                            AutonomousMode = auto,
                        };
                    }
                }
            }
            finally
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
