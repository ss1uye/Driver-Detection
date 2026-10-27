using HwOps.Core.Models;

namespace HwOps.Core.Collection;

public sealed class WmiHardwareCollector : IHardwareCollector
{
    private static readonly TimeSpan CollectBudget = TimeSpan.FromSeconds(10);

    public HardwareSnapshot Collect(CancellationToken cancellationToken = default)
    {
        // 整体时间预算：调用方取消与预算超时共用一条取消链路。
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetCts.CancelAfter(CollectBudget);
        var budgetToken = budgetCts.Token;

        // 互不依赖的采集类别并行执行；drivers→devices 有依赖，串行在同一任务内。
        // 每个类别使用独立的 warnings 列表，任务间不共享可变集合。
        var systemTask = Task.Run(() => RunCategory("系统信息", static () => new MachineSystemInfo(), budgetToken, cancellationToken, ReadSystem));
        var boardTask = Task.Run(() => RunCategory("主板信息", static () => new BoardInfo(), budgetToken, cancellationToken, ReadBoard));
        var biosTask = Task.Run(() => RunCategory("BIOS 信息", static () => new BiosInfo(), budgetToken, cancellationToken, ReadBios));
        var cpuTask = Task.Run(() => RunCategory("CPU", static () => new List<CpuInfo>(), budgetToken, cancellationToken, ReadCpu));
        var memoryTask = Task.Run(() => RunCategory("内存", static () => new List<MemoryModuleInfo>(), budgetToken, cancellationToken, ReadMemory));
        var gpusTask = Task.Run(() => RunCategory("显卡", static () => new List<GpuInfo>(), budgetToken, cancellationToken, ReadGpus));
        var disksTask = Task.Run(() => RunCategory("磁盘", static () => new List<DiskInfo>(), budgetToken, cancellationToken, ReadDisks));
        var networkTask = Task.Run(() => RunCategory("网络适配器", static () => new List<NetworkAdapterInfo>(), budgetToken, cancellationToken, ReadNetwork));
        var soundTask = Task.Run(() => RunCategory("声音设备", static () => new List<SoundDeviceInfo>(), budgetToken, cancellationToken, ReadSound));
        var devicesTask = Task.Run(() => RunCategory(
            "设备与驱动",
            static () => (Drivers: new Dictionary<string, DriverInfo>(StringComparer.OrdinalIgnoreCase), Devices: new List<DeviceInfo>()),
            budgetToken,
            cancellationToken,
            ReadDevicesWithDrivers));
        var machineIdTask = Task.Run(() => RunCategory("机器标识", static () => "", budgetToken, cancellationToken, ReadMachineId));

        // 调用方取消时由类别内部向外抛 OperationCanceledException；此处等待全部任务结束。
        Task.WhenAll(
                systemTask, boardTask, biosTask, cpuTask, memoryTask, gpusTask,
                disksTask, networkTask, soundTask, devicesTask, machineIdTask)
            .GetAwaiter().GetResult();

        var (system, systemWarnings) = systemTask.Result;
        var (board, boardWarnings) = boardTask.Result;
        var (bios, biosWarnings) = biosTask.Result;
        var (cpu, cpuWarnings) = cpuTask.Result;
        var (memory, memoryWarnings) = memoryTask.Result;
        var (gpus, gpusWarnings) = gpusTask.Result;
        var (disks, disksWarnings) = disksTask.Result;
        var (network, networkWarnings) = networkTask.Result;
        var (sound, soundWarnings) = soundTask.Result;
        var (driversAndDevices, devicesWarnings) = devicesTask.Result;
        var (machineId, machineIdWarnings) = machineIdTask.Result;
        var devices = driversAndDevices.Devices;

        // 按固定顺序合并告警，保证输出顺序可预期。
        var warnings = new List<string>();
        warnings.AddRange(systemWarnings);
        warnings.AddRange(boardWarnings);
        warnings.AddRange(biosWarnings);
        warnings.AddRange(cpuWarnings);
        warnings.AddRange(memoryWarnings);
        warnings.AddRange(gpusWarnings);
        warnings.AddRange(disksWarnings);
        warnings.AddRange(networkWarnings);
        warnings.AddRange(soundWarnings);
        warnings.AddRange(devicesWarnings);
        warnings.AddRange(machineIdWarnings);

        return new HardwareSnapshot
        {
            MachineId = machineId,
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
            ProblemDevices = BuildProblemDevices(devices),
            Collection = new CollectionInfo { Warnings = warnings }
        };
    }

    private static (T Value, List<string> Warnings) RunCategory<T>(
        string label,
        Func<T> empty,
        CancellationToken budgetToken,
        CancellationToken callerToken,
        Func<CancellationToken, List<string>, T> read)
    {
        var categoryWarnings = new List<string>();
        try
        {
            return (read(budgetToken, categoryWarnings), categoryWarnings);
        }
        catch (OperationCanceledException)
        {
            if (callerToken.IsCancellationRequested)
            {
                throw; // 调用方主动取消：向外抛
            }

            categoryWarnings.Add($"{label}: 超过采集时间预算，已跳过");
            return (empty(), categoryWarnings);
        }
    }

    private static string ReadMachineId(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        return MachineIdentity.Get(warnings);
    }

    private static (Dictionary<string, DriverInfo> Drivers, List<DeviceInfo> Devices) ReadDevicesWithDrivers(
        CancellationToken token,
        List<string> warnings)
    {
        var drivers = ReadDrivers(token, warnings);
        var devices = ReadDevices(token, warnings, drivers);
        return (drivers, devices);
    }

    internal static List<ProblemDevice> BuildProblemDevices(IReadOnlyList<DeviceInfo> devices)
    {
        return devices
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
    }

    internal static string? ScrubSerial(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed!.Equals("System Serial Number", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("to be filled by", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Type1 - Default string", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Default string", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    private static MachineSystemInfo ReadSystem(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        var info = new MachineSystemInfo { Hostname = Environment.MachineName };

        Wmi.Query("SELECT Manufacturer, Model FROM Win32_ComputerSystem", "系统信息", warnings, item =>
        {
            token.ThrowIfCancellationRequested();
            info.Manufacturer = item.Text("Manufacturer");
            info.Model = item.Text("Model");
        });

        token.ThrowIfCancellationRequested();
        Wmi.Query("SELECT UUID, IdentifyingNumber FROM Win32_ComputerSystemProduct", "整机标识", warnings, item =>
        {
            token.ThrowIfCancellationRequested();
            info.SmbiosUuid = item.Text("UUID");
            info.Serial = ScrubSerial(item.Text("IdentifyingNumber"));
        });

        token.ThrowIfCancellationRequested();
        Wmi.Query("SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem", "操作系统", warnings, item =>
        {
            token.ThrowIfCancellationRequested();
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

    private static BoardInfo ReadBoard(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        var info = new BoardInfo();
        Wmi.Query("SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard", "主板信息", warnings, item =>
        {
            token.ThrowIfCancellationRequested();
            info.Manufacturer = item.Text("Manufacturer");
            info.Product = item.Text("Product");
            info.Version = item.Text("Version");
            info.Serial = ScrubSerial(item.Text("SerialNumber"));
        });
        return info;
    }

    private static BiosInfo ReadBios(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        var info = new BiosInfo();
        Wmi.Query("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber FROM Win32_BIOS", "BIOS 信息", warnings, item =>
        {
            token.ThrowIfCancellationRequested();
            info.Vendor = item.Text("Manufacturer");
            info.Version = item.Text("SMBIOSBIOSVersion");
            info.ReleaseDate = item.WmiDate("ReleaseDate");
            info.Serial = ScrubSerial(item.Text("SerialNumber"));
        });
        return info;
    }

    private static List<CpuInfo> ReadCpu(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        var list = new List<CpuInfo>();
        Wmi.Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor", "CPU", warnings, item =>
        {
            token.ThrowIfCancellationRequested();
            list.Add(new CpuInfo
            {
                Name = item.Text("Name"),
                Cores = item.Int("NumberOfCores"),
                Threads = item.Int("NumberOfLogicalProcessors"),
                MaxClockMhz = item.Int("MaxClockSpeed")
            });
        });
        return list;
    }

    private static List<MemoryModuleInfo> ReadMemory(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        var list = new List<MemoryModuleInfo>();
        Wmi.Query(
            "SELECT Manufacturer, PartNumber, Capacity, Speed, ConfiguredClockSpeed, DeviceLocator, FormFactor FROM Win32_PhysicalMemory",
            "内存",
            warnings,
            item =>
            {
                token.ThrowIfCancellationRequested();
                list.Add(new MemoryModuleInfo
                {
                    Manufacturer = item.Text("Manufacturer"),
                    PartNumber = item.Text("PartNumber"),
                    CapacityBytes = item.Long("Capacity"),
                    SpeedMhz = item.Int("ConfiguredClockSpeed") ?? item.Int("Speed"),
                    Slot = item.Text("DeviceLocator"),
                    FormFactor = item.Text("FormFactor")
                });
            });
        return list;
    }

    private static List<GpuInfo> ReadGpus(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        var list = new List<GpuInfo>();
        Wmi.Query("SELECT Name, DriverVersion, DriverDate, PNPDeviceID, Status FROM Win32_VideoController", "显卡", warnings, item =>
        {
            token.ThrowIfCancellationRequested();
            var pnpId = item.Text("PNPDeviceID");
            if (DeviceIdPolicy.IsVirtualPnpId(pnpId))
            {
                return; // 排除 ROOT\DISPLAY 等虚拟显示器，避免驱动建议误导运维
            }

            list.Add(new GpuInfo
            {
                Name = item.Text("Name"),
                DriverVersion = item.Text("DriverVersion"),
                DriverDate = item.WmiDate("DriverDate"),
                PnpId = pnpId,
                Status = item.Text("Status")
            });
        });
        return list;
    }

    private static List<DiskInfo> ReadDisks(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        var list = new List<DiskInfo>();
        Wmi.Query(
            "SELECT Model, InterfaceType, MediaType, Size, PNPDeviceID, SerialNumber FROM Win32_DiskDrive",
            "磁盘",
            warnings,
            item =>
            {
                token.ThrowIfCancellationRequested();
                list.Add(new DiskInfo
                {
                    Model = item.Text("Model"),
                    InterfaceType = item.Text("InterfaceType"),
                    MediaType = item.Text("MediaType"),
                    SizeBytes = item.Long("Size"),
                    PnpId = item.Text("PNPDeviceID"),
                    Serial = ScrubSerial(item.Text("SerialNumber"))
                });
            });

        var mediaBySerial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        token.ThrowIfCancellationRequested();
        Wmi.Query(
            @"\\.\root\Microsoft\Windows\Storage",
            "SELECT SerialNumber, MediaType FROM MSFT_PhysicalDisk",
            "磁盘介质类型",
            warnings,
            item =>
            {
                token.ThrowIfCancellationRequested();
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
            token.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(disk.Serial) && mediaBySerial.TryGetValue(disk.Serial, out var media))
            {
                disk.MediaType = media;
            }
        }

        return list;
    }

    private static List<NetworkAdapterInfo> ReadNetwork(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        var list = new List<NetworkAdapterInfo>();
        Wmi.Query(
            "SELECT Name, MACAddress, PNPDeviceID, NetConnectionID, NetEnabled, Speed FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE",
            "网络适配器",
            warnings,
            item =>
            {
                token.ThrowIfCancellationRequested();
                var pnpId = item.Text("PNPDeviceID");
                if (string.IsNullOrEmpty(pnpId))
                {
                    return;
                }

                if (DeviceIdPolicy.IsVirtualPnpId(pnpId))
                {
                    return; // 排除 ROOT\NET、BTH 虚拟/蓝牙枚举网卡
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

    private static List<SoundDeviceInfo> ReadSound(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        var list = new List<SoundDeviceInfo>();
        Wmi.Query("SELECT Name, Manufacturer, PNPDeviceID, Status FROM Win32_SoundDevice", "声音设备", warnings, item =>
        {
            token.ThrowIfCancellationRequested();
            list.Add(new SoundDeviceInfo
            {
                Name = item.Text("Name"),
                Manufacturer = item.Text("Manufacturer"),
                PnpId = item.Text("PNPDeviceID"),
                Status = item.Text("Status")
            });
        });
        return list;
    }

    private static Dictionary<string, DriverInfo> ReadDrivers(CancellationToken token, List<string> warnings)
    {
        token.ThrowIfCancellationRequested();
        var drivers = new Dictionary<string, DriverInfo>(StringComparer.OrdinalIgnoreCase);
        Wmi.Query(
            "SELECT DeviceID, DriverVersion, DriverDate, DriverProviderName, InfName, IsSigned FROM Win32_PnPSignedDriver",
            "驱动信息",
            warnings,
            item =>
            {
                token.ThrowIfCancellationRequested();
                var deviceId = item.Text("DeviceID");
                if (string.IsNullOrEmpty(deviceId))
                {
                    return;
                }

                var info = new DriverInfo
                {
                    Version = item.Text("DriverVersion"),
                    Date = item.WmiDate("DriverDate"),
                    Provider = item.Text("DriverProviderName"),
                    Inf = item.Text("InfName"),
                    Signed = item.Bool("IsSigned")
                };

                // 全空 DriverInfo 不写入字典，避免报告出现空驱动单元格。
                if (string.IsNullOrWhiteSpace(info.Version)
                    && string.IsNullOrWhiteSpace(info.Date)
                    && string.IsNullOrWhiteSpace(info.Provider)
                    && string.IsNullOrWhiteSpace(info.Inf)
                    && info.Signed is null)
                {
                    return;
                }

                drivers[deviceId] = info;
            });
        return drivers;
    }

    private static List<DeviceInfo> ReadDevices(CancellationToken token, List<string> warnings, Dictionary<string, DriverInfo> drivers)
    {
        token.ThrowIfCancellationRequested();
        var list = new List<DeviceInfo>();
        Wmi.Query(
            "SELECT Name, PNPClass, DeviceID, ConfigManagerErrorCode, Status, Manufacturer FROM Win32_PnPEntity",
            "设备列表",
            warnings,
            item =>
            {
                token.ThrowIfCancellationRequested();
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
            if (byClass != 0)
            {
                return byClass;
            }

            var byName = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return byName != 0
                ? byName
                : string.Compare(left.PnpId, right.PnpId, StringComparison.OrdinalIgnoreCase);
        });

        return list;
    }
}
