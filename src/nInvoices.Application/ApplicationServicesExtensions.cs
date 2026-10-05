using FluentValidation;
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
        services.AddScoped<IInvoiceNumbering, InvoiceNumbering>();
        services.AddScoped<IDraftInvoiceSynchronizer, DraftInvoiceSynchronizer>();
        services.AddScoped<IMonthlyReportGenerationService, MonthlyReportGenerationService>();
        services.AddScoped<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddScoped<ITemplatePreviewService, TemplatePreviewService>();
        services.AddScoped<IProjectResolver, ProjectResolver>();
        services.AddScoped<Services.Holidays.IHolidayCalendarService, Services.Holidays.HolidayCalendarService>();
        services.AddScoped<Services.Email.IInvoiceEmailComposer, Services.Email.InvoiceEmailComposer>();
        services.AddCompliance();
        return services;
    }

    /// <summary>
    /// Registers the MediatR handlers and the FluentValidation validators of this assembly, with
    /// the pipeline that runs a request's validators before its handler.
    /// </summary>
    public static IServiceCollection AddApplicationRequests(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationAssemblyMarker).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}