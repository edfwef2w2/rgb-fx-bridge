using System.Net;
using RgbFx.Service;
using RgbFx.Service.Client;
using RgbFx.Service.Config;
using RgbFx.Service.Discovery;
using RgbFx.UI.Aura;
using RgbFx.UI.I18n;

namespace RgbFx.UI;

public sealed class MainForm : Form
{
    static readonly Color Bg = Color.FromArgb(22, 22, 26);
    static readonly Color Card = Color.FromArgb(34, 34, 40);
    static readonly Color Fg = Color.FromArgb(232, 232, 236);
    static readonly Color Muted = Color.FromArgb(150, 150, 160);
    static readonly Color Accent = Color.FromArgb(70, 140, 255);
    static readonly Color Ok = Color.FromArgb(80, 190, 120);
    static readonly Color Bad = Color.FromArgb(220, 90, 90);

    readonly BridgeHost _host = new();
    readonly System.Windows.Forms.Timer _tick = new() { Interval = 1500 };

    readonly Label _title = new();
    readonly Button _modeDyn = new();
    readonly Button _modeAura = new();
    readonly ComboBox _lang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    readonly Label _msiLabel = new();
    readonly ComboBox _targets = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
    readonly Label _msiDot = new() { AutoSize = true };
    readonly Button _change = new();

    readonly Panel _modeHost = new() { Dock = DockStyle.Top, Height = 88 };
    readonly Label _modeStatus = new();
    readonly ComboBox _source = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    readonly Button _primary = new();

    readonly Label _debugTitle = new();
    readonly ToolTip _tips = new() { AutoPopDelay = 12000, InitialDelay = 200, ReshowDelay = 200 };
    readonly Button _copy = new();
    readonly TextBox _debug = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        Font = new Font("Cascadia Mono", 8.5f),
        BackColor = Color.FromArgb(18, 18, 22),
        ForeColor = Color.FromArgb(190, 190, 200),
    };

    string _probeHint = "";
    bool _busy;
    bool _comboSyncing;
    CancellationTokenSource? _scanCts;

    public MainForm()
    {
        Locale.Load();
        var cfg0 = BridgeConfig.Load();
        Locale.Set(cfg0.UiLanguage);

        Text = "RgbFx Bridge";
        Width = 760;
        Height = 560;
        MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        ForeColor = Fg;
        Font = new Font("Segoe UI", 9.5f);

        _title.Font = new Font("Segoe UI Semibold", 14f);
        _title.AutoSize = true;
        _msiLabel.AutoSize = true;
        _targets.BackColor = Card;
        _targets.ForeColor = Fg;
        _targets.FlatStyle = FlatStyle.Flat;
        _modeStatus.AutoSize = true;
        _debugTitle.AutoSize = true;
        _debugTitle.ForeColor = Muted;

        StyleSeg(_modeDyn);
        StyleSeg(_modeAura);
        StyleGhost(_change);
        StyleGhost(_copy);
        StylePrimary(_primary);

        _source.Items.AddRange(new object[] { "auto", "pipe", "simulator" });
        _lang.Items.AddRange(new object[] { "system", "en", "zh-CN" });

        var header = new Panel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(16, 12, 16, 8) };
        _title.Location = new Point(16, 12);
        _modeDyn.Location = new Point(200, 12);
        _modeAura.Location = new Point(320, 12);
        _lang.Location = new Point(620, 14);
        _msiLabel.Location = new Point(16, 56);
        _targets.Location = new Point(56, 52);
        _msiDot.Location = new Point(404, 54);
        _change.Location = new Point(620, 50);
        header.Controls.AddRange(new Control[]
        {
            _title, _modeDyn, _modeAura, _lang, _msiLabel, _targets, _msiDot, _change
        });
        header.Resize += (_, _) =>
        {
            _lang.Left = header.ClientSize.Width - _lang.Width - 16;
            _change.Left = header.ClientSize.Width - _change.Width - 16;
            var comboRight = _change.Left - 12;
            _targets.Width = Math.Max(180, comboRight - 130 - _targets.Left);
            _msiDot.Left = _targets.Right + 8;
        };

        _modeHost.Padding = new Padding(16, 8, 16, 8);
        _modeHost.BackColor = Card;
        _modeStatus.Location = new Point(16, 16);
        _source.Location = new Point(16, 48);
        _primary.Location = new Point(140, 44);
        _modeHost.Controls.AddRange(new Control[] { _modeStatus, _source, _primary });

        var debugBar = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(16, 6, 16, 0) };
        _debugTitle.Location = new Point(16, 8);
        _copy.Location = new Point(640, 4);
        debugBar.Controls.Add(_debugTitle);
        debugBar.Controls.Add(_copy);
        debugBar.Resize += (_, _) => _copy.Left = debugBar.ClientSize.Width - _copy.Width - 16;

        var debugPad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 12) };
        debugPad.Controls.Add(_debug);

        Controls.Add(debugPad);
        Controls.Add(debugBar);
        Controls.Add(_modeHost);
        Controls.Add(header);

        _modeDyn.Click += (_, _) =>
        {
            if (!DynamicLightingOs.Supported)
            {
                ShowDynamicUnsupportedPrompt();
                return;
            }
            SetUiMode("dynamic");
        };
        _modeAura.Click += (_, _) => SetUiMode("aura");
        _change.Click += (_, _) => OnChangeTarget();
        _targets.SelectedIndexChanged += (_, _) => OnTargetPicked();
        _primary.Click += async (_, _) => await OnPrimary();
        _copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(_debug.Text);
                _copy.Text = Locale.T("debug.copied");
            }
            catch { /* ignore */ }
        };
        _lang.SelectedIndexChanged += (_, _) =>
        {
            var cfg = BridgeConfig.Load();
            cfg.UiLanguage = _lang.SelectedItem?.ToString() ?? "system";
            cfg.Save();
            Locale.Set(cfg.UiLanguage);
            ApplyLanguage();
        };
        _source.SelectedIndexChanged += (_, _) =>
        {
            var cfg = BridgeConfig.Load();
            cfg.SourceMode = _source.SelectedItem?.ToString() ?? "auto";
            cfg.Save();
            RefreshAll();
        };

        _host.StateChanged += OnHostStateChanged;
        _tick.Tick += (_, _) => RefreshAll();
        Load += (_, _) =>
        {
            if (!DynamicLightingOs.Supported)
            {
                var cfg = BridgeConfig.Load();
                if (!string.Equals(cfg.UiMode, "aura", StringComparison.OrdinalIgnoreCase))
                {
                    cfg.UiMode = "aura";
                    cfg.Save();
                }
            }
            ApplyLanguage();
            SyncFromConfig();
            RefreshTargetCombo();
            RefreshAll();
            _tick.Start();
            _ = ScanLanAsync();
            _ = ProbeAsync();
        };
    }

    void StyleSeg(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.Size = new Size(112, 28);
        b.Cursor = Cursors.Hand;
    }

    void StyleGhost(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
        b.BackColor = Card;
        b.ForeColor = Fg;
        b.Size = new Size(88, 28);
        b.Cursor = Cursors.Hand;
    }

    void StylePrimary(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = Accent;
        b.ForeColor = Color.White;
        b.Size = new Size(120, 32);
        b.Cursor = Cursors.Hand;
    }

    void SetUiMode(string mode)
    {
        if (string.Equals(mode, "dynamic", StringComparison.OrdinalIgnoreCase)
            && !DynamicLightingOs.Supported)
        {
            ShowDynamicUnsupportedPrompt();
            return;
        }
        var cfg = BridgeConfig.Load();
        cfg.UiMode = mode;
        cfg.Save();
        ApplyLanguage();
        RefreshAll();
    }

    void SyncFromConfig()
    {
        var cfg = BridgeConfig.Load();
        _lang.SelectedItem = cfg.UiLanguage is "en" or "zh-CN" or "system" ? cfg.UiLanguage : "system";
        if (!string.IsNullOrEmpty(cfg.SourceMode) && _source.Items.Contains(cfg.SourceMode))
            _source.SelectedItem = cfg.SourceMode;
        else
            _source.SelectedItem = "auto";
    }

    void ApplyLanguage()
    {
        var cfg = BridgeConfig.Load();
        var aura = string.Equals(cfg.UiMode, "aura", StringComparison.OrdinalIgnoreCase);
        Text = Locale.T("app.title");
        _title.Text = Locale.T("app.title");
        _modeDyn.Text = Locale.T("mode.dynamic");
        _modeAura.Text = Locale.T("mode.aura");
        _modeDyn.Enabled = true;
        _modeDyn.Cursor = DynamicLightingOs.Supported ? Cursors.Hand : Cursors.No;
        _tips.SetToolTip(_modeDyn, DynamicLightingOs.Supported
            ? ""
            : Locale.T("dynamic.unsupported", DynamicLightingOs.Build));
        _msiLabel.Text = Locale.T("target.label");
        _change.Text = Locale.T("target.change");
        _debugTitle.Text = Locale.T("debug.title");
        _copy.Text = Locale.T("debug.copy");
        PaintModeButtons(aura);
        _source.Visible = !aura;
        RefreshTargetCombo();
        RefreshModePanel();
    }

    void ShowDynamicUnsupportedPrompt()
    {
        MessageBox.Show(
            this,
            Locale.T("dynamic.unsupported", DynamicLightingOs.Build),
            Locale.T("dynamic.unsupported.title"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    void PaintModeButtons(bool aura)
    {
        if (!DynamicLightingOs.Supported)
        {
            _modeDyn.BackColor = Color.FromArgb(40, 40, 46);
            _modeDyn.ForeColor = Muted;
        }
        else
        {
            _modeDyn.BackColor = aura ? Card : Accent;
            _modeDyn.ForeColor = aura ? Fg : Color.White;
        }
        _modeAura.BackColor = aura ? Accent : Card;
        _modeAura.ForeColor = aura ? Color.White : Fg;
    }

    RemoteTarget? ActiveTarget()
    {
        var cfg = BridgeConfig.Load();
        return cfg.Targets.FirstOrDefault(t => t.Id == cfg.ActiveTargetId)
               ?? cfg.Targets.FirstOrDefault();
    }

    void OnChangeTarget()
    {
        var existing = ActiveTarget();
        using var dlg = new TargetDialog(existing);
        var ok = dlg.ShowDialog(this) == DialogResult.OK;
        if (ok)
            ApplyChosenTarget(dlg.TargetUrl, dlg.TargetName, dlg.TargetToken);
        else
            RefreshTargetCombo();
    }

    void OnTargetPicked()
    {
        if (_comboSyncing) return;
        if (_targets.SelectedItem is not TargetPick pick || string.IsNullOrEmpty(pick.Url))
            return;
        if (pick.Id is not null)
        {
            var cfg = BridgeConfig.Load();
            if (cfg.ActiveTargetId == pick.Id)
                return;
            cfg.ActiveTargetId = pick.Id;
            cfg.Save();
            _host.ReloadConfig();
            var tok = cfg.Targets.FirstOrDefault(t => t.Id == pick.Id)?.ApiToken;
            _ = PersistIdentityAsync(pick.Url, tok);
            _ = ProbeAsync();
            RefreshAll();
            return;
        }

        ApplyChosenTarget(pick.Url, pick.Name, null);
    }

    void ApplyChosenTarget(string rawUrl, string name, string? token)
    {
        if (!RgbFx.Service.Client.RemoteUrl.TryNormalize(rawUrl, out var url, out _))
            return;
        var cfg = BridgeConfig.Load();
        cfg.UpsertActive(url, string.IsNullOrWhiteSpace(name) ? Environment.MachineName : name, token);
        cfg.Save();
        _host.ReloadConfig();
        RefreshTargetCombo();
        _ = PersistIdentityAsync(url, token);
        _ = ProbeAsync();
        RefreshAll();
    }

    async Task PersistIdentityAsync(string url, string? token)
    {
        AuraHalStatus.WriteMsiUrl(url);
        var host = await ResolveDisplayPrefixAsync(url, token);
        if (!string.IsNullOrWhiteSpace(host) && !string.Equals(host, "device", StringComparison.OrdinalIgnoreCase))
            AuraHalStatus.WriteDisplayName(host);
        try
        {
            using var client = new MysticLightApiClient(url, token, TimeSpan.FromSeconds(3));
            var zones = await client.GetZonesAsync();
            var list = (zones?.Zones ?? new List<ZoneInfo>())
                .Where(z => !string.IsNullOrWhiteSpace(z.Name))
                .Select(z => new { name = z.Name, display = z.Display })
                .ToList();
            var dir = Path.GetDirectoryName(AuraHalStatus.DisplayNamePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "zones.json"),
                System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* HAL will GET /zones itself */ }
    }

    static async Task<string> ResolveDisplayPrefixAsync(string url, string? token)
    {
        string? host = null, board = null;
        try
        {
            using var client = new MysticLightApiClient(url, token, TimeSpan.FromSeconds(2));
            try
            {
                var zones = await client.GetZonesAsync();
                host = zones?.HostName;
                board = zones?.BoardId;
            }
            catch { /* try health */ }

            try
            {
                var health = await client.GetHealthAsync();
                host ??= health?.HostName;
                board ??= health?.BoardId;
            }
            catch { /* fall through */ }
        }
        catch { /* fall through */ }

        return DeviceIdentity.ComposePrefix(host, board);
    }

    void RefreshTargetCombo()
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(RefreshTargetCombo); } catch { /* closing */ }
            return;
        }

        _comboSyncing = true;
        try
        {
            var cfg = BridgeConfig.Load();
            var active = cfg.Targets.FirstOrDefault(t => t.Id == cfg.ActiveTargetId)
                         ?? cfg.Targets.FirstOrDefault();
            var byUrl = new Dictionary<string, DiscoveredDevice>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in LanScanCache.Devices)
            {
                var u = d.BaseUrl.TrimEnd('/');
                if (u.Length > 0)
                    byUrl[u] = d;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _targets.Items.Clear();
            foreach (var t in cfg.Targets)
            {
                var url = (t.BaseUrl ?? "").TrimEnd('/');
                if (url.Length == 0) continue;
                seen.Add(url);
                byUrl.TryGetValue(url, out var d);
                var name = ComboName(t.Name, d);
                _targets.Items.Add(new TargetPick(t.Id, url, name, ComboLabel(name, url)));
            }

            foreach (var d in LanScanCache.Devices)
            {
                var url = d.BaseUrl.TrimEnd('/');
                if (url.Length == 0 || !seen.Add(url)) continue;
                var name = ComboName(null, d);
                _targets.Items.Add(new TargetPick(null, url, name, ComboLabel(name, url)));
            }

            if (_targets.Items.Count == 0)
                _targets.Items.Add(new TargetPick(null, "", "", Locale.T("scan.none_short")));

            var want = active?.BaseUrl.TrimEnd('/');
            var idx = -1;
            if (!string.IsNullOrEmpty(want))
            {
                for (var i = 0; i < _targets.Items.Count; i++)
                {
                    if (_targets.Items[i] is TargetPick p &&
                        string.Equals(p.Url, want, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i;
                        break;
                    }
                }
            }

            _targets.SelectedIndex = idx >= 0 ? idx : 0;
        }
        finally
        {
            _comboSyncing = false;
        }
    }

    static string ComboName(string? saved, DiscoveredDevice? d)
    {
        if (d is not null)
        {
            var prefix = DeviceIdentity.ComposePrefix(d.HostName, d.BoardId, d.DisplayName);
            if (!string.IsNullOrWhiteSpace(prefix))
                return prefix;
        }

        if (!string.IsNullOrWhiteSpace(saved) && !DeviceIdentity.IsPlaceholder(saved))
            return saved!;
        return "";
    }

    static string ComboLabel(string name, string url)
        => string.IsNullOrWhiteSpace(name) ? url : $"{name}  {url}";

    void MergeScanIntoConfig(IReadOnlyList<DiscoveredDevice> list)
    {
        if (list.Count == 0) return;
        var cfg = BridgeConfig.Load();
        var changed = false;
        foreach (var d in list)
        {
            var url = d.BaseUrl.TrimEnd('/');
            var t = cfg.Targets.FirstOrDefault(x =>
                string.Equals((x.BaseUrl ?? "").TrimEnd('/'), url, StringComparison.OrdinalIgnoreCase));
            if (t is null) continue;
            var name = ComboName(t.Name, d);
            if (!string.IsNullOrWhiteSpace(name) &&
                !string.Equals(t.Name, name, StringComparison.Ordinal))
            {
                t.Name = name;
                changed = true;
            }
        }

        if (cfg.Targets.Count == 0)
        {
            var d = list[0];
            cfg.UpsertActive(d.BaseUrl, ComboName(null, d), null);
            changed = true;
        }

        if (changed)
        {
            cfg.Save();
            _host.ReloadConfig();
        }
    }

    async Task ScanLanAsync()
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var live = new List<DiscoveredDevice>();
        var progress = new Progress<ScanProgress>(p =>
        {
            if (IsDisposed || p.Found is null) return;
            if (live.Any(d => string.Equals(d.BaseUrl, p.Found.BaseUrl, StringComparison.OrdinalIgnoreCase)))
                return;
            live.Add(p.Found);
            LanScanCache.Replace(live.ToList(), Locale.T("scan.found", live.Count));
            RefreshTargetCombo();
        });

        try
        {
            var extra = BridgeConfig.Load().Targets.Select(t => t.BaseUrl);
            var list = await LanScanner.ScanAsync(extra, progress, ct).ConfigureAwait(true);
            if (IsDisposed || ct.IsCancellationRequested) return;
            LanScanCache.Replace(list, Locale.T("scan.found", list.Count));
            MergeScanIntoConfig(list);
            RefreshTargetCombo();
            if (list.Count == 1 && ActiveTarget() is null)
                await PersistIdentityAsync(list[0].BaseUrl, null);
            else if (ActiveTarget() is { } t)
                await PersistIdentityAsync(t.BaseUrl, t.ApiToken);
            await ProbeAsync();
        }
        catch (OperationCanceledException)
        {
            // closing
        }
        catch
        {
            if (!IsDisposed)
            {
                LanScanCache.Replace(live, Locale.T("scan.empty"));
                RefreshTargetCombo();
            }
        }
    }

    async Task ProbeAsync()
    {
        var t = ActiveTarget();
        if (t is null)
        {
            _probeHint = Locale.T("status.unknown");
            return;
        }
        try
        {
            var r = await _host.ProbeAsync(t);
            _probeHint = r.StartsWith("ok=", StringComparison.Ordinal) || r.Contains("ok=True")
                ? Locale.T("status.connected")
                : Locale.T("status.disconnected") + " · " + r;
        }
        catch
        {
            _probeHint = Locale.T("status.disconnected");
        }
        RefreshAll();
    }

    async Task OnPrimary()
    {
        if (_busy) return;
        var cfg = BridgeConfig.Load();
        if (string.Equals(cfg.UiMode, "aura", StringComparison.OrdinalIgnoreCase))
            await RunAuraAsync();
        else if (DynamicLightingOs.Supported)
            ToggleDynamic();
    }

    void ToggleDynamic()
    {
        if (_host.Status.StartsWith("starting", StringComparison.Ordinal)
            || _host.Status.Contains('→')
            || _host.Status.StartsWith("forward", StringComparison.Ordinal)
            || _host.Status.Contains("running", StringComparison.OrdinalIgnoreCase)
            || _host.FramesForwarded > 0 && _host.Status != "stopped" && _host.Status != "idle"
               && _host.Status != "forwarding disabled")
        {
            if (!_host.Status.Equals("stopped", StringComparison.Ordinal)
                && !_host.Status.Equals("idle", StringComparison.Ordinal)
                && !_host.Status.Equals("forwarding disabled", StringComparison.Ordinal))
            {
                _host.Stop();
                var c = BridgeConfig.Load();
                c.ForwardingEnabled = false;
                c.Save();
                RefreshAll();
                return;
            }
        }

        if (_host.Status is not "stopped" and not "idle" and not "forwarding disabled"
            && !_host.Status.StartsWith("no target", StringComparison.Ordinal))
        {
            _host.Stop();
            var c2 = BridgeConfig.Load();
            c2.ForwardingEnabled = false;
            c2.Save();
            RefreshAll();
            return;
        }

        var cfg = BridgeConfig.Load();
        if (ActiveTarget() is null)
        {
            OnChangeTarget();
            cfg = BridgeConfig.Load();
        }
        if (ActiveTarget() is null)
            return;
        cfg.ForwardingEnabled = true;
        cfg.SourceMode = _source.SelectedItem?.ToString() ?? "auto";
        cfg.Save();
        _ = PersistIdentityAsync(ActiveTarget()!.BaseUrl, ActiveTarget()!.ApiToken);
        _host.ReloadConfig();
        _host.Start();
        RefreshAll();
    }

    bool IsForwarding()
    {
        var s = _host.Status;
        return s is not "stopped" and not "idle" and not "forwarding disabled"
               && !s.StartsWith("no target", StringComparison.Ordinal);
    }

    async Task RunAuraAsync()
    {
        var st = AuraHalStatus.Read();
        _busy = true;
        _primary.Enabled = false;
        try
        {
            if (ActiveTarget() is null)
            {
                OnChangeTarget();
                if (ActiveTarget() is null)
                    return;
            }

            if (!st.Registered)
                await PersistIdentityAsync(ActiveTarget()!.BaseUrl, ActiveTarget()!.ApiToken);
            _modeStatus.Text = Locale.T(st.Registered ? "aura.uninstalling" : "aura.installing");
            var (code, output) = st.Registered
                ? await AuraSetup.UninstallAsync()
                : await AuraSetup.InstallAsync();
            var still = AuraHalStatus.Read().Registered;
            if (st.Registered && still)
                _probeHint = Locale.T("aura.failed") + " · " + TrimSetupOutput(output);
            else
                _probeHint = code == 0 && (!st.Registered || !still)
                    ? Locale.T("aura.done")
                    : Locale.T("aura.failed") + " · " + TrimSetupOutput(output);
        }
        finally
        {
            _busy = false;
            _primary.Enabled = true;
            RefreshAll();
        }
    }

    static string TrimSetupOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return "exit";
        var lines = output.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("Transcript ", StringComparison.OrdinalIgnoreCase)
                        && !l.StartsWith("**********************", StringComparison.Ordinal))
            .TakeLast(4);
        return string.Join(" | ", lines);
    }

    void OnHostStateChanged()
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(OnHostStateChanged); } catch { /* ignore */ }
            return;
        }
        RefreshModePanel();
        RefreshDebug();
    }

    void RefreshAll()
    {
        if (IsDisposed) return;
        var cfg = BridgeConfig.Load();
        var connected = _probeHint == Locale.T("status.connected")
                        || _probeHint.StartsWith(Locale.T("status.connected"), StringComparison.Ordinal);
        _msiDot.Text = "● " + (_probeHint.Length == 0 ? Locale.T("status.unknown") : _probeHint);
        _msiDot.ForeColor = connected ? Ok : Muted;
        PaintModeButtons(string.Equals(cfg.UiMode, "aura", StringComparison.OrdinalIgnoreCase));
        RefreshModePanel();
        RefreshDebug();
    }

    void RefreshModePanel()
    {
        var cfg = BridgeConfig.Load();
        var aura = string.Equals(cfg.UiMode, "aura", StringComparison.OrdinalIgnoreCase);
        _source.Visible = !aura;
        if (aura)
        {
            _modeStatus.ForeColor = Fg;
            var st = AuraHalStatus.Read();
            if (!st.Registered)
                _modeStatus.Text = Locale.T("aura.status.missing");
            else if (st.IsSending)
                _modeStatus.Text = Locale.T("aura.status.active");
            else
                _modeStatus.Text = Locale.T("aura.status.registered");
            _primary.Text = Locale.T(st.Registered ? "aura.uninstall" : "aura.install");
            _primary.Enabled = !_busy;
            _primary.Visible = true;
        }
        else if (!DynamicLightingOs.Supported)
        {
            _modeStatus.Text = Locale.T("dynamic.unsupported", DynamicLightingOs.Build);
            _modeStatus.ForeColor = Bad;
            _source.Visible = false;
            _primary.Visible = false;
        }
        else
        {
            _modeStatus.ForeColor = Fg;
            _modeStatus.Text = IsForwarding()
                ? Locale.T("dynamic.status.running")
                : Locale.T("dynamic.status.stopped");
            _primary.Text = Locale.T(IsForwarding() ? "dynamic.stop" : "dynamic.start");
            _primary.Enabled = !_busy;
            _primary.Visible = true;
        }
    }

    void RefreshDebug()
    {
        var cfg = BridgeConfig.Load();
        var t = ActiveTarget();
        var aura = AuraHalStatus.Read();
        var pipe = File.Exists(@"\\.\pipe\RgbFxLampArray") ? "yes" : "no";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{Locale.T("debug.target")}: {t?.Name ?? "-"}  {t?.BaseUrl ?? "-"}");
        sb.AppendLine($"{Locale.T("debug.scan")}: {(string.IsNullOrEmpty(LanScanCache.Summary) ? "-" : LanScanCache.Summary)}  ({LanScanCache.Devices.Count})");
        sb.AppendLine($"{Locale.T("debug.mode")}: {cfg.UiMode}  dynOS={(DynamicLightingOs.Supported ? "yes" : "no")} build={DynamicLightingOs.Build}");
        sb.AppendLine($"{Locale.T("debug.source")}: {cfg.SourceMode}");
        sb.AppendLine($"{Locale.T("debug.frames")}: {_host.FramesForwarded}");
        sb.AppendLine($"{Locale.T("debug.error")}: {_host.LastError ?? "-"}");
        sb.AppendLine($"{Locale.T("debug.hal")}: {(aura.Registered ? "registered" : "missing")}");
        sb.AppendLine($"{Locale.T("debug.clsid")}: {AuraHalStatus.Clsid}");
        sb.AppendLine($"{Locale.T("debug.localserver")}: {aura.LocalServer ?? "-"}");
        sb.AppendLine($"{Locale.T("debug.pipe")}: {pipe}");
        sb.AppendLine();
        sb.AppendLine(Locale.T("debug.log"));
        sb.AppendLine(string.IsNullOrEmpty(aura.LogTail) ? "-" : aura.LogTail);
        _debug.Text = sb.ToString();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _tick.Stop();
        try { _scanCts?.Cancel(); } catch { /* ignore */ }
        _host.Stop();
        AuraHalStatus.ClearRuntimeLogs();
        base.OnFormClosed(e);
    }

    sealed class TargetPick
    {
        public string? Id { get; }
        public string Url { get; }
        public string Name { get; }
        public string Label { get; }

        public TargetPick(string? id, string url, string name, string label)
        {
            Id = id;
            Url = url;
            Name = name;
            Label = label;
        }

        public override string ToString() => Label;
    }
}
