using HwOps.Core.Rules;
using Xunit;

namespace HwOps.Core.Tests;

public class HardwareIdTests
{
    [Theory]
    [InlineData("PCI\\VEN_10DE&DEV_2504&SUBSYS_146219DA&REV_A1", "10DE", "2504", "146219DA")]
    [InlineData("pci\\ven_8086&dev_a0f0", "8086", "A0F0", null)]
    [InlineData("PCI\\VEN_10EC&DEV_8168", "10EC", "8168", null)]
    public void Parse_Pci_ExtractsIds(string raw, string vendor, string device, string? subsystem)
    {
        var id = HardwareId.Parse(raw);

        Assert.True(id.IsPci);
        Assert.Equal(vendor, id.VendorId);
        Assert.Equal(device, id.DeviceId);
        Assert.Equal(subsystem, id.SubSystemId);
    }

    [Fact]
    public void Parse_Usb_ExtractsVidPid()
    {
        var id = HardwareId.Parse("USB\\VID_8087&PID_0026&REV_0001");

        Assert.True(id.IsUsb);
        Assert.Equal("8087", id.UsbVendorId);
        Assert.Equal("0026", id.UsbProductId);
    }

    [Fact]
    public void Parse_Empty_ReturnsEmptyIds()
    {
        var id = HardwareId.Parse(null);

        Assert.False(id.IsPci);
        Assert.False(id.IsUsb);
        Assert.Null(id.VendorId);
    }
}
