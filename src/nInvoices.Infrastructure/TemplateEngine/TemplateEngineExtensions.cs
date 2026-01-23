using Microsoft.Extensions.DependencyInjection;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.TemplateEngine;

/// <summary>
/// Extension methods for registering template engine services.
/// </summary>
public static class TemplateEngineExtensions
{
    /// <summary>
    /// Registers the template engine implementation.
    /// </summary>
    public static IServiceCollection AddTemplateEngine(this IServiceCollection services)
    {
        services.AddSingleton<ITemplateEngine, HandlebarsTemplateEngine>();
        return services;
    }
}
