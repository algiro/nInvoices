using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Application.Behaviors;
using nInvoices.Application.Compliance;
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
        services.AddScoped<Services.InvoiceGeneration.IInvoiceRateResolver, Services.InvoiceGeneration.InvoiceRateResolver>();
        services.AddScoped<Services.InvoiceGeneration.IInvoiceWorkDays, Services.InvoiceGeneration.InvoiceWorkDays>();
        services.AddScoped<IInvoiceNumbering, InvoiceNumbering>();
        services.AddScoped<IDraftInvoiceSynchronizer, DraftInvoiceSynchronizer>();
        services.AddScoped<IInvoiceFinalizer, InvoiceFinalizer>();
        services.AddScoped<IMonthlyReportGenerationService, MonthlyReportGenerationService>();
        services.AddScoped<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddScoped<ITemplatePreviewService, TemplatePreviewService>();
        services.AddScoped<IProjectResolver, ProjectResolver>();
        services.AddScoped<Services.Holidays.IHolidayCalendarService, Services.Holidays.HolidayCalendarService>();
        services.AddScoped<Services.Email.IInvoiceEmailComposer, Services.Email.InvoiceEmailComposer>();
        services.AddCompliance();
        return services;
    }

    /// <summary>
    /// Registers the mediator with this assembly's handlers (wired at compile time by the Mediator
    /// source generator) and the FluentValidation validators, with the pipeline that runs a
    /// request's validators before its handler.
    /// </summary>
    public static IServiceCollection AddApplicationRequests(this IServiceCollection services)
    {
        services.AddMediator((MediatorOptions options) =>
        {
            options.Assemblies = [typeof(ApplicationAssemblyMarker)];
            // Handlers use the request's DbContext and user: never the generator's default, Singleton
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
        });
        services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);
        return services;
    }
}