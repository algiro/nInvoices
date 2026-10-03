using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Compliance;

/// <summary>
/// A customer as one country rules see it: the common data plus the values that country module
/// asked for (keyed by <see cref="ComplianceField.Key"/>, without the country prefix).
/// </summary>
public sealed record CustomerProfile(
    string Name,
    string FiscalId,
    Address Address,
    IReadOnlyDictionary<string, string> Values);
