using System.Diagnostics;
using System.Text;

namespace RgbFx.UI.Aura;

static class AuraStackBounce
{
    public static void Run(StringBuilder log)
    {
        foreach (var name in new[] { "ArmouryCrate", "ArmouryCrate.UserSessionHelper", "ArmouryCrate.Service" })
            StopProcess(name);

        BounceService("LightingService", log);
        BounceService("ArmouryCrateService", log);
        log.AppendLine("Armoury UI closed; backends bounced (UI not relaunched)");
    }

    public static void StopProcess(string name)
    {
        try
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(4000);
                }
                catch { /* ignore */ }
                finally { p.Dispose(); }
            }
        }
        catch { /* ignore */ }
    }

    static void BounceService(string name, StringBuilder log)
    {
        RunSc("stop", name);
        Thread.Sleep(800);
        var code = RunSc("start", name);
        log.AppendLine(name + (code == 0 ? " started" : " start skipped/" + code));
    }

    static int RunSc(string verb, string name)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = verb + " \"" + name + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p is null)
                return -1;
            p.WaitForExit(15000);
            return p.ExitCode;
        }
        catch
        {
            return -1;
        }
    }
}
