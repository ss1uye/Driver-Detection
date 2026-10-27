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
                scope.Options.Timeout = TimeSpan.FromSeconds(10);
                scope.Connect();
            }

            using var searcher = scope is null
                ? new ManagementObjectSearcher(new ObjectQuery(wql))
                : new ManagementObjectSearcher(scope, new ObjectQuery(wql));
            searcher.Options.Timeout = TimeSpan.FromSeconds(5);

            using var results = searcher.Get();
            var rowIndex = 0;
            foreach (ManagementObject item in results)
            {
                rowIndex++;
                using (item)
                {
                    try
                    {
                        map(item);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        warnings.Add($"{label}[第 {rowIndex} 行]: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"{label}: {ex.Message}");
        }
        // ManagementScope 未实现 IDisposable（只有 Connect/IsConnected），
        // 其内部 WMI 会话由 RCW/GC 回收，因此不能也不需要显式 Dispose。
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
