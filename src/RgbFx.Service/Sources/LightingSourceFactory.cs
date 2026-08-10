using RgbFx.LampArray.Protocol;

namespace RgbFx.Service.Sources;

/// <summary>
/// Creates lighting sources. All modes remain available side-by-side.
/// </summary>
public static class LightingSourceFactory
{
    public static readonly string[] AllModes =
    {
        "auto",
        "simulator",
        "pipe",
        "aura-addressable-sim",
    };

    public static ILightingSource Create(string? mode, int lampOrLedCount = 4)
    {
        mode = (mode ?? "auto").Trim().ToLowerInvariant();
        return mode switch
        {
            "simulator" => new SimulatorSource(lampOrLedCount),
            "pipe" => new NamedPipeLampSource(),
            "aura-addressable-sim" or "aura-addressable" or "addressable" =>
                new AuraAddressableSimSource(ledCount: Math.Max(lampOrLedCount, 4)),
            // auto: prefer LampArray pipe; does not force aura mode
            _ => new CompositeSource(new NamedPipeLampSource(), new SimulatorSource(lampOrLedCount)),
        };
    }
}
