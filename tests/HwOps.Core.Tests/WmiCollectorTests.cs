using HwOps.Core.Collection;
using HwOps.Core.Models;
using Xunit;

namespace HwOps.Core.Tests;

public class WmiCollectorTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Collect_CurrentMachine_ReturnsCoreData()
    {
        var snapshot = new WmiHardwareCollector().Collect();

        Assert.Equal(32, snapshot.MachineId.Length);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.System.Hostname));
        Assert.True(snapshot.Devices.Count > 0);
        Assert.True(snapshot.Cpu.Count > 0);
        Assert.Equal(snapshot.Devices.Count(d => d.ProblemCode != 0), snapshot.ProblemDevices.Count);
        Assert.All(snapshot.Devices, d => Assert.False(string.IsNullOrWhiteSpace(d.PnpId)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Collect_Twice_IsDeterministicAndHealthy()
    {
        var first = new WmiHardwareCollector().Collect();
        var second = new WmiHardwareCollector().Collect();

        // 并发采集结果必须确定：machineId 与设备数量两次一致。
        Assert.Equal(first.MachineId, second.MachineId);
        Assert.Equal(first.Devices.Count, second.Devices.Count);

        // 健康机器上不应有任何告警（类别失败/预算超时都会留下告警）。
        Assert.Empty(first.Collection.Warnings);
    }

    [Fact]
    public void BuildProblemDevices_FiltersSortsAndDescribes()
    {
        var devices = new List<DeviceInfo>
        {
            new() { ClassName = "Display", Name = "Healthy GPU", PnpId = "PCI\\VEN_10DE&DEV_1", ProblemCode = 0 },
            new() { ClassName = "Net", Name = "Missing Driver NIC", PnpId = "PCI\\VEN_10EC&DEV_8125", ProblemCode = 28 },
            new() { ClassName = "USB", Name = "Failing Device", PnpId = "USB\\VID_1234", ProblemCode = 43 }
        };

        var problems = WmiHardwareCollector.BuildProblemDevices(devices);

        Assert.Equal(2, problems.Count);
        Assert.Equal(28, problems[0].ProblemCode);
        Assert.Equal("未安装驱动程序", problems[0].ProblemText);
        Assert.Equal(43, problems[1].ProblemCode);
        Assert.Equal("Windows 已停止此设备，因为它报告了问题", problems[1].ProblemText);
        // 顺序与输入一致（投影不重排）。
        Assert.Equal("PCI\\VEN_10EC&DEV_8125", problems[0].PnpId);
        Assert.Equal("USB\\VID_1234", problems[1].PnpId);
        Assert.Equal("Missing Driver NIC", problems[0].Name);
        Assert.Equal("Net", problems[0].ClassName);
    }

    [Theory]
    [InlineData(@"ROOT\DISPLAY\0000", true)]
    [InlineData(@"BTH\MS_BTHPAN\X", true)]
    [InlineData(@"USBSTOR\Disk&Ven_Kingston&Prod_SXRF", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(@"PCI\VEN_10DE&DEV_2486&SUBSYS_36801462", false)]
    [InlineData(@"PCI\VEN_10EC&DEV_8125&SUBSYS_7C571458", false)]
    public void IsVirtualPnpId_ClassifiesRootAndRealDevices(string? pnpId, bool expected)
    {
        Assert.Equal(expected, WmiHardwareCollector.IsVirtualPnpId(pnpId));
    }

    [Theory]
    [InlineData("System Serial Number")]
    [InlineData("  To Be Filled By O.E.M.  ")]
    [InlineData("Type1 - Default string")]
    [InlineData("")]
    public void ScrubSerial_NormalizesPlaceholderToNull(string raw)
    {
        Assert.Null(WmiHardwareCollector.ScrubSerial(raw));
    }

    [Theory]
    [InlineData("WD-WCC4JPRLEZZ7", "WD-WCC4JPRLEZZ7")]
    [InlineData("  PF4A80ABCD1234  ", "PF4A80ABCD1234")]
    public void ScrubSerial_KeepsRealSerialTrimmed(string raw, string expected)
    {
        Assert.Equal(expected, WmiHardwareCollector.ScrubSerial(raw));
    }

    [Fact]
    public void Collect_CallerCancellation_PropagatesOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var collector = new WmiHardwareCollector();
        Assert.ThrowsAny<OperationCanceledException>(() => collector.Collect(cts.Token));
    }
}
