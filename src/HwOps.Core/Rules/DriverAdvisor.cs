using HwOps.Core.Collection;
using HwOps.Core.Models;

namespace HwOps.Core.Rules;

public sealed class MachineAdvice
{
    public List<DriverSuggestion> Platform { get; set; } = new();
    public List<DeviceAdvice> GpuDevices { get; set; } = new();
    public List<DeviceAdvice> ProblemDevices { get; set; } = new();
}

public sealed class DeviceAdvice
{
    public string DeviceName { get; set; } = "";
    public string PnpId { get; set; } = "";
    public int ProblemCode { get; set; }
    public List<DriverSuggestion> Suggestions { get; set; } = new();
}

public sealed class DriverAdvisor
{
    private readonly RuleMatcher _matcher;

    public DriverAdvisor(RuleSet rules)
    {
        _matcher = new RuleMatcher(rules);
    }

    public MachineAdvice Advise(HardwareSnapshot snapshot)
    {
        var platform = _matcher.SuggestForMachine(snapshot).ToList();

        var gpuDevices = snapshot.Devices
            .Where(d => string.Equals(d.ClassName, "Display", StringComparison.OrdinalIgnoreCase))
            .Where(d => !WmiHardwareCollector.IsVirtualPnpId(d.PnpId))
            .Select(d => ToAdvice(snapshot, d))
            .ToList();

        var problemDevices = snapshot.ProblemDevices
            .Select(problem =>
            {
                var device = snapshot.Devices.FirstOrDefault(d =>
                                 string.Equals(d.PnpId, problem.PnpId, StringComparison.OrdinalIgnoreCase))
                             ?? new DeviceInfo
                             {
                                 Name = problem.Name,
                                 ClassName = problem.ClassName,
                                 PnpId = problem.PnpId,
                                 ProblemCode = problem.ProblemCode
                             };
                var advice = ToAdvice(snapshot, device);
                advice.ProblemCode = problem.ProblemCode;
                return advice;
            })
            .ToList();

        return new MachineAdvice
        {
            Platform = platform,
            GpuDevices = gpuDevices,
            ProblemDevices = problemDevices
        };
    }

    private DeviceAdvice ToAdvice(HardwareSnapshot snapshot, DeviceInfo device)
        => new()
        {
            DeviceName = device.Name,
            PnpId = device.PnpId,
            ProblemCode = device.ProblemCode,
            Suggestions = _matcher.SuggestForDevice(snapshot, device).Take(3).ToList()
        };
}
