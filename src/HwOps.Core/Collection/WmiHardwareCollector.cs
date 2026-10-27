using HwOps.Core.Models;

namespace HwOps.Core.Collection;

public sealed class WmiHardwareCollector : IHardwareCollector
{
    public HardwareSnapshot Collect(CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();

        cancellationToken.ThrowIfCancellationRequested();
        var system = ReadSystem(warnings);
        var board = ReadBoard(warnings);
        var bios = ReadBios(warnings);
        var cpu = ReadCpu(warnings);
        var memory = ReadMemory(warnings);
        var gpus = ReadGpus(warnings);
        var disks = ReadDisks(warnings);
        var network = ReadNetwork(warnings);
        var sound = ReadSound(warnings);

        cancellationToken.ThrowIfCancellationRequested();
        var drivers = ReadDrivers(warnings);
        var devices = ReadDevices(warnings, drivers);

        var problems = devices
            .Where(d => d.ProblemCode != 0)
            .Select(d => new ProblemDevice
            {
                PnpId = d.PnpId,
                Name = d.Name,
                ClassName = d.ClassName,
                ProblemCode = d.ProblemCode,
                ProblemText = ProblemCodes.Describe(d.ProblemCode)
            })
            .ToList();

        return new HardwareSnapshot
        {
            MachineId = MachineIdentity.Get(warnings),
            CollectedAt = DateTimeOffset.Now,
            System = system,
            Board = board,
            Bios = bios,
            Cpu = cpu,
            Memory = memory,
            Gpus = gpus,
            Disks = disks,
            Network = network,
            Sound = sound,
            Devices = devices,
            ProblemDevices = problems,
            Collection = new CollectionInfo { Warnings = warnings }
        };
    }

    private static MachineSystemInfo ReadSystem(List<string> warnings)
    {
        var info = new MachineSystemInfo { Hostname = Environment.MachineName };

        Wmi.Query("SELECT Manufacturer, Model FROM Win32_ComputerSystem", "系统信息", warnings, item =>
        {
            info.Manufacturer = item.Text("Manufacturer");
            info.Model = item.Text("Model");
        });

        Wmi.Query("SELECT UUID, IdentifyingNumber FROM Win32_ComputerSystemProduct", "整机标识", warnings, item =>
        {
            info.SmbiosUuid = item.Text("UUID");
            info.Serial = item.Text("IdentifyingNumber");
        });

        Wmi.Query("SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem", "操作系统", warnings, item =>
        {
            info.Os = new OsInfo
            {
                Caption = item.Text("Caption"),
                Version = item.Text("Version"),
                Build = item.Text("BuildNumber"),
                Arch = item.Text("OSArchitecture")
            };
        });

        return info;
    }

    private static BoardInfo ReadBoard(List<string> warnings)
    {
        var info = new BoardInfo();
        Wmi.Query("SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard", "主板信息", warnings, item =>
        {
            info.Manufacturer = item.Text("Manufacturer");
            info.Product = item.Text("Product");
            info.Version = item.Text("Version");
            info.Serial = item.Text("SerialNumber");
        });
        return info;
    }

    private static BiosInfo ReadBios(List<string> warnings)
    {
        var info = new BiosInfo();
        Wmi.Query("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber FROM Win32_BIOS", "BIOS 信息", warnings, item =>
        {
            info.Vendor = item.Text("Manufacturer");
            info.Version = item.Text("SMBIOSBIOSVersion");
            info.ReleaseDate = item.WmiDate("ReleaseDate");
            info.Serial = item.Text("SerialNumber");
        });
        return info;
    }

    private static List<CpuInfo> ReadCpu(List<string> warnings)
    {
        var list = new List<CpuInfo>();
        Wmi.Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor", "CPU", warnings, item =>
            list.Add(new CpuInfo
            {
                Name = item.Text("Name"),
                Cores = item.Int("NumberOfCores"),
                Threads = item.Int("NumberOfLogicalProcessors"),
                MaxClockMhz = item.Int("MaxClockSpeed")
            }));
        return list;
    }

    private static List<MemoryModuleInfo> ReadMemory(List<string> warnings)
    {
        var list = new List<MemoryModuleInfo>();
        Wmi.Query(
            "SELECT Manufacturer, PartNumber, Capacity, Speed, ConfiguredClockSpeed, DeviceLocator, FormFactor FROM Win32_PhysicalMemory",
            "内存",
            warnings,
            item => list.Add(new MemoryModuleInfo
            {
                Manufacturer = item.Text("Manufacturer"),
                PartNumber = item.Text("PartNumber"),
                CapacityBytes = item.Long("Capacity"),
                SpeedMhz = item.Int("ConfiguredClockSpeed") ?? item.Int("Speed"),
                Slot = item.Text("DeviceLocator"),
                FormFactor = item.Text("FormFactor")
            }));
        return list;
    }

    private static List<GpuInfo> ReadGpus(List<string> warnings)
    {
        var list = new List<GpuInfo>();
        Wmi.Query("SELECT Name, DriverVersion, DriverDate, PNPDeviceID, Status FROM Win32_VideoController", "显卡", warnings, item =>
            list.Add(new GpuInfo
            {
                Name = item.Text("Name"),
                DriverVersion = item.Text("DriverVersion"),
                DriverDate = item.WmiDate("DriverDate"),
                PnpId = item.Text("PNPDeviceID"),
                Status = item.Text("Status")
            }));
        return list;
    }

    private static List<DiskInfo> ReadDisks(List<string> warnings)
    {
        var list = new List<DiskInfo>();
        Wmi.Query(
            "SELECT Model, InterfaceType, MediaType, Size, PNPDeviceID, SerialNumber FROM Win32_DiskDrive",
            "磁盘",
            warnings,
            item => list.Add(new DiskInfo
            {
                Model = item.Text("Model"),
                InterfaceType = item.Text("InterfaceType"),
                MediaType = item.Text("MediaType"),
                SizeBytes = item.Long("Size"),
                PnpId = item.Text("PNPDeviceID"),
                Serial = item.Text("SerialNumber")
            }));

        var mediaBySerial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Wmi.Query(
            @"\\.\root\Microsoft\Windows\Storage",
            "SELECT SerialNumber, MediaType FROM MSFT_PhysicalDisk",
            "磁盘介质类型",
            warnings,
            item =>
            {
                var serial = item.Text("SerialNumber");
                var media = item.Int("MediaType") switch
                {
                    3 => "HDD",
                    4 => "SSD",
                    5 => "SCM",
                    _ => null
                };
                if (!string.IsNullOrEmpty(serial) && media is not null)
                {
                    mediaBySerial[serial] = media;
                }
            });

        foreach (var disk in list)
        {
            if (!string.IsNullOrEmpty(disk.Serial) && mediaBySerial.TryGetValue(disk.Serial, out var media))
            {
                disk.MediaType = media;
            }
        }

        return list;
    }

    private static List<NetworkAdapterInfo> ReadNetwork(List<string> warnings)
    {
        var list = new List<NetworkAdapterInfo>();
        Wmi.Query(
            "SELECT Name, MACAddress, PNPDeviceID, NetConnectionID, NetEnabled, Speed FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE",
            "网络适配器",
            warnings,
            item =>
            {
                var pnpId = item.Text("PNPDeviceID");
                if (string.IsNullOrEmpty(pnpId))
                {
                    return;
                }

                list.Add(new NetworkAdapterInfo
                {
                    Name = item.Text("Name"),
                    Mac = item.Text("MACAddress"),
                    PnpId = pnpId,
                    ConnectionName = item.Text("NetConnectionID"),
                    Enabled = item.Bool("NetEnabled"),
                    SpeedBps = item.Long("Speed")
                });
            });
        return list;
    }

    private static List<SoundDeviceInfo> ReadSound(List<string> warnings)
    {
        var list = new List<SoundDeviceInfo>();
        Wmi.Query("SELECT Name, Manufacturer, PNPDeviceID, Status FROM Win32_SoundDevice", "声音设备", warnings, item =>
            list.Add(new SoundDeviceInfo
            {
                Name = item.Text("Name"),
                Manufacturer = item.Text("Manufacturer"),
                PnpId = item.Text("PNPDeviceID"),
                Status = item.Text("Status")
            }));
        return list;
    }

    private static Dictionary<string, DriverInfo> ReadDrivers(List<string> warnings)
    {
        var drivers = new Dictionary<string, DriverInfo>(StringComparer.OrdinalIgnoreCase);
        Wmi.Query(
            "SELECT DeviceID, DriverVersion, DriverDate, DriverProviderName, InfName, IsSigned FROM Win32_PnPSignedDriver",
            "驱动信息",
            warnings,
            item =>
            {
                var deviceId = item.Text("DeviceID");
                if (string.IsNullOrEmpty(deviceId))
                {
                    return;
                }

                drivers[deviceId] = new DriverInfo
                {
                    Version = item.Text("DriverVersion"),
                    Date = item.WmiDate("DriverDate"),
                    Provider = item.Text("DriverProviderName"),
                    Inf = item.Text("InfName"),
                    Signed = item.Bool("IsSigned")
                };
            });
        return drivers;
    }

    private static List<DeviceInfo> ReadDevices(List<string> warnings, Dictionary<string, DriverInfo> drivers)
    {
        var list = new List<DeviceInfo>();
        Wmi.Query(
            "SELECT Name, PNPClass, DeviceID, ConfigManagerErrorCode, Status, Manufacturer FROM Win32_PnPEntity",
            "设备列表",
            warnings,
            item =>
            {
                var deviceId = item.Text("DeviceID");
                if (string.IsNullOrEmpty(deviceId))
                {
                    return;
                }

                drivers.TryGetValue(deviceId, out var driver);
                list.Add(new DeviceInfo
                {
                    ClassName = item.Text("PNPClass") ?? "",
                    Name = item.Text("Name") ?? "(未知设备)",
                    Manufacturer = item.Text("Manufacturer"),
                    PnpId = deviceId,
                    Status = item.Text("Status"),
                    ProblemCode = item.Int("ConfigManagerErrorCode") ?? 0,
                    Driver = driver
                });
            });

        list.Sort((left, right) =>
        {
            var byClass = string.Compare(left.ClassName, right.ClassName, StringComparison.OrdinalIgnoreCase);
            return byClass != 0
                ? byClass
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        return list;
    }
}
