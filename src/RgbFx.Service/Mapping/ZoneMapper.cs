using RgbFx.LampArray.Protocol;
using RgbFx.Service.Client;

namespace RgbFx.Service.Mapping;

/// <summary>
/// Maps simulator lamp indices to MSI board zone names 1:1 by order from GET /api/v1/zones.
/// Always fills every board zone so the web UI does not look like "only one device moves".
/// </summary>
public sealed class ZoneMapper
{
    private readonly IReadOnlyList<string> _zones;

    public ZoneMapper(IEnumerable<string> zoneNames, LampArrayDeviceDescription? device = null)
    {
        _ = device; // naming comes from the board list only
        _zones = zoneNames
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (_zones.Count == 0)
            _zones = new[] { "JRGB1", "JRAINBOW1", "ONBOARD" };
    }

    public IReadOnlyList<string> ZoneNames => _zones;

    public IReadOnlyDictionary<string, ZoneColorSpec> MapFrame(LightingFrame frame)
    {
        var result = new Dictionary<string, ZoneColorSpec>(StringComparer.OrdinalIgnoreCase);
        var orderedColors = frame.Updates
            .OrderBy(u => u.LampId)
            .Select(u => u.Color)
            .ToList();

        if (orderedColors.Count == 0)
            orderedColors.Add(new RgbColor(255, 255, 255));

        for (var i = 0; i < _zones.Count; i++)
        {
            var c = orderedColors[i % orderedColors.Count];
            var brightness = (int)Math.Round(c.Intensity / 255.0 * 100.0);
            result[_zones[i]] = new ZoneColorSpec
            {
                Color = c.ToHex(),
                Brightness = Math.Clamp(brightness, 1, 100),
            };
        }

        return result;
    }
}
