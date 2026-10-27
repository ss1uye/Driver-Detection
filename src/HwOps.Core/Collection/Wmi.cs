using System.Globalization;
using System.Management;

namespace HwOps.Core.Collection;

internal static class Wmi
{
    public static void Query(string wql, string label, List<string> warnings, Action<ManagementObject> map)
        => Query(null, wql, label, warnings, map);

    public static void Query(string? scopePath, string wql, string label, List<string> warnings, Action<ManagementObject> map)
    {
        try
        {
            ManagementScope? scope = null;
            if (scopePath is not null)
            {
                scope = new ManagementScope(scopePath);
                scope.Connect();
            }

            using var searcher = scope is null
                ? new ManagementObjectSearcher(new ObjectQuery(wql))
                : new ManagementObjectSearcher(scope, new ObjectQuery(wql));
            searcher.Options.Timeout = TimeSpan.FromSeconds(5);

            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    map(item);
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"{label}: {ex.Message}");
        }
        // System.Management 8.0.0 的 ManagementScope 未实现 IDisposable（仅有 Connect/IsConnected），
        // 其底层 WMI 绑定由类型自身 finalizer 释放，无需也不能显式 Dispose。
    }

    public static string? Text(this ManagementObject item, string property)
    {
        try
        {
            return item[property]?.ToString()?.Trim();
        }
        catch
        {
            return null;
        }
    }

    public static int? Int(this ManagementObject item, string property)
    {
        try
        {
            var value = item[property];
            return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public static long? Long(this ManagementObject item, string property)
    {
        try
        {
            var value = item[property];
            return value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public static bool? Bool(this ManagementObject item, string property)
    {
        try
        {
            var value = item[property];
            return value is null ? null : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public static string? WmiDate(this ManagementObject item, string property)
    {
        var raw = item.Text(property);
        if (string.IsNullOrEmpty(raw) || raw.Length < 8)
        {
            return raw;
        }

        return DateTime.TryParseExact(
            raw[..8],
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : raw;
    }
}
