namespace RgbFx.Service.Client;

/// <summary>
/// Normalize user-entered remote base URLs so missing http:// does not throw Invalid URI.
/// MSI Mystic Light Web listens on port 17700 (never HTTP default 80).
/// </summary>
public static class RemoteUrl
{
    /// <summary>Default listen port of msi-mystic-light-web.</summary>
    public const int DefaultPort = 17700;

    /// <summary>
    /// Accepts forms like:
    ///   http://192.168.1.10:17700
    ///   https://host:17700/
    ///   192.168.1.10:17700
    ///   192.168.1.10
    /// Returns absolute URI with trailing slash for HttpClient BaseAddress.
    /// Bare hosts (no port) use <see cref="DefaultPort"/> instead of 80/443.
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException(
                $"Remote URL is empty. Example: http://192.168.1.10:{DefaultPort}",
                nameof(input));

        var s = input.Trim().TrimEnd('/');

        if (s.StartsWith("//", StringComparison.Ordinal))
            s = "http:" + s;

        if (!s.Contains("://", StringComparison.Ordinal))
            s = "http://" + s;

        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"Invalid remote URL: '{input}'. Use http://IP:{DefaultPort} (example: http://192.168.1.10:{DefaultPort})",
                nameof(input));
        }

        var builder = new UriBuilder(uri)
        {
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };

        // Uri defaults http→80 / https→443 when the user omits a port.
        // This product always serves on 17700 unless the user explicitly chose another non-default port.
        if (builder.Port is 80 or 443 or <= 0)
            builder.Port = DefaultPort;

        // Always emit host:port so BaseAddress never silently targets :80
        return $"{builder.Scheme}://{builder.Host}:{builder.Port}/";
    }

    public static bool TryNormalize(string? input, out string normalized, out string? error)
    {
        try
        {
            normalized = Normalize(input);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            normalized = "";
            error = ex.Message;
            return false;
        }
    }
}
