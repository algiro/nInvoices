using Microsoft.Extensions.DependencyInjection;

namespace nInvoices.Application.TaxPrediction;

public static class TaxPredictionExtensions
{
    /// <summary>
    /// Registers the registry of the countries whose taxes can be estimated. Each country registers its
    /// <c>ITaxPredictionModule</c> in its plugin (<c>nInvoices.Countries.*</c>).
    /// </summary>
    public static IServiceCollection AddTaxPrediction(this IServiceCollection services)
    {
        services.AddSingleton<ITaxPredictionRegistry, TaxPredictionRegistry>();
        return services;
    }
}
