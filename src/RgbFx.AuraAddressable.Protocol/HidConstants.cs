namespace RgbFx.AuraAddressable.Protocol;

/// <summary>
/// Publicly documented Aura USB / Addressable HID constants (OpenRGB wiki).
/// Virtual device uses a configurable product id to reduce clash with real MB controllers.
/// </summary>
public static class HidConstants
{
    /// <summary>ASUS vendor id.</summary>
    public const ushort AsusVid = 0x0B05;

    /// <summary>
    /// Default PID for *virtual* external-style addressable controller.
    /// Real board controllers often use 0x18F3 (X570-era) — do not steal that on a system that already has one.
    /// </summary>
    public const ushort DefaultVirtualPid = 0x18F3;

    /// <summary>HID report payload size used by Aura USB (wiki: 65 bytes, zero-filled).</summary>
    public const int ReportSize = 65;

    public const byte Magic = 0xEC;

    // Host → device command bytes (byte[1] after magic)
    public const byte CmdRequestFirmware = 0x82;
    public const byte CmdRequestConfigTable = 0xB0;
    public const byte CmdStartUpdate = 0x35;
    public const byte CmdSetColors = 0x36;

    // Device → host response command bytes
    public const byte RspFirmware = 0x02;
    public const byte RspConfigTable = 0x30;

    public const string DefaultFirmwareString = "RGBFX-AARG-0101";
}
