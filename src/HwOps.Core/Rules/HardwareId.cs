using System.Text.RegularExpressions;

namespace HwOps.Core.Rules;

public sealed class HardwareId
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public string Raw { get; }
    public string? VendorId { get; }
    public string? DeviceId { get; }
    public string? SubSystemId { get; }
    public string? UsbVendorId { get; }
    public string? UsbProductId { get; }
    public bool IsPci => Raw.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase);
    public bool IsUsb => Raw.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase);

    private HardwareId(string raw, string? vendorId, string? deviceId, string? subSystemId, string? usbVendorId, string? usbProductId)
    {
        Raw = raw;
        VendorId = vendorId;
        DeviceId = deviceId;
        SubSystemId = subSystemId;
        UsbVendorId = usbVendorId;
        UsbProductId = usbProductId;
    }

    public static HardwareId Parse(string? raw)
    {
        var value = raw?.Trim() ?? "";
        return new HardwareId(
            value,
            Find(value, @"VEN_([0-9A-Fa-f]{4})"),
            Find(value, @"DEV_([0-9A-Fa-f]{4})"),
            Find(value, @"SUBSYS_([0-9A-Fa-f]{8})"),
            Find(value, @"VID_([0-9A-Fa-f]{4})"),
            Find(value, @"PID_([0-9A-Fa-f]{4})"));
    }

    private static string? Find(string input, string pattern)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase, RegexTimeout);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }
}
