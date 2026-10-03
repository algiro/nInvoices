namespace nInvoices.Core.Configuration;

/// <summary>
/// Which country regimes this installation offers. Loaded from the "Compliance" section, e.g.
/// <c>"Compliance": { "Countries": { "ES": { "Enabled": true } } }</c>. A country that is not
/// listed is offered; set <c>Enabled</c> to false to switch it off for everyone. Even when
/// offered, nothing applies to a user until they turn it on themselves.
/// </summary>
public sealed class ComplianceOptions
{
    public const string SectionName = "Compliance";

    public Dictionary<string, CountryComplianceOptions> Countries { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsOffered(string countryCode) =>
        !Countries.TryGetValue(countryCode, out var country) || country.Enabled;
}

public sealed class CountryComplianceOptions
{
    public bool Enabled { get; init; } = true;
}
