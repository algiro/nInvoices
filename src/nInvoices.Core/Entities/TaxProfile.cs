namespace nInvoices.Core.Entities;

/// <summary>
/// Where the user pays taxes and how, for the tax prediction: the country, the regime chosen among
/// those its module offers, and the regime's settings. At most one per user.
/// </summary>
public sealed class TaxProfile : OwnedEntityBase
{
    /// <summary>ISO 3166-1 alpha-2 code, upper case.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>One of the country module's regime ids.</summary>
    public string RegimeId { get; private set; } = string.Empty;

    /// <summary>The regime's settings, by field key.</summary>
    public Dictionary<string, string> Values { get; private set; } = new();

    private TaxProfile() { }

    /// <exception cref="ArgumentException">The country is not a two-letter code, or the regime is empty.</exception>
    public TaxProfile(string countryCode, string regimeId, IReadOnlyDictionary<string, string>? values)
    {
        Update(countryCode, regimeId, values);
    }

    /// <exception cref="ArgumentException">The country is not a two-letter code, or the regime is empty.</exception>
    public void Update(string countryCode, string regimeId, IReadOnlyDictionary<string, string>? values)
    {
        if (string.IsNullOrWhiteSpace(regimeId))
            throw new ArgumentException("Choose a tax regime", nameof(regimeId));

        CountryCode = ComplianceSettings.NormalizeCountryCode(countryCode);
        RegimeId = regimeId.Trim();
        Values = (values ?? new Dictionary<string, string>())
            .Where(v => !string.IsNullOrWhiteSpace(v.Value))
            .ToDictionary(v => v.Key, v => v.Value.Trim());
    }
}
