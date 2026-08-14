using System.Text.Json;

namespace RgbFx.Service.Config;

public sealed class BridgeConfig
{
    public List<RemoteTarget> Targets { get; set; } = new();
    public string? ActiveTargetId { get; set; }
    public bool ForwardingEnabled { get; set; }
    public string SourceMode { get; set; } = "auto"; // auto | pipe | simulator
    public int MaxFrameHz { get; set; } = 15;
    /// <summary>system | en | zh-CN</summary>
    public string UiLanguage { get; set; } = "system";
    /// <summary>dynamic | aura — which mode panel is shown</summary>
    public string UiMode { get; set; } = "dynamic";

    public static string ConfigPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "RgbFxBridge",
            "config.json");

    public static BridgeConfig Load()
    {
        try
        {
            var path = ConfigPath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<BridgeConfig>(json) ?? new BridgeConfig();
            }
        }
        catch
        {
            // ignore corrupt config
        }
        return new BridgeConfig();
    }

    public void Save()
    {
        var path = ConfigPath;
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public RemoteTarget UpsertActive(string baseUrl, string name, string? token)
    {
        var url = baseUrl.Trim().TrimEnd('/');
        var existing = Targets.FirstOrDefault(t =>
            string.Equals((t.BaseUrl ?? "").TrimEnd('/'), url, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new RemoteTarget
            {
                Name = string.IsNullOrWhiteSpace(name) ? Environment.MachineName : name.Trim(),
                BaseUrl = url,
                ApiToken = token,
            };
            Targets.Add(existing);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(name))
                existing.Name = name.Trim();
            existing.BaseUrl = url;
            if (token is not null)
                existing.ApiToken = string.IsNullOrWhiteSpace(token) ? null : token;
        }

        ActiveTargetId = existing.Id;
        return existing;
    }
}

public sealed class RemoteTarget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = Environment.MachineName;
    public string BaseUrl { get; set; } = "http://127.0.0.1:17700"; // msi-mystic-light-web default port
    public string? ApiToken { get; set; }
}
