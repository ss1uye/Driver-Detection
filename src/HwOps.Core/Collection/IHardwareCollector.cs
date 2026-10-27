using HwOps.Core.Models;

namespace HwOps.Core.Collection;

public interface IHardwareCollector
{
    HardwareSnapshot Collect(CancellationToken cancellationToken = default);
}
