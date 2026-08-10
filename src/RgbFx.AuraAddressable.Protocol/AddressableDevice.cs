using System.Text;
using RgbFx.LampArray.Protocol;

namespace RgbFx.AuraAddressable.Protocol;

/// <summary>
/// Device-side state machine for an Aura Addressable-style HID controller.
/// Applies host reports documented on the OpenRGB ASUS Aura USB page (clean-room).
/// </summary>
public sealed class AddressableDevice
{
    private readonly RgbColor[] _leds;
    private bool _updateActive;

    public AddressableDevice(int ledCount = 8, string firmwareString = HidConstants.DefaultFirmwareString)
    {
        if (ledCount < 1 || ledCount > 120)
            throw new ArgumentOutOfRangeException(nameof(ledCount));
        LedCount = ledCount;
        FirmwareString = firmwareString.Length > 16 ? firmwareString[..16] : firmwareString;
        _leds = new RgbColor[ledCount];
        for (var i = 0; i < ledCount; i++)
            _leds[i] = new RgbColor(0, 0, 0);
    }

    public int LedCount { get; }
    public string FirmwareString { get; }
    public bool UpdateActive => _updateActive;
    public IReadOnlyList<RgbColor> Leds => _leds;

    /// <summary>
    /// Process one host HID report (65 bytes). Returns a device reply report if any,
    /// and whether LED colors changed enough to emit a lighting frame.
    /// </summary>
    public ApplyResult ApplyHostReport(ReadOnlySpan<byte> report)
    {
        if (report.Length < 2 || report[0] != HidConstants.Magic)
            return ApplyResult.Ignore;

        return report[1] switch
        {
            HidConstants.CmdRequestFirmware => ApplyResult.WithReply(BuildFirmwareResponse()),
            HidConstants.CmdRequestConfigTable => ApplyResult.WithReply(BuildConfigTableResponse()),
            HidConstants.CmdStartUpdate => HandleStartUpdate(report),
            HidConstants.CmdSetColors => HandleSetColors(report),
            _ => ApplyResult.Ignore,
        };
    }

    public LightingFrame ToLightingFrame()
    {
        var updates = new List<LampColorUpdate>(LedCount);
        for (var i = 0; i < LedCount; i++)
        {
            updates.Add(new LampColorUpdate
            {
                LampId = i,
                Color = _leds[i],
            });
        }
        return new LightingFrame { Updates = updates };
    }

    /// <summary>
    /// Downsample LEDs into N zone colors (segment average). Used for MSI zone mapping.
    /// </summary>
    public IReadOnlyList<RgbColor> SampleZoneColors(int zoneCount)
    {
        zoneCount = Math.Max(1, zoneCount);
        var result = new RgbColor[zoneCount];
        for (var z = 0; z < zoneCount; z++)
        {
            var start = z * LedCount / zoneCount;
            var end = (z + 1) * LedCount / zoneCount;
            if (end <= start) end = start + 1;
            end = Math.Min(end, LedCount);
            long r = 0, g = 0, b = 0, n = 0;
            for (var i = start; i < end; i++)
            {
                r += _leds[i].R;
                g += _leds[i].G;
                b += _leds[i].B;
                n++;
            }
            if (n == 0) n = 1;
            result[z] = new RgbColor((byte)(r / n), (byte)(g / n), (byte)(b / n));
        }
        return result;
    }

    private ApplyResult HandleStartUpdate(ReadOnlySpan<byte> report)
    {
        // Wiki: EC 35 00 00 00 01
        _updateActive = true;
        return ApplyResult.Ok;
    }

    private ApplyResult HandleSetColors(ReadOnlySpan<byte> report)
    {
        // Wiki: EC 36 00 FF 00 | color data...
        // Color data layout (clean-room practical mapping used by many captures):
        // optional start index at [5], then RGB triplets. If short, treat [5..] as RGBRGB...
        if (report.Length < 8)
            return ApplyResult.Ok;

        var offset = 5;
        var startLed = 0;
        // Heuristic: if byte5 looks like a small LED index and remaining length fits, use it
        if (report[5] < LedCount && report.Length >= 5 + 1 + 3)
        {
            // Prefer direct RGB stream from offset 5 when host packs dense RGB
            // OpenRGB-style packs often place RGB starting at index 5 after 00 FF 00
        }

        var changed = false;
        var led = startLed;
        // Dense RGB from byte 5
        for (var i = 5; i + 2 < report.Length && led < LedCount; i += 3)
        {
            var c = new RgbColor(report[i], report[i + 1], report[i + 2]);
            if (!_leds[led].Equals(c))
            {
                _leds[led] = c;
                changed = true;
            }
            led++;
        }

        // If no RGB triplets fit, try GRB (some addressable strips)
        if (!changed && report.Length >= 8)
        {
            led = 0;
            for (var i = 5; i + 2 < report.Length && led < LedCount; i += 3)
            {
                // interpret as R G B still; alternate path reserved
                var c = new RgbColor(report[i], report[i + 1], report[i + 2]);
                _leds[led++] = c;
                changed = true;
            }
        }

        return changed ? ApplyResult.OkColorsChanged : ApplyResult.Ok;
    }

    private byte[] BuildFirmwareResponse()
    {
        var buf = new byte[HidConstants.ReportSize];
        buf[0] = HidConstants.Magic;
        buf[1] = HidConstants.RspFirmware;
        var bytes = Encoding.ASCII.GetBytes(FirmwareString);
        var n = Math.Min(bytes.Length, 16);
        bytes.AsSpan(0, n).CopyTo(buf.AsSpan(2));
        return buf;
    }

    private byte[] BuildConfigTableResponse()
    {
        var buf = new byte[HidConstants.ReportSize];
        buf[0] = HidConstants.Magic;
        buf[1] = HidConstants.RspConfigTable;
        // Minimal synthetic config: LED count at [3], channel count 1 at [4]
        buf[3] = (byte)Math.Min(255, LedCount);
        buf[4] = 0x01; // one addressable channel
        buf[5] = 0x01; // present
        return buf;
    }

    /// <summary>Build a host SetColors report for tests / offline demo injection.</summary>
    public static byte[] BuildSetColorsReport(IReadOnlyList<RgbColor> colors)
    {
        var buf = new byte[HidConstants.ReportSize];
        buf[0] = HidConstants.Magic;
        buf[1] = HidConstants.CmdSetColors;
        buf[2] = 0x00;
        buf[3] = 0xFF;
        buf[4] = 0x00;
        var i = 5;
        foreach (var c in colors)
        {
            if (i + 2 >= buf.Length) break;
            buf[i++] = c.R;
            buf[i++] = c.G;
            buf[i++] = c.B;
        }
        return buf;
    }

    public static byte[] BuildStartUpdateReport()
    {
        var buf = new byte[HidConstants.ReportSize];
        buf[0] = HidConstants.Magic;
        buf[1] = HidConstants.CmdStartUpdate;
        buf[5] = 0x01;
        return buf;
    }
}

public readonly struct ApplyResult
{
    public bool WasIgnored { get; private init; }
    public bool WasHandled { get; private init; }
    public bool ColorsChanged { get; private init; }
    public byte[]? ReplyReport { get; private init; }

    public static ApplyResult Ignore { get; } = new() { WasIgnored = true };
    public static ApplyResult Ok { get; } = new() { WasHandled = true };
    public static ApplyResult OkColorsChanged { get; } = new() { WasHandled = true, ColorsChanged = true };

    public static ApplyResult WithReply(byte[] reply) => new()
    {
        WasHandled = true,
        ReplyReport = reply,
    };
}
