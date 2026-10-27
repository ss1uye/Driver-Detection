using HwOps.Core.Collection;
using Xunit;

namespace HwOps.Core.Tests;

public class MachineIdentityTests
{
    [Fact]
    [Trait("Category", "Integration")] // 依赖本机 WMI / 注册表，非纯单元测试
    public void Get_ReturnsStable32CharHex()
    {
        var first = MachineIdentity.Get(new List<string>());
        var second = MachineIdentity.Get(new List<string>());

        Assert.Equal(first, second);
        Assert.Equal(32, first.Length);
        Assert.Matches("^[0-9a-f]{32}$", first);
    }
}
