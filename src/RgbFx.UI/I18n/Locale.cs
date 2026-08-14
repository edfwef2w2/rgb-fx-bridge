using System.Globalization;
using System.Text.Json;

namespace RgbFx.UI.I18n;

public static class Locale
{
    public const string System = "system";
    public const string En = "en";
    public const string ZhCn = "zh-CN";

    private static Dictionary<string, string> _en = new(StringComparer.Ordinal);
    private static Dictionary<string, string> _zh = new(StringComparer.Ordinal);
    private static string _choice = System;

    public static string Choice => _choice;

    public static string Effective
    {
        get
        {
            if (_choice == ZhCn || _choice == En)
                return _choice;
            var ui = CultureInfo.CurrentUICulture.Name;
            return ui.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? ZhCn : En;
        }
    }

    public static event Action? Changed;

    public static void Load()
    {
        _en = ReadJson("en.json");
        _zh = ReadJson("zh-CN.json");
    }

    public static void Set(string choice)
    {
        _choice = string.IsNullOrWhiteSpace(choice) ? System : choice.Trim();
        Changed?.Invoke();
    }

    public static string T(string key)
    {
        var table = Effective == ZhCn ? _zh : _en;
        if (table.TryGetValue(key, out var s) && !string.IsNullOrEmpty(s))
            return s;
        if (_en.TryGetValue(key, out var fallback) && !string.IsNullOrEmpty(fallback))
            return fallback;
        return key;
    }

    public static string T(string key, params object[] args)
    {
        try { return string.Format(T(key), args); }
        catch { return T(key); }
    }

    static Dictionary<string, string> ReadJson(string fileName)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "I18n", fileName);
            if (!File.Exists(path))
                path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path))
                return new Dictionary<string, string>(StringComparer.Ordinal);
            var json = File.ReadAllText(path);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return map ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
