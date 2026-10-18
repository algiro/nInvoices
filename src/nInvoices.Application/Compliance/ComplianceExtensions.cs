using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using nInvoices.Application.Compliance.EInvoice;
using nInvoices.Application.Features.Invoices;

namespace nInvoices.Application.Compliance;

public static class ComplianceExtensions
{
    /// <summary>
    /// Registers what every country's invoicing rules share: the registry, the gate, customer and tax data,
    /// e-invoice generation and delivery. The countries themselves (<c>ICountryComplianceModule</c>,
    /// <c>IEInvoiceFormat</c>, <c>IEInvoiceChannel</c>...) are registered by their plugins
    /// (<c>nInvoices.Countries.*</c>). The host binds <c>ComplianceOptions</c> from the "Compliance" section.
    /// </summary>
    public static IServiceCollection AddCompliance(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IComplianceRegistry, ComplianceRegistry>();
        services.AddScoped<IComplianceGate, ComplianceGate>();
        services.AddScoped<ICustomerCompliance, CustomerCompliance>();
        services.AddScoped<ITaxCompliance, TaxCompliance>();
        services.AddScoped<IEInvoiceDocumentFactory, EInvoiceDocumentFactory>();
        services.AddScoped<SigningCertificateLoader>();
        services.AddScoped<IEInvoiceService, EInvoiceService>();
        services.AddScoped<IEInvoiceDeliveryService, EInvoiceDeliveryService>();
        services.AddScoped<IInvoiceLifecycleStep, EInvoiceDeliveryLifecycleStep>();
        return services;
    }
}
