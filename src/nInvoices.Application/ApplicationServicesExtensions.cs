using Microsoft.Extensions.DependencyInjection;
using nInvoices.Application.Services;

namespace nInvoices.Application;

/// <summary>
/// Extension methods for registering application services.
/// </summary>
public static class ApplicationServicesExtensions
{
    /// <summary>
    /// Registers application layer services.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IInvoiceGenerationService, InvoiceGenerationService>();
        return services;
    }
}