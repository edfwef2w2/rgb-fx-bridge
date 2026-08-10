using System.Text.Json;

namespace RgbFx.Service.Config;

public sealed class BridgeConfig
{
    public List<RemoteTarget> Targets { get; set; } = new();
    public string? ActiveTargetId { get; set; }
    public bool ForwardingEnabled { get; set; }
    public string SourceMode { get; set; } = "auto"; // auto | pipe | simulator
    public int MaxFrameHz { get; set; } = 15;

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
}

public sealed class RemoteTarget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "MSI Host";
    public string BaseUrl { get; set; } = "http://127.0.0.1:17700";
    public string? ApiToken { get; set; }
}
