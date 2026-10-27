namespace HwOps.Core.Rules;

public sealed class DriverRule
{
    public string Id { get; set; } = "";
    public int Tier { get; set; }
    public RuleMatch Match { get; set; } = new();
    public string Title { get; set; } = "";
    public string UrlTemplate { get; set; } = "";
    public string? Note { get; set; }
}

public sealed class RuleMatch
{
    public string Type { get; set; } = "any";
    public string? VendorId { get; set; }
    public string? DeviceId { get; set; }
    public string? ManufacturerPattern { get; set; }
    public string? ProductPattern { get; set; }
    public string? ClassPattern { get; set; }
}

public sealed class DriverSuggestion
{
    public string RuleId { get; set; } = "";
    public int Tier { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Note { get; set; }
    public string MatchReason { get; set; } = "";
}
