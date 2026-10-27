namespace HwOps.Core.Models;

public sealed class HardwareSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public string MachineId { get; set; } = "";
    public DateTimeOffset CollectedAt { get; set; }
    public MachineSystemInfo System { get; set; } = new();
    public BoardInfo Board { get; set; } = new();
    public BiosInfo Bios { get; set; } = new();
    public List<CpuInfo> Cpu { get; set; } = new();
    public List<MemoryModuleInfo> Memory { get; set; } = new();
    public List<GpuInfo> Gpus { get; set; } = new();
    public List<DiskInfo> Disks { get; set; } = new();
    public List<NetworkAdapterInfo> Network { get; set; } = new();
    public List<SoundDeviceInfo> Sound { get; set; } = new();
    public List<DeviceInfo> Devices { get; set; } = new();
    public List<ProblemDevice> ProblemDevices { get; set; } = new();
    public CollectionInfo Collection { get; set; } = new();
}

public sealed class MachineSystemInfo
{
    public string? Hostname { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Serial { get; set; }
    public string? SmbiosUuid { get; set; }
    public OsInfo Os { get; set; } = new();
}

public sealed class OsInfo
{
    public string? Caption { get; set; }
    public string? Version { get; set; }
    public string? Build { get; set; }
    public string? Arch { get; set; }
}

public sealed class BoardInfo
{
    public string? Manufacturer { get; set; }
    public string? Product { get; set; }
    public string? Version { get; set; }
    public string? Serial { get; set; }
}

public sealed class BiosInfo
{
    public string? Vendor { get; set; }
    public string? Version { get; set; }
    public string? ReleaseDate { get; set; }
    public string? Serial { get; set; }
}

public sealed class CpuInfo
{
    public string? Name { get; set; }
    public int? Cores { get; set; }
    public int? Threads { get; set; }
    public int? MaxClockMhz { get; set; }
}

public sealed class MemoryModuleInfo
{
    public string? Manufacturer { get; set; }
    public string? PartNumber { get; set; }
    public long? CapacityBytes { get; set; }
    public int? SpeedMhz { get; set; }
    public string? Slot { get; set; }
    public string? FormFactor { get; set; }
}

public sealed class CollectionInfo
{
    public List<string> Warnings { get; set; } = new();
}
