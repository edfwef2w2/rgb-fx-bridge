using RgbFx.LampArray.Protocol;

namespace RgbFx.Service.Sources;

/// <summary>
/// Prefer primary (pipe); force simulator with env RGBFX_FORCE_SIM=1.
/// </summary>
public sealed class CompositeSource : ILightingSource
{
    private readonly ILightingSource _primary;
    private readonly ILightingSource _fallback;

    public CompositeSource(ILightingSource primary, ILightingSource fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async IAsyncEnumerable<LightingFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("RGBFX_FORCE_SIM"), "1", StringComparison.Ordinal))
        {
            await foreach (var f in _fallback.ReadFramesAsync(ct).ConfigureAwait(false))
                yield return f;
            yield break;
        }

        await foreach (var f in _primary.ReadFramesAsync(ct).ConfigureAwait(false))
            yield return f;
    }
}
