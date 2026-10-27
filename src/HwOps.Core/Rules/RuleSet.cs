using System.Reflection;
using System.Text.Json;
using HwOps.Core.Models;

namespace HwOps.Core.Rules;

public sealed class RuleSet
{
    public string RulesVersion { get; set; } = "0";
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

        foreach (var rule in set.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                throw new InvalidDataException("规则缺少 id");
            }

            if (string.IsNullOrWhiteSpace(rule.UrlTemplate))
            {
                throw new InvalidDataException($"规则 {rule.Id} 缺少 urlTemplate");
            }
        }

        return set;
    }

    public RuleSet MergeWith(RuleSet overrideSet)
    {
        var merged = Rules.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
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
