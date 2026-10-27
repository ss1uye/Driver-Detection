using HwOps.Core.Models;
using HwOps.Core.Rules;
using Xunit;

namespace HwOps.Core.Tests;

public class DriverAdvisorTests
{
    private static RuleSet Rules() => new()
    {
        RulesVersion = "test",
        Rules =
        {
            new DriverRule
            {
                Id = "board-asus",
                Tier = 3,
                Match = new RuleMatch { Type = "board", ManufacturerPattern = "ASUS" },
                Title = "华硕主板 {boardProduct}",
                UrlTemplate = "https://example.com/board/{boardProduct}"
            },
            new DriverRule
            {
                Id = "gpu-nvidia",
                Tier = 1,
                Match = new RuleMatch { Type = "pci", VendorId = "10DE", ClassPattern = "^Display$" },
                Title = "NVIDIA 驱动",
                UrlTemplate = "https://example.com/nvidia"
            },
            new DriverRule
            {
                Id = "fallback",
                Tier = 5,
                Match = new RuleMatch { Type = "any" },
                Title = "Catalog {hardwareId}",
                UrlTemplate = "https://example.com/catalog?q={hardwareId}"
            }
        }
    };

    [Fact]
    public void Advise_ReturnsPlatformGpuAndProblemSuggestions()
    {
        var gpu = new DeviceInfo
        {
            ClassName = "Display",
            Name = "NVIDIA GeForce RTX 3060",
            PnpId = "PCI\\VEN_10DE&DEV_2504"
        };
        var usb = new DeviceInfo
        {
            ClassName = "USB",
            Name = "未知 USB 设备",
            PnpId = "USB\\VID_1234&PID_5678",
            ProblemCode = 28
        };
        var snapshot = new HardwareSnapshot
        {
            Board = new BoardInfo { Manufacturer = "ASUS", Product = "PRIME B560M-A" },
            Devices = { gpu, usb },
            ProblemDevices =
            {
                new ProblemDevice
                {
                    PnpId = usb.PnpId,
                    Name = usb.Name,
                    ClassName = usb.ClassName,
                    ProblemCode = 28,
                    ProblemText = "未安装驱动程序"
                }
            }
        };

        var advice = new DriverAdvisor(Rules()).Advise(snapshot);

        Assert.Contains(advice.Platform, s => s.RuleId == "board-asus");
        Assert.Contains(advice.GpuDevices, d => d.DeviceName.Contains("RTX 3060"));
        Assert.Contains(advice.GpuDevices.Single().Suggestions, s => s.RuleId == "gpu-nvidia");
        var problem = Assert.Single(advice.ProblemDevices);
        Assert.Equal(28, problem.ProblemCode);
        Assert.Contains(problem.Suggestions, s => s.RuleId == "fallback");
    }

    [Fact]
    public void Advise_UsesProblemDeviceCode_WhenDeviceRecordDiffers()
    {
        var pnpId = "USB\\VID_1234&PID_0001";
        var snapshot = new HardwareSnapshot
        {
            Devices =
            {
                new DeviceInfo
                {
                    ClassName = "USB",
                    Name = "USB 控制器",
                    PnpId = pnpId,
                    ProblemCode = 0
                }
            },
            ProblemDevices =
            {
                new ProblemDevice
                {
                    PnpId = pnpId,
                    Name = "USB 控制器",
                    ClassName = "USB",
                    ProblemCode = 28,
                    ProblemText = "未安装驱动程序"
                }
            }
        };

        var advice = new DriverAdvisor(Rules()).Advise(snapshot);

        Assert.Equal(28, advice.ProblemDevices.Single().ProblemCode);
    }

    [Fact]
    public void Advise_HandlesProblemDeviceMissingFromDevices()
    {
        var snapshot = new HardwareSnapshot
        {
            ProblemDevices =
            {
                new ProblemDevice
                {
                    PnpId = "USB\\VID_9999&PID_0002",
                    Name = "缺失的未知设备",
                    ClassName = "USB",
                    ProblemCode = 28,
                    ProblemText = "未安装驱动程序"
                }
            }
        };

        var advice = new DriverAdvisor(Rules()).Advise(snapshot);

        var problem = Assert.Single(advice.ProblemDevices);
        Assert.Equal("缺失的未知设备", problem.DeviceName);
        Assert.Equal(28, problem.ProblemCode);
        Assert.Contains(problem.Suggestions, s => s.RuleId == "fallback");
    }

    [Fact]
    public void Advise_TruncatesSuggestionsToThree()
    {
        var snapshot = new HardwareSnapshot
        {
            Devices =
            {
                new DeviceInfo
                {
                    ClassName = "USB",
                    Name = "未知 USB 设备",
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
        var rules = new RuleSet
        {
            RulesVersion = "test",
            Rules =
            {
                AnyRule("any-1", "https://example.com/any/1"),
                AnyRule("any-2", "https://example.com/any/2"),
                AnyRule("any-3", "https://example.com/any/3"),
                AnyRule("any-4", "https://example.com/any/4")
            }
        };

        var advice = new DriverAdvisor(rules).Advise(snapshot);

        Assert.Equal(3, advice.ProblemDevices.Single().Suggestions.Count);
    }

    private static DriverRule AnyRule(string id, string url) => new()
    {
        Id = id,
        Tier = 5,
        Match = new RuleMatch { Type = "any" },
        Title = $"通用候选 {id}",
        UrlTemplate = url
    };
}
