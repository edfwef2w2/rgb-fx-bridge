namespace RgbFx.LampArray.Protocol;

/// <summary>
/// Encode/decode LampArray HID reports used by the virtual device.
/// Layout follows Microsoft sample devices (Arduino / RP2040 HID for Windows)
/// and HUT Lighting page semantics — fixed report IDs defined in <see cref="HidUsages"/>.
/// </summary>
public static class ReportCodec
{
    /// <summary>
    /// LampArrayAttributesReport (Feature IN): lamp count, size mm, kind, min interval.
    /// </summary>
    public static byte[] BuildAttributesReport(LampArrayDeviceDescription device)
    {
        // ReportId, lampCount(2), width(4), height(4), depth(4), kind(1), minInterval(4)
        var buf = new byte[20];
        buf[0] = HidUsages.ReportIdAttributes;
        BitConverter.TryWriteBytes(buf.AsSpan(1), (ushort)device.Lamps.Count);
        // bounding box in micrometers (placeholder chassis layout)
        BitConverter.TryWriteBytes(buf.AsSpan(3), 200_000);  // width
        BitConverter.TryWriteBytes(buf.AsSpan(7), 50_000);   // height
        BitConverter.TryWriteBytes(buf.AsSpan(11), 20_000);  // depth
        buf[15] = (byte)device.Kind;
        BitConverter.TryWriteBytes(buf.AsSpan(16), (uint)(device.MinUpdateIntervalMs * 1000));
        return buf;
    }

    /// <summary>Parse Multi-Update report: ReportId, count, lampIds[], colors RGBI[]</summary>
    public static bool TryParseMultiUpdate(ReadOnlySpan<byte> report, out LightingFrame? frame)
    {
        frame = null;
        if (report.Length < 3 || report[0] != HidUsages.ReportIdMultiUpdate)
            return false;

        var count = report[1];
        // flags byte optional at [2]; lamp entries follow
        var offset = 3;
        var updates = new List<LampColorUpdate>(count);
        for (var i = 0; i < count; i++)
        {
            if (offset + 5 > report.Length)
                break;
            var lampId = report[offset];
            var r = report[offset + 1];
            var g = report[offset + 2];
            var b = report[offset + 3];
            var intensity = report[offset + 4];
            updates.Add(new LampColorUpdate
            {
                LampId = lampId,
                Color = new RgbColor(r, g, b, intensity),
            });
            offset += 5;
        }

        frame = new LightingFrame { Updates = updates, AutonomousMode = false };
        return true;
    }

    /// <summary>Parse Range-Update: ReportId, startId, endId, RGBI</summary>
    public static bool TryParseRangeUpdate(ReadOnlySpan<byte> report, out LightingFrame? frame)
    {
        frame = null;
        if (report.Length < 7 || report[0] != HidUsages.ReportIdRangeUpdate)
            return false;

        var start = report[1];
        var end = report[2];
        var color = new RgbColor(report[3], report[4], report[5], report[6]);
        var updates = new List<LampColorUpdate>();
        for (var id = start; id <= end; id++)
            updates.Add(new LampColorUpdate { LampId = id, Color = color });

        frame = new LightingFrame { Updates = updates };
        return true;
    }

    /// <summary>Control report: autonomous mode bit.</summary>
    public static bool TryParseControl(ReadOnlySpan<byte> report, out bool autonomous)
    {
        autonomous = false;
        if (report.Length < 2 || report[0] != HidUsages.ReportIdControl)
            return false;
        // bit0 = AutonomousMode
        autonomous = (report[1] & 0x01) != 0;
        return true;
    }

    public static bool TryParseAny(ReadOnlySpan<byte> report, out LightingFrame? frame, out bool? autonomous)
    {
        frame = null;
        autonomous = null;
        if (report.IsEmpty)
            return false;

        switch (report[0])
        {
            case HidUsages.ReportIdMultiUpdate:
                return TryParseMultiUpdate(report, out frame);
            case HidUsages.ReportIdRangeUpdate:
                return TryParseRangeUpdate(report, out frame);
            case HidUsages.ReportIdControl:
                if (!TryParseControl(report, out var auto))
                    return false;
                autonomous = auto;
                frame = new LightingFrame
                {
                    Updates = Array.Empty<LampColorUpdate>(),
                    AutonomousMode = auto,
                };
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Minimal HID report descriptor blob for a Chassis LampArray (documentation + driver share).
    /// Full vendor descriptor may be refined against ArduinoHidForWindows samples during driver bring-up.
    /// </summary>
    public static ReadOnlySpan<byte> GetReportDescriptor() => ReportDescriptorBytes.Descriptor;
}
