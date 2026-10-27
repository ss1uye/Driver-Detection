using HwOps.Core.Collection;
using Xunit;

namespace HwOps.Core.Tests;

public class WmiQueryTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Query_InvalidClass_AddsWarning()
    {
        var warnings = new List<string>();

        Wmi.Query("SELECT * FROM Win32_NoSuchClass_HwOps", "探针", warnings, _ => Assert.Fail("不应调用 map"));

        Assert.Single(warnings);
        Assert.Contains("探针", warnings[0]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Query_RowException_KeepsProcessingOtherRows()
    {
        var warnings = new List<string>();

        var rowCount = 0;
        Wmi.Query("SELECT * FROM Win32_LogicalDisk", "磁盘", warnings, _ => rowCount++);
        if (rowCount < 2)
        {
            // 单盘环境无法构造第 2 行失败场景，避免脆弱断言。
            return;
        }

        var calls = 0;
        Wmi.Query("SELECT * FROM Win32_LogicalDisk", "磁盘", warnings, item =>
        {
            calls++;
            if (calls == 2)
            {
                throw new InvalidCastException("probe");
            }
        });

        Assert.True(warnings.Count >= 1);
        Assert.Contains(warnings, w => w.Contains("第 2 行"));
        Assert.Equal(rowCount, calls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void WmiDate_ParsesCimDatetime()
    {
        var warnings = new List<string>();
        string? parsed = null;

        Wmi.Query("SELECT ReleaseDate FROM Win32_BIOS", "BIOS 日期", warnings, item =>
        {
            parsed = item.WmiDate("ReleaseDate");
        });

        Assert.NotNull(parsed);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", parsed!);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Text_MissingProperty_ReturnsNull()
    {
        var warnings = new List<string>();
        var checkedCount = 0;

        Wmi.Query("SELECT * FROM Win32_BIOS", "BIOS", warnings, item =>
        {
            checkedCount++;
            Assert.Null(item.Text("NoSuchProperty_HwOps"));
        });

        Assert.True(checkedCount >= 1);
    }
}
