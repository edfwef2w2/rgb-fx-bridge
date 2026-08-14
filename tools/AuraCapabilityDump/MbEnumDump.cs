using System.Runtime.InteropServices;
using System.Text;
using RgbFx.AacHal;

namespace AuraCapabilityDump;

/// <summary>Dump real MB HAL Enumerate / Enumerate2 payloads (run under win-x86 host).</summary>
internal static class MbEnumDump
{
    public static int Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        Ole32.CoInitializeEx(IntPtr.Zero, Ole32.COINIT_APARTMENTTHREADED);
        try
        {
            DumpOne("MB", new Guid(AacIds.MbHalClsid), outDir);
            DumpOne("EXT", new Guid(AacIds.ExtHalClsid), outDir);
        }
        finally
        {
            Ole32.CoUninitialize();
        }
        return 0;
    }

    /// <summary>CoCreate our own RgbFx HAL and dump Enumerate2 + GetCapability (out-of-proc).</summary>
    public static int RunSelf(string outDir)
    {
        Directory.CreateDirectory(outDir);
        Ole32.CoInitializeEx(IntPtr.Zero, Ole32.COINIT_APARTMENTTHREADED);
        try
        {
            DumpOne("SELF", new Guid(AacIds.RgbFxHalClsid), outDir);
        }
        finally
        {
            Ole32.CoUninitialize();
        }
        return 0;
    }

    static void DumpOne(string tag, Guid clsid, string outDir)
    {
        Console.WriteLine($"=== {tag} {clsid} ===");
        var iid = Ole32.IidIUnknown;
        int hr = Ole32.CoCreateInstance(ref clsid, IntPtr.Zero,
            Ole32.CLSCTX_LOCAL_SERVER | Ole32.CLSCTX_INPROC_SERVER, ref iid, out var unk);
        File.WriteAllText(Path.Combine(outDir, $"{tag}-cocreate.txt"), $"hr=0x{hr:X8}");
        if (hr != 0 || unk is null)
        {
            Console.WriteLine($"  CoCreate 0x{hr:X8}");
            return;
        }

        try
        {
            // Prefer Hal2
            IAsusAacLedDeviceHal2? h2 = null;
            try { h2 = (IAsusAacLedDeviceHal2)unk; } catch { /* */ }
            if (h2 is not null)
            {
                DumpEnumerate2(tag, "hal2", h2.Enumerate2, outDir);
                DumpEnumerateAsus(tag, h2, outDir);
                return;
            }

            try
            {
                var h = (IAacLedDeviceHal)unk;
                DumpEnumerate2(tag, "dram", h.Enumerate2, outDir);
                DumpEnumerateDram(tag, h, outDir);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(outDir, $"{tag}-qi.txt"), ex.ToString());
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(unk);
        }
    }

    static void DumpEnumerate2(string tag, string mode, Func<IntPtr, IntPtr, int> enumerate2, string outDir)
    {
        // Client-style: stack VARIANT + count (same as CComVariant)
        int varSize = IntPtr.Size == 8 ? 24 : 16;
        IntPtr pVar = Marshal.AllocHGlobal(varSize);
        IntPtr pCount = Marshal.AllocHGlobal(4);
        try
        {
            for (int i = 0; i < varSize; i++) Marshal.WriteByte(pVar, i, 0);
            Marshal.WriteInt32(pCount, 0);

            int hr = enumerate2(pVar, pCount);
            uint count = unchecked((uint)Marshal.ReadInt32(pCount));
            var bytes = new byte[varSize];
            Marshal.Copy(pVar, bytes, 0, varSize);
            short vt = BitConverter.ToInt16(bytes, 0);
            long p0 = IntPtr.Size == 8
                ? BitConverter.ToInt64(bytes, 8)
                : BitConverter.ToInt32(bytes, 8);

            var sb = new StringBuilder();
            sb.AppendLine($"hr=0x{hr:X8}");
            sb.AppendLine($"count={count}");
            sb.AppendLine($"vt=0x{vt:X4}");
            sb.AppendLine($"p0=0x{p0:X}");
            sb.AppendLine($"raw={BitConverter.ToString(bytes)}");
            Console.WriteLine($"  Enumerate2 hr=0x{hr:X8} count={count} vt=0x{vt:X4} p0=0x{p0:X}");

            if (hr == 0 && (vt & VariantVt.VT_ARRAY) != 0 && p0 != 0)
            {
                IntPtr psa = new(p0);
                DumpSafeArray(tag, mode, psa, sb, outDir);
            }
            else if (hr == 0 && (vt == (short)VariantVt.VT_UNKNOWN || vt == (short)VariantVt.VT_DISPATCH) && p0 != 0)
            {
                DumpDeviceIface(tag, mode, 0, new IntPtr(p0), sb, outDir);
            }

            File.WriteAllText(Path.Combine(outDir, $"{tag}-{mode}-enumerate2.txt"), sb.ToString());
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, $"{tag}-{mode}-enumerate2-error.txt"), ex.ToString());
            Console.WriteLine("  " + ex.Message);
        }
        finally
        {
            // Don't VariantClear if we don't own - just free our stack buffers
            Marshal.FreeHGlobal(pVar);
            Marshal.FreeHGlobal(pCount);
        }
    }

    static void DumpSafeArray(string tag, string mode, IntPtr psa, StringBuilder sb, string outDir)
    {
        // SAFEARRAY header (x86): USHORT cDims, fFeatures; ULONG cbElements, cLocks; PVOID pvData; SAFEARRAYBOUND[1]
        short cDims = Marshal.ReadInt16(psa, 0);
        short fFeatures = Marshal.ReadInt16(psa, 2);
        int cbElements = Marshal.ReadInt32(psa, 4);
        int cLocks = Marshal.ReadInt32(psa, 8);
        IntPtr pvData = Marshal.ReadIntPtr(psa, 12); // x86 offset 12; x64 is 16
        if (IntPtr.Size == 8)
            pvData = Marshal.ReadIntPtr(psa, 16);

        int cElements = Marshal.ReadInt32(psa, IntPtr.Size == 8 ? 24 : 16);
        sb.AppendLine($"SAFEARRAY cDims={cDims} fFeatures=0x{fFeatures:X} cbEl={cbElements} cLocks={cLocks} cElements={cElements} pvData=0x{pvData.ToInt64():X}");
        Console.WriteLine($"  SAFEARRAY cDims={cDims} feat=0x{fFeatures:X} cbEl={cbElements} n={cElements}");

        if (pvData == IntPtr.Zero || cElements <= 0 || cElements > 64) return;

        for (int i = 0; i < cElements; i++)
        {
            // VT_UNKNOWN array: each element is IUnknown*
            // VT_VARIANT array: each element is 16-byte VARIANT
            if (cbElements == IntPtr.Size || cbElements == 4 || cbElements == 8)
            {
                IntPtr punk = Marshal.ReadIntPtr(pvData, i * IntPtr.Size);
                sb.AppendLine($"  [{i}] punk=0x{punk.ToInt64():X}");
                if (punk != IntPtr.Zero)
                    DumpDeviceIface(tag, mode, i, punk, sb, outDir);
            }
            else if (cbElements == 16)
            {
                short evt = Marshal.ReadInt16(pvData, i * 16);
                IntPtr ep0 = Marshal.ReadIntPtr(pvData, i * 16 + 8);
                sb.AppendLine($"  [{i}] var.vt=0x{evt:X} p0=0x{ep0.ToInt64():X}");
                if ((evt == (short)VariantVt.VT_UNKNOWN || evt == (short)VariantVt.VT_DISPATCH) && ep0 != IntPtr.Zero)
                    DumpDeviceIface(tag, mode, i, ep0, sb, outDir);
            }
        }
    }

    static void DumpDeviceIface(string tag, string mode, int index, IntPtr punk, StringBuilder sb, string outDir)
    {
        try
        {
            var obj = Marshal.GetObjectForIUnknown(punk);
            var dev = (IAacLedDevice)obj;
            int hr = dev.GetCapability(out var cap);
            sb.AppendLine($"  [{index}] GetCapability hr=0x{hr:X8} len={cap?.Length ?? -1}");
            Console.WriteLine($"  [{index}] GetCapability hr=0x{hr:X8} len={cap?.Length ?? -1}");
            if (cap is not null)
            {
                File.WriteAllText(Path.Combine(outDir, $"{tag}-{mode}-dev{index}-capability.txt"), cap, Encoding.UTF8);
                File.WriteAllBytes(Path.Combine(outDir, $"{tag}-{mode}-dev{index}-capability.utf16.bin"),
                    Encoding.Unicode.GetBytes(cap));
                var prev = cap.Length > 300 ? cap[..300] : cap;
                Console.WriteLine($"    {prev.Replace('\r', ' ').Replace('\n', ' ')}");
                sb.AppendLine("  preview=" + prev.Replace('\n', ' '));
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  [{index}] device error: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"  [{index}] {ex.Message}");
            File.WriteAllText(Path.Combine(outDir, $"{tag}-{mode}-dev{index}-error.txt"), ex.ToString());
        }
    }

    static void DumpEnumerateAsus(string tag, IAsusAacLedDeviceHal2 h2, string outDir)
    {
        try
        {
            var meta = new StringBuilder();
            // Enumerate2 said count=4 for MB — try Enumerate with preallocated IUnknown*[N]
            foreach (uint nTry in new uint[] { 0, 1, 4, 8, 16 })
            {
                if (nTry == 0)
                {
                    uint c0 = 0;
                    int hr0 = h2.Enumerate(IntPtr.Zero, ref c0, 0);
                    meta.AppendLine($"null n=0 hr=0x{hr0:X8} count={c0}");
                    continue;
                }

                var arr = new IntPtr[nTry];
                var gch = GCHandle.Alloc(arr, GCHandleType.Pinned);
                try
                {
                    uint c = nTry;
                    int hr = h2.Enumerate(gch.AddrOfPinnedObject(), ref c, 0);
                    meta.AppendLine($"array nTry={nTry} hr=0x{hr:X8} countOut={c}");
                    Console.WriteLine($"  Enumerate nTry={nTry} hr=0x{hr:X8} countOut={c}");
                    for (int i = 0; i < nTry; i++)
                    {
                        if (arr[i] == IntPtr.Zero) continue;
                        meta.AppendLine($"  [{i}]=0x{arr[i].ToInt64():X}");
                        var sb = new StringBuilder();
                        DumpDeviceIface(tag, "enum", i, arr[i], sb, outDir);
                        meta.Append(sb);
                        try { Marshal.Release(arr[i]); } catch { /* */ }
                    }
                }
                finally
                {
                    gch.Free();
                }
            }

            // Also try Enumerate2 with reversed args (count, devices)
            {
                IntPtr pVar = Marshal.AllocHGlobal(24);
                IntPtr pCount = Marshal.AllocHGlobal(4);
                for (int i = 0; i < 24; i++) Marshal.WriteByte(pVar, i, 0);
                Marshal.WriteInt32(pCount, 0);
                // call as (count, var) by swapping — need alternate interface
                meta.AppendLine("(see enumerate2 file for normal order)");
                Marshal.FreeHGlobal(pVar);
                Marshal.FreeHGlobal(pCount);
            }

            // Two-phase Enumerate2 (real MB protocol)
            {
                IntPtr pVar = Marshal.AllocHGlobal(24);
                IntPtr pCount = Marshal.AllocHGlobal(4);
                for (int i = 0; i < 24; i++) Marshal.WriteByte(pVar, i, 0);
                Marshal.WriteInt32(pCount, 0);
                int hr = h2.Enumerate2(pVar, pCount);
                uint count = unchecked((uint)Marshal.ReadInt32(pCount));
                meta.AppendLine($"enum2 phase1 hr=0x{hr:X8} count={count} vt=0x{Marshal.ReadInt16(pVar):X}");
                Console.WriteLine($"  Enumerate2 phase1 hr=0x{hr:X8} count={count}");

                for (int i = 0; i < 24; i++) Marshal.WriteByte(pVar, i, 0);
                Marshal.WriteInt32(pCount, (int)Math.Max(count, 1));
                hr = h2.Enumerate2(pVar, pCount);
                count = unchecked((uint)Marshal.ReadInt32(pCount));
                short vt = Marshal.ReadInt16(pVar, 0);
                IntPtr p0 = Marshal.ReadIntPtr(pVar, 8);
                meta.AppendLine($"enum2 phase2 hr=0x{hr:X8} count={count} vt=0x{vt:X} p0=0x{p0.ToInt64():X}");
                Console.WriteLine($"  Enumerate2 phase2 hr=0x{hr:X8} count={count} vt=0x{vt:X}");
                if ((vt & VariantVt.VT_ARRAY) != 0 && p0 != IntPtr.Zero)
                {
                    var sb = new StringBuilder();
                    DumpSafeArray(tag, "enum2b", p0, sb, outDir);
                    meta.Append(sb);
                }
                else if ((vt == (short)VariantVt.VT_UNKNOWN || vt == (short)VariantVt.VT_DISPATCH) && p0 != IntPtr.Zero)
                {
                    var sb = new StringBuilder();
                    DumpDeviceIface(tag, "enum2b", 0, p0, sb, outDir);
                    meta.Append(sb);
                }
                Marshal.FreeHGlobal(pVar);
                Marshal.FreeHGlobal(pCount);
            }

            File.WriteAllText(Path.Combine(outDir, $"{tag}-enumerate.txt"), meta.ToString());
            Console.WriteLine("  Enumerate meta written");
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, $"{tag}-enumerate-error.txt"), ex.ToString());
            Console.WriteLine("  Enumerate error: " + ex.Message);
        }
    }

    static void DumpEnumerateDram(string tag, IAacLedDeviceHal h, string outDir)
    {
        try
        {
            uint count = 0;
            int hr = h.Enumerate(IntPtr.Zero, ref count);
            File.WriteAllText(Path.Combine(outDir, $"{tag}-enumerate.txt"), $"hr=0x{hr:X8} count={count}");
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, $"{tag}-enumerate-error.txt"), ex.ToString());
        }
    }
}
