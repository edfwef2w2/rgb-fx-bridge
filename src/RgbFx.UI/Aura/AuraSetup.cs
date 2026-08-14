using System.Diagnostics;
using System.Security.Principal;

namespace RgbFx.UI.Aura;

/// <summary>Elevates this exe and runs <see cref="AuraHalOps"/>. No scripts.</summary>
public static class AuraSetup
{
    public static string SetupLogPath => AuraHalOps.LogPath;

    public static Task<(int code, string output)> InstallAsync()
        => RunAsync("--hal-add");

    public static Task<(int code, string output)> UninstallAsync()
        => RunAsync("--hal-remove");

    static async Task<(int code, string output)> RunAsync(string flag)
    {
        if (IsElevated())
        {
            var code = string.Equals(flag, "--hal-remove", StringComparison.OrdinalIgnoreCase)
                ? AuraHalOps.Remove()
                : AuraHalOps.Add();
            return (code, ReadLog());
        }

        var psi = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath ?? Application.ExecutablePath,
            Arguments = flag,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Normal,
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is null)
                return (-1, "failed to start elevated host");
            await p.WaitForExitAsync().ConfigureAwait(false);
            return (p.ExitCode, ReadLog());
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    static string ReadLog()
    {
        try
        {
            if (File.Exists(AuraHalOps.LogPath))
                return File.ReadAllText(AuraHalOps.LogPath).Trim();
        }
        catch { /* ignore */ }
        return "";
    }

    static bool IsElevated()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
