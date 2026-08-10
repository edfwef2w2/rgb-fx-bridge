using RgbFx.Service;
using RgbFx.Service.Client;
using RgbFx.Service.Config;

namespace RgbFx.UI;

/// <summary>
/// Intentionally minimal: remote target selection + probe + start/stop forwarding.
/// Lighting effects come from Windows Dynamic Lighting (or simulator), not this UI.
/// </summary>
public sealed class MainForm : Form
{
    private readonly BridgeHost _host = new();
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _name = new() { PlaceholderText = "Name" };
    private readonly TextBox _url = new() { PlaceholderText = $"http://192.168.50.8:{RemoteUrl.DefaultPort}", Width = 320 };
    private readonly TextBox _token = new() { PlaceholderText = "API token (optional)", Width = 180 };
    private readonly ComboBox _source = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(8) };
    private readonly CheckBox _forward = new() { Text = "Forwarding enabled", AutoSize = true };

    public MainForm()
    {
        Text = "RgbFx Bridge — Remote targets";
        Width = 720;
        Height = 420;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        // Keep every simulation path selectable (Windows LampArray + software + Aura Addressable experimental)
        _source.Items.AddRange(new object[]
        {
            "auto",
            "pipe",
            "simulator",
            "aura-addressable-sim",
        });
        _source.SelectedItem = "auto";

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 72,
            Padding = new Padding(8),
            WrapContents = true,
        };
        top.Controls.AddRange(new Control[]
        {
            _name, _url, _token, _source, _forward,
            Btn("Add", OnAdd),
            Btn("Remove", OnRemove),
            Btn("Probe", OnProbe),
            Btn("Set Active", OnSetActive),
            Btn("Start", OnStart),
            Btn("Stop", OnStop),
            Btn("Save", OnSave),
        });

        Controls.Add(_list);
        Controls.Add(_status);
        Controls.Add(top);

        // Do not BeginInvoke until the form handle exists (ctor/LoadConfig used to crash UI instantly).
        _host.StateChanged += OnHostStateChanged;
        LoadConfigToUi();
        RefreshStatus();
    }

    private void OnHostStateChanged()
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            if (!IsHandleCreated)
                return;
            try
            {
                BeginInvoke(OnHostStateChanged);
            }
            catch (InvalidOperationException)
            {
                // Handle destroyed between check and marshal.
            }
            return;
        }

        RefreshStatus();
    }

    private static Button Btn(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true };
        b.Click += onClick;
        return b;
    }

    private void LoadConfigToUi()
    {
        var cfg = BridgeConfig.Load();
        _list.Items.Clear();
        foreach (var t in cfg.Targets)
        {
            var mark = t.Id == cfg.ActiveTargetId ? "*" : " ";
            _list.Items.Add($"{mark} [{t.Id[..Math.Min(6, t.Id.Length)]}] {t.Name}  {t.BaseUrl}");
        }
        _forward.Checked = cfg.ForwardingEnabled;
        if (!string.IsNullOrEmpty(cfg.SourceMode) && _source.Items.Contains(cfg.SourceMode))
            _source.SelectedItem = cfg.SourceMode;
        // Reload host config without relying on UI marshaling during construction.
        _host.ReloadConfig();
    }

    private BridgeConfig ReadUiConfig()
    {
        var cfg = BridgeConfig.Load();
        cfg.ForwardingEnabled = _forward.Checked;
        cfg.SourceMode = _source.SelectedItem?.ToString() ?? "auto";
        return cfg;
    }

    private void OnAdd(object? sender, EventArgs e)
    {
        var raw = string.IsNullOrWhiteSpace(_url.Text)
            ? $"http://127.0.0.1:{RemoteUrl.DefaultPort}"
            : _url.Text.Trim();
        if (!RemoteUrl.TryNormalize(raw, out var baseUrl, out var err))
        {
            MessageBox.Show(err ?? "Invalid URL", "Invalid remote URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Persist normalized form (always includes http://)
        _url.Text = baseUrl.TrimEnd('/');

        var cfg = ReadUiConfig();
        cfg.Targets.Add(new RemoteTarget
        {
            Name = string.IsNullOrWhiteSpace(_name.Text) ? "MSI Host" : _name.Text.Trim(),
            BaseUrl = baseUrl.TrimEnd('/'),
            ApiToken = string.IsNullOrWhiteSpace(_token.Text) ? null : _token.Text.Trim(),
        });
        if (cfg.ActiveTargetId == null && cfg.Targets.Count > 0)
            cfg.ActiveTargetId = cfg.Targets[0].Id;
        cfg.Save();
        LoadConfigToUi();
        _status.Text = "Added remote: " + baseUrl.TrimEnd('/');
    }

    private void OnRemove(object? sender, EventArgs e)
    {
        var cfg = BridgeConfig.Load();
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= cfg.Targets.Count) return;
        var id = cfg.Targets[idx].Id;
        cfg.Targets.RemoveAt(idx);
        if (cfg.ActiveTargetId == id)
            cfg.ActiveTargetId = cfg.Targets.FirstOrDefault()?.Id;
        cfg.Save();
        LoadConfigToUi();
    }

    private async void OnProbe(object? sender, EventArgs e)
    {
        var cfg = BridgeConfig.Load();
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= cfg.Targets.Count)
        {
            _status.Text = "Select a target to probe.";
            return;
        }
        _status.Text = "Probing…";
        try
        {
            var result = await _host.ProbeAsync(cfg.Targets[idx]);
            _status.Text = "Probe: " + result;
        }
        catch (Exception ex)
        {
            _status.Text = "Probe failed: " + ex.Message;
        }
    }

    private void OnSetActive(object? sender, EventArgs e)
    {
        var cfg = ReadUiConfig();
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= cfg.Targets.Count) return;
        cfg.ActiveTargetId = cfg.Targets[idx].Id;
        cfg.Save();
        LoadConfigToUi();
    }

    private void OnStart(object? sender, EventArgs e)
    {
        var cfg = ReadUiConfig();
        cfg.ForwardingEnabled = true;
        _forward.Checked = true;
        cfg.Save();
        _host.ReloadConfig();
        _host.Start();
        RefreshStatus();
    }

    private void OnStop(object? sender, EventArgs e)
    {
        _host.Stop();
        var cfg = BridgeConfig.Load();
        cfg.ForwardingEnabled = false;
        cfg.Save();
        _forward.Checked = false;
        RefreshStatus();
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var cfg = ReadUiConfig();
        cfg.Save();
        _status.Text = "Saved " + BridgeConfig.ConfigPath;
        _host.ReloadConfig();
    }

    private void RefreshStatus()
    {
        _status.Text = $"{_host.Status} | frames={_host.FramesForwarded} | err={_host.LastError ?? "-"} | cfg={BridgeConfig.ConfigPath}";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _host.Stop();
        base.OnFormClosed(e);
    }
}
