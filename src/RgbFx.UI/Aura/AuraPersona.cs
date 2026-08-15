namespace RgbFx.UI.Aura;

/// <summary>
/// Keyboard (0x80000): one 20x6 per-key board (strip-like async ARGB).
/// Category DeviceType is the HAL kind LS enumerates; lightingname comes from capability type.
/// </summary>
public static class AuraPersona
{
    public const int CapabilityType = 0x80000;
    public const string CapabilityTypeDecimal = "524288";
    public const string CategoryKind = "Keyboard";
    public const string RlsDeviceType = "Keyboard";
    public const string OfficialSection = "AddressableHeader";
    public const bool SingleDevice = true;
}
