namespace RgbFx.AuraAddressable.Protocol;

/// <summary>Lightweight classification of Aura Addressable HID reports.</summary>
public static class ReportParser
{
    public static bool IsAuraReport(ReadOnlySpan<byte> report)
        => report.Length >= 2 && report[0] == HidConstants.Magic;

    public static byte GetCommand(ReadOnlySpan<byte> report)
        => report.Length >= 2 ? report[1] : (byte)0;

    public static string Describe(ReadOnlySpan<byte> report)
    {
        if (!IsAuraReport(report))
            return "non-aura";
        return report[1] switch
        {
            HidConstants.CmdRequestFirmware => "request-firmware",
            HidConstants.CmdRequestConfigTable => "request-config",
            HidConstants.CmdStartUpdate => "start-update",
            HidConstants.CmdSetColors => "set-colors",
            HidConstants.RspFirmware => "rsp-firmware",
            HidConstants.RspConfigTable => "rsp-config",
            _ => $"cmd-0x{report[1]:X2}",
        };
    }
}
