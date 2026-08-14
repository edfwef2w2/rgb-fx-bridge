using System.Text;

namespace RgbFx.AacHal;

/// <summary>
/// Builds GetCapability BSTR matching real MB AddressableStrip XML
/// (dumped via --mb-dump with precount Enumerate2).
/// </summary>
public static class CapabilityBuilder
{
    /// <summary>Default LED count; LS often SetManualLedCount(120) on varied strips.</summary>
    public const int DefaultLedCount = 120;

    /// <summary>0x11000 — AddressableStrip (same as MB ARGB HEADER devices).</summary>
    public const int TypeAddressableStrip = 0x11000; // 69632

    /// <summary>Neighbor type in ASUS HAL tables (experimental card/terminal family).</summary>
    public const int TypeAddressableFamily2 = 0x12000; // 73728

    public static string BuildDefault(int ledCount = DefaultLedCount)
    {
        var path = Environment.GetEnvironmentVariable("RGBFX_AAC_CAPABILITY_FILE");
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return File.ReadAllText(path);

        ledCount = Math.Clamp(ledCount, 1, 500);

        // Default 0xF0000 = Ext Header / Extension_Card — independent LS type (not merged into MB strips).
        // Override: RGBFX_AAC_TYPE env, or %ProgramData%\RgbFx\AacHal\type.txt
        // (LS service process does not pick up new Machine env without reboot).
        int type = 0xF0000;
        var typeEnv = Environment.GetEnvironmentVariable("RGBFX_AAC_TYPE");
        if (string.IsNullOrWhiteSpace(typeEnv))
        {
            try
            {
                var typeFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "RgbFx", "AacHal", "type.txt");
                if (File.Exists(typeFile))
                    typeEnv = File.ReadAllText(typeFile).Trim();
            }
            catch { /* ignore */ }
        }
        if (!string.IsNullOrWhiteSpace(typeEnv))
        {
            if (typeEnv.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                int.TryParse(typeEnv.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out type);
            else
                int.TryParse(typeEnv, out type);
        }

        // argb_id: must not collide with MB headers 0/1/2 or UI viewport merges/drops us.
        int argbId = 3;
        if (int.TryParse(Environment.GetEnvironmentVariable("RGBFX_ARGB_ID"), out var aid) && aid >= 0)
            argbId = aid;

        // Strip index for "AddressableStrip N" naming (MB uses 1..3).
        int stripIndex = argbId + 1;
        if (int.TryParse(Environment.GetEnvironmentVariable("RGBFX_STRIP_INDEX"), out var si) && si > 0)
            stripIndex = si;

        string deviceName = Environment.GetEnvironmentVariable("RGBFX_DEVICE_NAME") ?? "";
        if (string.IsNullOrWhiteSpace(deviceName))
            deviceName = $"AddressableStrip {stripIndex}";

        string? model = Environment.GetEnvironmentVariable("RGBFX_DEVICE_MODEL");
        if (string.IsNullOrWhiteSpace(model))
            model = ReadDisplayNameFile();
        if (string.IsNullOrWhiteSpace(model))
            model = Environment.MachineName;

        // MB strips use manufacture typo "Unkown" in QueryAllDevice; keep optional override.
        string mfr = Environment.GetEnvironmentVariable("RGBFX_DEVICE_MFR") ?? "";
        if (string.IsNullOrWhiteSpace(mfr))
            mfr = "Unkown";

        var sb = new StringBuilder(4096);
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="no" ?>""");
        sb.AppendLine("<root>");
        sb.AppendLine("    <version>1</version>");
        sb.AppendLine($"    <type>{type}</type>");
        sb.AppendLine("    <device>");
        sb.AppendLine($"        <name>{deviceName}</name>");
        sb.AppendLine("        <id>0</id>");
        sb.AppendLine("        <layout>");
        sb.AppendLine($"            <led_count>{ledCount}</led_count>");
        sb.AppendLine("            <varied>1</varied>");
        sb.AppendLine($"            <max_led_count>{Math.Max(ledCount, 500)}</max_led_count>");
        sb.AppendLine($"            <max_total_led_count>{Math.Max(ledCount, 500)}</max_total_led_count>");
        sb.AppendLine("            <static_id>0</static_id>");
        sb.AppendLine($"            <argb_id>{argbId}</argb_id>");
        sb.AppendLine("            <size>");
        sb.AppendLine($"                <width>{ledCount}</width>");
        sb.AppendLine("                <height>1</height>");
        sb.AppendLine("            </size>");
        sb.AppendLine("            <led_name>");
        for (int i = 1; i <= ledCount; i++)
            sb.AppendLine($"                <led>{deviceName}_{i}</led>");
        sb.AppendLine("            </led_name>");
        sb.AppendLine("        </layout>");
        sb.AppendLine("        <supported_effect>");
        // Order matches real MB AddressableStrip capability dump
        AppendEffect(sb, "Manual", 0, 0);
        AppendEffect(sb, "Default", 255, 0);
        AppendEffect(sb, "Static", 1, 0);
        AppendEffect(sb, "Breathing", 2, 1);
        AppendEffect(sb, "Strobing", 3, 0);
        AppendEffect(sb, "Color cycle", 4, 1);
        AppendEffect(sb, "Rainbow", 5, 0);
        AppendEffect(sb, "Comet", 8, 1);
        AppendEffect(sb, "Flash and Dash", 10, 0);
        AppendEffect(sb, "Wave", 11, 0);
        AppendEffect(sb, "Glowing Yoyo", 12, 0);
        AppendEffect(sb, "Starry Night", 13, 0);
        sb.AppendLine("        </supported_effect>");
        // MB dump often omits manufacturer and only has <model>
        if (!string.Equals(mfr, "-", StringComparison.Ordinal))
            sb.AppendLine($"        <manufacturer>{mfr}</manufacturer>");
        sb.AppendLine($"        <model>{model}</model>");
        sb.AppendLine("    </device>");
        sb.AppendLine("</root>");
        return sb.ToString();
    }

    static string? ReadDisplayNameFile()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "RgbFx", "AacHal", "display-name.txt");
            if (!File.Exists(path))
                return null;
            var s = File.ReadAllText(path).Trim();
            return s.Length == 0 ? null : s;
        }
        catch
        {
            return null;
        }
    }

    static void AppendEffect(StringBuilder sb, string name, int id, int sync)
    {
        sb.AppendLine("            <effect>");
        sb.AppendLine($"                <name>{name}</name>");
        sb.AppendLine($"                <id>{id}</id>");
        sb.AppendLine($"                <synchronizable>{sync}</synchronizable>");
        sb.AppendLine("            </effect>");
    }
}
