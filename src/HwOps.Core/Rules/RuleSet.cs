using System.Reflection;
using System.Text.Json;
using HwOps.Core.Models;

namespace HwOps.Core.Rules;

public sealed class RuleSet
{
    public string RulesVersion { get; set; } = "";
    public List<DriverRule> Rules { get; set; } = new();

    public static RuleSet LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("HwOps.Core.Rules.seed-rules.json")
            ?? throw new InvalidOperationException("内置规则资源缺失：HwOps.Core.Rules.seed-rules.json");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    public static RuleSet LoadFile(string path) => Parse(File.ReadAllText(path));

    public static RuleSet Parse(string json)
    {
        var set = JsonSerializer.Deserialize<RuleSet>(json, JsonDefaults.Options)
            ?? throw new InvalidDataException("规则 JSON 为空");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in set.Rules)
        {
            if (rule is null)
            {
                throw new InvalidDataException("规则列表包含空项");
            }

            rule.Id = rule.Id.Trim();
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                throw new InvalidDataException("规则缺少 id");
            }

            if (!seen.Add(rule.Id))
            {
                throw new InvalidDataException($"规则 id 重复: {rule.Id}");
            }

            if (string.IsNullOrWhiteSpace(rule.Title))
            {
                throw new InvalidDataException($"规则 {rule.Id} 缺少 title");
            }

            if (string.IsNullOrWhiteSpace(rule.UrlTemplate))
            {
                throw new InvalidDataException($"规则 {rule.Id} 缺少 urlTemplate");
            }

            if (!rule.UrlTemplate.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !rule.UrlTemplate.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"规则 {rule.Id} 的 urlTemplate 必须使用 http/https");
            }

            if (rule.Tier is < 1 or > 5)
            {
                throw new InvalidDataException($"规则 {rule.Id} 的 tier 必须在 1–5 之间");
            }

            if (rule.Match is null)
            {
                throw new InvalidDataException($"规则 {rule.Id} 缺少 match");
            }

            var type = rule.Match.Type?.Trim().ToLowerInvariant();
            if (type is not ("oem" or "board" or "pci" or "usb" or "class" or "any"))
            {
                throw new InvalidDataException($"规则 {rule.Id} 的 match.type 无效: {rule.Match.Type}");
            }

            rule.Match.Type = type;
        }

        return set;
    }

    public RuleSet MergeWith(RuleSet overrideSet)
    {
        var merged = new Dictionary<string, DriverRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in Rules)
        {
            merged[rule.Id] = rule;
        }

        foreach (var rule in overrideSet.Rules)
        {
            merged[rule.Id] = rule;
        }

        return new RuleSet
        {
            RulesVersion = string.IsNullOrWhiteSpace(overrideSet.RulesVersion) ? RulesVersion : overrideSet.RulesVersion,
            Rules = merged.Values
                .OrderBy(r => r.Tier)
                .ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}
