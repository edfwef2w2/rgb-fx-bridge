using System.Text;
using System.Xml.Linq;

namespace RgbFx.UI.Aura;

/// <summary>Live count of the motherboard's official ARGB headers. Never assume 3.</summary>
public static class AuraMbHeaders
{
    public static string PersistPath =>
        Path.Combine(AuraHalStatus.DataDir, "official-header-count.txt");

    public static int Detect(StringBuilder log)
    {
        var live = CountArgbHeadersInQueryAll();
        if (live > 0)
        {
            Persist(live);
            log.AppendLine("MB ARGB headers (QueryAllDevice)=" + live);
            return live;
        }

        var fromLog = CountFromLightingServiceLog();
        if (fromLog > 0)
        {
            Persist(fromLog);
            log.AppendLine("MB ARGB headers (LS NumberOfHals)=" + fromLog);
            return fromLog;
        }

        var stored = ReadPersisted();
        if (stored > 0)
        {
            log.AppendLine("MB ARGB headers (persisted)=" + stored);
            return stored;
        }

        var bak = CountFromDeviceInfoBak();
        if (bak > 0)
        {
            Persist(bak);
            log.AppendLine("MB ARGB headers (deviceinfo.bak)=" + bak);
            return bak;
        }

        log.AppendLine("MB ARGB headers unknown — skip expanding official group");
        return 0;
    }

    public static int ReadPersisted()
    {
        try
        {
            if (!File.Exists(PersistPath))
                return 0;
            return int.TryParse(File.ReadAllText(PersistPath).Trim(), out var n) ? Math.Clamp(n, 0, 16) : 0;
        }
        catch
        {
            return 0;
        }
    }

    static void Persist(int n)
    {
        try
        {
            Directory.CreateDirectory(AuraHalStatus.DataDir);
            File.WriteAllText(PersistPath, n.ToString());
        }
        catch { /* ignore */ }
    }

    static int CountArgbHeadersInQueryAll()
    {
        var path = @"C:\ProgramData\ASUS\RogAura30\QueryAllDevice.xml";
        try
        {
            if (!File.Exists(path))
                return 0;
            var doc = XDocument.Load(path);
            var n = 0;
            foreach (var d in doc.Descendants("device"))
            {
                var lighting = (string?)d.Element("lightingname") ?? (string?)d.Element("type") ?? "";
                var model = ((string?)d.Element("model") ?? "").Trim();
                if (!lighting.Equals("AddressableStrip", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (model.Equals("ARGB HEADER", StringComparison.OrdinalIgnoreCase))
                    n++;
            }

            return n;
        }
        catch
        {
            return 0;
        }
    }

    static int CountFromLightingServiceLog()
    {
        var path = @"C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\LightingService\LightingService.log";
        try
        {
            if (!File.Exists(path))
                return 0;
            var lines = File.ReadLines(path).Reverse().Take(4000);
            foreach (var line in lines)
            {
                const string mark = "Number of the ADD_HEADERs detected.) ==> ";
                var i = line.IndexOf(mark, StringComparison.Ordinal);
                if (i < 0)
                    continue;
                var rest = line[(i + mark.Length)..].Trim();
                if (int.TryParse(rest, out var n) && n > 0 && n <= 16)
                    return n;
            }
        }
        catch { /* ignore */ }
        return 0;
    }

    static int CountFromDeviceInfoBak()
    {
        foreach (var bak in new[]
                 {
                     @"C:\ProgramData\ASUS\ROG Live Service\deviceinfo.ini.bak-rgbfx",
                     @"C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\ROG Live Service\deviceinfo.ini.bak-rgbfx",
                 })
        {
            try
            {
                if (!File.Exists(bak))
                    continue;
                var text = AuraDeviceInfo.ReadText(bak);
                var n = AuraDeviceInfo.ReadSectionDeviceCount(text, AuraPersona.OfficialSection);
                if (n > 0 && n <= 16)
                    return n;
            }
            catch { /* ignore */ }
        }

        return 0;
    }
}
