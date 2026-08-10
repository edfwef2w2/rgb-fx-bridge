namespace RgbFx.Service.Sources;

/// <summary>
/// Creates lighting sources for Windows Dynamic Lighting / software simulator.
/// </summary>
public static class LightingSourceFactory
{
    public static readonly string[] AllModes =
    {
        "auto",
        "simulator",
        "pipe",
    };

    public static ILightingSource Create(string? mode, int lampOrLedCount = 4)
    {
        mode = (mode ?? "auto").Trim().ToLowerInvariant();
        return mode switch
        {
            "simulator" => new SimulatorSource(lampOrLedCount),
            // Pure pipe: only works when VHF driver is installed; otherwise no frames
            "pipe" => new NamedPipeLampSource(),
            // auto / default: try pipe, fall back to software rainbow so remotes still light up
            _ => new PipeWithSimulatorFallbackSource(lampOrLedCount),
        };
    }
}
