namespace RgbFx.Service.Client;

/// <summary>
/// Normalize user-entered remote base URLs so missing http:// does not throw Invalid URI.
/// </summary>
public static class RemoteUrl
{
    /// <summary>
    /// Accepts forms like:
    ///   http://192.168.1.10:17700
    ///   https://host:17700/
    ///   192.168.1.10:17700
    ///   192.168.1.10
    /// Returns absolute URI with trailing slash for HttpClient BaseAddress.
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException(
                "Remote URL is empty. Example: http://192.168.1.10:17700",
                nameof(input));

        var s = input.Trim().TrimEnd('/');

        // Strip accidental path-only mistakes
        if (s.StartsWith("//", StringComparison.Ordinal))
            s = "http:" + s;

        if (!s.Contains("://", StringComparison.Ordinal))
            s = "http://" + s;

        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"Invalid remote URL: '{input}'. Use http://IP:17700 (example: http://192.168.1.10:17700)",
                nameof(input));
        }

        // Default port if user only typed IP without port — MSI service listens on 17700
        if (!uri.IsDefaultPort)
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/";

        // Host only (http://192.168.1.10) → assume 17700
        if (uri.IsDefaultPort && (uri.Port == 80 || uri.Port == 443 || uri.Port <= 0))
        {
            var builder = new UriBuilder(uri) { Port = 17700 };
            return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/";
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/";
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
