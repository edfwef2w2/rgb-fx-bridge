using System.Runtime.InteropServices;
using RgbFx.AacHal;

// COM LocalServer entry. Aura activates via LocalServer32.
// Also supports:
//   RgbFx.AacHal.exe --register-message   (print CLSID path help)
//   RgbFx.AacHal.exe --ping               (construct sink only)

if (args.Any(a => a is "-h" or "--help" or "/?"))
{
    Console.WriteLine("RgbFx.AacHal — Aura AAC HAL LocalServer");
    Console.WriteLine("  (no args / -Embedding)  register class object and wait");
    Console.WriteLine("  Env: RGBFX_MSI_URL, RGBFX_ZONES, RGBFX_LED_COUNT, RGBFX_AAC_CAPABILITY_FILE, RGBFX_COLOR_ORDER");
    return 0;
}

if (args.Any(a => a is "--ping"))
{
    using var sink = new MsiFrameSink();
    sink.ApplySolid(0x000000FF); // red-ish depending on order
    Console.WriteLine($"ping frames={sink.FramesSent} err={sink.LastError}");
    return sink.LastError is null ? 0 : 1;
}

Ole32.CoInitializeEx(IntPtr.Zero, Ole32.COINIT_APARTMENTTHREADED);

uint cookie = 0;
try
{
    var clsid = new Guid(AacIds.RgbFxHalClsid);
    var factory = new RgbFxAacHalClassFactory();
    // Get COM IClassFactory pointer via CCW
    int hr = Ole32.CoRegisterClassObject(
        ref clsid,
        factory,
        Ole32.CLSCTX_LOCAL_SERVER,
        Ole32.REGCLS_MULTIPLEUSE,
        out cookie);
    if (hr != 0)
    {
        Log($"CoRegisterClassObject failed 0x{hr:X8}");
        return 1;
    }

    Ole32.CoResumeClassObjects();
    Log($"LocalServer running CLSID={clsid} cookie={cookie}");

    // Keep process alive until killed (COM clients hold refs) or stdin closed when interactive
    using var exit = new ManualResetEvent(false);
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.Set(); };
    // Also exit when idle for a long time? Keep simple: wait forever / until cancel
    exit.WaitOne();
}
finally
{
    if (cookie != 0)
        Ole32.CoRevokeClassObject(cookie);
    Ole32.CoUninitialize();
}

return 0;

static void Log(string msg)
{
    Console.WriteLine(msg);
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
