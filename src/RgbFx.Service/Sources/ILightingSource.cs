using RgbFx.LampArray.Protocol;

namespace RgbFx.Service.Sources;

/// <summary>Produces lighting frames (VHF driver pipe, or software simulator).</summary>
public interface ILightingSource
{
    IAsyncEnumerable<LightingFrame> ReadFramesAsync(CancellationToken ct);
}
