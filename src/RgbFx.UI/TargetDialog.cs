using RgbFx.Service;
using RgbFx.Service.Client;
using RgbFx.Service.Config;
using RgbFx.Service.Discovery;
using RgbFx.UI.I18n;

namespace RgbFx.UI;

internal sealed class TargetDialog : Form
{
    static readonly Color Bg = Color.FromArgb(32, 32, 36);
    static readonly Color Card = Color.FromArgb(24, 24, 28);
    static readonly Color Fg = Color.FromArgb(230, 230, 235);
    static readonly Color Muted = Color.FromArgb(150, 150, 160);
    static readonly Color Accent = Color.FromArgb(70, 140, 255);
    static readonly Color OkColor = Color.FromArgb(100, 200, 130);
    static readonly Color BadColor = Color.FromArgb(230, 120, 120);

    readonly TabControl _tabs = new() { Dock = DockStyle.Fill, SizeMode = TabSizeMode.Fixed };
    readonly TabPage _lanPage = new();
    readonly TabPage _manualPage = new();

    readonly ListView _list = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        HideSelection = false,
    };
    readonly Button _scan = new();
    readonly Label _scanStatus = new() { AutoSize = true };

    readonly TextBox _name = new();
    readonly TextBox _url = new();
    readonly TextBox _token = new();
    readonly Label _probe = new() { AutoSize = true };
    readonly Label _lName = new() { AutoSize = true };
    readonly Label _lUrl = new() { AutoSize = true };
    readonly Label _lToken = new() { AutoSize = true };

    readonly Button _ok = new();
    readonly Button _cancel = new();

    CancellationTokenSource? _cts;
    bool _scanning;

    public string TargetName { get; private set; } = "";
    public string TargetUrl { get; private set; } = "";
    public string? TargetToken { get; private set; }

    public TargetDialog(RemoteTarget? existing)
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(560, 420);
        BackColor = Bg;
        ForeColor = Fg;
        Font = new Font("Segoe UI", 9.5f);
        ShowInTaskbar = false;

        _tabs.ItemSize = new Size(140, 30);
        _tabs.Padding = new Point(12, 4);
        _tabs.Appearance = TabAppearance.Normal;
        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.DrawItem += OnDrawTab;

        _list.BackColor = Card;
        _list.ForeColor = Fg;
        _list.Font = new Font("Segoe UI", 9.5f);
        _list.Columns.Add("", 180);
        _list.Columns.Add("", 220);
        _list.Columns.Add("", 120);
        _list.DoubleClick += (_, _) => _ = AcceptAsync();

        var lanBar = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(8, 6, 8, 6) };
        _scan.Size = new Size(88, 28);
        _scan.FlatStyle = FlatStyle.Flat;
        _scan.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
        _scan.BackColor = Color.FromArgb(40, 40, 48);
        _scan.ForeColor = Fg;
        _scan.Location = new Point(8, 6);
        _scanStatus.ForeColor = Muted;
        _scanStatus.Location = new Point(108, 11);
        lanBar.Controls.Add(_scan);
        lanBar.Controls.Add(_scanStatus);

        _lanPage.BackColor = Bg;
        _lanPage.Padding = new Padding(0);
        _lanPage.Controls.Add(_list);
        _lanPage.Controls.Add(lanBar);

        _name.Width = 360;
        _url.Width = 360;
        _token.Width = 360;
        _name.BackColor = Card;
        _url.BackColor = Card;
        _token.BackColor = Card;
        _name.ForeColor = Fg;
        _url.ForeColor = Fg;
        _token.ForeColor = Fg;
        _name.BorderStyle = BorderStyle.FixedSingle;
        _url.BorderStyle = BorderStyle.FixedSingle;
        _token.BorderStyle = BorderStyle.FixedSingle;

        if (existing is not null)
        {
            _name.Text = existing.Name;
            _url.Text = existing.BaseUrl;
            _token.Text = existing.ApiToken ?? "";
        }
        else
        {
            _url.Text = $"http://127.0.0.1:{RemoteUrl.DefaultPort}";
        }

        _manualPage.BackColor = Bg;
        _manualPage.Padding = new Padding(16);
        var y = 20;
        _lName.Location = new Point(16, y);
        _name.Location = new Point(120, y - 3);
        y += 40;
        _lUrl.Location = new Point(16, y);
        _url.Location = new Point(120, y - 3);
        y += 40;
        _lToken.Location = new Point(16, y);
        _token.Location = new Point(120, y - 3);
        y += 40;
        _probe.Location = new Point(120, y);
        _probe.ForeColor = Muted;
        _manualPage.Controls.AddRange(new Control[] { _lName, _name, _lUrl, _url, _lToken, _token, _probe });

        _tabs.TabPages.Add(_lanPage);
        _tabs.TabPages.Add(_manualPage);

        _ok.DialogResult = DialogResult.None;
        _cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = _ok;
        CancelButton = _cancel;
        _ok.Size = new Size(128, 30);
        _cancel.Size = new Size(88, 30);
        _ok.FlatStyle = FlatStyle.Flat;
        _ok.FlatAppearance.BorderSize = 0;
        _ok.BackColor = Accent;
        _ok.ForeColor = Color.White;
        _cancel.FlatStyle = FlatStyle.Flat;
        _cancel.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
        _cancel.BackColor = Color.FromArgb(40, 40, 48);
        _cancel.ForeColor = Fg;

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 48 };
        void PlaceFooter()
        {
            _cancel.Left = Math.Max(12, footer.ClientSize.Width - _cancel.Width - 12);
            _ok.Left = Math.Max(12, _cancel.Left - _ok.Width - 8);
            _ok.Top = 10;
            _cancel.Top = 10;
        }
        footer.Resize += (_, _) => PlaceFooter();
        PlaceFooter();
        footer.Controls.Add(_ok);
        footer.Controls.Add(_cancel);

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 0) };
        body.Controls.Add(_tabs);

        Controls.Add(body);
        Controls.Add(footer);

        _scan.Click += async (_, _) => await ScanAsync();
        _ok.Click += async (_, _) => await AcceptAsync();

        ApplyLanguage();
        SeedList(LanScanCache.Devices, existing?.BaseUrl);
        Shown += async (_, _) => await ScanAsync();
        FormClosed += (_, _) =>
        {
            try { _cts?.Cancel(); } catch { /* ignore */ }
        };
    }

    public void ApplyLanguage()
    {
        Text = Locale.T("target.dialog");
        _lanPage.Text = Locale.T("target.tab.lan");
        _manualPage.Text = Locale.T("target.tab.manual");
        _lName.Text = Locale.T("target.name");
        _lUrl.Text = Locale.T("target.url");
        _lToken.Text = Locale.T("target.token");
        _ok.Text = Locale.T("target.ok");
        _cancel.Text = Locale.T("target.cancel");
        _scan.Text = Locale.T("scan.button");
        if (_list.Columns.Count >= 3)
        {
            _list.Columns[0].Text = Locale.T("scan.col.name");
            _list.Columns[1].Text = Locale.T("scan.col.url");
            _list.Columns[2].Text = Locale.T("scan.col.status");
        }
    }

    void OnDrawTab(object? sender, DrawItemEventArgs e)
    {
        var selected = e.Index == _tabs.SelectedIndex;
        using var bg = new SolidBrush(selected ? Color.FromArgb(48, 48, 56) : Bg);
        e.Graphics.FillRectangle(bg, e.Bounds);
        var text = _tabs.TabPages[e.Index].Text;
        var color = selected ? Fg : Muted;
        TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    void SeedList(IReadOnlyList<DiscoveredDevice> devices, string? selectUrl)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var d in devices)
            _list.Items.Add(MakeRow(d));
        _list.EndUpdate();

        if (!string.IsNullOrWhiteSpace(selectUrl))
        {
            var want = selectUrl.Trim().TrimEnd('/');
            foreach (ListViewItem it in _list.Items)
            {
                if (it.Tag is DiscoveredDevice d &&
                    string.Equals(d.BaseUrl.TrimEnd('/'), want, StringComparison.OrdinalIgnoreCase))
                {
                    it.Selected = true;
                    it.EnsureVisible();
                    break;
                }
            }
        }
        else if (_list.Items.Count > 0)
        {
            _list.Items[0].Selected = true;
        }

        if (devices.Count == 0 && !_scanning)
            _scanStatus.Text = Locale.T("scan.empty");
        else if (devices.Count > 0 && !_scanning)
            _scanStatus.Text = Locale.T("scan.found", devices.Count);
    }

    ListViewItem MakeRow(DiscoveredDevice d)
    {
        var status = d.Connected ? Locale.T("scan.hw_ok") : Locale.T("scan.hw_off");
        var item = new ListViewItem(d.DisplayName) { Tag = d };
        item.SubItems.Add(d.BaseUrl);
        item.SubItems.Add(status);
        item.ForeColor = d.Connected ? OkColor : Fg;
        return item;
    }

    async Task ScanAsync()
    {
        if (_scanning) return;
        _scanning = true;
        _scan.Enabled = false;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var extra = BridgeConfig.Load().Targets.Select(t => t.BaseUrl);
        var found = new List<DiscoveredDevice>();
        var progress = new Progress<ScanProgress>(p =>
        {
            if (IsDisposed) return;
            _scanStatus.Text = Locale.T("scan.scanning", p.Done, p.Total);
            if (p.Found is null) return;
            if (found.Any(d => string.Equals(d.BaseUrl, p.Found.BaseUrl, StringComparison.OrdinalIgnoreCase)))
                return;
            found.Add(p.Found);
            _list.Items.Add(MakeRow(p.Found));
        });

        _list.Items.Clear();
        _scanStatus.ForeColor = Muted;
        _scanStatus.Text = Locale.T("scan.scanning", 0, 0);

        try
        {
            var list = await LanScanner.ScanAsync(extra, progress, ct).ConfigureAwait(true);
            if (IsDisposed) return;
            LanScanCache.Replace(list, Locale.T("scan.found", list.Count));
            SeedList(list, SelectedOrTypedUrl());
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
                _scanStatus.Text = Locale.T("scan.found", found.Count);
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                _scanStatus.ForeColor = BadColor;
                _scanStatus.Text = ex.Message;
            }
        }
        finally
        {
            _scanning = false;
            if (!IsDisposed)
                _scan.Enabled = true;
        }
    }

    string? SelectedOrTypedUrl()
    {
        if (_list.SelectedItems.Count > 0 && _list.SelectedItems[0].Tag is DiscoveredDevice d)
            return d.BaseUrl;
        return string.IsNullOrWhiteSpace(_url.Text) ? null : _url.Text;
    }

    async Task AcceptAsync()
    {
        if (_tabs.SelectedTab == _lanPage)
        {
            if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not DiscoveredDevice d)
            {
                _scanStatus.ForeColor = BadColor;
                _scanStatus.Text = Locale.T("target.pick");
                return;
            }

            TargetName = d.DisplayName;
            TargetUrl = d.BaseUrl;
            TargetToken = string.IsNullOrWhiteSpace(_token.Text) ? null : _token.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        if (!RemoteUrl.TryNormalize(_url.Text.Trim(), out var baseUrl, out _))
        {
            _probe.Text = Locale.T("err.invalid_url");
            _probe.ForeColor = BadColor;
            return;
        }

        _url.Text = baseUrl.TrimEnd('/');
        _probe.ForeColor = Muted;
        _probe.Text = Locale.T("target.probing");
        _ok.Enabled = false;
        try
        {
            var host = new BridgeHost();
            var r = await host.ProbeAsync(new RemoteTarget
            {
                Name = string.IsNullOrWhiteSpace(_name.Text) ? Environment.MachineName : _name.Text.Trim(),
                BaseUrl = _url.Text,
                ApiToken = string.IsNullOrWhiteSpace(_token.Text) ? null : _token.Text.Trim(),
            });
            _probe.Text = Locale.T("target.probe_ok") + " · " + r;
            _probe.ForeColor = OkColor;
        }
        catch
        {
            _probe.Text = Locale.T("target.probe_fail");
            _probe.ForeColor = BadColor;
        }
        finally
        {
            TargetName = string.IsNullOrWhiteSpace(_name.Text) ? Environment.MachineName : _name.Text.Trim();
            TargetUrl = _url.Text;
            TargetToken = string.IsNullOrWhiteSpace(_token.Text) ? null : _token.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
