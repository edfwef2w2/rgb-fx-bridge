using System.Text;

namespace RgbFx.UI.Aura;

/// <summary>Safe deviceinfo.ini edits. Official AddressableHeader is restored, not expanded.</summary>
public static class AuraDeviceInfo
{
    public const string ClsidBare = AuraHalOps.ClsidBare;

    static readonly string[] OfficialSectionNames = { "ALL MB", "AddressableHeader", "Memory" };

    static readonly string[] IniPaths =
    {
        @"C:\ProgramData\ASUS\ROG Live Service\deviceinfo.ini",
        @"C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\ROG Live Service\deviceinfo.ini",
    };

    public static void UpsertIndependentSection(string display, int zoneCount, StringBuilder log)
    {
        display = SanitizeIniName(display);
        zoneCount = Math.Clamp(zoneCount, 1, 16);
        foreach (var ini in IniPaths)
        {
            if (!File.Exists(ini))
                continue;
            try
            {
                var (text, enc) = ReadIni(ini);
                var originalLen = text.Length;
                var sections = ParseIni(text);
                var keptOfficial = OfficialSectionNames
                    .Where(n => sections.Any(s => HeaderEquals(s.Header, n)))
                    .ToArray();

                sections.RemoveAll(OwnsOurClsid);
                sections.Add(new IniSection("[" + display + "]", BuildExtCardLines(display, zoneCount)));

                var missing = keptOfficial.FirstOrDefault(name => !sections.Any(s => HeaderEquals(s.Header, name)));
                if (missing is not null)
                {
                    log.AppendLine("ABORT deviceinfo would drop " + missing + " " + ini);
                    continue;
                }

                var next = RenderIni(sections);
                if (originalLen >= 2000 && next.Length < originalLen * 3 / 5)
                {
                    log.AppendLine("ABORT deviceinfo shrink " + originalLen + " -> " + next.Length + " " + ini);
                    continue;
                }

                EnsureOfficialBackup(ini);
                File.WriteAllText(ini, next, enc);
                log.AppendLine("deviceinfo " + AuraPersona.RlsDeviceType + " " + display +
                               " " + originalLen + "->" + next.Length + " -> " + ini);
            }
            catch (Exception ex)
            {
                log.AppendLine("skip deviceinfo " + ini + " " + ex.Message);
            }
        }

        CleanupLeftoverKits(log);
    }

    public static void RestoreOfficialGroup(int officialHeaders, StringBuilder log)
    {
        officialHeaders = Math.Clamp(officialHeaders, 0, 16);
        foreach (var ini in IniPaths)
        {
            if (!File.Exists(ini))
                continue;
            try
            {
                var (text, enc) = ReadIni(ini);
                var sections = ParseIni(text);
                var changed = sections.RemoveAll(OwnsOurClsid) > 0;
                if (officialHeaders > 0)
                    changed |= RestoreAddressableHeader(sections, officialHeaders);
                if (!changed)
                    continue;
                File.WriteAllText(ini, RenderIni(sections), enc);
                log.AppendLine("restored AddressableHeader DeviceCount=" + officialHeaders + " " + ini);
            }
            catch (Exception ex)
            {
                log.AppendLine("skip deviceinfo " + ini + " " + ex.Message);
            }
        }

        CleanupLeftoverKits(log);
    }

    public static string ReadText(string path) => ReadIni(path).text;

    public static int ReadSectionDeviceCount(string text, string sectionName)
    {
        var sec = ParseIni(text).FirstOrDefault(s => HeaderEquals(s.Header, sectionName));
        if (sec is null)
            return 0;
        foreach (var line in sec.Lines)
        {
            var t = line.Trim();
            if (!t.StartsWith("DeviceCount=", StringComparison.OrdinalIgnoreCase))
                continue;
            return int.TryParse(t.AsSpan("DeviceCount=".Length), out var n) ? n : 0;
        }

        return 0;
    }

    static void StripOurSectionsOnly(StringBuilder log)
    {
        foreach (var ini in IniPaths)
        {
            if (!File.Exists(ini))
                continue;
            try
            {
                var (text, enc) = ReadIni(ini);
                var sections = ParseIni(text);
                if (sections.RemoveAll(OwnsOurClsid) == 0)
                    continue;
                File.WriteAllText(ini, RenderIni(sections), enc);
                log.AppendLine("stripped our RLS section " + ini);
            }
            catch (Exception ex)
            {
                log.AppendLine("skip strip " + ini + " " + ex.Message);
            }
        }
    }

    static bool OwnsOurClsid(IniSection s)
    {
        foreach (var line in s.Lines)
        {
            var t = line.Trim();
            if (t.StartsWith("GUID=", StringComparison.OrdinalIgnoreCase) &&
                t.AsSpan(4).TrimStart('=').Trim().Equals(ClsidBare, StringComparison.OrdinalIgnoreCase))
                return true;
            if (t.StartsWith("HAL_regkey_", StringComparison.OrdinalIgnoreCase) &&
                t.Contains(ClsidBare, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static List<string> BuildExtCardLines(string display, int zoneCount)
    {
        return new List<string>
        {
            "Name=" + display,
            "DisplayName=" + display,
            "DeviceType=" + AuraPersona.RlsDeviceType,
            "PrimitiveDeviceType=" + AuraPersona.RlsDeviceType,
            "LStype=" + AuraPersona.RlsDeviceType,
            "DeviceCount=" + zoneCount,
            "LightingMode=SUPPORTAURA",
            "PID=none",
            "PIDMode=none",
            "Mode=none",
            "GUID=" + ClsidBare,
            "ErrorCode=0",
            "SyncStatus=true",
            "NeedRestart=false",
            "Plugin=1",
            "Firmware_Count=0",
            "HAL_Count=1",
            "HAL_regkey_1=" + ClsidBare,
            "HAL_regkeyname_1=Version",
            "Parameters_Count=0",
            "HTML_Count=0",
            "SDK_Count=0",
            "FirmwareFlow=0",
            "SupportMatrix=0",
            "MatrixUpdateStatus=0",
            "DependentJsonVersion=0",
            "StageRollOutSkipDownload=0",
        };
    }

    static string SanitizeIniName(string raw)
    {
        var s = AuraHalStatus.SanitizeHostName(raw);
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_')
                sb.Append(c);
        }

        s = sb.ToString().Trim('-', '_');
        if (s.Length > 48)
            s = s[..48];
        return s.Length > 0 ? s : "RgbFx";
    }

    static bool RestoreAddressableHeader(List<IniSection> sections, int officialHeaders)
    {
        var sec = sections.FirstOrDefault(s => HeaderEquals(s.Header, AuraPersona.OfficialSection));
        if (sec is null)
            return false;
        var before = string.Join('\n', sec.Lines);
        SetKey(sec, "DeviceCount", officialHeaders.ToString());
        sec.Lines.RemoveAll(l => LedIndex(l) > officialHeaders);
        return before != string.Join('\n', sec.Lines);
    }

    static int LedIndex(string line)
    {
        var t = line.Trim();
        const string type = "LEDType_";
        const string count = "LEDCount_";
        string? rest = null;
        if (t.StartsWith(type, StringComparison.OrdinalIgnoreCase))
            rest = t[type.Length..];
        else if (t.StartsWith(count, StringComparison.OrdinalIgnoreCase))
            rest = t[count.Length..];
        if (rest is null)
            return -1;
        var eq = rest.IndexOf('=');
        if (eq >= 0)
            rest = rest[..eq];
        return int.TryParse(rest, out var n) ? n : -1;
    }

    static void CleanupLeftoverKits(StringBuilder log)
    {
        var official = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ALL MB", "AddressableHeader", "Memory",
        };
        foreach (var root in KitRoots())
        {
            if (!Directory.Exists(root))
                continue;
            foreach (var dir in Directory.GetDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (official.Contains(name))
                    continue;
                try
                {
                    var files = Directory.GetFiles(dir);
                    if (files.Length <= 4 &&
                        files.All(f =>
                        {
                            var n = Path.GetFileName(f);
                            return n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                                   || n.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
                        }))
                    {
                        Directory.Delete(dir, recursive: true);
                        log.AppendLine("removed leftover kit " + dir);
                    }
                }
                catch (Exception ex)
                {
                    log.AppendLine("skip kit " + dir + " " + ex.Message);
                }
            }
        }
    }

    static IEnumerable<string> KitRoots()
    {
        yield return @"C:\ProgramData\ASUS\ROG Live Service\DeviceContent";
        var pkg = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages");
        if (!Directory.Exists(pkg))
            yield break;
        foreach (var dir in Directory.GetDirectories(pkg, "B9ECED6F.AURACreator_*"))
            yield return Path.Combine(dir, "LocalState", "Devices");
    }

    static void EnsureOfficialBackup(string ini)
    {
        var bak = ini + ".bak-rgbfx";
        try
        {
            if (!File.Exists(bak))
                File.Copy(ini, bak);
        }
        catch { /* ignore */ }
    }

    sealed class IniSection
    {
        public IniSection(string header, List<string> lines)
        {
            Header = header;
            Lines = lines;
        }

        public string Header { get; }
        public List<string> Lines { get; }
    }

    static List<IniSection> ParseIni(string text)
    {
        var sections = new List<IniSection>();
        var header = "";
        var lines = new List<string>();
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var t = raw.Trim();
            if (t.StartsWith('[') && t.EndsWith(']') && t.Length >= 2)
            {
                sections.Add(new IniSection(header, lines));
                header = t;
                lines = new List<string>();
            }
            else
            {
                lines.Add(raw);
            }
        }

        sections.Add(new IniSection(header, lines));
        return sections;
    }

    static string RenderIni(List<IniSection> sections)
    {
        var sb = new StringBuilder();
        foreach (var s in sections)
        {
            if (!string.IsNullOrEmpty(s.Header))
                sb.Append(s.Header).Append('\n');
            foreach (var line in s.Lines)
                sb.Append(line).Append('\n');
        }

        return sb.ToString().TrimEnd('\n') + "\n";
    }

    static bool HeaderEquals(string header, string name)
    {
        var h = header.Trim();
        if (h.StartsWith('[') && h.EndsWith(']'))
            h = h[1..^1];
        return h.Equals(name, StringComparison.OrdinalIgnoreCase);
    }

    static void SetKey(IniSection sec, string key, string value)
    {
        var prefix = key + "=";
        for (var i = 0; i < sec.Lines.Count; i++)
        {
            var t = sec.Lines[i].TrimStart();
            if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                sec.Lines[i] = prefix + value;
                return;
            }
        }

        sec.Lines.Add(prefix + value);
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
}
