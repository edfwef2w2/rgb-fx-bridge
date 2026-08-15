using System.Text;

namespace RgbFx.Service.Client;

/// <summary>
/// Aura label: {serverHostname}-{liveBoardId}-{zone}.
/// Hostname is the lighting host (Linux gethostname), including names like linux.
/// </summary>
public static class DeviceIdentity
{
    public static bool IsPlaceholder(string? raw)
        => string.IsNullOrEmpty(Token(raw, firstLabel: true));

    public static string ComposePrefix(string? hostName, string? boardId, string? fallbackHost = null)
    {
        var host = Token(hostName, firstLabel: true)
                   ?? Token(fallbackHost, firstLabel: true);

        var board = Token(boardId, firstLabel: false);
        if (string.IsNullOrEmpty(host) && string.IsNullOrEmpty(board))
            return "device";
        if (string.IsNullOrEmpty(board))
            return host!;
        if (string.IsNullOrEmpty(host))
            return board;
        return host + "-" + board;
    }

    public static string? Token(string? raw, bool firstLabel)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var s = raw.Trim();
        if (firstLabel)
        {
            var dot = s.IndexOf('.');
            if (dot > 0)
                s = s[..dot];
        }

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_')
                sb.Append(c);
            else if (c is ' ' or '.')
                sb.Append('-');
        }

        var t = sb.ToString().Trim('-', '_');
        if (t.Length > 48)
            t = t[..48];
        return t.Length > 0 ? t : null;
    }
}
