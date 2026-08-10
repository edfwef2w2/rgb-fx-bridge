using RgbFx.LampArray.Protocol;
using Xunit;

namespace RgbFx.LampArray.Protocol.Tests;

public class ReportCodecTests
{
    [Fact]
    public void AttributesReport_HasReportIdAndLampCount()
    {
        var device = LampArrayDeviceDescription.CreateDefaultChassis(4);
        var report = ReportCodec.BuildAttributesReport(device);
        Assert.Equal(HidUsages.ReportIdAttributes, report[0]);
        Assert.Equal(4, BitConverter.ToUInt16(report, 1));
        Assert.Equal((byte)LampArrayKind.Chassis, report[15]);
    }

    [Fact]
    public void MultiUpdate_RoundTripParse()
    {
        // ReportId, count, flags, then N x (id,r,g,b,i)
        var report = new byte[]
        {
            HidUsages.ReportIdMultiUpdate, 2, 0x00,
            0, 255, 0, 0, 255,
            1, 0, 255, 0, 128,
        };
        Assert.True(ReportCodec.TryParseMultiUpdate(report, out var frame));
        Assert.NotNull(frame);
        Assert.Equal(2, frame!.Updates.Count);
        Assert.Equal(255, frame.Updates[0].Color.R);
        Assert.Equal(1, frame.Updates[1].LampId);
        Assert.Equal(128, frame.Updates[1].Color.Intensity);
    }

    [Fact]
    public void RangeUpdate_FillsInclusiveIds()
    {
        var report = new byte[]
        {
            HidUsages.ReportIdRangeUpdate, 0, 2, 10, 20, 30, 255,
        };
        Assert.True(ReportCodec.TryParseRangeUpdate(report, out var frame));
        Assert.Equal(3, frame!.Updates.Count);
        Assert.All(frame.Updates, u => Assert.Equal(10, u.Color.R));
    }

    [Fact]
    public void Control_AutonomousBit()
    {
        Assert.True(ReportCodec.TryParseControl(new byte[] { HidUsages.ReportIdControl, 0x01 }, out var auto));
        Assert.True(auto);
        Assert.True(ReportCodec.TryParseControl(new byte[] { HidUsages.ReportIdControl, 0x00 }, out auto));
        Assert.False(auto);
    }

    [Fact]
    public void ReportDescriptor_IsNonEmptyLightingPage()
    {
        var d = ReportDescriptorBytes.Descriptor;
        Assert.True(d.Length > 16);
        Assert.Equal(0x05, d[0]);
        Assert.Equal(0x59, d[1]); // Lighting page
    }

    [Fact]
    public void RgbColor_FromHex()
    {
        var c = RgbColor.FromHex("#AABBCC");
        Assert.Equal(0xAA, c.R);
        Assert.Equal("AABBCC", c.ToHex());
    }
}
