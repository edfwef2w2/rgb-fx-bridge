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
            // Pure pipe: only works when VHF driver is installed; otherwise no frames
            "pipe" => new NamedPipeLampSource(),
            "aura-addressable-sim" or "aura-addressable" or "addressable" =>
                new AuraAddressableSimSource(ledCount: Math.Max(lampOrLedCount, 4)),
            // auto / default: try pipe, fall back to software rainbow so remotes still light up
            "auto" or _ => new PipeWithSimulatorFallbackSource(lampOrLedCount),
        };
    }
