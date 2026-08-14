using RgbFx.Service;
using RgbFx.Service.Config;
using RgbFx.Service.Discovery;

// Headless service entry: rgb-fx-service.exe [--start] [--simulator] [--scan]
// UI process uses BridgeHost directly; this EXE is for scheduled/background use.

Console.WriteLine("RgbFx Bridge Service");
Console.WriteLine("Config: " + BridgeConfig.ConfigPath);

if (args.Contains("--scan", StringComparer.OrdinalIgnoreCase))
{
    var extra = BridgeConfig.Load().Targets.Select(t => t.BaseUrl);
    Console.WriteLine("Scanning LAN for msi-mystic-light-web on :17700 …");
    var list = await LanScanner.ScanAsync(extra);
    if (list.Count == 0)
        Console.WriteLine("No hosts found.");
    foreach (var d in list)
        Console.WriteLine($"{(d.Connected ? "online " : "offline")} {d.DisplayName}  {d.BaseUrl}  api={d.ApiVersion ?? "-"}");
    return;
}

var cfg = BridgeConfig.Load();
if (args.Contains("--simulator", StringComparer.OrdinalIgnoreCase))
{
    cfg.SourceMode = "simulator";
    cfg.ForwardingEnabled = true;
    cfg.Save();
    Console.WriteLine("Source forced to simulator and forwarding enabled.");
}

if (args.Contains("--start", StringComparer.OrdinalIgnoreCase) || cfg.ForwardingEnabled)
{
    var host = new BridgeHost();
    host.StateChanged += () =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {host.Status} frames={host.FramesForwarded} err={host.LastError}");
    host.Start();
    Console.WriteLine("Running. Press Ctrl+C to stop.");
    var exit = new ManualResetEventSlim(false);
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.Set(); };
    exit.Wait();
    host.Stop();
}
else
{
    Console.WriteLine("Forwarding disabled. Configure targets with RgbFx.UI or set ForwardingEnabled=true.");
    Console.WriteLine("Usage: RgbFx.Service --start [--simulator] | --scan");
}
