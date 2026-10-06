using Microsoft.Extensions.DependencyInjection;
using nInvoices.Application.Services;
using QuestPDF.Infrastructure;

namespace nInvoices.Infrastructure.PdfExport;

/// <summary>
/// Extension methods for registering PDF export services.
/// </summary>
public static class PdfExportExtensions
{
    /// <summary>
    /// Registers PDF export: rendered templates go through headless Chrome (PuppeteerSharp), the
    /// built-in layouts through QuestPDF.
    /// </summary>
    public static IServiceCollection AddPdfExport(this IServiceCollection services)
    {
        // A process-wide setting QuestPDF needs before its first document: set once, here
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddScoped<IPdfExportService, PdfExportService>();
        services.AddSingleton<IHtmlToPdfConverter, PuppeteerPdfConverter>();

        return services;
    }
}
