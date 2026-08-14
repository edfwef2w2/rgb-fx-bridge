using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace RgbFx.UI.Aura;

/// <summary>Register / unregister the RgbFx AAC HAL. Must run elevated. No scripts.</summary>
public static class AuraHalOps
{
    public const string Clsid = "{B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73}";
    public const string ClsidBare = "B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73";
    const string CategoryRoot = "{9C9E903E-BBC7-4A0E-8326-ED6AC85B9FCC}";
    const string CategoryInst = "{E9BBD754-6CF4-492E-BA89-782177A2771B}";
    const string ProgId = "RgbFx.AacHal.1";
    const string ProgIdVi = "RgbFx.AacHal";
    const string TypeLib = "{57E4E792-2CF6-48E5-BF2B-10F19F857B9E}";
    const string DeviceType = "Extension Card";

    static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "RgbFx", "AacHal");

    public static string LogPath => Path.Combine(DataDir, "setup.log");

    public static int Add()
    {
        Directory.CreateDirectory(DataDir);
        var log = new StringBuilder();
        try
        {
            var exe = FindHalExe();
            if (exe is null)
            {
                log.AppendLine("HAL host not found (hal/AuraCapabilityDump.exe)");
                WriteLog(log);
                return 2;
            }

            var display = ReadDisplayName();
            File.WriteAllText(Path.Combine(DataDir, "display-name.txt"), display);
            File.WriteAllText(Path.Combine(DataDir, "type.txt"), "983040");
            log.AppendLine("DisplayName=" + display);
            log.AppendLine("HAL=" + exe);

            var localServer = "\"" + exe + "\" --server";
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                WriteClsidAndCategory(view, localServer, display, log);

            WriteProgId(RegistryView.Registry64, log);
            WriteProgId(RegistryView.Registry32, log);
            UpsertDeviceInfo(display, log);

            StopProcess("AuraCapabilityDump");
            BounceAuraStack(log);
            log.AppendLine("add ok");
            WriteLog(log);
            return 0;
        }
        catch (Exception ex)
        {
            log.AppendLine(ex.ToString());
            WriteLog(log);
            return 1;
        }
    }

    public static int Remove()
    {
        Directory.CreateDirectory(DataDir);
        var log = new StringBuilder();
        try
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                DeleteClsidAndCategory(view, log);

            DeleteProgId(RegistryView.Registry64, log);
            DeleteProgId(RegistryView.Registry32, log);
            StripDeviceInfo(log);

            StopProcess("AuraCapabilityDump");
            BounceAuraStack(log);
            log.AppendLine("remove ok");
            WriteLog(log);
            return 0;
        }
        catch (Exception ex)
        {
            log.AppendLine(ex.ToString());
            WriteLog(log);
            return 1;
        }
    }

    public static string? FindHalExe()
    {
        foreach (var p in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "hal", "AuraCapabilityDump.exe"),
                     Path.Combine(AppContext.BaseDirectory, "..", "hal", "AuraCapabilityDump.exe"),
                     Path.Combine(AppContext.BaseDirectory, "AuraCapabilityDump.exe"),
                 })
        {
            if (File.Exists(p))
                return Path.GetFullPath(p);
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            foreach (var leaf in new[] { "aachal-x86-v9", "aachal-x86-v8", "aachal-x86" })
            {
                var p = Path.Combine(dir.FullName, "artifacts", leaf, "AuraCapabilityDump.exe");
                if (File.Exists(p))
                    return Path.GetFullPath(p);
            }
        }

        return null;
    }

    static string ReadDisplayName()
    {
        try
        {
            var f = Path.Combine(DataDir, "display-name.txt");
            if (File.Exists(f))
            {
                var s = File.ReadAllText(f).Trim();
                if (s.Length > 0)
                    return AuraHalStatus.SanitizeHostName(s);
            }
        }
        catch { /* ignore */ }
        return AuraHalStatus.SanitizeHostName(Environment.MachineName);
    }

    static void WriteClsidAndCategory(RegistryView view, string localServer, string display, StringBuilder log)
    {
        using var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        var clsidRoot = @"SOFTWARE\Classes\CLSID\" + Clsid;
        using (var k = hk.CreateSubKey(clsidRoot, true))
        {
            k.SetValue(null, "RgbFx AAC HAL");
            using (var ls = k.CreateSubKey("LocalServer32", true))
                ls.SetValue(null, localServer);
            using (var p = k.CreateSubKey("ProgID", true))
                p.SetValue(null, ProgId);
            using (var p = k.CreateSubKey("VersionIndependentProgID", true))
                p.SetValue(null, ProgIdVi);
            using (var t = k.CreateSubKey("TypeLib", true))
                t.SetValue(null, TypeLib);
            using (var v = k.CreateSubKey("Version", true))
                v.SetValue(null, "1.0");
        }
        log.AppendLine("CLSID " + view + " " + clsidRoot);

        var inst = $@"SOFTWARE\Classes\CLSID\{CategoryRoot}\Instance\{CategoryInst}\Instance\{Clsid}";
        using (var k = hk.CreateSubKey(inst, true))
        {
            k.SetValue("Name", display);
            k.SetValue("Description", "RgbFx lighting bridge");
            k.SetValue("Manufacturer", "ASUSTeK COMPUTER INC.");
            k.SetValue("DeviceModel", display);
            k.SetValue("DeviceType", DeviceType);
            k.SetValue("Version", "0.1.0");
            k.SetValue("SpecVersion", "1.0.0");
            k.SetValue("Pluging", 1, RegistryValueKind.DWord);
        }
        log.AppendLine("Category " + view);
    }

    static void DeleteClsidAndCategory(RegistryView view, StringBuilder log)
    {
        using var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        TryDelete(hk, @"SOFTWARE\Classes\CLSID\" + Clsid, log);
        TryDelete(hk,
            $@"SOFTWARE\Classes\CLSID\{CategoryRoot}\Instance\{CategoryInst}\Instance\{Clsid}",
            log);
    }

    static void WriteProgId(RegistryView view, StringBuilder log)
    {
        using var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using (var k = hk.CreateSubKey(@"SOFTWARE\Classes\" + ProgId, true))
        {
            k.SetValue(null, "RgbFx AAC HAL");
            using var c = k.CreateSubKey("CLSID", true);
            c.SetValue(null, Clsid);
        }
        using (var k = hk.CreateSubKey(@"SOFTWARE\Classes\" + ProgIdVi, true))
        {
            k.SetValue(null, "RgbFx AAC HAL");
            using (var c = k.CreateSubKey("CLSID", true))
                c.SetValue(null, Clsid);
            using var v = k.CreateSubKey("CurVer", true);
            v.SetValue(null, ProgId);
        }
        log.AppendLine("ProgId " + view);
    }

    static void DeleteProgId(RegistryView view, StringBuilder log)
    {
        using var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        TryDelete(hk, @"SOFTWARE\Classes\" + ProgId, log);
        TryDelete(hk, @"SOFTWARE\Classes\" + ProgIdVi, log);
    }

    static void TryDelete(RegistryKey hk, string path, StringBuilder log)
    {
        try
        {
            hk.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
            log.AppendLine("removed " + path);
        }
        catch (Exception ex)
        {
            log.AppendLine("skip " + path + " " + ex.Message);
        }
    }

    static void UpsertDeviceInfo(string display, StringBuilder log)
    {
        var section = BuildIniSection(display);
        foreach (var ini in DeviceInfoPaths())
        {
            if (!File.Exists(ini))
                continue;
            try
            {
                var (text, enc) = ReadIni(ini);
                var stripped = StripClsidSection(text);
                File.WriteAllText(ini, stripped.TrimEnd() + section, enc);
                log.AppendLine("deviceinfo " + display + " -> " + ini);
            }
            catch (Exception ex)
            {
                log.AppendLine("skip deviceinfo " + ini + " " + ex.Message);
            }
        }
    }

    static void StripDeviceInfo(StringBuilder log)
    {
        foreach (var ini in DeviceInfoPaths())
        {
            if (!File.Exists(ini))
                continue;
            try
            {
                var (text, enc) = ReadIni(ini);
                var next = StripClsidSection(text);
                if (next != text)
                {
                    File.WriteAllText(ini, next, enc);
                    log.AppendLine("stripped " + ini);
                }
            }
            catch (Exception ex)
            {
                log.AppendLine("skip deviceinfo " + ini + " " + ex.Message);
            }
        }
    }

    static string BuildIniSection(string display) => $@"

[{display}]
Name={display}
DisplayName={display}
DeviceType=Extension_Card
PrimitiveDeviceType=Extension_Card
LStype=Extension_Card
DeviceCount=1
LightingMode=SUPPORTAURA
PID=none
PIDMode=none
Mode=none
GUID={ClsidBare}
ErrorCode=0
SyncStatus=true
NeedRestart=false
Plugin=1
Firmware_Count=0
HAL_Count=1
HAL_regkey_1={ClsidBare}
HAL_regkeyname_1=Version
Parameters_Count=0
HTML_Count=0
SDK_Count=0
FirmwareFlow=0
SupportMatrix=0
MatrixUpdateStatus=0
DependentJsonVersion=0
StageRollOutSkipDownload=0
";

    static string StripClsidSection(string raw)
        => Regex.Replace(raw, @"(?ms)^\[.*?\](?:(?!^\[).)*?" + Regex.Escape(ClsidBare) + @"(?:(?!^\[).)*", "");

    static IEnumerable<string> DeviceInfoPaths()
    {
        yield return @"C:\ProgramData\ASUS\ROG Live Service\deviceinfo.ini";
        yield return @"C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\ROG Live Service\deviceinfo.ini";
    }

    static (string text, Encoding enc) ReadIni(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (Encoding.Unicode.GetString(bytes), Encoding.Unicode);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (Encoding.UTF8.GetString(bytes), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return (Encoding.UTF8.GetString(bytes), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Restart lighting backends and close Armoury UI. Does not reopen the UI. Does not touch ROG Live Service.</summary>
    static void BounceAuraStack(StringBuilder log)
    {
        foreach (var name in new[] { "ArmouryCrate", "ArmouryCrate.UserSessionHelper", "ArmouryCrate.Service" })
            StopProcess(name);

        BounceService("LightingService", log);
        BounceService("ArmouryCrateService", log);
        log.AppendLine("Armoury UI closed; backends bounced (UI not relaunched)");
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

    static void StopProcess(string name)
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

    static void WriteLog(StringBuilder log)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(LogPath, log.ToString());
        }
        catch { /* ignore */ }
    }
}
