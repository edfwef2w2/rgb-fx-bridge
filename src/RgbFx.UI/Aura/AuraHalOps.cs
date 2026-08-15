using System.Text;

namespace RgbFx.UI.Aura;

/// <summary>Register / unregister the RgbFx AAC HAL. Must run elevated. No scripts.</summary>
public static class AuraHalOps
{
    public const string Clsid = "{B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73}";
    public const string ClsidBare = "B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73";

    static readonly string DataDir = AuraHalStatus.DataDir;

    public static string LogPath => Path.Combine(DataDir, "setup.log");

    public static int Add()
    {
        Directory.CreateDirectory(DataDir);
        var log = new StringBuilder();
        try
        {
            var exe = FindHalExe();
            if (exe is null)
            {
                log.AppendLine("HAL host not found (hal/AuraCapabilityDump.exe)");
                WriteLog(log);
                return 2;
            }

            var display = FetchLivePrefix(log);
            if (string.IsNullOrWhiteSpace(display))
                display = ReadDisplayName();
            if (string.IsNullOrWhiteSpace(display))
            {
                log.AppendLine("no server hostname/board_id — abort (will not use this PC name)");
                WriteLog(log);
                return 3;
            }

            File.WriteAllText(Path.Combine(DataDir, "display-name.txt"), display);
            File.WriteAllText(Path.Combine(DataDir, "type.txt"), AuraPersona.CapabilityTypeDecimal);
            try
            {
                // Leftover Machine env from strip/extcard tests overrides Keyboard after reboot.
                Environment.SetEnvironmentVariable("RGBFX_AAC_TYPE", AuraPersona.CapabilityTypeDecimal,
                    EnvironmentVariableTarget.Machine);
            }
            catch { /* type.txt still wins in ResolveType */ }
            log.AppendLine("DisplayName=" + display);
            log.AppendLine("persona type=" + AuraPersona.CapabilityTypeDecimal +
                           " category=" + AuraPersona.CategoryKind);
            log.AppendLine("HAL=" + exe);

            AuraRegistry.Write("\"" + exe + "\" --server", display, log);

            var official = AuraMbHeaders.Detect(log);
            AuraDeviceInfo.RestoreOfficialGroup(official, log);
            var rlsCount = AuraPersona.SingleDevice ? 1 : ReadZoneCount();
            AuraDeviceInfo.UpsertIndependentSection(display, rlsCount, log);

            AuraStackBounce.StopProcess("AuraCapabilityDump");
            AuraStackBounce.Run(log);
            log.AppendLine("add ok officialHeaders=" + official +
                           " rls=" + AuraPersona.RlsDeviceType +
                           " cap=" + AuraPersona.CapabilityTypeDecimal);
            WriteLog(log);
            return 0;
        }
        catch (Exception ex)
        {
            log.AppendLine(ex.ToString());
            WriteLog(log);
            return 1;
        }
    }

    public static int Remove()
    {
        Directory.CreateDirectory(DataDir);
        var log = new StringBuilder();
        try
        {
            AuraRegistry.Delete(log);
            try
            {
                Environment.SetEnvironmentVariable("RGBFX_AAC_TYPE", null, EnvironmentVariableTarget.Machine);
            }
            catch { /* ignore */ }
            var official = AuraMbHeaders.Detect(log);
            if (official == 0)
                official = AuraMbHeaders.ReadPersisted();
            AuraDeviceInfo.RestoreOfficialGroup(official, log);

            AuraStackBounce.StopProcess("AuraCapabilityDump");
            AuraStackBounce.Run(log);
            log.AppendLine("remove ok official=" + official);
            WriteLog(log);
            return 0;
        }
        catch (Exception ex)
        {
            log.AppendLine(ex.ToString());
            WriteLog(log);
            return 1;
        }
    }

    public static string? FindHalExe()
    {
        foreach (var p in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "hal", "AuraCapabilityDump.exe"),
                     Path.Combine(AppContext.BaseDirectory, "..", "hal", "AuraCapabilityDump.exe"),
                     Path.Combine(AppContext.BaseDirectory, "AuraCapabilityDump.exe"),
                 })
        {
            if (File.Exists(p))
                return Path.GetFullPath(p);
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            foreach (var leaf in DiscoverHalLeaves(dir.FullName))
            {
                var p = Path.Combine(dir.FullName, "artifacts", leaf, "AuraCapabilityDump.exe");
                if (File.Exists(p))
                    return Path.GetFullPath(p);
            }
        }

        return null;
    }

    static IEnumerable<string> DiscoverHalLeaves(string root)
    {
        var art = Path.Combine(root, "artifacts");
        if (Directory.Exists(art))
        {
            foreach (var d in Directory.GetDirectories(art, "aachal-x86-v*")
                         .Select(p => (path: p, n: ParseHalVer(Path.GetFileName(p))))
                         .OrderByDescending(x => x.n))
                yield return Path.GetFileName(d.path)!;
        }

        yield return "aachal-x86";
    }

    static int ParseHalVer(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;
        var i = name.LastIndexOf('v');
        return i >= 0 && int.TryParse(name[(i + 1)..], out var n) ? n : 0;
    }

    static string ReadDisplayName()
    {
        try
        {
            var f = Path.Combine(DataDir, "display-name.txt");
            if (File.Exists(f))
            {
                var s = File.ReadAllText(f).Trim();
                if (s.Length > 0)
                    return AuraHalStatus.SanitizeHostName(s);
            }
        }
        catch { /* ignore */ }
        return "";
    }

    static string FetchLivePrefix(StringBuilder log)
    {
        var url = AuraHalStatus.UrlFilePath;
        string? root = null;
        try
        {
            if (File.Exists(url))
                root = File.ReadAllText(url).Trim().TrimEnd('/');
        }
        catch { /* ignore */ }

        if (string.IsNullOrWhiteSpace(root))
            return "";
        if (!root.Contains("://", StringComparison.Ordinal))
            root = "http://" + root;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            string? host = null, board = null;
            foreach (var path in new[] { "/api/v1/zones", "/api/v1/health", "/api/v1/board" })
            {
                using var res = http.GetAsync(root + path).GetAwaiter().GetResult();
                var body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                var el = doc.RootElement;
                if (host is null && el.TryGetProperty("hostname", out var h) &&
                    h.ValueKind == System.Text.Json.JsonValueKind.String)
                    host = h.GetString();
                if (board is null && el.TryGetProperty("board_id", out var b) &&
                    b.ValueKind == System.Text.Json.JsonValueKind.String)
                    board = b.GetString();
                if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(board))
                    break;
            }

            var prefix = RgbFx.Service.Client.DeviceIdentity.ComposePrefix(host, board);
            log.AppendLine("live hostname=" + (host ?? "") + " board_id=" + (board ?? "") + " prefix=" + prefix);
            return prefix == "device" ? "" : prefix;
        }
        catch (Exception ex)
        {
            log.AppendLine("live identity failed " + ex.Message);
            return "";
        }
    }

    static int ReadZoneCount()
    {
        try
        {
            var p = Path.Combine(DataDir, "zones.json");
            if (!File.Exists(p))
                return 1;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(p));
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                return Math.Clamp(doc.RootElement.GetArrayLength(), 1, 16);
        }
        catch { /* ignore */ }
        return 1;
    }

    static void WriteLog(StringBuilder log)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(LogPath, log.ToString());
        }
        catch { /* ignore */ }
    }
}
