using RgbFx.LampArray.Protocol;
using RgbFx.Service.Client;

namespace RgbFx.Service.Mapping;

/// <summary>
/// Lamps in order form one framebuffer. Async zones get the clipped leds[];
/// 12V / onboard zones get a single color.
/// </summary>
public sealed class ZoneMapper
{
    private readonly IReadOnlyList<ZoneInfo> _zones;
    private readonly int _hwLeds;

    public ZoneMapper(IEnumerable<string> zoneNames, LampArrayDeviceDescription? device = null)
        : this(zoneNames.Select(n => new ZoneInfo
        {
            Name = n,
            Async = n.Contains("RAINBOW", StringComparison.OrdinalIgnoreCase),
        }), hwLeds: 72, device)
    {
    }

    public ZoneMapper(IEnumerable<ZoneInfo> zones, int hwLeds = 72, LampArrayDeviceDescription? device = null)
    {
        _ = device;
        _hwLeds = Math.Clamp(hwLeds, 1, 500);
        _zones = zones
            .Where(z => !string.IsNullOrWhiteSpace(z.Name))
            .GroupBy(z => z.Name!, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        if (_zones.Count == 0)
            _zones = new[]
            {
                new ZoneInfo { Name = "JRGB1" },
                new ZoneInfo { Name = "JRAINBOW1", Async = true },
                new ZoneInfo { Name = "ONBOARD" },
            };
    }

    public IReadOnlyList<string> ZoneNames => _zones.Select(z => z.Name!).ToList();

    public IReadOnlyDictionary<string, ZoneColorSpec> MapFrame(LightingFrame frame)
    {
        var result = new Dictionary<string, ZoneColorSpec>(StringComparer.OrdinalIgnoreCase);
        var pixels = frame.Updates
            .OrderBy(u => u.LampId)
            .Select(u => u.Color.ToHex())
            .ToList();
        if (pixels.Count == 0)
            pixels.Add("FFFFFF");

        var clipped = FrameClip.Even(pixels, _hwLeds);
        var first = clipped[0];

        for (var i = 0; i < _zones.Count; i++)
        {
            var z = _zones[i];
            var name = z.Name!;
            if (z.Async)
            {
                result[name] = new ZoneColorSpec
                {
                    Color = first,
                    Brightness = 100,
                    LedCount = _hwLeds,
                    Leds = clipped,
                };
            }
            else
            {
                var color = pixels.Count == _zones.Count ? pixels[i] : first;
                result[name] = new ZoneColorSpec
                {
                    Color = color,
                    Brightness = 100,
                };
            }
        }

        return result;
    }
}
