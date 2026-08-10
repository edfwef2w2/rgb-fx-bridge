namespace RgbFx.LampArray.Protocol;

/// <summary>
/// Shared report descriptor used by VHF driver (exported as C array in driver headers)
/// and validated by unit tests for size/header sanity.
/// </summary>
public static class ReportDescriptorBytes
{
    // Simplified Lighting page collection — enough structure for Dynamic Lighting enumeration.
    // Production bring-up should diff against https://github.com/microsoft/ArduinoHidForWindows
    public static readonly byte[] Descriptor =
    {
        0x05, 0x59,       // Usage Page (Lighting And Illumination)
        0x09, 0x01,       // Usage (LampArray)
        0xA1, 0x01,       // Collection (Application)

        // Attributes feature report
        0x85, 0x01,       //   Report ID 1
        0x09, 0x02,       //   Usage (LampArrayAttributesReport)
        0xA1, 0x02,       //   Collection (Logical)
        0x75, 0x08,       //     Report Size 8
        0x95, 0x13,       //     Report Count 19
        0xB1, 0x03,       //     Feature (Cnst,Var,Abs)
        0xC0,             //   End Collection

        // Multi update output
        0x85, 0x04,       //   Report ID 4
        0x09, 0x05,       //   Usage (LampMultiUpdateReport)
        0xA1, 0x02,
        0x75, 0x08,
        0x95, 0x40,       //     up to 64 bytes payload
        0x91, 0x02,       //     Output (Data,Var,Abs)
        0xC0,

        // Range update output
        0x85, 0x05,       //   Report ID 5
        0x09, 0x06,       //   Usage (LampRangeUpdateReport)
        0xA1, 0x02,
        0x75, 0x08,
        0x95, 0x08,
        0x91, 0x02,
        0xC0,

        // Control feature
        0x85, 0x06,       //   Report ID 6
        0x09, 0x07,       //   Usage (LampArrayControlReport)
        0xA1, 0x02,
        0x75, 0x08,
        0x95, 0x01,
        0xB1, 0x02,
        0xC0,

        0xC0,             // End Collection
    };
}
