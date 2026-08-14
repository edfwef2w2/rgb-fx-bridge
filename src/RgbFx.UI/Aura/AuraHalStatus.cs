using System.Globalization;
using Microsoft.Win32;

namespace RgbFx.UI.Aura;

public sealed class AuraHalStatus
{
    public const string Clsid = "{B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73}";
    static readonly TimeSpan LiveWindow = TimeSpan.FromSeconds(12);

    public bool Registered { get; init; }
    public string? LocalServer { get; init; }
    public string? LastCapabilityAt { get; init; }
    public string? LastSetEffect { get; init; }
    public DateTime? LastSetEffectAt { get; init; }
    public string? LastFrameError { get; init; }
    public string LogTail { get; init; } = "";

    /// <summary>True only when a SetEffect line was written in the last few seconds.</summary>
    public bool IsSending =>
        LastSetEffectAt is { } t && DateTime.Now - t < LiveWindow;

    public static string LogPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "RgbFx", "AacHal", "aachal.log");

    public static string UrlFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "RgbFx", "AacHal", "msi-url.txt");

    public static string DisplayNamePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "RgbFx", "AacHal", "display-name.txt");

    public static AuraHalStatus Read()
    {
        string? ls = null;
        bool reg = false;
        foreach (var root in new[]
                 {
                     @"SOFTWARE\WOW6432Node\Classes\CLSID\" + Clsid + @"\LocalServer32",
                     @"SOFTWARE\Classes\WOW6432Node\CLSID\" + Clsid + @"\LocalServer32",
                     @"SOFTWARE\Classes\CLSID\" + Clsid + @"\LocalServer32",
                 })
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(root);
                var v = k?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(v))
                {
                    ls = v;
                    reg = true;
                    break;
                }
            }
            catch { /* ignore */ }
        }

        string tail = "";
        string? cap = null, set = null, ferr = null;
        DateTime? setAt = null;
        try
        {
            if (File.Exists(LogPath))
            {
                var lines = File.ReadAllLines(LogPath);
                tail = string.Join(Environment.NewLine, lines.TakeLast(12));
                foreach (var line in lines.Reverse())
                {
                    if (cap is null && line.Contains("GetCapability", StringComparison.Ordinal))
                        cap = line;
                    if (set is null && line.Contains("SetEffect", StringComparison.Ordinal))
                    {
                        set = line;
                        setAt = ParseLogTime(line);
                    }
                    if (ferr is null && line.Contains("frame ", StringComparison.Ordinal))
                        ferr = line;
                    if (cap is not null && set is not null && ferr is not null)
                        break;
                }
            }
        }
        catch { /* ignore */ }

        return new AuraHalStatus
        {
            Registered = reg,
            LocalServer = ls,
            LastCapabilityAt = cap,
            LastSetEffect = set,
            LastSetEffectAt = setAt,
            LastFrameError = ferr,
            LogTail = tail,
        };
    }

    static DateTime? ParseLogTime(string line)
    {
        if (line.Length < 22 || line[0] != '[')
            return null;
        var end = line.IndexOf(']');
        if (end <= 1)
            return null;
        if (DateTime.TryParse(line.AsSpan(1, end - 1), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var dt))
            return dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
        return null;
    }

    public static void WriteMsiUrl(string baseUrl)
    {
        try
        {
            var dir = Path.GetDirectoryName(UrlFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(UrlFilePath, baseUrl.Trim().TrimEnd('/'));
        }
        catch { /* ignore */ }
    }

    public static string SanitizeHostName(string? raw)
    {
        var s = (raw ?? "").Trim();
        var dot = s.IndexOf('.');
        if (dot > 0)
            s = s[..dot];
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c is '[' or ']' or '=' or '<' or '>' or '&' or '"' or '\'')
                chars[i] = '-';
        }
        s = new string(chars).Trim().Trim('-');
        if (s.Length > 48)
            s = s[..48];
        return s.Length > 0 ? s : Environment.MachineName;
    }

    public static void WriteDisplayName(string? name)
    {
        try
        {
            var dir = Path.GetDirectoryName(DisplayNamePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(DisplayNamePath, SanitizeHostName(name));
        }
        catch { /* ignore */ }
    }

    public static string ReadDisplayName()
    {
        try
        {
            if (File.Exists(DisplayNamePath))
            {
                var s = File.ReadAllText(DisplayNamePath).Trim();
                if (s.Length > 0)
                    return SanitizeHostName(s);
            }
        }
        catch { /* ignore */ }
        return SanitizeHostName(Environment.MachineName);
    }
}
