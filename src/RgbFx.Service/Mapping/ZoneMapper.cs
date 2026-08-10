using RgbFx.LampArray.Protocol;
using RgbFx.Service.Client;

namespace RgbFx.Service.Mapping;

/// <summary>
/// Maps LampArray lamp IDs to MSI zone names. Default: last-write wins per zone.
/// </summary>
public sealed class ZoneMapper
{
    private readonly Dictionary<int, string> _lampToZone;

    public ZoneMapper(IEnumerable<string> zoneNames, LampArrayDeviceDescription? device = null)
    {
        var zones = zoneNames.Where(z => !string.IsNullOrWhiteSpace(z)).ToList();
        if (zones.Count == 0)
            zones = new List<string> { "JRGB1", "JRAINBOW1", "JRAINBOW2", "ONBOARD" };

        _lampToZone = new Dictionary<int, string>();
        if (device != null)
        {
            foreach (var lamp in device.Lamps)
            {
                var name = !string.IsNullOrEmpty(lamp.SuggestedZoneName)
                    ? lamp.SuggestedZoneName!
                    : zones[lamp.LampId % zones.Count];
                // Prefer real zone list membership
                if (!zones.Contains(name))
                    name = zones[lamp.LampId % zones.Count];
                _lampToZone[lamp.LampId] = name;
            }
        }
        else
        {
            for (var i = 0; i < Math.Max(zones.Count, 8); i++)
                _lampToZone[i] = zones[i % zones.Count];
        }
    }

    public IReadOnlyDictionary<string, ZoneColorSpec> MapFrame(LightingFrame frame)
    {
        var result = new Dictionary<string, ZoneColorSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in frame.Updates)
        {
            if (!_lampToZone.TryGetValue(u.LampId, out var zone))
                continue;
            var brightness = (int)Math.Round(u.Color.Intensity / 255.0 * 100.0);
            result[zone] = new ZoneColorSpec
            {
                Color = u.Color.ToHex(),
                Brightness = Math.Clamp(brightness, 0, 100),
            };
        }
        return result;
    }
}
