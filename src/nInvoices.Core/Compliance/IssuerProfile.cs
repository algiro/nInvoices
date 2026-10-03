using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Compliance;

/// <summary>
/// Who issues the invoices, as the country rules see it: the common identity plus the module's
/// own settings (keyed by <see cref="ComplianceField.Key"/>).
/// </summary>
public sealed record IssuerProfile(
    string? LegalName,
    string? TaxId,
    Address? Address,
    IReadOnlyDictionary<string, string> Values);
