namespace RgbFx.LampArray.Protocol;

/// <summary>
/// HID Lighting and Illumination page (0x59) — see USB HUT / HUTRR84 and
/// Microsoft Dynamic Lighting device guidance.
/// </summary>
public static class HidUsages
{
    public const ushort LightingAndIlluminationPage = 0x59;

    public const byte LampArray = 0x01;
    public const byte LampArrayAttributesReport = 0x02;
    public const byte LampAttributesRequestReport = 0x03;
    public const byte LampAttributesResponseReport = 0x04;
    public const byte LampMultiUpdateReport = 0x05;
    public const byte LampRangeUpdateReport = 0x06;
    public const byte LampArrayControlReport = 0x07;
    public const byte LampArrayKind = 0x08;

    // Report IDs used by this virtual chassis device
    public const byte ReportIdAttributes = 0x01;
    public const byte ReportIdAttrRequest = 0x02;
    public const byte ReportIdAttrResponse = 0x03;
    public const byte ReportIdMultiUpdate = 0x04;
    public const byte ReportIdRangeUpdate = 0x05;
    public const byte ReportIdControl = 0x06;
}

/// <summary>LampArrayKind values (HID Lighting page).</summary>
public enum LampArrayKind : byte
{
    Undefined = 0x00,
    Keyboard = 0x01,
    Mouse = 0x02,
    GameController = 0x03,
    Peripheral = 0x04,
    Scene = 0x05,
    Notification = 0x06,
    Chassis = 0x07,
    Wearable = 0x08,
    Furniture = 0x09,
    Art = 0x0A,
    Headset = 0x0B,
    VendorDefined = 0x1F,
}
