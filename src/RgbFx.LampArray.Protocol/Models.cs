namespace RgbFx.LampArray.Protocol;

public readonly record struct RgbColor(byte R, byte G, byte B, byte Intensity = 255)
{
    public static RgbColor FromHex(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length < 6)
            throw new ArgumentException("Expected RRGGBB hex color", nameof(hex));
        return new RgbColor(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }

    public string ToHex() => $"{R:X2}{G:X2}{B:X2}";
}

public sealed class LampInfo
{
    public required int LampId { get; init; }
    public required int PositionX { get; init; }
    public required int PositionY { get; init; }
    public required int PositionZ { get; init; }
    public uint UpdateLatencyUs { get; init; } = 10_000;
    public uint Purposes { get; init; } = 0x01; // Control
    public byte InputBinding { get; init; }
    public byte LampLevelCounts { get; init; } = 3; // RGB
    public byte IsProgrammable { get; init; } = 1;
    public string? SuggestedZoneName { get; init; }
}

public sealed class LampArrayDeviceDescription
{
    public required string Name { get; init; }
    public LampArrayKind Kind { get; init; } = LampArrayKind.Chassis;
    public ushort MinUpdateIntervalMs { get; init; } = 33; // ~30 Hz cap at HID layer
    public required IReadOnlyList<LampInfo> Lamps { get; init; }

    public static LampArrayDeviceDescription CreateDefaultChassis(int lampCount = 4)
    {
        var lamps = new List<LampInfo>(lampCount);
        for (var i = 0; i < lampCount; i++)
        {
            lamps.Add(new LampInfo
            {
                LampId = i,
                PositionX = i * 40,
                PositionY = 0,
                PositionZ = 0,
                SuggestedZoneName = i switch
                {
                    0 => "JRGB1",
                    1 => "JRAINBOW1",
                    2 => "JRAINBOW2",
                    3 => "ONBOARD",
                    _ => $"ZONE{i}",
                },
            });
        }

        return new LampArrayDeviceDescription
        {
            Name = "RgbFx Virtual Chassis",
            Kind = LampArrayKind.Chassis,
            Lamps = lamps,
        };
    }
}

public sealed class LampColorUpdate
{
    public required int LampId { get; init; }
    public required RgbColor Color { get; init; }
}

/// <summary>One host→device lighting frame after parsing HID reports.</summary>
public sealed class LightingFrame
{
    public required IReadOnlyList<LampColorUpdate> Updates { get; init; }
    public bool AutonomousMode { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
