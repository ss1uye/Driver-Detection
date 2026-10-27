using HwOps.Core.Models;
using HwOps.Core.Rules;
using Xunit;

namespace HwOps.Core.Tests;

public class RuleMatcherTests
{
    private static RuleSet Rules(params DriverRule[] rules) => new()
    {
        RulesVersion = "test",
        Rules = rules.ToList()
    };

    [Fact]
    public void SuggestForMachine_MatchesBoardPattern_AndRendersProduct()
    {
        var rules = Rules(new DriverRule
        {
            Id = "board-asus",
            Tier = 3,
            Match = new RuleMatch { Type = "board", ManufacturerPattern = "ASUSTeK|ASUS" },
            Title = "华硕 {boardProduct} 驱动",
            UrlTemplate = "https://example.com/download/{boardProduct}",
            Note = "在支持页搜索 {boardProduct}"
        });
        var snapshot = new HardwareSnapshot
        {
            Board = new BoardInfo { Manufacturer = "ASUSTeK COMPUTER INC.", Product = "PRIME B560M-A" }
        };

        var suggestion = new RuleMatcher(rules).SuggestForMachine(snapshot).Single();

        Assert.Equal("华硕 PRIME B560M-A 驱动", suggestion.Title);
        Assert.Equal("https://example.com/download/PRIME%20B560M-A", suggestion.Url);
        Assert.Equal("在支持页搜索 PRIME B560M-A", suggestion.Note);
        Assert.Equal("主板型号匹配", suggestion.MatchReason);
    }

    [Fact]
    public void SuggestForDevice_MatchesPciVendorAndClass()
    {
        var rules = Rules(new DriverRule
        {
            Id = "gpu-nvidia",
            Tier = 1,
            Match = new RuleMatch { Type = "pci", VendorId = "10DE", ClassPattern = "^Display$" },
            Title = "NVIDIA 驱动",
            UrlTemplate = "https://example.com/{vendorId}/{deviceId}"
        });
        var device = new DeviceInfo
        {
            ClassName = "Display",
            PnpId = "PCI\\VEN_10DE&DEV_2504&SUBSYS_146219DA"
        };
        var snapshot = new HardwareSnapshot { Devices = { device } };

        var suggestion = new RuleMatcher(rules).SuggestForDevice(snapshot, device).Single();

        Assert.Equal("https://example.com/10DE/2504", suggestion.Url);
    }

    [Fact]
    public void SuggestForDevice_RespectsClassFilter()
    {
        var rules = Rules(new DriverRule
        {
            Id = "gpu-intel",
            Tier = 1,
            Match = new RuleMatch { Type = "pci", VendorId = "8086", ClassPattern = "^Display$" },
            Title = "Intel 显卡",
            UrlTemplate = "https://example.com/intel"
        });
        var nic = new DeviceInfo { ClassName = "Net", PnpId = "PCI\\VEN_8086&DEV_A0F0" };
        var snapshot = new HardwareSnapshot { Devices = { nic } };

        Assert.Empty(new RuleMatcher(rules).SuggestForDevice(snapshot, nic));
    }

    [Fact]
    public void SuggestForDevice_FallbackAny_EncodesHardwareId()
    {
        var rules = Rules(new DriverRule
        {
            Id = "fallback",
            Tier = 5,
            Match = new RuleMatch { Type = "any" },
            Title = "Catalog {hardwareId}",
            UrlTemplate = "https://example.com/search?q={hardwareId}"
        });
        var device = new DeviceInfo { ClassName = "USB", PnpId = "USB\\VID_1234&PID_5678" };
        var snapshot = new HardwareSnapshot { Devices = { device } };

        var suggestion = new RuleMatcher(rules).SuggestForDevice(snapshot, device).Single();

        Assert.Equal("https://example.com/search?q=USB%5CVID_1234%26PID_5678", suggestion.Url);
        Assert.Equal("通用兜底", suggestion.MatchReason);
    }

    [Fact]
    public void SuggestForDevice_MatchesUsbVidPid()
    {
        var rules = Rules(new DriverRule
        {
            Id = "bt-intel",
            Tier = 1,
            Match = new RuleMatch { Type = "usb", VendorId = "8087", DeviceId = "0026", ClassPattern = "^Bluetooth$" },
            Title = "Intel 蓝牙驱动",
            UrlTemplate = "https://example.com/bluetooth"
        });
        var device = new DeviceInfo { ClassName = "Bluetooth", PnpId = "USB\\VID_8087&PID_0026&REV_0001" };
        var snapshot = new HardwareSnapshot { Devices = { device } };

        var suggestion = new RuleMatcher(rules).SuggestForDevice(snapshot, device).Single();

        Assert.Equal("硬件 ID 匹配", suggestion.MatchReason);
    }

    [Fact]
    public void SuggestForDevice_DoesNotCrossMatchBusType()
    {
        var rules = Rules(new DriverRule
        {
            Id = "bt-intel",
            Tier = 1,
            Match = new RuleMatch { Type = "usb", VendorId = "8087", DeviceId = "0026" },
            Title = "Intel 蓝牙驱动",
            UrlTemplate = "https://example.com/bluetooth"
        });
        var device = new DeviceInfo { ClassName = "Bluetooth", PnpId = "PCI\\VEN_8087&DEV_0026" };
        var snapshot = new HardwareSnapshot { Devices = { device } };

        Assert.Empty(new RuleMatcher(rules).SuggestForDevice(snapshot, device));
    }

    [Fact]
    public void InvalidRegex_DoesNotMatch_AndDoesNotThrow()
    {
        var rules = Rules(new DriverRule
        {
            Id = "bad",
            Tier = 3,
            Match = new RuleMatch { Type = "board", ManufacturerPattern = "([" },
            Title = "Bad",
            UrlTemplate = "https://example.com"
        });
        var snapshot = new HardwareSnapshot { Board = new BoardInfo { Manufacturer = "ASUS" } };

        Assert.Empty(new RuleMatcher(rules).SuggestForMachine(snapshot));
    }
}
