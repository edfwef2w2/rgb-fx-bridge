using RgbFx.LampArray.Protocol;

namespace RgbFx.Service.Sinks;

public interface ILightingSink
{
    Task ApplyAsync(LightingFrame frame, CancellationToken ct = default);
}

/// <summary>Future: feed Windows VHF / local preview only.</summary>
public sealed class NullSink : ILightingSink
{
    public Task ApplyAsync(LightingFrame frame, CancellationToken ct = default) => Task.CompletedTask;
}
