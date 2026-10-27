namespace HwOps.Core.Models;

public sealed class GpuInfo
{
    public string? Name { get; set; }
    public string? DriverVersion { get; set; }
    public string? DriverDate { get; set; }
    public string? PnpId { get; set; }
    public string? Status { get; set; }
}

public sealed class DiskInfo
{
    public string? Model { get; set; }
    public string? MediaType { get; set; }
    public string? InterfaceType { get; set; }
    public long? SizeBytes { get; set; }
    public string? PnpId { get; set; }
    public string? Serial { get; set; }
}

public sealed class NetworkAdapterInfo
{
    public string? Name { get; set; }
    public string? Mac { get; set; }
    public string? PnpId { get; set; }
    public string? ConnectionName { get; set; }
    public bool? Enabled { get; set; }
    public long? SpeedBps { get; set; }
}

public sealed class SoundDeviceInfo
{
    public string? Name { get; set; }
    public string? Manufacturer { get; set; }
    public string? PnpId { get; set; }
    public string? Status { get; set; }
}

public sealed class DriverInfo
{
    public string? Version { get; set; }
    public string? Date { get; set; }
    public string? Provider { get; set; }
    public string? Inf { get; set; }
    public bool? Signed { get; set; }
}

public sealed class DeviceInfo
{
    public string ClassName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Manufacturer { get; set; }
    public string PnpId { get; set; } = "";
    public string? Status { get; set; }
    public int ProblemCode { get; set; }
    public DriverInfo? Driver { get; set; }
}

public sealed class ProblemDevice
{
    public string PnpId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public int ProblemCode { get; set; }
    public string ProblemText { get; set; } = "";
}
