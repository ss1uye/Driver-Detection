using HwOps.Core.Models;
using HwOps.Core.Reporting;
using HwOps.Core.Rules;
using Xunit;

namespace HwOps.Core.Tests;

public class HtmlReportTests
{
    private static HardwareSnapshot MakeSnapshot() => new()
    {
        MachineId = "x",
        CollectedAt = DateTimeOffset.Parse("2026-10-27T09:00:00+08:00"),
        System = new MachineSystemInfo
        {
            Hostname = "<script>alert(1)</script>",
            Os = new OsInfo { Caption = "Windows 11", Arch = "64-bit" }
        },
        Board = new BoardInfo { Manufacturer = "ASUS", Product = "B560M" },
        Devices =
        {
            new DeviceInfo
            {
                Name = "未知 USB 设备",
                ClassName = "USB",
                PnpId = "USB\\VID_1234&PID_5678",
                ProblemCode = 28
            }
        },
        ProblemDevices =
        {
            new ProblemDevice
            {
                PnpId = "USB\\VID_1234&PID_5678",
                Name = "未知 USB 设备",
                ClassName = "USB",
                ProblemCode = 28,
                ProblemText = "未安装驱动程序"
            }
        }
    };

    private static MachineAdvice MakeAdvice() => new()
    {
        Platform =
        {
            new DriverSuggestion
            {
                RuleId = "board-asus",
                Tier = 3,
                Title = "主板驱动",
                Url = "https://example.com/board?a=1&b=2",
                Note = "在页面内搜索型号"
            }
        },
        ProblemDevices =
        {
            new DeviceAdvice
            {
                DeviceName = "未知 USB 设备",
                PnpId = "USB\\VID_1234&PID_5678",
                ProblemCode = 28,
                Suggestions =
                {
                    new DriverSuggestion
                    {
                        RuleId = "fallback",
                        Tier = 5,
                        Title = "Catalog 搜索",
                        Url = "https://example.com/catalog?q=USB%5CVID"
                    }
                }
            }
        }
    };

    [Fact]
    public void Build_EncodesHtml_AndIncludesAdviceLinks()
    {
        var html = HtmlReportBuilder.Build(MakeSnapshot(), MakeAdvice(), "0.1.0");

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("https://example.com/board?a=1&amp;b=2", html);
        Assert.Contains("问题设备", html);
        Assert.Contains("未安装驱动程序", html);
        Assert.Contains("0.1.0", html);
    }

    [Fact]
    public void Build_RejectsUnsafeUrlScheme()
    {
        var advice = new MachineAdvice
        {
            Platform =
            {
                new DriverSuggestion
                {
                    RuleId = "evil",
                    Tier = 1,
                    Title = "危险链接",
                    Url = "javascript:alert(1)"
                }
            }
        };

        var html = HtmlReportBuilder.Build(MakeSnapshot(), advice, "0.1.0");

        Assert.DoesNotContain("href=\"javascript:", html);
        Assert.Contains("链接无效", html);
        Assert.Contains("危险链接", html);
    }

    [Fact]
    public void Build_IncludesAllDeviceCountLabel()
    {
        var html = HtmlReportBuilder.Build(MakeSnapshot(), MakeAdvice(), "0.1.0");

        Assert.Contains("全部设备（含软件与虚拟设备，共", html);
    }

    [Fact]
    public void Build_RendersWarnings()
    {
        var snapshot = MakeSnapshot();
        snapshot.Collection.Warnings.Add("磁盘: 模拟失败");

        var html = HtmlReportBuilder.Build(snapshot, MakeAdvice(), "0.1.0");

        Assert.Contains("采集告警", html);
        Assert.Contains("磁盘: 模拟失败", html);
        Assert.Contains("采集告警条数", html);
        Assert.Contains("<td>1</td>", html);
    }
}
