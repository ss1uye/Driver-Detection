using HwOps.Core.Models;
using Xunit;

namespace HwOps.Core.Tests;

public class ProblemCodesTests
{
    [Theory]
    [InlineData(0, "正常")]
    [InlineData(1, "设备未正确配置")]
    [InlineData(3, "该设备的驱动程序可能已损坏")]
    [InlineData(10, "设备无法启动")]
    [InlineData(12, "设备无法找到足够的可用资源")]
    [InlineData(14, "在重新启动计算机之前，该设备无法正常工作")]
    [InlineData(16, "Windows 无法识别该设备使用的所有资源")]
    [InlineData(18, "请重新安装此设备的驱动程序")]
    [InlineData(19, "注册表信息不完整或已损坏")]
    [InlineData(21, "Windows 正在删除此设备")]
    [InlineData(22, "设备已禁用")]
    [InlineData(24, "设备不存在、工作不正常或未安装全部驱动")]
    [InlineData(28, "未安装驱动程序")]
    [InlineData(29, "该设备已被禁用，因为其固件未能提供足够的资源")]
    [InlineData(31, "设备无法正常工作，因为 Windows 无法加载所需驱动")]
    [InlineData(32, "设备的驱动程序服务已禁用")]
    [InlineData(33, "Windows 无法判定这个设备需要哪些资源")]
    [InlineData(34, "Windows 无法决定这个设备的设置，请手动重新配置")]
    [InlineData(35, "计算机的固件没有为这个设备提供足够的资源")]
    [InlineData(36, "该设备正在请求 PCI 中断，但计算机的 BIOS 无法提供")]
    [InlineData(37, "Windows 无法初始化此设备的驱动程序")]
    [InlineData(38, "该设备驱动程序的上一个实例仍在内存中，通常需要重新启动计算机")]
    [InlineData(39, "Windows 无法加载此设备的驱动程序")]
    [InlineData(40, "Windows 无法访问该硬件，其驱动程序可能缺失、损坏或无法访问")]
    [InlineData(41, "Windows 已成功加载驱动程序，但找不到该硬件设备")]
    [InlineData(42, "Windows 无法加载驱动程序，因为驱动程序服务的筛选器有问题或版本不正确")]
    [InlineData(43, "Windows 已停止此设备，因为它报告了问题")]
    [InlineData(44, "某个应用程序或服务已停止这个硬件设备")]
    [InlineData(45, "设备当前未连接到计算机")]
    [InlineData(46, "Windows 无法访问该硬件，因为其配置信息仍在准备中")]
    [InlineData(47, "Windows 无法使用该硬件设备，因为其配置信息尚未完成")]
    [InlineData(48, "该设备的软件已被阻止启动，通常需要更新设备驱动程序")]
    [InlineData(49, "Windows 无法启动新的硬件设备，因为配置信息不完整")]
    [InlineData(50, "Windows 无法应用该设备的所有属性，某个驱动程序提供了无效的属性数据")]
    [InlineData(51, "该设备正在等待另一台完成初始化的设备，请重新启动计算机")]
    [InlineData(52, "Windows 无法验证该设备所需驱动程序的数字签名，请重新安装驱动程序")]
    public void Describe_ReturnsKnownText(int code, string expected)
    {
        Assert.Equal(expected, ProblemCodes.Describe(code));
    }

    [Fact]
    public void Describe_UnknownCode_ReturnsFallbackText()
    {
        Assert.Equal("设备问题代码 99", ProblemCodes.Describe(99));
    }

    [Fact]
    public void Describe_NegativeCode_ReturnsFallbackText()
    {
        Assert.Equal("设备问题代码 -1", ProblemCodes.Describe(-1));
    }
}
