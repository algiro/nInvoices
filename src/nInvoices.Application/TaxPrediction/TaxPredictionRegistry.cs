using nInvoices.Core.TaxPrediction;

namespace nInvoices.Application.TaxPrediction;

/// <summary>The countries whose taxes this installation can estimate.</summary>
public interface ITaxPredictionRegistry
{
    IReadOnlyList<ITaxPredictionModule> Offered { get; }

    /// <returns>The module for the country, or null if there is none.</returns>
    ITaxPredictionModule? Find(string countryCode);
}

public sealed class TaxPredictionRegistry : ITaxPredictionRegistry
{
    private readonly Dictionary<string, ITaxPredictionModule> _modules;

    public TaxPredictionRegistry(IEnumerable<ITaxPredictionModule> modules)
    {
        _modules = modules
            .OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(m => m.CountryCode, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ITaxPredictionModule> Offered => _modules.Values.ToList();

    public ITaxPredictionModule? Find(string countryCode) => _modules.GetValueOrDefault(countryCode);
}
