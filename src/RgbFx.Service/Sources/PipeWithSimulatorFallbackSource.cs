using RgbFx.LampArray.Protocol;

namespace RgbFx.Service.Sources;

/// <summary>
/// Tries the LampArray named pipe briefly; if the VHF driver is not installed,
/// falls back to the software simulator so users still see lights on the remote.
/// </summary>
public sealed class PipeWithSimulatorFallbackSource : ILightingSource
{
    private readonly int _lampCount;
    private readonly int _pipeConnectTimeoutMs;

    public string ActiveBackend { get; private set; } = "pipe";

    public PipeWithSimulatorFallbackSource(int lampCount = 4, int pipeConnectTimeoutMs = 1500)
    {
        _lampCount = lampCount;
        _pipeConnectTimeoutMs = pipeConnectTimeoutMs;
    }

    public async IAsyncEnumerable<LightingFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("RGBFX_FORCE_SIM"), "1", StringComparison.Ordinal))
        {
            ActiveBackend = "simulator(forced)";
            await foreach (var f in new SimulatorSource(_lampCount).ReadFramesAsync(ct).ConfigureAwait(false))
                yield return f;
            yield break;
        }

        // Quick probe: can we open the pipe at all?
        var pipeOk = await CanConnectPipeAsync(ct).ConfigureAwait(false);
        if (pipeOk)
        {
            ActiveBackend = "pipe";
            await foreach (var f in new NamedPipeLampSource().ReadFramesAsync(ct).ConfigureAwait(false))
                yield return f;
            yield break;
        }

        ActiveBackend = "simulator(fallback-no-driver)";
        await foreach (var f in new SimulatorSource(_lampCount).ReadFramesAsync(ct).ConfigureAwait(false))
            yield return f;
    }

    private async Task<bool> CanConnectPipeAsync(CancellationToken ct)
    {
        try
        {
            await using var pipe = new System.IO.Pipes.NamedPipeClientStream(
                ".", NamedPipeLampSource.DefaultPipeName,
                System.IO.Pipes.PipeDirection.In,
                System.IO.Pipes.PipeOptions.Asynchronous);
            await pipe.ConnectAsync(_pipeConnectTimeoutMs, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
