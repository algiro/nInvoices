namespace nInvoices.Core.Entities;

/// <summary>
/// The country the user works from as a freelancer: the one whose invoicing rules and tax regimes apply to
/// them. One per user; the other countries' settings are kept, turned off.
/// </summary>
public sealed class UserCountry : OwnedEntityBase
{
    /// <summary>ISO 3166-1 alpha-2 code, upper case.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    private UserCountry() { }

    /// <exception cref="ArgumentException">The country is not a two-letter code.</exception>
    public UserCountry(string countryCode)
    {
        Change(countryCode);
    }

    /// <exception cref="ArgumentException">The country is not a two-letter code.</exception>
    public void Change(string countryCode)
    {
        CountryCode = ComplianceSettings.NormalizeCountryCode(countryCode);
    }
}
