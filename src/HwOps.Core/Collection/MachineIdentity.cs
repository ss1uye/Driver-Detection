using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace HwOps.Core.Collection;

public static class MachineIdentity
{
    public static string Get(List<string> warnings)
    {
        var uuid = ReadUuid(warnings);
        if (!string.IsNullOrWhiteSpace(uuid) && !IsPlaceholder(uuid))
        {
            return Hash(uuid!);
        }

        var machineGuid = ReadMachineGuid();
        if (!string.IsNullOrWhiteSpace(machineGuid))
        {
            return Hash(machineGuid!);
        }

        return Hash(Environment.MachineName + "|" + ReadFirstMac());
    }

    private static string? ReadUuid(List<string> warnings)
    {
        string? uuid = null;
        Wmi.Query("SELECT UUID FROM Win32_ComputerSystemProduct", "机器 UUID", warnings, item => uuid = item.Text("UUID"));
        return uuid;
    }

    private static bool IsPlaceholder(string uuid)
    {
        var normalized = uuid.Replace("-", "").Trim();
        return normalized.Length == 0
               || normalized.All(c => c is '0' or 'F' or 'f')
               || uuid.Contains("Default string", StringComparison.OrdinalIgnoreCase)
               || uuid.Contains("To be filled", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadMachineGuid()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid")?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string ReadFirstMac()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(n => n.GetPhysicalAddress().ToString())
                .FirstOrDefault(mac => mac.Length == 12) ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant()));
        return Convert.ToHexString(bytes.AsSpan(0, 16)).ToLowerInvariant();
    }
}
