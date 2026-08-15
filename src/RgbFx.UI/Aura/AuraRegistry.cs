using System.Text;
using Microsoft.Win32;

namespace RgbFx.UI.Aura;

/// <summary>
/// LightingService is x86 and enumerates
/// HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{9C9E…}\Instance\….
/// .NET RegistryView.Registry32 may land in Classes\WOW6432Node instead — write the WOW path explicitly.
/// </summary>
static class AuraRegistry
{
    public const string Clsid = AuraHalOps.Clsid;
    const string CategoryRoot = "{9C9E903E-BBC7-4A0E-8326-ED6AC85B9FCC}";
    const string CategoryInst = "{E9BBD754-6CF4-492E-BA89-782177A2771B}";
    const string ProgId = "RgbFx.AacHal.1";
    const string ProgIdVi = "RgbFx.AacHal";
    const string TypeLib = "{57E4E792-2CF6-48E5-BF2B-10F19F857B9E}";

    static readonly string[] ClsidRoots =
    {
        @"SOFTWARE\WOW6432Node\Classes\CLSID\",
        @"SOFTWARE\Classes\WOW6432Node\CLSID\",
        @"SOFTWARE\Classes\CLSID\",
    };

    public static void Write(string localServer, string display, StringBuilder log)
    {
        using var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        foreach (var root in ClsidRoots)
        {
            WriteClsid(hk, root + Clsid, localServer, log);
            WriteCategory(hk, root + CategoryRoot + @"\Instance\" + CategoryInst + @"\Instance\" + Clsid,
                display, log);
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            WriteProgId(view, log);

        if (!IsWowCategoryPresent())
            throw new InvalidOperationException(
                "HAL category missing under WOW6432Node\\Classes\\CLSID — LightingService will not load it");
        log.AppendLine("WOW category ok");
    }

    public static void Delete(StringBuilder log)
    {
        using var hk64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        foreach (var root in ClsidRoots)
        {
            TryDelete(hk64, root + Clsid, log);
            TryDelete(hk64, root + CategoryRoot + @"\Instance\" + CategoryInst + @"\Instance\" + Clsid, log);
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            TryDelete(hk, @"SOFTWARE\Classes\" + ProgId, log);
            TryDelete(hk, @"SOFTWARE\Classes\" + ProgIdVi, log);
            TryDelete(hk, @"SOFTWARE\Classes\CLSID\" + Clsid, log);
        }
    }

    public static bool IsWowCategoryPresent()
    {
        using var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var k = hk.OpenSubKey(
            @"SOFTWARE\WOW6432Node\Classes\CLSID\" + CategoryRoot +
            @"\Instance\" + CategoryInst + @"\Instance\" + Clsid);
        return k is not null;
    }

    static void WriteClsid(RegistryKey hk, string clsidPath, string localServer, StringBuilder log)
    {
        using (var k = hk.CreateSubKey(clsidPath, true))
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
        log.AppendLine("CLSID " + clsidPath);
    }

    static void WriteCategory(RegistryKey hk, string instPath, string display, StringBuilder log)
    {
        using (var k = hk.CreateSubKey(instPath, true))
        {
            k.SetValue("Name", display);
            k.SetValue("Description", "RgbFx lighting bridge");
            k.SetValue("Manufacturer", "ASUSTeK COMPUTER INC.");
            k.SetValue("DeviceModel", display);
            k.SetValue("DeviceType", AuraPersona.CategoryKind);
            k.SetValue("Version", "0.1.0");
            k.SetValue("SpecVersion", "1.0.0");
            k.SetValue("Pluging", 1, RegistryValueKind.DWord);
        }
        log.AppendLine("Category " + instPath);
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
}
