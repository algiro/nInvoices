using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Countries;

/// <summary>
/// The country the current user works from: only its invoicing rules and tax regimes apply. Until the user
/// chooses one it is inferred from what they already use, so existing data keeps working.
/// </summary>
public interface IUserCountry
{
    /// <returns>
    /// The chosen country; otherwise the country of the invoicing rules they turned on (their tax profile's if
    /// several), or of their tax profile; null when there is nothing to go by.
    /// </returns>
    Task<string?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the country was chosen, rather than inferred.</summary>
    Task<bool> IsChosenAsync(CancellationToken cancellationToken = default);
}

public sealed class UserCountryService : IUserCountry
{
    private readonly IRepository<UserCountry> _countries;
    private readonly IRepository<ComplianceSettings> _compliance;
    private readonly IRepository<TaxProfile> _profiles;

    public UserCountryService(IRepository<UserCountry> countries, IRepository<ComplianceSettings> compliance, IRepository<TaxProfile> profiles)
    {
        _countries = countries;
        _compliance = compliance;
        _profiles = profiles;
    }

    public async Task<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        // The query filters return only the current user's rows
        var chosen = (await _countries.GetAllAsync(cancellationToken)).FirstOrDefault();
        if (chosen is not null)
            return chosen.CountryCode;

        var profile = (await _profiles.GetAllAsync(cancellationToken)).FirstOrDefault();
        var enabled = (await _compliance.GetAllAsync(cancellationToken))
            .Where(s => s.IsEnabled)
            .Select(s => s.CountryCode)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (profile is not null && (enabled.Count == 0 || enabled.Contains(profile.CountryCode)))
            return profile.CountryCode;
        return enabled.FirstOrDefault();
    }

    public async Task<bool> IsChosenAsync(CancellationToken cancellationToken = default) =>
        (await _countries.GetAllAsync(cancellationToken)).Any();
}
