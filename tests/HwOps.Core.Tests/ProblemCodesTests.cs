using HwOps.Core.Models;
using Xunit;

namespace HwOps.Core.Tests;

public class ProblemCodesTests
{
    [Theory]
    [InlineData(28, "未安装驱动程序")]
    [InlineData(10, "设备无法启动")]
    [InlineData(43, "Windows 已停止此设备，因为它报告了问题")]
    public void Describe_ReturnsKnownText(int code, string expected)
    {
        Assert.Equal(expected, ProblemCodes.Describe(code));
    }

    [Fact]
    public void Describe_UnknownCode_IncludesNumber()
    {
        var text = ProblemCodes.Describe(99);

        Assert.Contains("99", text);
    }
}
