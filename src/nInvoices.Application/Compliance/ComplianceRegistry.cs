using Microsoft.Extensions.Options;
using nInvoices.Core.Compliance;
using nInvoices.Core.Configuration;

namespace nInvoices.Application.Compliance;

/// <summary>The country modules this installation offers.</summary>
public interface IComplianceRegistry
{
    IReadOnlyList<ICountryComplianceModule> Offered { get; }

    /// <returns>The module for the country, or null if there is none or the installation switched it off.</returns>
    ICountryComplianceModule? Find(string countryCode);
}

public sealed class ComplianceRegistry : IComplianceRegistry
{
    private readonly Dictionary<string, ICountryComplianceModule> _modules;

    public ComplianceRegistry(IEnumerable<ICountryComplianceModule> modules, IOptions<ComplianceOptions> options)
    {
        _modules = modules
            .Where(m => options.Value.IsOffered(m.CountryCode))
            .OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(m => m.CountryCode, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ICountryComplianceModule> Offered => _modules.Values.ToList();

    public ICountryComplianceModule? Find(string countryCode) =>
        _modules.GetValueOrDefault(countryCode);
}
