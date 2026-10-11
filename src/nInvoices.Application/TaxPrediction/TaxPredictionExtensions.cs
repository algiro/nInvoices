using Microsoft.Extensions.DependencyInjection;
using nInvoices.Application.TaxPrediction.Spain;
using nInvoices.Core.TaxPrediction;

namespace nInvoices.Application.TaxPrediction;

public static class TaxPredictionExtensions
{
    /// <summary>
    /// Registers the countries whose taxes can be estimated, and their registry. A new country is one
    /// more <c>AddSingleton&lt;ITaxPredictionModule, ...&gt;</c> line here.
    /// </summary>
    public static IServiceCollection AddTaxPrediction(this IServiceCollection services)
    {
        services.AddSingleton<ITaxPredictionModule, SpainTaxPredictionModule>();
        services.AddSingleton<ITaxPredictionRegistry, TaxPredictionRegistry>();
        return services;
    }
}
