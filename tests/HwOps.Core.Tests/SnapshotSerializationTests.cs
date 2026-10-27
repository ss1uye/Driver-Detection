using HwOps.Core.Models;
using Xunit;

namespace HwOps.Core.Tests;

public class SnapshotSerializationTests
{
    [Fact]
    public void Serialize_UsesCamelCase_AndRoundTrips()
    {
        var snapshot = new HardwareSnapshot
        {
            MachineId = "abc123",
            CollectedAt = DateTimeOffset.Parse("2026-10-27T09:00:00+08:00"),
            System = new MachineSystemInfo
            {
                Hostname = "PC-01",
                Manufacturer = "ASUS",
                Model = "B560M"
            },
            Devices =
            {
                new DeviceInfo
                {
                    Name = "RTX 3060",
                    ClassName = "Display",
                    PnpId = "PCI\\VEN_10DE&DEV_2504",
                    ProblemCode = 0
                }
            }
        };

        var json = JsonDefaults.Serialize(snapshot);

        Assert.Contains("\"machineId\": \"abc123\"", json);
        Assert.Contains("\"hostname\": \"PC-01\"", json);

        var back = JsonDefaults.Deserialize<HardwareSnapshot>(json);

        Assert.NotNull(back);
        Assert.Equal("PC-01", back!.System.Hostname);
        Assert.Single(back.Devices);
        Assert.Equal("PCI\\VEN_10DE&DEV_2504", back.Devices[0].PnpId);
    }

    [Fact]
    public void Deserialize_AllowsMissingOptionals()
    {
        var snapshot = JsonDefaults.Deserialize<HardwareSnapshot>("""{"schemaVersion":1,"machineId":"x"}""");

        Assert.NotNull(snapshot);
        Assert.Equal("x", snapshot!.MachineId);
        Assert.Null(snapshot.System.Hostname);
        Assert.Empty(snapshot.Devices);
    }
}
