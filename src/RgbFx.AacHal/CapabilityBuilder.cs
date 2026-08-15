using System.Text;

namespace RgbFx.AacHal;

/// <summary>
/// Builds GetCapability BSTR matching real MB AddressableStrip XML
/// (dumped via --mb-dump with precount Enumerate2).
/// </summary>
public static class CapabilityBuilder
{
    /// <summary>Fallback only when handshake lighting.json is missing.</summary>
    public const int DefaultLedCount = 120;

    /// <summary>Fallback hardware clip length when handshake is missing.</summary>
    public const int DefaultHwLeds = 72;

    /// <summary>Upper clamp for GetCapability / SetManualLedCount (not the reported count).</summary>
    public const int MaxReportedLeds = 500;

    /// <summary>0x11000 — AddressableStrip (same as MB Addressable HEADER devices).</summary>
    public const int TypeAddressableStrip = 0x11000; // 69632

    /// <summary>Neighbor type in ASUS HAL tables (experimental card/terminal family).</summary>
    public const int TypeAddressableFamily2 = 0x12000; // 73728

    /// <summary>0x80000 — Keyboard. LS lightingname Keyboard; AuAacKeyboard wants a 2D key grid.</summary>
    public const int TypeKeyboard = 0x80000; // 524288

    /// <summary>Default JRAINBOW bead count. Do not use this to clamp Aura-facing led_count.</summary>
    public const int RainbowMaxLeds = DefaultHwLeds;

    public const int KeyboardWidth = 20;
    public const int KeyboardHeight = 6;
    public const int KeyboardLedCount = DefaultLedCount;

    public static bool IsKeyboard(int type) => type == TypeKeyboard;

    public static int ResolveType()
    {
        int type = TypeKeyboard;
        // Install writes type.txt. A leftover RGBFX_AAC_TYPE (Machine) from an older
        // strip/extcard experiment must not override Keyboard after reboot.
        var typeEnv = TryReadTypeFile() ?? Environment.GetEnvironmentVariable("RGBFX_AAC_TYPE");
        if (string.IsNullOrWhiteSpace(typeEnv))
            return type;
        if (typeEnv.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            int.TryParse(typeEnv.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out type);
        else
            int.TryParse(typeEnv, out type);
        return type;
    }

    static string? TryReadTypeFile()
    {
        try
        {
            var typeFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "RgbFx", "AacHal", "type.txt");
            if (File.Exists(typeFile))
            {
                var s = File.ReadAllText(typeFile).Trim();
                if (s.Length > 0)
                    return s;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    public static string BuildDefault(
        int ledCount = DefaultLedCount,
        string? title = null,
        int argbId = 3,
        IReadOnlyList<string>? ledNames = null)
    {
        var path = Environment.GetEnvironmentVariable("RGBFX_AAC_CAPABILITY_FILE");
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return File.ReadAllText(path);

        ledCount = Math.Clamp(ledCount, 1, 500);
        int type = ResolveType();

        if (title is null && int.TryParse(Environment.GetEnvironmentVariable("RGBFX_ARGB_ID"), out var aid) && aid >= 0)
            argbId = aid;

        var host = ReadDisplayNameFile();
        if (string.IsNullOrWhiteSpace(host))
            host = "device";
        var deviceName = XmlText(string.IsNullOrWhiteSpace(title) ? host : title.Trim());
        var model = deviceName;

        if (IsKeyboard(type))
            return BuildKeyboard(deviceName, model, ledCount, ledNames);

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
        if (!string.Equals(mfr, "-", StringComparison.Ordinal))
            sb.AppendLine($"        <manufacturer>{mfr}</manufacturer>");
        sb.AppendLine($"        <model>{model}</model>");
        sb.AppendLine("    </device>");
        sb.AppendLine("</root>");
        return sb.ToString();
    }

    static string BuildKeyboard(
        string deviceName,
        string model,
        int ledCount,
        IReadOnlyList<string>? ledNames)
    {
        int n = Math.Clamp(ledCount > 0 ? ledCount : DefaultLedCount, 1, MaxReportedLeds);
        var names = new string[n];
        for (int i = 0; i < n; i++)
            names[i] = "LED" + (i + 1);
        _ = ledNames;

        string mfr = Environment.GetEnvironmentVariable("RGBFX_DEVICE_MFR") ?? "";
        if (string.IsNullOrWhiteSpace(mfr))
            mfr = "ASUSTeK COMPUTER INC.";

        var sb = new StringBuilder(4096);
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="no" ?>""");
        sb.AppendLine("<root>");
        sb.AppendLine("    <version>1</version>");
        sb.AppendLine($"    <type>{TypeKeyboard}</type>");
        sb.AppendLine("    <device>");
        sb.AppendLine($"        <name>{deviceName}</name>");
        sb.AppendLine("        <id>0</id>");
        sb.AppendLine("        <layout>");
        sb.AppendLine($"            <led_count>{n}</led_count>");
        sb.AppendLine("            <varied>0</varied>");
        sb.AppendLine("            <static_id>0</static_id>");
        sb.AppendLine("            <size>");
        sb.AppendLine($"                <width>{n}</width>");
        sb.AppendLine("                <height>1</height>");
        sb.AppendLine("            </size>");
        sb.AppendLine("            <led_name>");
        foreach (var name in names)
            sb.AppendLine($"                <led>{XmlText(name)}</led>");
        sb.AppendLine("            </led_name>");
        sb.AppendLine("            <led_location_index>");
        for (int i = 0; i < n; i++)
            sb.AppendLine($"                <index>{i}</index>");
        sb.AppendLine("            </led_location_index>");
        sb.AppendLine("        </layout>");
        sb.AppendLine("        <supported_effect>");
        AppendEffect(sb, "Manual", 0, 0);
        AppendEffect(sb, "Default", 255, 0);
        AppendEffect(sb, "Static", 1, 0);
        AppendEffect(sb, "Breathing", 2, 1);
        AppendEffect(sb, "Color cycle", 4, 1);
        AppendEffect(sb, "Rainbow", 5, 0);
        AppendEffect(sb, "Wave", 11, 0);
        AppendEffect(sb, "Starry Night", 13, 0);
        sb.AppendLine("        </supported_effect>");
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

    static string XmlText(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&apos;"); break;
                default:
                    if (c is >= ' ' and <= '~' || c is '-' or '_')
                        sb.Append(c);
                    break;
            }
        }
        var t = sb.ToString().Trim();
        return t.Length > 0 ? t : "device";
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
