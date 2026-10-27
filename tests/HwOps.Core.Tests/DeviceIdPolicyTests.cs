using HwOps.Core.Models;
using Xunit;

namespace HwOps.Core.Tests;

public class DeviceIdPolicyTests
{
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
        Assert.Equal(expected, DeviceIdPolicy.IsVirtualPnpId(pnpId));
    }
}
