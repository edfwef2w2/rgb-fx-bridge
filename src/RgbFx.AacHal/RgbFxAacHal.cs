using System.Runtime.InteropServices;

namespace RgbFx.AacHal;

/// <summary>COM coclass — new AAC HAL (LocalServer for LightingService x86).</summary>
[ComVisible(true)]
[Guid(AacIds.RgbFxHalClsid)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class RgbFxAacHal :
    IAsusAacLedDeviceHal,
    IAsusMotherboardHal,
    IAsusAacLedDeviceHal2,
    IAacLedDeviceHal,
    IAsusAacLedDeviceHalWDL,
    ICustomQueryInterface
{
    private readonly MsiFrameSink _sink = new();
    private readonly RgbFxLedDevice _device;

    public RgbFxAacHal()
    {
        var n = CapabilityBuilder.DefaultLedCount;
        if (int.TryParse(Environment.GetEnvironmentVariable("RGBFX_LED_COUNT"), out var lc) && lc > 0)
            n = Math.Clamp(lc, 1, 120);
        _device = new RgbFxLedDevice(_sink, n);
        Log($"RgbFxAacHal constructed pid={Environment.ProcessId} leds={n} mode={Enum2Mode}");
    }

    static string Enum2Mode =>
        (Environment.GetEnvironmentVariable("RGBFX_ENUM2_MODE") ?? "arrayiid").Trim().ToLowerInvariant();

    CustomQueryInterfaceResult ICustomQueryInterface.GetInterface(ref Guid iid, out IntPtr ppv)
    {
        if (iid != Ole32.IidIUnknown)
            Log($"hal QI {iid}");
        ppv = IntPtr.Zero;
        return CustomQueryInterfaceResult.NotHandled;
    }

    int IAsusAacLedDeviceHal.Enumerate(IntPtr devices, ref uint count, long value)
        => EnumerateCore(devices, ref count);

    int IAsusMotherboardHal.Enumerate(IntPtr devices, ref uint count, long value)
        => EnumerateCore(devices, ref count);

    int IAsusMotherboardHal.GetBiosOnOff(out int pVal)
    {
        pVal = 1;
        return 0;
    }

    int IAsusMotherboardHal.SetBiosOnOff(int pVal) => 0;

    int IAsusMotherboardHal.GetBiosStandbyOnOff(out int pVal)
    {
        pVal = 0;
        return 0;
    }

    int IAsusMotherboardHal.SetBiosStandbyOnOff(int pVal) => 0;

    int IAsusAacLedDeviceHal2.Enumerate(IntPtr devices, ref uint count, long value)
        => EnumerateCore(devices, ref count);

    int IAsusAacLedDeviceHal2.GetBiosOnOff(out int pVal)
        => ((IAsusMotherboardHal)this).GetBiosOnOff(out pVal);

    int IAsusAacLedDeviceHal2.SetBiosOnOff(int pVal)
        => ((IAsusMotherboardHal)this).SetBiosOnOff(pVal);

    int IAsusAacLedDeviceHal2.GetBiosStandbyOnOff(out int pVal)
        => ((IAsusMotherboardHal)this).GetBiosStandbyOnOff(out pVal);

    int IAsusAacLedDeviceHal2.SetBiosStandbyOnOff(int pVal)
        => ((IAsusMotherboardHal)this).SetBiosStandbyOnOff(pVal);

    int IAsusAacLedDeviceHal2.Enumerate2(IntPtr pDevicesVariant, IntPtr pCount)
        => Enumerate2Core(pDevicesVariant, pCount);

    int IAacLedDeviceHal.Enumerate(IntPtr devices, ref uint count)
        => EnumerateCore(devices, ref count);

    int IAacLedDeviceHal.Enumerate2(IntPtr pDevicesVariant, IntPtr pCount)
        => Enumerate2Core(pDevicesVariant, pCount);

    int IAsusAacLedDeviceHalWDL.EnumerateWDL4DLL(IntPtr devices, ref uint count, long value)
        => EnumerateCore(devices, ref count);

    int IAsusAacLedDeviceHalWDL.EnumerateWDL4EXE(IntPtr pDevicesVariant, IntPtr pCount)
    {
        Log("EnumerateWDL4EXE");
        return Enumerate2Core(pDevicesVariant, pCount);
    }

    int EnumerateCore(IntPtr devices, ref uint count)
    {
        if (devices == IntPtr.Zero || count == 0)
        {
            count = 1;
            Log("Enumerate phase1 count=1");
            return 0;
        }

        count = 1;
        // Prefer richest device interface for native clients
        var pDev = GetDeviceIfacePtr(preferVaried: true);
        Marshal.WriteIntPtr(devices, pDev);
        Log($"Enumerate phase2 punk=0x{pDev.ToInt64():X}");
        return 0;
    }

    int Enumerate2Core(IntPtr pDevicesVariant, IntPtr pCount)
    {
        // Real MB HAL two-phase Enumerate2:
        //  Phase1: *count == 0  → set *count = N, leave VARIANT empty
        //  Phase2: *count  > 0  → fill VARIANT (default VT_ARRAY|VT_UNKNOWN + HAVEIID)
        const int deviceCount = 1;
        uint countIn = 0;
        if (pCount != IntPtr.Zero)
            countIn = unchecked((uint)Marshal.ReadInt32(pCount));

        int variantSize = IntPtr.Size == 8 ? 24 : 16;
        string mode = Enum2Mode;

        if (mode is "empty" or "zero")
        {
            if (pCount != IntPtr.Zero)
                Marshal.WriteInt32(pCount, 0);
            if (pDevicesVariant != IntPtr.Zero)
                Zero(pDevicesVariant, variantSize);
            Log("Enumerate2 mode=empty count=0");
            return 0;
        }

        if (countIn == 0)
        {
            if (pCount != IntPtr.Zero)
                Marshal.WriteInt32(pCount, deviceCount);
            if (pDevicesVariant != IntPtr.Zero)
                Zero(pDevicesVariant, variantSize);
            Log($"Enumerate2 phase1 countOut={deviceCount} varSize={variantSize} mode={mode}");
            return 0;
        }

        if (pDevicesVariant == IntPtr.Zero)
        {
            if (pCount != IntPtr.Zero)
                Marshal.WriteInt32(pCount, deviceCount);
            Log("Enumerate2 phase2 null VARIANT*");
            return 0;
        }

        try
        {
            // Clear any previous VARIANT contents carefully
            try { OleAut.VariantClear(pDevicesVariant); } catch { /* ignore */ }
            Zero(pDevicesVariant, variantSize);

            int hr = mode switch
            {
                "single" => WriteSingleUnknown(pDevicesVariant),
                "vector" => WriteArrayUnknown(pDevicesVariant, withIid: false, preferVaried: true),
                "vararray" => WriteArrayVariant(pDevicesVariant),
                "arraybase" => WriteArrayUnknown(pDevicesVariant, withIid: true, preferVaried: false),
                // default / arrayiid: SAFEARRAY VT_UNKNOWN + HAVEIID = IAacLedDeviceVariedLedCount
                _ => WriteArrayUnknown(pDevicesVariant, withIid: true, preferVaried: true),
            };

            if (pCount != IntPtr.Zero)
                Marshal.WriteInt32(pCount, deviceCount);

            short vt = Marshal.ReadInt16(pDevicesVariant, 0);
            IntPtr p0 = Marshal.ReadIntPtr(pDevicesVariant, 8);
            Log($"Enumerate2 phase2 mode={mode} countIn={countIn} hr=0x{hr:X8} vt=0x{vt:X4} p0=0x{p0.ToInt64():X}");
            return hr;
        }
        catch (Exception ex)
        {
            Log("Enumerate2 error: " + ex);
            return unchecked((int)0x80004005);
        }
    }

    int WriteSingleUnknown(IntPtr pVar)
    {
        var pDev = GetDeviceIfacePtr(preferVaried: true);
        Marshal.WriteInt16(pVar, 0, unchecked((short)VariantVt.VT_UNKNOWN));
        Marshal.WriteIntPtr(pVar, 8, pDev);
        return 0;
    }

    int WriteArrayUnknown(IntPtr pVar, bool withIid, bool preferVaried)
    {
        var pDev = GetDeviceIfacePtr(preferVaried);
        IntPtr psa;
        if (withIid)
        {
            var iid = new Guid(preferVaried ? AacIds.IAacLedDeviceVariedLedCount : AacIds.IAacLedDevice);
            var bound = new SafeArrayBound { cElements = 1, lLbound = 0 };
            IntPtr pIid = Marshal.AllocHGlobal(16);
            try
            {
                Marshal.StructureToPtr(iid, pIid, false);
                psa = OleAut.SafeArrayCreateEx(VariantVt.VT_UNKNOWN, 1, ref bound, pIid);
            }
            finally
            {
                Marshal.FreeHGlobal(pIid);
            }
        }
        else
        {
            psa = OleAut.SafeArrayCreateVector(VariantVt.VT_UNKNOWN, 0, 1);
        }

        if (psa == IntPtr.Zero)
        {
            Marshal.Release(pDev);
            Log("SafeArray create failed");
            return unchecked((int)0x80004005);
        }

        int index = 0;
        int putHr = OleAut.SafeArrayPutElement(psa, ref index, pDev);
        Marshal.Release(pDev);
        if (putHr != 0)
        {
            OleAut.SafeArrayDestroy(psa);
            Log($"SafeArrayPutElement hr=0x{putHr:X8}");
            return putHr;
        }

        // Log SAFEARRAY header (x86)
        try
        {
            short feat = Marshal.ReadInt16(psa, 2);
            int cbEl = Marshal.ReadInt32(psa, 4);
            int cEl = Marshal.ReadInt32(psa, IntPtr.Size == 8 ? 24 : 16);
            Log($"SAFEARRAY feat=0x{feat:X} cbEl={cbEl} n={cEl} iid={(withIid ? (preferVaried ? "Varied" : "Base") : "none")}");
        }
        catch { /* ignore */ }

        Marshal.WriteInt16(pVar, 0, unchecked((short)(VariantVt.VT_ARRAY | VariantVt.VT_UNKNOWN)));
        Marshal.WriteIntPtr(pVar, 8, psa);
        return 0;
    }

    int WriteArrayVariant(IntPtr pVar)
    {
        // VT_ARRAY|VT_VARIANT with one element VT_UNKNOWN
        var pDev = GetDeviceIfacePtr(preferVaried: true);
        IntPtr psa = OleAut.SafeArrayCreateVector(VariantVt.VT_VARIANT, 0, 1);
        if (psa == IntPtr.Zero)
        {
            Marshal.Release(pDev);
            return unchecked((int)0x80004005);
        }

        // Build a 16-byte VARIANT element (x86) or 24-byte (x64)
        int vs = IntPtr.Size == 8 ? 24 : 16;
        IntPtr pElem = Marshal.AllocHGlobal(vs);
        try
        {
            Zero(pElem, vs);
            Marshal.WriteInt16(pElem, 0, unchecked((short)VariantVt.VT_UNKNOWN));
            Marshal.WriteIntPtr(pElem, 8, pDev);
            int index = 0;
            // For VT_VARIANT, PutElement copies the VARIANT (and AddRefs punk)
            int putHr = OleAut.SafeArrayPutElement(psa, ref index, pElem);
            // Release our local ref; array holds its own via copy
            Marshal.Release(pDev);
            if (putHr != 0)
            {
                OleAut.SafeArrayDestroy(psa);
                Log($"vararray PutElement hr=0x{putHr:X8}");
                return putHr;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pElem);
        }

        Marshal.WriteInt16(pVar, 0, unchecked((short)(VariantVt.VT_ARRAY | VariantVt.VT_VARIANT)));
        Marshal.WriteIntPtr(pVar, 8, psa);
        return 0;
    }

    IntPtr GetDeviceIfacePtr(bool preferVaried)
    {
        var unk = Marshal.GetIUnknownForObject(_device);
        try
        {
            if (preferVaried)
            {
                var iidV = new Guid(AacIds.IAacLedDeviceVariedLedCount);
                int qi = Marshal.QueryInterface(unk, ref iidV, out var pV);
                if (qi == 0 && pV != IntPtr.Zero)
                    return pV;
            }

            var iid = new Guid(AacIds.IAacLedDevice);
            int qi2 = Marshal.QueryInterface(unk, ref iid, out var pDev);
            if (qi2 == 0 && pDev != IntPtr.Zero)
                return pDev;

            // Fallback: IUnknown (extra ref already from GetIUnknownForObject)
            Marshal.AddRef(unk);
            return unk;
        }
        finally
        {
            Marshal.Release(unk);
        }
    }

    static void Zero(IntPtr p, int n)
    {
        for (int i = 0; i < n; i++)
            Marshal.WriteByte(p, i, 0);
    }

    static void Log(string msg)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "RgbFx", "AacHal");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "aachal.log"),
                $"[{DateTime.Now:O}] {msg}{Environment.NewLine}");
        }
        catch { /* ignore */ }
    }
}

[ComImport]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}

public sealed class RgbFxAacHalClassFactory : IClassFactory
{
    public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;
        if (pUnkOuter != IntPtr.Zero)
            return unchecked((int)0x80040110);

        var obj = new RgbFxAacHal();
        var unk = Marshal.GetIUnknownForObject(obj);
        try
        {
            // Prefer exact interface match so native QI lands on correct vtable
            if (riid == Ole32.IidIUnknown ||
                riid == new Guid(AacIds.RgbFxHalClsid) ||
                riid == new Guid(AacIds.IAsusAacLedDeviceHal) ||
                riid == new Guid(AacIds.IAsusAacLedDeviceHal2) ||
                riid == new Guid(AacIds.IAacLedDeviceHal) ||
                riid == new Guid(AacIds.IAsusAacLedDeviceHalWDL) ||
                riid == new Guid(AacIds.IAsusMotherboardHal))
            {
                return Marshal.QueryInterface(unk, ref riid, out ppvObject);
            }

            var iid = Ole32.IidIUnknown;
            return Marshal.QueryInterface(unk, ref iid, out ppvObject);
        }
        finally
        {
            Marshal.Release(unk);
        }
    }

    public int LockServer(bool fLock) => 0;
}
