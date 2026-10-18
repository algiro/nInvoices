using nInvoices.Application.Countries;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Compliance;

/// <summary>
/// Whether a country's rules apply to the current user: the installation offers the country, it is the
/// country the user works from (<see cref="IUserCountry"/>) AND the user turned its rules on. Every country-specific step (extra fields, XML, signing...) must ask
/// the gate first, so with nothing enabled the app behaves exactly as without compliance support.
/// </summary>
public interface IComplianceGate
{
    Task<bool> IsEnabledAsync(string countryCode, CancellationToken cancellationToken = default);

    /// <summary>The modules that apply to the current user, with the user's settings.</summary>
    Task<IReadOnlyList<ActiveCompliance>> GetActiveAsync(CancellationToken cancellationToken = default);
}

public sealed record ActiveCompliance(ICountryComplianceModule Module, ComplianceSettings Settings);

public sealed class ComplianceGate : IComplianceGate
{
    private readonly IComplianceRegistry _registry;
    private readonly IRepository<ComplianceSettings> _settings;
    private readonly IUserCountry? _country;

    /// <param name="country">The user's country; without it (tests of one country) every country turned on applies.</param>
    public ComplianceGate(IComplianceRegistry registry, IRepository<ComplianceSettings> settings, IUserCountry? country = null)
    {
        _registry = registry;
        _settings = settings;
        _country = country;
    }

    public async Task<bool> IsEnabledAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        var active = await GetActiveAsync(cancellationToken);
        return active.Any(a => string.Equals(a.Module.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<ActiveCompliance>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (_registry.Offered.Count == 0)
            return [];

        // The query filter returns only the current user's rows
        var settings = await _settings.GetAllAsync(cancellationToken);
        var country = _country is null ? null : await _country.GetAsync(cancellationToken);
        return settings
            .Where(s => s.IsEnabled)
            // One country at a time: the one the user works from
            .Where(s => _country is null || string.Equals(s.CountryCode, country, StringComparison.OrdinalIgnoreCase))
            .Select(s => (Module: _registry.Find(s.CountryCode), Settings: s))
            .Where(x => x.Module is not null)
            .Select(x => new ActiveCompliance(x.Module!, x.Settings))
            .ToList();
    }
}
