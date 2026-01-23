using Microsoft.Extensions.DependencyInjection;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.TaxHandlers;

/// <summary>
/// Extension methods for registering tax calculation services.
/// </summary>
public static class TaxHandlerExtensions
{
    /// <summary>
    /// Registers all tax handlers and the tax calculation service.
    /// Uses automatic discovery of ITaxHandler implementations.
    /// </summary>
    public static IServiceCollection AddTaxHandlers(this IServiceCollection services)
    {
        // Register all tax handlers
        services.AddSingleton<ITaxHandler, PercentageTaxHandler>();
        services.AddSingleton<ITaxHandler, FixedAmountTaxHandler>();
        services.AddSingleton<ITaxHandler, CompoundTaxHandler>();

        // Register the tax calculation service
        services.AddScoped<TaxCalculationService>();

        return services;
    }
}

