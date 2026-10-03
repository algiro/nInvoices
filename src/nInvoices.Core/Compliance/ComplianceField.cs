namespace nInvoices.Core.Compliance;

public enum ComplianceFieldType
{
    Text,
    Choice,
    Boolean
}

/// <param name="Key">Stable key the value is stored under.</param>
/// <param name="Label">What the user sees.</param>
/// <param name="Type">How the value is entered.</param>
/// <param name="Required">Whether it must be filled in when the regime is enabled.</param>
/// <param name="Help">Optional explanation shown with the field.</param>
/// <param name="Options">For <see cref="ComplianceFieldType.Choice"/>: the allowed values.</param>
public sealed record ComplianceField(
    string Key,
    string Label,
    ComplianceFieldType Type = ComplianceFieldType.Text,
    bool Required = false,
    string? Help = null,
    IReadOnlyList<ComplianceFieldOption>? Options = null);

public sealed record ComplianceFieldOption(string Value, string Label);
