using RgbFx.AuraAddressable.Protocol;
using RgbFx.LampArray.Protocol;
using Xunit;

namespace RgbFx.AuraAddressable.Protocol.Tests;

public class AddressableDeviceTests
{
    [Fact]
    public void FirmwareRequest_ReturnsString()
    {
        var dev = new AddressableDevice(8, "TESTFW-0101");
        var req = new byte[HidConstants.ReportSize];
        req[0] = HidConstants.Magic;
        req[1] = HidConstants.CmdRequestFirmware;

        var r = dev.ApplyHostReport(req);
        Assert.NotNull(r.ReplyReport);
        Assert.Equal(HidConstants.Magic, r.ReplyReport![0]);
        Assert.Equal(HidConstants.RspFirmware, r.ReplyReport[1]);
        var s = System.Text.Encoding.ASCII.GetString(r.ReplyReport, 2, 12).TrimEnd('\0');
        Assert.StartsWith("TESTFW", s);
    }

    [Fact]
    public void SetColors_UpdatesLedsAndFlagsChanged()
    {
        var dev = new AddressableDevice(4);
        var colors = new[]
        {
            new RgbColor(255, 0, 0),
            new RgbColor(0, 255, 0),
            new RgbColor(0, 0, 255),
            new RgbColor(16, 32, 48),
        };
        var report = AddressableDevice.BuildSetColorsReport(colors);
        var r = dev.ApplyHostReport(report);
        Assert.True(r.ColorsChanged);
        Assert.Equal(255, dev.Leds[0].R);
        Assert.Equal(255, dev.Leds[1].G);
        Assert.Equal(255, dev.Leds[2].B);
        Assert.Equal(48, dev.Leds[3].B);
    }

    [Fact]
    public void StartUpdate_ThenSetColors_ToLightingFrame()
    {
        var dev = new AddressableDevice(4);
        dev.ApplyHostReport(AddressableDevice.BuildStartUpdateReport());
        Assert.True(dev.UpdateActive);

        dev.ApplyHostReport(AddressableDevice.BuildSetColorsReport(new[]
        {
            new RgbColor(10, 20, 30),
            new RgbColor(40, 50, 60),
            new RgbColor(70, 80, 90),
            new RgbColor(1, 2, 3),
        }));

        var frame = dev.ToLightingFrame();
        Assert.Equal(4, frame.Updates.Count);
        Assert.Equal(10, frame.Updates[0].Color.R);
        Assert.Equal(3, frame.Updates[3].Color.B);
    }

    [Fact]
    public void SampleZoneColors_AveragesSegments()
    {
        var dev = new AddressableDevice(4);
        dev.ApplyHostReport(AddressableDevice.BuildSetColorsReport(new[]
        {
            new RgbColor(100, 0, 0),
            new RgbColor(0, 0, 0),
            new RgbColor(0, 100, 0),
            new RgbColor(0, 0, 0),
        }));
        var zones = dev.SampleZoneColors(2);
        Assert.Equal(2, zones.Count);
        Assert.Equal(50, zones[0].R); // avg of 100 and 0
        Assert.Equal(50, zones[1].G);
    }

    [Fact]
    public void NonAuraReport_Ignored()
    {
        var dev = new AddressableDevice(4);
        var r = dev.ApplyHostReport(new byte[] { 0x00, 0x01, 0x02 });
        Assert.True(r.WasIgnored);
    }

    [Fact]
    public void ReportParser_DescribeSetColors()
    {
        var report = AddressableDevice.BuildSetColorsReport(new[] { new RgbColor(1, 2, 3) });
        Assert.Equal("set-colors", ReportParser.Describe(report));
    }
}
