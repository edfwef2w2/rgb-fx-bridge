using System.Runtime.InteropServices;

namespace RgbFx.AacHal;

/// <summary>ASUS AAC COM IIDs/CLSID from local TLBs (read-only reverse engineering).</summary>
public static class AacIds
{
    // Real ASUS (for dump only — never overwrite these registrations)
    public const string MbHalClsid = "E7C8DA76-C9B9-4297-8681-DD878330AFE7";
    public const string ExtHalClsid = "662181CB-F1F8-4AD8-ABDD-3661A51A85C7";
    public const string DramHalClsid = "4BBABBFE-17CA-4AFA-8959-3B1DEFC4421D";

    // Our NEW device (do not collide with ASUS)
    public const string RgbFxHalClsid = "B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73";
    public const string RgbFxHalProgId = "RgbFx.AacHal.1";
    public const string RgbFxHalProgIdVi = "RgbFx.AacHal";

    public const string IAacLedDevice = "61711778-AB59-4026-89E8-7A63422C29C2";
    public const string IAsusAacLedDeviceHal = "F2C8D5B4-3854-4325-8A4F-FD7C5072E3BA";
    public const string IAsusMotherboardHal = "B2EE849F-0C8B-4856-9542-3EF33734DF5D";
    public const string IAsusAacLedDeviceHal2 = "816E764A-7763-4435-8C15-A42B6C8EBA9B";
    public const string IAacLedDeviceHal = "F2C8D5B4-3854-4325-8A4F-FD7C5072E3B9";
    public const string IAacLedDeviceOpt = "68F0C6E1-7469-40B3-84D5-E0793F449E4D";
    public const string IAacLedDevice2 = "C40349D9-D85A-477A-8A52-65F32C0B5D2F";
    /// <summary>IAacLedDevice2 + SetEffectOptSpeed2 (LS QIs this after enum).</summary>
    public const string IAacLedDeviceOpt2 = "A146F057-41D4-4358-8AEB-57D34D2C5943";
    /// <summary>AddressableStrip varied LED count (TLB: IAacLedDeviceVariedLedCount).</summary>
    public const string IAacLedDeviceVariedLedCount = "C36296FF-DA7F-4367-ADF9-914000245740";
    /// <summary>EXE-HAL enumerate (TLB: IAsusAacLedDeviceHalWDL).</summary>
    public const string IAsusAacLedDeviceHalWDL = "9F07F709-AF84-4D4A-A1D7-F1C06216F491";

    public const string CategoryRoot = "9C9E903E-BBC7-4A0E-8326-ED6AC85B9FCC";
    public const string CategoryInstance = "E9BBD754-6CF4-492E-BA89-782177A2771B";
}

/// <summary>IAacLedDevice — IUnknown + 3 methods (oVft 24/32/40).</summary>
[ComImport]
[Guid(AacIds.IAacLedDevice)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAacLedDevice
{
    [PreserveSig]
    int GetCapability([MarshalAs(UnmanagedType.BStr)] out string capability);

    [PreserveSig]
    int SetEffect(uint effectId, IntPtr colors, uint numberOfColors);

    [PreserveSig]
    int Synchronize(uint effectId, ulong milliseconds);
}

/// <summary>IAsusAacLedDeviceHal — Enumerate only.</summary>
[ComImport]
[Guid(AacIds.IAsusAacLedDeviceHal)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAsusAacLedDeviceHal
{
    /// <summary>
    /// Two-phase: first call with devices==null to get count; then allocate IAacLedDevice*[count].
    /// </summary>
    [PreserveSig]
    int Enumerate(IntPtr devices, ref uint count, long value);
}

/// <summary>IAsusMotherboardHal — Hal Enumerate + 4×Bios (TLB inheritance).</summary>
[ComImport]
[Guid(AacIds.IAsusMotherboardHal)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAsusMotherboardHal
{
    [PreserveSig]
    int Enumerate(IntPtr devices, ref uint count, long value);

    [PreserveSig]
    int GetBiosOnOff(out int pVal);

    [PreserveSig]
    int SetBiosOnOff(int pVal);

    [PreserveSig]
    int GetBiosStandbyOnOff(out int pVal);

    [PreserveSig]
    int SetBiosStandbyOnOff(int pVal);
}

/// <summary>
/// Flattened vtable for IAsusAacLedDeviceHal2:
/// IUnknown + Enumerate + 4×Bios + Enumerate2 (matches TLB inheritance).
/// </summary>
[ComImport]
[Guid(AacIds.IAsusAacLedDeviceHal2)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAsusAacLedDeviceHal2
{
    [PreserveSig]
    int Enumerate(IntPtr devices, ref uint count, long value);

    [PreserveSig]
    int GetBiosOnOff(out int pVal);

    [PreserveSig]
    int SetBiosOnOff(int pVal);

    [PreserveSig]
    int GetBiosStandbyOnOff(out int pVal);

    [PreserveSig]
    int SetBiosStandbyOnOff(int pVal);

    /// <summary>VARIANT* devices, UINT* count — use raw pointers for reliable out VARIANT.</summary>
    [PreserveSig]
    int Enumerate2(IntPtr pDevicesVariant, IntPtr pCount);
}

/// <summary>DRAM-style HAL — clean Enumerate + Enumerate2.</summary>
[ComImport]
[Guid(AacIds.IAacLedDeviceHal)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAacLedDeviceHal
{
    [PreserveSig]
    int Enumerate(IntPtr devices, ref uint count);

    [PreserveSig]
    int Enumerate2(IntPtr pDevicesVariant, IntPtr pCount);
}

/// <summary>x64 VARIANT layout (24 bytes).</summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct Variant16
{
    [FieldOffset(0)] public ushort vt;
    [FieldOffset(8)] public IntPtr p0;
    [FieldOffset(16)] public IntPtr p1;
}

public static class VariantVt
{
    public const ushort VT_EMPTY = 0;
    public const ushort VT_UNKNOWN = 13;
    public const ushort VT_DISPATCH = 9;
    public const ushort VT_VARIANT = 12;
    public const ushort VT_ARRAY = 0x2000;
    public const ushort VT_BYREF = 0x4000;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SafeArrayBound
{
    public uint cElements;
    public int lLbound;
}

internal static class OleAut
{
    [DllImport("oleaut32.dll")]
    public static extern IntPtr SafeArrayCreateVector(ushort vt, int lLbound, uint cElements);

    /// <summary>For VT_UNKNOWN/VT_DISPATCH arrays, pvExtra is pointer to IID.</summary>
    [DllImport("oleaut32.dll")]
    public static extern IntPtr SafeArrayCreateEx(ushort vt, uint cDims, ref SafeArrayBound rgsabound, IntPtr pvExtra);

    [DllImport("oleaut32.dll")]
    public static extern int SafeArrayPutElement(IntPtr psa, ref int rgIndices, IntPtr pv);

    [DllImport("oleaut32.dll")]
    public static extern int SafeArrayDestroy(IntPtr psa);

    [DllImport("oleaut32.dll")]
    public static extern int VariantClear(IntPtr pvar);
}

[ComImport]
[Guid(AacIds.IAacLedDeviceOpt)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAacLedDeviceOpt
{
    // IAacLedDevice
    [PreserveSig]
    int GetCapability([MarshalAs(UnmanagedType.BStr)] out string capability);

    [PreserveSig]
    int SetEffect(uint effectId, IntPtr colors, uint numberOfColors);

    [PreserveSig]
    int Synchronize(uint effectId, ulong milliseconds);

    // Opt
    [PreserveSig]
    int SetEffectOptSpeed(uint effectId, IntPtr colors, uint numberOfColors, uint speed, uint direction);
}

/// <summary>
/// Flattened: IAacLedDevice + Opt + VariedLedCount (real ARGB strips use this).
/// LS often QI this before/during device init when capability has varied=1.
/// </summary>
[ComImport]
[Guid(AacIds.IAacLedDeviceVariedLedCount)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAacLedDeviceVariedLedCount
{
    [PreserveSig]
    int GetCapability([MarshalAs(UnmanagedType.BStr)] out string capability);

    [PreserveSig]
    int SetEffect(uint effectId, IntPtr colors, uint numberOfColors);

    [PreserveSig]
    int Synchronize(uint effectId, ulong milliseconds);

    [PreserveSig]
    int SetEffectOptSpeed(uint effectId, IntPtr colors, uint numberOfColors, uint speed, uint direction);

    [PreserveSig]
    int GetManualLedCount(out uint ledCount);

    [PreserveSig]
    int SetManualLedCount(uint ledCount);
}

/// <summary>IAacLedDeviceOpt + SetEffect2(effect, VARIANT colors, count).</summary>
[ComImport]
[Guid(AacIds.IAacLedDevice2)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAacLedDevice2
{
    [PreserveSig]
    int GetCapability([MarshalAs(UnmanagedType.BStr)] out string capability);

    [PreserveSig]
    int SetEffect(uint effectId, IntPtr colors, uint numberOfColors);

    [PreserveSig]
    int Synchronize(uint effectId, ulong milliseconds);

    [PreserveSig]
    int SetEffectOptSpeed(uint effectId, IntPtr colors, uint numberOfColors, uint speed, uint direction);

    /// <summary>colors is VARIANT (often VT_ARRAY|VT_UI4 or VT_ARRAY|VT_I4).</summary>
    [PreserveSig]
    int SetEffect2(uint effectId, [MarshalAs(UnmanagedType.Struct)] object colors, uint numberOfColors);
}

/// <summary>IAacLedDevice2 + SetEffectOptSpeed2 — LS QIs this after Enumerate2.</summary>
[ComImport]
[Guid(AacIds.IAacLedDeviceOpt2)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAacLedDeviceOpt2
{
    [PreserveSig]
    int GetCapability([MarshalAs(UnmanagedType.BStr)] out string capability);

    [PreserveSig]
    int SetEffect(uint effectId, IntPtr colors, uint numberOfColors);

    [PreserveSig]
    int Synchronize(uint effectId, ulong milliseconds);

    [PreserveSig]
    int SetEffectOptSpeed(uint effectId, IntPtr colors, uint numberOfColors, uint speed, uint direction);

    [PreserveSig]
    int SetEffect2(uint effectId, [MarshalAs(UnmanagedType.Struct)] object colors, uint numberOfColors);

    [PreserveSig]
    int SetEffectOptSpeed2(uint effectId, [MarshalAs(UnmanagedType.Struct)] object colors, uint numberOfColors, uint speed, uint direction);
}

/// <summary>WDL EXE path: EnumerateWDL4DLL + EnumerateWDL4EXE (same shape as Enumerate2).</summary>
[ComImport]
[Guid(AacIds.IAsusAacLedDeviceHalWDL)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAsusAacLedDeviceHalWDL
{
    /// <summary>IAacLedDevice*** devices, ULONG* count, LONGLONG value</summary>
    [PreserveSig]
    int EnumerateWDL4DLL(IntPtr devices, ref uint count, long value);

    /// <summary>VARIANT* devices, ULONG* count — same two-phase as Enumerate2</summary>
    [PreserveSig]
    int EnumerateWDL4EXE(IntPtr pDevicesVariant, IntPtr pCount);
}

internal static class Ole32
{
    public const uint CLSCTX_LOCAL_SERVER = 0x4;
    public const uint CLSCTX_INPROC_SERVER = 0x1;
    public const uint CLSCTX_ALL = 0x17;
    public const uint REGCLS_MULTIPLEUSE = 1;
    public const uint COINIT_APARTMENTTHREADED = 0x2;

    [DllImport("ole32.dll")]
    public static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("ole32.dll")]
    public static extern void CoUninitialize();

    [DllImport("ole32.dll")]
    public static extern int CoCreateInstance(
        [In] ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

    [DllImport("ole32.dll")]
    public static extern int CoRegisterClassObject(
        [In] ref Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext,
        uint flags,
        out uint lpdwRegister);

    [DllImport("ole32.dll")]
    public static extern int CoRevokeClassObject(uint dwRegister);

    [DllImport("ole32.dll")]
    public static extern int CoResumeClassObjects();

    public static readonly Guid IidIUnknown = new("00000000-0000-0000-C000-000000000046");
}
