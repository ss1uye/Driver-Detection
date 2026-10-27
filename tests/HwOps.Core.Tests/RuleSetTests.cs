using HwOps.Core.Rules;
using Xunit;

namespace HwOps.Core.Tests;

public class RuleSetTests
{
    private static string SingleRuleJson(
        string id = "r",
        int tier = 1,
        string type = "any",
        string title = "T",
        string url = "https://example.com")
        => $$"""{"rules":[{"id":"{{id}}","tier":{{tier}},"match":{"type":"{{type}}"},"title":"{{title}}","urlTemplate":"{{url}}"}]}""";

    [Fact]
    public void LoadEmbedded_ContainsSeedRules()
    {
        var rules = RuleSet.LoadEmbedded();

        Assert.False(string.IsNullOrWhiteSpace(rules.RulesVersion));
        Assert.Equal(40, rules.Rules.Count);
        Assert.Equal(rules.Rules.Count,
            rules.Rules.Select(r => r.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(rules.Rules, r => Assert.InRange(r.Tier, 1, 5));
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

    [Fact]
    public void KeepsBaseVersion_WhenOverrideHasNoVersion()
    {
        var baseSet = RuleSet.Parse("""
        {"rulesVersion":"1","rules":[
          {"id":"a","tier":1,"match":{"type":"any"},"title":"A","urlTemplate":"https://a"}
        ]}
        """);
        var external = RuleSet.Parse("""
        {"rules":[
          {"id":"b","tier":2,"match":{"type":"any"},"title":"B","urlTemplate":"https://b"}
        ]}
        """);

        var merged = baseSet.MergeWith(external);

        Assert.Equal("1", merged.RulesVersion);
        Assert.Equal(2, merged.Rules.Count);
    }

    [Fact]
    public void RejectsDuplicateIds()
    {
        var json = """
        {"rules":[
          {"id":"a","tier":1,"match":{"type":"any"},"title":"A","urlTemplate":"https://a"},
          {"id":"A","tier":1,"match":{"type":"any"},"title":"A2","urlTemplate":"https://a2"}
        ]}
        """;

        Assert.Throws<InvalidDataException>(() => RuleSet.Parse(json));
    }

    [Fact]
    public void ReplacesIdCaseInsensitively()
    {
        var baseSet = RuleSet.Parse("""
        {"rulesVersion":"1","rules":[
          {"id":"a","tier":1,"match":{"type":"any"},"title":"A","urlTemplate":"https://a"}
        ]}
        """);
        var external = RuleSet.Parse("""
        {"rules":[
          {"id":"A","tier":3,"match":{"type":"any"},"title":"A-external","urlTemplate":"https://a-ext"}
        ]}
        """);

        var merged = baseSet.MergeWith(external);

        Assert.Single(merged.Rules);
        Assert.Equal("A-external", merged.Rules[0].Title);
    }

    [Fact]
    public void Merge_DoesNotMutateInputs()
    {
        var baseSet = RuleSet.Parse("""
        {"rulesVersion":"1","rules":[
          {"id":"a","tier":1,"match":{"type":"any"},"title":"A","urlTemplate":"https://a"},
          {"id":"b","tier":2,"match":{"type":"any"},"title":"B","urlTemplate":"https://b"}
        ]}
        """);
        var external = RuleSet.Parse("""
        {"rulesVersion":"2","rules":[
          {"id":"a","tier":2,"match":{"type":"any"},"title":"A2","urlTemplate":"https://a2"}
        ]}
        """);

        _ = baseSet.MergeWith(external);

        Assert.Equal(2, baseSet.Rules.Count);
        Assert.Equal("A", baseSet.Rules.Single(r => r.Id == "a").Title);
        Assert.Equal("B", baseSet.Rules.Single(r => r.Id == "b").Title);
        Assert.Single(external.Rules);
        Assert.Equal("A2", external.Rules[0].Title);
        Assert.Equal("1", baseSet.RulesVersion);
        Assert.Equal("2", external.RulesVersion);
    }

    [Fact]
    public void Parse_RejectsNullRule()
    {
        Assert.Throws<InvalidDataException>(() => RuleSet.Parse("""{"rules":[null]}"""));
    }

    public static TheoryData<string, string> InvalidRuleJson => new()
    {
        { "tier=0", SingleRuleJson(tier: 0) },
        { "tier=6", SingleRuleJson(tier: 6) },
        { "type=thunderbolt", SingleRuleJson(type: "thunderbolt") },
        { "url=javascript", SingleRuleJson(url: "javascript:alert(1)") },
        { "url=file", SingleRuleJson(url: "file:///C:/x") },
        { "title empty", SingleRuleJson(title: "") },
    };

    [Theory]
    [MemberData(nameof(InvalidRuleJson))]
    public void Parse_RejectsInvalidRules(string caseName, string json)
    {
        var ex = Assert.Throws<InvalidDataException>(() => RuleSet.Parse(json));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message), caseName);
    }
}
