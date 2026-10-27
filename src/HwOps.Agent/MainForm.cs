using System.Diagnostics;
using System.Text;
using HwOps.Core.Collection;
using HwOps.Core.Models;
using HwOps.Core.Reporting;
using HwOps.Core.Rules;

namespace HwOps.Agent;

public sealed class MainForm : Form
{
    private const string ToolVersion = "0.1.0";

    private readonly IHardwareCollector _collector = new WmiHardwareCollector();
    private readonly RuleSet _rules;
    private readonly ToolTip _toolTip = new();
    private readonly Label _headerLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _collectButton = new();
    private readonly Button _exportHtmlButton = new();
    private readonly Button _exportJsonButton = new();
    private readonly TabControl _tabs = new();
    private readonly TableLayoutPanel _summaryGrid = new();
    private readonly DataGridView _deviceGrid = new();
    private readonly TableLayoutPanel _problemPanel = new();
    private readonly TableLayoutPanel _advicePanel = new();
    private readonly NotifyIcon _trayIcon;

    private HardwareSnapshot? _snapshot;
    private MachineAdvice? _advice;
    private bool _allowExit;

    public MainForm()
    {
        _rules = LoadRules();

        Text = "硬件助手";
        Width = 960;
        Height = 700;
        MinimumSize = new Size(800, 580);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _headerLabel.AutoSize = false;
        _headerLabel.Dock = DockStyle.Fill;
        _headerLabel.Height = 70;
        _headerLabel.Padding = new Padding(12, 10, 12, 4);
        _headerLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        _headerLabel.Text = "正在读取本机信息…";
        root.Controls.Add(_headerLabel, 0, 0);

        _tabs.Dock = DockStyle.Fill;
        root.Controls.Add(_tabs, 0, 1);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 4,
            Padding = new Padding(8)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.Controls.Add(bottom, 0, 2);

        ConfigureButton(_collectButton, "重新采集");
        ConfigureButton(_exportHtmlButton, "导出 HTML");
        ConfigureButton(_exportJsonButton, "导出 JSON");

        _collectButton.Click += async (_, _) => await CollectAsync();
        _exportHtmlButton.Click += (_, _) => ExportHtml();
        _exportJsonButton.Click += (_, _) => ExportJson();

        bottom.Controls.Add(_collectButton, 0, 0);
        bottom.Controls.Add(_exportHtmlButton, 1, 0);
        bottom.Controls.Add(_exportJsonButton, 2, 0);

        _statusLabel.AutoSize = false;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.Text = "准备就绪";
        bottom.Controls.Add(_statusLabel, 3, 0);

        BuildSummaryTab();
        BuildDeviceTab();
        BuildProblemTab();
        BuildAdviceTab();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "硬件助手",
            Visible = true
        };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("打开面板", null, (_, _) => ShowPanel());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出", null, (_, _) =>
        {
            _allowExit = true;
            Close();
        });
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.DoubleClick += (_, _) => ShowPanel();

        FormClosing += (_, e) =>
        {
            if (!_allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };
        FormClosed += (_, _) =>
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        };

        Shown += async (_, _) => await CollectAsync();
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(10, 4, 10, 4);
        button.Margin = new Padding(0, 0, 8, 0);
    }

    private static RuleSet LoadRules()
    {
        try
        {
            return RuleSet.LoadEmbedded();
        }
        catch (Exception ex)
        {
            MessageBox.Show("驱动规则加载失败：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return new RuleSet();
        }
    }

    private void BuildSummaryTab()
    {
        var page = new TabPage("概况");
        _summaryGrid.Dock = DockStyle.Fill;
        _summaryGrid.AutoScroll = true;
        _summaryGrid.ColumnCount = 2;
        _summaryGrid.RowCount = 0;
        _summaryGrid.Padding = new Padding(14);
        _summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
        _summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        page.Controls.Add(_summaryGrid);
        _tabs.TabPages.Add(page);
    }

    private void BuildDeviceTab()
    {
        var page = new TabPage("设备列表");
        _deviceGrid.Dock = DockStyle.Fill;
        _deviceGrid.ReadOnly = true;
        _deviceGrid.AllowUserToAddRows = false;
        _deviceGrid.AllowUserToDeleteRows = false;
        _deviceGrid.AllowUserToResizeRows = false;
        _deviceGrid.RowHeadersVisible = false;
        _deviceGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _deviceGrid.MultiSelect = false;
        _deviceGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _deviceGrid.BackgroundColor = Color.White;
        page.Controls.Add(_deviceGrid);
        _tabs.TabPages.Add(page);
    }

    private void BuildProblemTab()
    {
        var page = new TabPage("问题设备");
        _problemPanel.Dock = DockStyle.Fill;
        _problemPanel.AutoScroll = true;
        _problemPanel.ColumnCount = 1;
        _problemPanel.Padding = new Padding(14);
        _problemPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        page.Controls.Add(_problemPanel);
        _tabs.TabPages.Add(page);
    }

    private void BuildAdviceTab()
    {
        var page = new TabPage("驱动建议");
        _advicePanel.Dock = DockStyle.Fill;
        _advicePanel.AutoScroll = true;
        _advicePanel.ColumnCount = 1;
        _advicePanel.Padding = new Padding(14);
        _advicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        page.Controls.Add(_advicePanel);
        _tabs.TabPages.Add(page);
    }

    private async Task CollectAsync()
    {
        _collectButton.Enabled = false;
        _statusLabel.Text = "正在采集硬件信息…";
        try
        {
            var snapshot = await Task.Run(() => _collector.Collect());
            var advice = new DriverAdvisor(_rules).Advise(snapshot);
            _snapshot = snapshot;
            _advice = advice;
            RenderAll();
            _statusLabel.Text = $"采集完成：{snapshot.CollectedAt:HH:mm:ss}，设备 {snapshot.Devices.Count} 个，问题 {snapshot.ProblemDevices.Count} 个";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "采集失败：" + ex.Message;
            MessageBox.Show(this, "采集失败：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _collectButton.Enabled = true;
        }
    }

    private void RenderAll()
    {
        if (_snapshot is null || _advice is null)
        {
            return;
        }

        RenderHeader();
        RenderSummary();
        RenderDevices();
        RenderProblems();
        RenderAdvice();
    }

    private void RenderHeader()
    {
        var s = _snapshot!;
        _headerLabel.Text =
            $"{s.System.Hostname}  |  {s.System.Os.Caption} {s.System.Os.Arch}\r\n" +
            $"{s.Board.Manufacturer} {s.Board.Product}  |  BIOS {s.Bios.Version}（{s.Bios.ReleaseDate}）";
    }

    private void RenderSummary()
    {
        _summaryGrid.SuspendLayout();
        _summaryGrid.Controls.Clear();
        _summaryGrid.RowStyles.Clear();
        _summaryGrid.RowCount = 0;

        var s = _snapshot!;
        AddRow("主机名", s.System.Hostname);
        AddRow("操作系统", $"{s.System.Os.Caption} {s.System.Os.Arch}".Trim());
        AddRow("整机", $"{s.System.Manufacturer} {s.System.Model}".Trim());
        AddRow("序列号", s.System.Serial);
        AddRow("主板", $"{s.Board.Manufacturer} {s.Board.Product} {s.Board.Version}".Trim());
        AddRow("BIOS", $"{s.Bios.Vendor} {s.Bios.Version}（{s.Bios.ReleaseDate}）".Trim());
        AddRow("CPU", string.Join(" / ", s.Cpu.Select(c => $"{c.Name}（{c.Cores} 核 / {c.Threads} 线程）")));
        AddRow("内存", $"{s.Memory.Count} 条，共 {FormatGb(s.Memory.Sum(m => m.CapacityBytes ?? 0))} GB");
        AddRow("显卡（已排除虚拟设备）", string.Join(" / ", s.Gpus.Select(g => g.Name)));
        AddRow("磁盘", string.Join(" / ", s.Disks.Select(d => $"{d.Model}（{d.MediaType ?? d.InterfaceType}）")));
        AddRow("网络", string.Join(" / ", s.Network.Select(n => n.Name)));
        AddRow("设备总数", s.Devices.Count.ToString());
        AddRow("采集时间", s.CollectedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        AddRow("规则库版本", _rules.RulesVersion);

        foreach (var warning in s.Collection.Warnings)
        {
            AddRow("采集告警", warning, Color.Firebrick);
        }

        _summaryGrid.ResumeLayout();
    }

    private void AddRow(string label, string? value, Color? color = null)
    {
        var row = _summaryGrid.RowCount++;
        _summaryGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _summaryGrid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 5, 10, 5)
        }, 0, row);
        _summaryGrid.Controls.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = color ?? Color.Black,
            Margin = new Padding(0, 5, 0, 5)
        }, 1, row);
    }

    private void RenderDevices()
    {
        _deviceGrid.DataSource = _snapshot!.Devices.Select(d => new
        {
            类别 = d.ClassName,
            名称 = d.Name,
            厂商 = d.Manufacturer,
            状态 = d.Status,
            问题代码 = d.ProblemCode == 0 ? "" : d.ProblemCode.ToString(),
            驱动版本 = d.Driver?.Version,
            驱动日期 = d.Driver?.Date,
            硬件ID = d.PnpId
        }).ToList();
    }

    private void RenderProblems()
    {
        _problemPanel.SuspendLayout();
        _problemPanel.Controls.Clear();
        _problemPanel.RowStyles.Clear();
        _problemPanel.RowCount = 0;

        if (_advice!.ProblemDevices.Count == 0)
        {
            AddProblemControl(new Label
            {
                Text = "没有发现带问题代码的设备。",
                AutoSize = true,
                ForeColor = Color.DimGray
            });
        }
        else
        {
            foreach (var device in _advice.ProblemDevices)
            {
                AddProblemControl(BuildDeviceCard(device));
            }
        }

        _problemPanel.ResumeLayout();
    }

    private void AddProblemControl(Control control)
    {
        var row = _problemPanel.RowCount++;
        _problemPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _problemPanel.Controls.Add(control, 0, row);
    }

    private Control BuildDeviceCard(DeviceAdvice device)
    {
        var card = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.FromArgb(248, 249, 250)
        };
        card.Controls.Add(new Label
        {
            Text = $"{device.DeviceName}（代码 {device.ProblemCode}：{ProblemCodes.Describe(device.ProblemCode)}）",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6)
        });

        if (device.Suggestions.Count == 0)
        {
            card.Controls.Add(new Label
            {
                Text = "未命中规则，请用硬件 ID 搜索 Microsoft Update Catalog。",
                AutoSize = true
            });
        }
        else
        {
            foreach (var suggestion in device.Suggestions)
            {
                card.Controls.Add(CreateSuggestionLink(suggestion));
            }
        }

        return card;
    }

    private void RenderAdvice()
    {
        _advicePanel.SuspendLayout();
        _advicePanel.Controls.Clear();
        _advicePanel.RowStyles.Clear();
        _advicePanel.RowCount = 0;

        AddAdviceSection("平台驱动（主板 / 整机厂商）", _advice!.Platform);
        AddAdviceSection(
            "显卡驱动",
            _advice.GpuDevices.SelectMany(d => d.Suggestions).DistinctBy(s => s.Url).ToList(),
            "数据来源：显示类设备，已排除 ROOT\\* 虚拟显示器");
        AddAdviceSection(
            "问题设备处理",
            _advice.ProblemDevices.SelectMany(d => d.Suggestions).DistinctBy(s => s.Url).ToList());

        _advicePanel.ResumeLayout();
    }

    private void AddAdviceSection(string title, List<DriverSuggestion> suggestions, string? sourceNote = null)
    {
        var row = _advicePanel.RowCount++;
        _advicePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var section = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 12)
        };
        section.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        });

        if (sourceNote is not null)
        {
            section.Controls.Add(new Label
            {
                Text = sourceNote,
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 0, 0, 4)
            });
        }

        if (suggestions.Count == 0)
        {
            section.Controls.Add(new Label
            {
                Text = "暂无匹配建议。",
                AutoSize = true,
                ForeColor = Color.DimGray
            });
        }
        else
        {
            foreach (var suggestion in suggestions)
            {
                section.Controls.Add(CreateSuggestionLink(suggestion));
            }
        }

        _advicePanel.Controls.Add(section, 0, row);
    }

    private LinkLabel CreateSuggestionLink(DriverSuggestion suggestion)
    {
        var link = new LinkLabel
        {
            Text = suggestion.Title,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2)
        };
        if (!string.IsNullOrWhiteSpace(suggestion.Note))
        {
            _toolTip.SetToolTip(link, suggestion.Note);
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("复制链接", null, (_, _) => CopyToClipboard(suggestion.Url));
        link.ContextMenuStrip = menu;
        link.LinkClicked += (_, _) => OpenUrl(suggestion.Url);
        return link;
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            _statusLabel.Text = "链接已复制";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "复制失败：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "链接格式无效：" + url, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "无法打开链接：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportHtml()
    {
        if (_snapshot is null || _advice is null)
        {
            MessageBox.Show(this, "请先完成采集。", "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "HTML 报告 (*.html)|*.html",
            FileName = $"硬件报告_{SafeFileName(_snapshot.System.Hostname)}_{DateTime.Now:yyyyMMdd_HHmm}.html"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, HtmlReportBuilder.Build(_snapshot, _advice, ToolVersion), new UTF8Encoding(true));
            _statusLabel.Text = "已导出 HTML：" + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportJson()
    {
        if (_snapshot is null)
        {
            MessageBox.Show(this, "请先完成采集。", "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "JSON 快照 (*.json)|*.json",
            FileName = $"硬件快照_{SafeFileName(_snapshot.System.Hostname)}_{DateTime.Now:yyyyMMdd_HHmm}.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, JsonDefaults.Serialize(_snapshot), new UTF8Encoding(false));
            _statusLabel.Text = "已导出 JSON：" + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowPanel()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private static string SafeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "PC";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    private static string FormatGb(long bytes) => (bytes / 1073741824.0).ToString("0.#");
}
