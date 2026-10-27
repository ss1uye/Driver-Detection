using HwOps.Core.Rules;
using Xunit;

namespace HwOps.Core.Tests;

public class RuleSetTests
{
    [Fact]
    public void LoadEmbedded_ContainsSeedRules()
    {
        var rules = RuleSet.LoadEmbedded();

        Assert.False(string.IsNullOrWhiteSpace(rules.RulesVersion));
        Assert.True(rules.Rules.Count >= 30, $"seed rules count = {rules.Rules.Count}");
        Assert.Contains(rules.Rules, r => r.Match.Type == "board");
        Assert.Contains(rules.Rules, r => r.Match.Type == "oem");
        Assert.Contains(rules.Rules, r => r.Match.Type == "pci");
        Assert.Contains(rules.Rules, r => r.Match.Type == "any");
    }

    [Fact]
    public void MergeWith_ReplacesSameId_AndUsesNewVersion()
    {
        var baseSet = RuleSet.Parse("""
        {"rulesVersion":"1","rules":[
          {"id":"a","tier":1,"match":{"type":"any"},"title":"A","urlTemplate":"https://a"},
          {"id":"b","tier":1,"match":{"type":"any"},"title":"B","urlTemplate":"https://b"}
        ]}
        """);
        var external = RuleSet.Parse("""
        {"rulesVersion":"2","rules":[
          {"id":"a","tier":1,"match":{"type":"any"},"title":"A2","urlTemplate":"https://a2"}
        ]}
        """);

        var merged = baseSet.MergeWith(external);

        Assert.Equal("2", merged.RulesVersion);
        Assert.Equal(2, merged.Rules.Count);
        Assert.Equal("A2", merged.Rules.Single(r => r.Id == "a").Title);
        Assert.Equal("https://b", merged.Rules.Single(r => r.Id == "b").UrlTemplate);
    }

    [Fact]
    public void Parse_RejectsRuleWithoutUrl()
    {
        var json = """{"rulesVersion":"1","rules":[{"id":"x","tier":1,"match":{"type":"any"},"title":"X"}]}""";

        Assert.Throws<InvalidDataException>(() => RuleSet.Parse(json));
    }
}
