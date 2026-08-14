using System.Runtime.InteropServices;
using System.Text;
using RgbFx.AacHal;

namespace AuraCapabilityDump;

/// <summary>
/// Mimic native LightingService Enumerate2 consumption via oleaut SAFEARRAY APIs
/// (no Marshal.GetObjectForIUnknown / RCW cast shortcuts).
/// </summary>
internal static class NativeStyleEnum
{
    public static int Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        Ole32.CoInitializeEx(IntPtr.Zero, Ole32.COINIT_APARTMENTTHREADED);
        try
        {
            var clsid = new Guid(AacIds.RgbFxHalClsid);
            var iidUnk = Ole32.IidIUnknown;
            int hr = Ole32.CoCreateInstance(ref clsid, IntPtr.Zero,
                Ole32.CLSCTX_LOCAL_SERVER | Ole32.CLSCTX_INPROC_SERVER,
                ref iidUnk, out var unkObj);
            sb.AppendLine($"CoCreate hr=0x{hr:X8}");
            if (hr != 0 || unkObj is null)
            {
                File.WriteAllText(Path.Combine(outDir, "native-style.txt"), sb.ToString());
                return 1;
            }

            // Get raw IUnknown for HAL
            IntPtr pUnk = Marshal.GetIUnknownForObject(unkObj);
            try
            {
                var iidHal2 = new Guid(AacIds.IAsusAacLedDeviceHal2);
                hr = Marshal.QueryInterface(pUnk, ref iidHal2, out var pHal2);
                sb.AppendLine($"QI Hal2 hr=0x{hr:X8} p=0x{pHal2.ToInt64():X}");
                if (hr != 0 || pHal2 == IntPtr.Zero)
                {
                    File.WriteAllText(Path.Combine(outDir, "native-style.txt"), sb.ToString());
                    return 2;
                }

                try
                {
                    // Vtable slot for Enumerate2 on Hal2 (x86):
                    // 0 QI,1 AddRef,2 Release,3 Enumerate,4-7 Bios,8 Enumerate2
                    // x64 same slots, 8-byte pointers
                    int slot = 8;
                    IntPtr pEnumerate2 = GetVTableFunc(pHal2, slot);
                    sb.AppendLine($"Enumerate2 fn=0x{pEnumerate2.ToInt64():X} slot={slot}");

                    var enumerate2 = Marshal.GetDelegateForFunctionPointer<Enumerate2Delegate>(pEnumerate2);

                    int varSize = IntPtr.Size == 8 ? 24 : 16;
                    IntPtr pVar = Marshal.AllocHGlobal(varSize);
                    IntPtr pCount = Marshal.AllocHGlobal(4);
                    try
                    {
                        // Phase 1
                        Zero(pVar, varSize);
                        Marshal.WriteInt32(pCount, 0);
                        hr = enumerate2(pHal2, pVar, pCount);
                        uint count = unchecked((uint)Marshal.ReadInt32(pCount));
                        short vt = Marshal.ReadInt16(pVar, 0);
                        sb.AppendLine($"phase1 hr=0x{hr:X8} count={count} vt=0x{vt:X4}");

                        // Phase 2
                        Zero(pVar, varSize);
                        Marshal.WriteInt32(pCount, (int)count);
                        hr = enumerate2(pHal2, pVar, pCount);
                        count = unchecked((uint)Marshal.ReadInt32(pCount));
                        vt = Marshal.ReadInt16(pVar, 0);
                        IntPtr p0 = Marshal.ReadIntPtr(pVar, 8);
                        sb.AppendLine($"phase2 hr=0x{hr:X8} count={count} vt=0x{vt:X4} p0=0x{p0.ToInt64():X}");

                        if (hr == 0 && (vt & 0x2000) != 0 && p0 != IntPtr.Zero)
                        {
                            // Native-style SAFEARRAY access
                            int saHr = OleAutEx.SafeArrayGetVartype(p0, out var saVt);
                            sb.AppendLine($"SafeArrayGetVartype hr=0x{saHr:X8} vt={saVt}");
                            saHr = OleAutEx.SafeArrayGetUBound(p0, 1, out var ub);
                            int saHr2 = OleAutEx.SafeArrayGetLBound(p0, 1, out var lb);
                            sb.AppendLine($"bounds lb={lb} ub={ub} hr={saHr:X8}/{saHr2:X8}");

                            saHr = OleAutEx.SafeArrayAccessData(p0, out var pvData);
                            sb.AppendLine($"SafeArrayAccessData hr=0x{saHr:X8} pv=0x{pvData.ToInt64():X}");
                            if (saHr == 0 && pvData != IntPtr.Zero)
                            {
                                int n = (int)(ub - lb + 1);
                                for (int i = 0; i < n && i < 8; i++)
                                {
                                    IntPtr punkDev = Marshal.ReadIntPtr(pvData, i * IntPtr.Size);
                                    sb.AppendLine($"  [{i}] punk=0x{punkDev.ToInt64():X}");
                                    if (punkDev == IntPtr.Zero) continue;

                                    // QI IAacLedDevice
                                    var iidDev = new Guid(AacIds.IAacLedDevice);
                                    int qi = Marshal.QueryInterface(punkDev, ref iidDev, out var pDev);
                                    sb.AppendLine($"  [{i}] QI IAacLedDevice hr=0x{qi:X8} p=0x{pDev.ToInt64():X}");
                                    if (qi != 0 || pDev == IntPtr.Zero) continue;

                                    try
                                    {
                                        // GetCapability is slot 3 (after QI/AddRef/Release)
                                        IntPtr pGetCap = GetVTableFunc(pDev, 3);
                                        var getCap = Marshal.GetDelegateForFunctionPointer<GetCapabilityDelegate>(pGetCap);
                                        hr = getCap(pDev, out var bstr);
                                        sb.AppendLine($"  [{i}] GetCapability hr=0x{hr:X8}");
                                        if (hr == 0 && bstr != IntPtr.Zero)
                                        {
                                            string cap = Marshal.PtrToStringBSTR(bstr) ?? "";
                                            Marshal.FreeBSTR(bstr);
                                            sb.AppendLine($"  [{i}] capLen={cap.Length}");
                                            File.WriteAllText(Path.Combine(outDir, $"native-dev{i}-cap.txt"), cap, Encoding.UTF8);
                                            var prev = cap.Length > 200 ? cap[..200] : cap;
                                            sb.AppendLine("  preview=" + prev.Replace('\n', ' '));
                                        }

                                        // Also try VariedLedCount
                                        var iidVar = new Guid("C36296FF-DA7F-4367-ADF9-914000245740");
                                        qi = Marshal.QueryInterface(punkDev, ref iidVar, out var pVarLed);
                                        sb.AppendLine($"  [{i}] QI VariedLedCount hr=0x{qi:X8}");
                                        if (qi == 0 && pVarLed != IntPtr.Zero)
                                        {
                                            // GetManualLedCount is slot after IAacLedDeviceOpt full chain
                                            // IUnknown3 + IAacLedDevice3 + Opt1 + Varied.Get = slot 7?
                                            // IAacLedDevice: 3 methods (slots 3,4,5)
                                            // Opt: SetEffectOptSpeed slot 6
                                            // Varied: GetManualLedCount slot 7, Set slot 8
                                            try
                                            {
                                                IntPtr pGet = GetVTableFunc(pVarLed, 7);
                                                var getN = Marshal.GetDelegateForFunctionPointer<GetUIntDelegate>(pGet);
                                                hr = getN(pVarLed, out var nLeds);
                                                sb.AppendLine($"  [{i}] GetManualLedCount hr=0x{hr:X8} n={nLeds}");
                                            }
                                            catch (Exception ex)
                                            {
                                                sb.AppendLine($"  [{i}] GetManualLedCount err: {ex.Message}");
                                            }
                                            Marshal.Release(pVarLed);
                                        }
                                    }
                                    finally
                                    {
                                        Marshal.Release(pDev);
                                    }
                                }
                                OleAutEx.SafeArrayUnaccessData(p0);
                            }
                        }
                        else if (hr == 0 && (vt == 13 || vt == 9) && p0 != IntPtr.Zero)
                        {
                            sb.AppendLine("single VT_UNKNOWN/DISPATCH path");
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pVar);
                        Marshal.FreeHGlobal(pCount);
                    }
                }
                finally
                {
                    Marshal.Release(pHal2);
                }
            }
            finally
            {
                Marshal.Release(pUnk);
                Marshal.FinalReleaseComObject(unkObj);
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("EXCEPTION: " + ex);
            Console.WriteLine(ex);
        }
        finally
        {
            Ole32.CoUninitialize();
        }

        File.WriteAllText(Path.Combine(outDir, "native-style.txt"), sb.ToString());
        Console.WriteLine(sb.ToString());
        return 0;
    }

    static IntPtr GetVTableFunc(IntPtr pIface, int slot)
    {
        IntPtr vtable = Marshal.ReadIntPtr(pIface, 0);
        return Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
    }

    static void Zero(IntPtr p, int n)
    {
        for (int i = 0; i < n; i++) Marshal.WriteByte(p, i, 0);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int Enumerate2Delegate(IntPtr thisPtr, IntPtr pVar, IntPtr pCount);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int GetCapabilityDelegate(IntPtr thisPtr, out IntPtr bstr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int GetUIntDelegate(IntPtr thisPtr, out uint value);
}

internal static class OleAutEx
{
    [DllImport("oleaut32.dll")]
    public static extern int SafeArrayGetVartype(IntPtr psa, out ushort pvt);

    [DllImport("oleaut32.dll")]
    public static extern int SafeArrayGetUBound(IntPtr psa, uint nDim, out int plUbound);

    [DllImport("oleaut32.dll")]
    public static extern int SafeArrayGetLBound(IntPtr psa, uint nDim, out int plLbound);

    [DllImport("oleaut32.dll")]
    public static extern int SafeArrayAccessData(IntPtr psa, out IntPtr ppvData);

    [DllImport("oleaut32.dll")]
    public static extern int SafeArrayUnaccessData(IntPtr psa);
}
