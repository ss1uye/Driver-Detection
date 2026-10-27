using System.Text.RegularExpressions;
using HwOps.Core.Models;

namespace HwOps.Core.Rules;

public sealed class RuleMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);
    private readonly IReadOnlyList<DriverRule> _rules;

    public RuleMatcher(RuleSet ruleSet)
    {
        _rules = ruleSet.Rules
            .OrderBy(r => r.Tier)
            .ThenByDescending(Specificity)
            .ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<DriverSuggestion> SuggestForMachine(HardwareSnapshot snapshot)
    {
        var result = new List<DriverSuggestion>();
        foreach (var rule in _rules)
        {
            var type = rule.Match.Type?.ToLowerInvariant();
            if (type is not ("oem" or "board"))
            {
                continue;
            }

            if (!Matches(rule, snapshot, null))
            {
                continue;
            }

            result.Add(ToSuggestion(rule, snapshot, null, type == "oem" ? "整机厂商匹配" : "主板型号匹配"));
        }

        return result;
    }

    public IReadOnlyList<DriverSuggestion> SuggestForDevice(HardwareSnapshot snapshot, DeviceInfo device)
    {
        var result = new List<DriverSuggestion>();
        foreach (var rule in _rules)
        {
            var type = rule.Match.Type?.ToLowerInvariant();
            if (type is "oem" or "board")
            {
                continue;
            }

            if (!Matches(rule, snapshot, device))
            {
                continue;
            }

            result.Add(ToSuggestion(rule, snapshot, device, "硬件 ID 匹配"));
        }

        return result;
    }

    private static bool Matches(DriverRule rule, HardwareSnapshot snapshot, DeviceInfo? device)
    {
        var match = rule.Match;
        var type = match.Type?.ToLowerInvariant() ?? "any";
        return type switch
        {
            "oem" => MachinePatternMatch(snapshot.System.Manufacturer, snapshot.System.Model, match),
            "board" => MachinePatternMatch(snapshot.Board.Manufacturer, snapshot.Board.Product, match),
            "pci" => device is not null && MatchPci(device, match),
            "usb" => device is not null && MatchUsb(device, match),
            "class" => device is not null && Pattern(device.ClassName, match.ClassPattern),
            "any" => true,
            _ => false
        };
    }

    private static bool MachinePatternMatch(string? manufacturer, string? model, RuleMatch match)
    {
        if (string.IsNullOrWhiteSpace(match.ManufacturerPattern) && string.IsNullOrWhiteSpace(match.ProductPattern))
        {
            return false;
        }

        return Pattern(manufacturer, match.ManufacturerPattern) && Pattern(model, match.ProductPattern);
    }

    private static bool MatchPci(DeviceInfo device, RuleMatch match)
    {
        var id = HardwareId.Parse(device.PnpId);
        if (!id.IsPci)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(match.VendorId) && !SameId(id.VendorId, match.VendorId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(match.DeviceId) && !SameId(id.DeviceId, match.DeviceId))
        {
            return false;
        }

        return Pattern(device.ClassName, match.ClassPattern);
    }

    private static bool MatchUsb(DeviceInfo device, RuleMatch match)
    {
        var id = HardwareId.Parse(device.PnpId);
        if (!id.IsUsb)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(match.VendorId) && !SameId(id.UsbVendorId, match.VendorId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(match.DeviceId) && !SameId(id.UsbProductId, match.DeviceId))
        {
            return false;
        }

        return Pattern(device.ClassName, match.ClassPattern);
    }

    private static bool SameId(string? left, string right)
        => string.Equals(left?.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Pattern(string? input, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static DriverSuggestion ToSuggestion(DriverRule rule, HardwareSnapshot snapshot, DeviceInfo? device, string reason)
        => new()
        {
            RuleId = rule.Id,
            Tier = rule.Tier,
            Title = Render(rule.Title, snapshot, device, escape: false),
            Url = Render(rule.UrlTemplate, snapshot, device, escape: true),
            Note = rule.Note,
            MatchReason = reason
        };

    private static string Render(string template, HardwareSnapshot snapshot, DeviceInfo? device, bool escape)
    {
        var hardwareId = device is null ? null : HardwareId.Parse(device.PnpId);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["hardwareId"] = device?.PnpId,
            ["className"] = device?.ClassName,
            ["deviceName"] = device?.Name,
            ["boardManufacturer"] = snapshot.Board.Manufacturer,
            ["boardProduct"] = snapshot.Board.Product,
            ["oemManufacturer"] = snapshot.System.Manufacturer,
            ["oemModel"] = snapshot.System.Model,
            ["serial"] = snapshot.System.Serial,
            ["vendorId"] = hardwareId?.VendorId ?? hardwareId?.UsbVendorId,
            ["deviceId"] = hardwareId?.DeviceId ?? hardwareId?.UsbProductId
        };

        var result = template;
        foreach (var (key, value) in values)
        {
            var replacement = value ?? "";
            if (escape)
            {
                replacement = Uri.EscapeDataString(replacement);
            }

            result = result.Replace("{" + key + "}", replacement, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static int Specificity(DriverRule rule)
        => (string.IsNullOrEmpty(rule.Match.DeviceId) ? 0 : 4)
           + (string.IsNullOrEmpty(rule.Match.ProductPattern) ? 0 : 2)
           + (string.IsNullOrEmpty(rule.Match.ClassPattern) ? 0 : 1);
}
