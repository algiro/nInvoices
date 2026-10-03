using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Compliance;

/// <summary>
/// Whether a country's rules apply to the current user: the installation offers the country AND
/// the user turned it on. Every country-specific step (extra fields, XML, signing...) must ask
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

    public ComplianceGate(IComplianceRegistry registry, IRepository<ComplianceSettings> settings)
    {
        _registry = registry;
        _settings = settings;
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
        return settings
            .Where(s => s.IsEnabled)
            .Select(s => (Module: _registry.Find(s.CountryCode), Settings: s))
            .Where(x => x.Module is not null)
            .Select(x => new ActiveCompliance(x.Module!, x.Settings))
            .ToList();
    }
}
