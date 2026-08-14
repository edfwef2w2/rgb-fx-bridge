using System.Diagnostics;
using Microsoft.Win32;

namespace RgbFx.Setup;

static class Program
{
    const string Product = "RgbFx Bridge";
    const string UninstallId = "RgbFx";

    static string TargetDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RgbFx");

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var remove = args.Any(a =>
            string.Equals(a, "--remove", StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase));
        try
        {
            var msg = remove ? RemoveApp() : AddApp();
            MessageBox.Show(msg, Product, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Product, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }

    static string AddApp()
    {
        var root = AppContext.BaseDirectory;
        var appSrc = Path.Combine(root, "app");
        var halSrc = Path.Combine(root, "hal");
        if (!Directory.Exists(appSrc) || !File.Exists(Path.Combine(appSrc, "RgbFx.UI.exe")))
            throw new InvalidOperationException("Missing app\\RgbFx.UI.exe next to Setup.");
        if (!Directory.Exists(halSrc) || !File.Exists(Path.Combine(halSrc, "AuraCapabilityDump.exe")))
            throw new InvalidOperationException("Missing hal\\AuraCapabilityDump.exe next to Setup.");

        var dest = TargetDir;
        CopyTree(appSrc, dest);
        CopyTree(halSrc, Path.Combine(dest, "hal"));
        var self = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(self) && File.Exists(self))
            File.Copy(self, Path.Combine(dest, "RgbFx.Setup.exe"), overwrite: true);

        var exe = Path.Combine(dest, "RgbFx.UI.exe");
        var startDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs");
        Directory.CreateDirectory(startDir);
        File.WriteAllText(
            Path.Combine(startDir, Product + ".cmd"),
            "@start \"\" \"" + exe + "\"" + Environment.NewLine);

        WriteUninstallKey(exe, dest);
        return "Installed to " + dest;
    }

    static string RemoveApp()
    {
        var dest = TargetDir;
        try
        {
            var ui = Path.Combine(dest, "RgbFx.UI.exe");
            if (File.Exists(ui))
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = ui,
                    Arguments = "--hal-remove",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                p?.WaitForExit(60000);
            }
        }
        catch { /* still remove files */ }

        var lnk = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs", Product + ".cmd");
        if (File.Exists(lnk))
            File.Delete(lnk);

        if (Directory.Exists(dest))
            Directory.Delete(dest, recursive: true);

        using var hk = Registry.LocalMachine.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall", writable: true);
        hk?.DeleteSubKeyTree(UninstallId, throwOnMissingSubKey: false);
        return "Removed " + dest;
    }

    static void CopyTree(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(src, dest, StringComparison.OrdinalIgnoreCase));
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(src, dest, StringComparison.OrdinalIgnoreCase), overwrite: true);
    }

    static void WriteUninstallKey(string exe, string dest)
    {
        using var k = Registry.LocalMachine.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + UninstallId);
        k.SetValue("DisplayName", Product);
        k.SetValue("Publisher", "RgbFx");
        k.SetValue("InstallLocation", dest);
        k.SetValue("DisplayIcon", exe);
        k.SetValue("UninstallString", "\"" + Path.Combine(dest, "RgbFx.Setup.exe") + "\" --remove");
        k.SetValue("NoModify", 1, RegistryValueKind.DWord);
        k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }
}
