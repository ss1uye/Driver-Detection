using System.Net;
using System.Text;
using HwOps.Core.Models;
using HwOps.Core.Rules;

namespace HwOps.Core.Reporting;

public static class HtmlReportBuilder
{
    private const string Css = """
        body{font-family:"Microsoft YaHei UI","Microsoft YaHei",sans-serif;margin:32px;color:#1f2328;line-height:1.55;background:#fff}
        h1{font-size:24px;margin:0 0 4px}
        h2{font-size:17px;margin:28px 0 10px;border-left:4px solid #2f6feb;padding-left:8px}
        .muted{color:#6e7781;font-size:13px}
        table{border-collapse:collapse;width:100%;margin:8px 0;font-size:13px}
        th,td{border:1px solid #d0d7de;padding:6px 8px;text-align:left;vertical-align:top}
        th{background:#f6f8fa;white-space:nowrap}
        .problem{color:#b00020;font-weight:600}
        .warning{color:#9a6700}
        ul{margin:6px 0;padding-left:20px}
        li{margin:3px 0}
        code{background:#f6f8fa;padding:1px 4px;border-radius:4px;font-size:12px}
        .card{border:1px solid #d0d7de;border-radius:8px;padding:12px 14px;margin:10px 0}
        details{margin:10px 0}
        summary{cursor:pointer;color:#2f6feb}
        """;

    public static string Build(HardwareSnapshot snapshot, MachineAdvice advice, string toolVersion)
    {
        var sb = new StringBuilder(16 * 1024);
        sb.Append("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<title>硬件体检报告 - ").Append(E(snapshot.System.Hostname)).Append("</title>")
            .Append("<style>").Append(Css).Append("</style></head><body>");

        sb.Append("<h1>硬件体检报告</h1>");
        sb.Append("<p class=\"muted\">主机：").Append(E(snapshot.System.Hostname))
            .Append(" ｜ 采集时间：").Append(E(snapshot.CollectedAt.ToString("yyyy-MM-dd HH:mm:ss")))
            .Append(" ｜ 工具版本：").Append(E(toolVersion)).Append("</p>");

        AppendOverview(sb, snapshot);
        AppendProblems(sb, advice);
        AppendSuggestions(sb, "平台驱动（主板 / 整机厂商）", advice.Platform);
        AppendDeviceSuggestions(sb, "显卡驱动", advice.GpuDevices);
        AppendDeviceSuggestions(sb, "问题设备处理建议", advice.ProblemDevices);
        AppendAllDevices(sb, snapshot);
        AppendWarnings(sb, snapshot);

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void AppendOverview(StringBuilder sb, HardwareSnapshot s)
    {
        sb.Append("<h2>系统概况</h2><table>");
        Row(sb, "操作系统", $"{s.System.Os.Caption} {s.System.Os.Arch}".Trim());
        Row(sb, "整机", $"{s.System.Manufacturer} {s.System.Model}".Trim());
        Row(sb, "序列号", s.System.Serial);
        Row(sb, "主板", $"{s.Board.Manufacturer} {s.Board.Product} {s.Board.Version}".Trim());
        Row(sb, "BIOS", $"{s.Bios.Vendor} {s.Bios.Version} ({s.Bios.ReleaseDate})".Trim());
        Row(sb, "CPU", string.Join(" / ", s.Cpu.Select(c => $"{c.Name}（{c.Cores} 核 / {c.Threads} 线程）")));
        Row(sb, "内存", $"{s.Memory.Count} 条，共 {FormatGb(s.Memory.Sum(m => m.CapacityBytes ?? 0))} GB");
        Row(sb, "显卡", string.Join(" / ", s.Gpus.Select(g => g.Name)));
        Row(sb, "磁盘", string.Join(" / ", s.Disks.Select(d => $"{d.Model}（{d.MediaType ?? d.InterfaceType}）")));
        Row(sb, "网络", string.Join(" / ", s.Network.Select(n => n.Name)));
        Row(sb, "设备总数", s.Devices.Count.ToString());
        Row(sb, "采集告警条数", s.Collection.Warnings.Count.ToString());
        sb.Append("</table>");
    }

    private static void AppendProblems(StringBuilder sb, MachineAdvice advice)
    {
        sb.Append("<h2>问题设备</h2>");
        if (advice.ProblemDevices.Count == 0)
        {
            sb.Append("<p class=\"muted\">未发现带问题代码的设备。</p>");
            return;
        }

        sb.Append("<table><tr><th>设备</th><th>硬件 ID</th><th>代码</th><th>说明</th></tr>");
        foreach (var device in advice.ProblemDevices)
        {
            sb.Append("<tr><td>").Append(E(device.DeviceName))
                .Append("</td><td><code>").Append(E(device.PnpId))
                .Append("</code></td><td class=\"problem\">").Append(device.ProblemCode)
                .Append("</td><td>").Append(E(ProblemCodes.Describe(device.ProblemCode))).Append("</td></tr>");
        }
        sb.Append("</table>");
    }

    private static void AppendSuggestions(StringBuilder sb, string title, List<DriverSuggestion> suggestions)
    {
        sb.Append("<h2>").Append(E(title)).Append("</h2>");
        AppendList(sb, suggestions);
    }

    private static void AppendDeviceSuggestions(StringBuilder sb, string title, List<DeviceAdvice> devices)
    {
        if (devices.Count == 0)
        {
            return;
        }

        sb.Append("<h2>").Append(E(title)).Append("</h2>");
        foreach (var device in devices)
        {
            sb.Append("<div class=\"card\"><strong>").Append(E(device.DeviceName)).Append("</strong>");
            if (device.ProblemCode != 0)
            {
                sb.Append(" <span class=\"problem\">代码 ").Append(device.ProblemCode)
                    .Append("：").Append(E(ProblemCodes.Describe(device.ProblemCode))).Append("</span>");
            }
            sb.Append("<div class=\"muted\"><code>").Append(E(device.PnpId)).Append("</code></div>");
            AppendList(sb, device.Suggestions);
            sb.Append("</div>");
        }
    }

    private static void AppendList(StringBuilder sb, IEnumerable<DriverSuggestion> suggestions)
    {
        var items = suggestions.ToList();
        if (items.Count == 0)
        {
            sb.Append("<p class=\"muted\">未命中规则。</p>");
            return;
        }

        sb.Append("<ul>");
        foreach (var suggestion in items)
        {
            sb.Append("<li>");
            if (IsSafeUrl(suggestion.Url))
            {
                sb.Append("<a href=\"").Append(E(suggestion.Url))
                    .Append("\" target=\"_blank\" rel=\"noopener\">").Append(E(suggestion.Title)).Append("</a>");
            }
            else
            {
                sb.Append(E(suggestion.Title))
                    .Append("<span class=\"muted\">（链接无效，已忽略）</span>");
            }
            if (!string.IsNullOrWhiteSpace(suggestion.Note))
            {
                sb.Append("<div class=\"muted\">").Append(E(suggestion.Note)).Append("</div>");
            }
            sb.Append("</li>");
        }
        sb.Append("</ul>");
    }

    private static void AppendAllDevices(StringBuilder sb, HardwareSnapshot snapshot)
    {
        sb.Append("<details><summary>全部设备（含软件与虚拟设备，共 ")
            .Append(snapshot.Devices.Count).Append(" 项）</summary>");
        sb.Append("<table><tr><th>类别</th><th>名称</th><th>厂商</th><th>状态</th><th>问题</th><th>驱动版本</th><th>硬件 ID</th></tr>");
        foreach (var device in snapshot.Devices)
        {
            sb.Append("<tr><td>").Append(E(device.ClassName))
                .Append("</td><td>").Append(E(device.Name))
                .Append("</td><td>").Append(E(device.Manufacturer))
                .Append("</td><td>").Append(E(device.Status))
                .Append("</td><td>").Append(device.ProblemCode == 0 ? "" : device.ProblemCode.ToString())
                .Append("</td><td>").Append(E(device.Driver?.Version))
                .Append("</td><td><code>").Append(E(device.PnpId)).Append("</code></td></tr>");
        }
        sb.Append("</table></details>");
    }

    private static void AppendWarnings(StringBuilder sb, HardwareSnapshot snapshot)
    {
        if (snapshot.Collection.Warnings.Count == 0)
        {
            return;
        }

        sb.Append("<h2>采集告警</h2><ul>");
        foreach (var warning in snapshot.Collection.Warnings)
        {
            sb.Append("<li class=\"warning\">").Append(E(warning)).Append("</li>");
        }
        sb.Append("</ul>");
    }

    private static void Row(StringBuilder sb, string label, string? value)
    {
        sb.Append("<tr><th>").Append(E(label)).Append("</th><td>")
            .Append(string.IsNullOrWhiteSpace(value) ? "—" : E(value))
            .Append("</td></tr>");
    }

    private static bool IsSafeUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string FormatGb(long bytes)
        => (bytes / 1073741824.0).ToString("0.#");

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
}
