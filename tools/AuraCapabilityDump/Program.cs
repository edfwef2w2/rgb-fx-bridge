using System.Runtime.InteropServices;
using System.Text;
using RgbFx.AacHal;

namespace AuraCapabilityDump;

/// <summary>
/// Dual host: capability dump + COM LocalServer (--server).
/// Built as AuraCapabilityDump.exe (also registered as RgbFx AAC HAL LocalServer32).
/// </summary>
internal static class Program
{
    static int Main(string[] args)
    {
        if (args.Any(a => a is "--server" or "-Embedding" or "/Embedding"))
            return RunComServer();
        if (args.Any(a => a is "--ping"))
            return RunPing();
        if (args.Any(a => a is "--mb-dump"))
        {
            var dir = args.SkipWhile(a => a != "--mb-dump").Skip(1).FirstOrDefault()
                      ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "mb-dump"));
            return MbEnumDump.Run(dir);
        }
        if (args.Any(a => a is "--self-dump"))
        {
            var dir = args.SkipWhile(a => a != "--self-dump").Skip(1).FirstOrDefault()
                      ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "self-dump"));
            return MbEnumDump.RunSelf(dir);
        }
        if (args.Any(a => a is "--native-style"))
        {
            var dir = args.SkipWhile(a => a != "--native-style").Skip(1).FirstOrDefault()
                      ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "native-style"));
            return NativeStyleEnum.Run(dir);
        }
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            Console.WriteLine("AuraCapabilityDump / RgbFx AAC HAL host");
            Console.WriteLine("  --dump [dir]    dump ASUS HAL capability probes (default)");
            Console.WriteLine("  --mb-dump [dir] deep Enumerate2/Enumerate dump of MB HAL");
            Console.WriteLine("  --self-dump     dump our RgbFx HAL out-of-proc");
            Console.WriteLine("  --native-style  raw vtable Enumerate2 like LightingService");
            Console.WriteLine("  --server        COM LocalServer for new RgbFx HAL CLSID");
            Console.WriteLine("  --ping          test MSI /api/v1/frame");
            Console.WriteLine("  Env RGBFX_ENUM2_MODE=arrayiid|vector|single|vararray|arraybase|empty");
            return 0;
        }

        return RunDump(args);
    }

    static int RunPing()
    {
        using var sink = new MsiFrameSink();
        sink.ApplySolid(0x000000FF);
        Console.WriteLine($"ping frames={sink.FramesSent} err={sink.LastError}");
        return sink.LastError is null ? 0 : 1;
    }

    static int RunComServer()
    {
        // MTA: LightingService calls from worker threads; STA without a message pump
        // breaks SAFEARRAY(IUnknown) marshal → DoEnumerateDevices EXCEPTION - 01.
        const uint COINIT_MULTITHREADED = 0x0;
        int initHr = Ole32.CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED);
        Console.WriteLine($"CoInitializeEx MTA hr=0x{initHr:X8}");
        uint cookie = 0;
        try
        {
            var clsid = new Guid(AacIds.RgbFxHalClsid);
            var factory = new RgbFxAacHalClassFactory();
            int hr = Ole32.CoRegisterClassObject(
                ref clsid,
                factory,
                Ole32.CLSCTX_LOCAL_SERVER,
                Ole32.REGCLS_MULTIPLEUSE,
                out cookie);
            if (hr != 0)
            {
                Console.WriteLine($"CoRegisterClassObject failed 0x{hr:X8}");
                return 1;
            }

            Ole32.CoResumeClassObjects();
            Console.WriteLine($"LocalServer MTA running CLSID={clsid}");
            try
            {
                File.AppendAllText(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "RgbFx", "AacHal", "aachal.log"),
                    $"[{DateTime.Now:O}] LocalServer MTA registered cookie={cookie}{Environment.NewLine}");
            }
            catch { /* ignore */ }

            using var exit = new ManualResetEvent(false);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.Set(); };
            // When started by COM, stay until process killed
            exit.WaitOne();
        }
        finally
        {
            if (cookie != 0)
                Ole32.CoRevokeClassObject(cookie);
            Ole32.CoUninitialize();
        }

        return 0;
    }

    static int RunDump(string[] args)
    {
        var pathArgs = args.Where(a => a is not "--dump" && !a.StartsWith('-')).ToArray();
        var outDir = pathArgs.Length > 0
            ? Path.GetFullPath(pathArgs[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "capability-dump"));
        Directory.CreateDirectory(outDir);
        Console.WriteLine($"Output: {outDir}");
        Ole32.CoInitializeEx(IntPtr.Zero, Ole32.COINIT_APARTMENTTHREADED);
        try
        {
            Dump("MB", new Guid(AacIds.MbHalClsid), outDir);
            Dump("EXT", new Guid(AacIds.ExtHalClsid), outDir);
            Dump("DRAM", new Guid(AacIds.DramHalClsid), outDir);
        }
        finally
        {
            Ole32.CoUninitialize();
        }

        Console.WriteLine("Done.");
        return 0;
    }

    static void Dump(string tag, Guid clsid, string outDir)
    {
        Console.WriteLine($"=== {tag} ===");
        var iid = Ole32.IidIUnknown;
        int hr = Ole32.CoCreateInstance(ref clsid, IntPtr.Zero,
            Ole32.CLSCTX_LOCAL_SERVER | Ole32.CLSCTX_INPROC_SERVER, ref iid, out var unk);
        Console.WriteLine($"  CoCreate hr=0x{hr:X8}");
        if (hr != 0 || unk is null)
        {
            File.WriteAllText(Path.Combine(outDir, $"{tag}-error.txt"), $"0x{hr:X8}");
            return;
        }

        try
        {
            var h2 = (IAsusAacLedDeviceHal2)unk;
            var pVar = Marshal.AllocHGlobal(24);
            var pCount = Marshal.AllocHGlobal(4);
            for (int i = 0; i < 24; i++) Marshal.WriteByte(pVar, i, 0);
            Marshal.WriteInt32(pCount, 0);
            hr = h2.Enumerate2(pVar, pCount);
            uint count = unchecked((uint)Marshal.ReadInt32(pCount));
            Console.WriteLine($"  Enumerate2 hr=0x{hr:X8} count={count}");
            File.WriteAllText(Path.Combine(outDir, $"{tag}-enumerate2.txt"), $"hr=0x{hr:X8} count={count}");
            Marshal.FreeHGlobal(pVar);
            Marshal.FreeHGlobal(pCount);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  " + ex.Message);
            File.WriteAllText(Path.Combine(outDir, $"{tag}-error.txt"), ex.ToString());
        }
        finally
        {
            Marshal.FinalReleaseComObject(unk);
        }
    }
}
