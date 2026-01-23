using Microsoft.Extensions.DependencyInjection;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.PdfExport;

/// <summary>
/// Extension methods for registering PDF export services.
/// Follows Dependency Inversion and Single Responsibility principles.
/// </summary>
public static class PdfExportExtensions
{
    /// <summary>
    /// Registers PDF export services in the DI container.
    /// </summary>
    public static IServiceCollection AddPdfExport(this IServiceCollection services)
    {
        services.AddScoped<IPdfExportService, PdfExportService>();

        return services;
    }
}
