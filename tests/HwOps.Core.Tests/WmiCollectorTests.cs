using HwOps.Core.Collection;
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
}
