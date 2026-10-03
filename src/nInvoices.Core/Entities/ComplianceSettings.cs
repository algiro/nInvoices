using nInvoices.Core.Compliance;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Entities;

/// <summary>
/// A user's setup for one country's invoicing regime: whether it is turned on, who the issuer
/// is, and the country-specific values. At most one record per owner and country, created when
/// the user first saves it. Off unless the user turns it on, so users elsewhere are unaffected.
/// </summary>
public sealed class ComplianceSettings : OwnedEntityBase
{
    /// <summary>ISO 3166-1 alpha-2 code, upper case.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public string? LegalName { get; private set; }

    /// <summary>The issuer's tax id (NIF, VAT number...), as the user typed it.</summary>
    public string? TaxId { get; private set; }

    public Address? Address { get; private set; }

    /// <summary>The module's own settings, by field key.</summary>
    public Dictionary<string, string> Values { get; private set; } = new();

    private ComplianceSettings() { }

    /// <exception cref="ArgumentException">The country code is not two letters.</exception>
    public ComplianceSettings(string countryCode)
    {
        CountryCode = NormalizeCountryCode(countryCode);
    }

    public static string NormalizeCountryCode(string? countryCode)
    {
        var code = countryCode?.Trim().ToUpperInvariant();
        if (code is not { Length: 2 } || !code.All(char.IsAsciiLetter))
            throw new ArgumentException("The country must be a two-letter ISO code", nameof(countryCode));
        return code;
    }

    public void Update(bool isEnabled, string? legalName, string? taxId, Address? address, IReadOnlyDictionary<string, string>? values)
    {
        IsEnabled = isEnabled;
        LegalName = Clean(legalName);
        TaxId = Clean(taxId);
        Address = address;
        Values = (values ?? new Dictionary<string, string>())
            .Where(v => !string.IsNullOrWhiteSpace(v.Value))
            .ToDictionary(v => v.Key, v => v.Value.Trim());
    }

    public IssuerProfile ToIssuerProfile() => new(LegalName, TaxId, Address, Values);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
