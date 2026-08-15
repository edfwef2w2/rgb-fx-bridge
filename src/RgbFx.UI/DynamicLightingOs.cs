using Microsoft.Win32;

namespace RgbFx.UI;

/// <summary>Windows Dynamic Lighting exists from Windows 11 22H2 (build 22621).</summary>
public static class DynamicLightingOs
{
    public const int MinBuild = 22621;

    public static bool Supported { get; } = Detect();
    public static int Build { get; } = ReadBuild();

    static bool Detect() => Build >= MinBuild;

    static int ReadBuild()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var s = k?.GetValue("CurrentBuildNumber") as string ?? k?.GetValue("CurrentBuild") as string;
            if (int.TryParse(s, out var n))
                return n;
        }
        catch { /* ignore */ }
        return 0;
    }
}
