namespace HwOps.Core.Models;

// 设备枚举 ID 的归类策略：ROOT（根枚举/虚拟显示器）、BTH（蓝牙枚举）、USBSTOR（USB 存储）
// 均非真实硬件，不应生成驱动建议。放在 Models 层，供 Collection 与 Rules 共同使用。
public static class DeviceIdPolicy
{
    public static bool IsVirtualPnpId(string? pnpId)
        => string.IsNullOrEmpty(pnpId) // null 与空串均视为不可用的枚举 ID
           || pnpId.StartsWith("ROOT\\", StringComparison.OrdinalIgnoreCase)
           || pnpId.StartsWith("BTH\\", StringComparison.OrdinalIgnoreCase)
           || pnpId.StartsWith("USBSTOR\\", StringComparison.OrdinalIgnoreCase);
}
