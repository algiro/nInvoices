using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using nInvoices.Application.Compliance;
using nInvoices.Application.Services;
using nInvoices.Application.TaxPrediction;

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
        services.AddScoped<Services.InvoiceGeneration.IInvoiceRateResolver, Services.InvoiceGeneration.InvoiceRateResolver>();
        services.AddScoped<Services.InvoiceGeneration.IInvoiceWorkDays, Services.InvoiceGeneration.InvoiceWorkDays>();
        services.AddScoped<IInvoiceNumbering, InvoiceNumbering>();
        services.AddScoped<IDraftInvoiceSynchronizer, DraftInvoiceSynchronizer>();
        services.AddScoped<IInvoiceFinalizer, InvoiceFinalizer>();
        services.AddScoped<IMonthlyReportGenerationService, MonthlyReportGenerationService>();
        services.AddScoped<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.TryAddSingleton<Localization.IAppText>(_ => new Localization.AppText());
        services.AddScoped<ITemplatePreviewService, TemplatePreviewService>();
        services.AddScoped<IProjectResolver, ProjectResolver>();
        services.AddScoped<Services.Holidays.IHolidayCalendarService, Services.Holidays.HolidayCalendarService>();
        services.AddScoped<Services.Email.IInvoiceEmailComposer, Services.Email.InvoiceEmailComposer>();
        services.AddCompliance();
        services.AddTaxPrediction();
        return services;
    }
}
