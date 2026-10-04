using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using nInvoices.Application.Compliance.Spain.Face;
using nInvoices.Application.Compliance.Spain.Facturae;
using nInvoices.Application.Compliance.Spain.Verifactu;
using nInvoices.Application.Features.Invoices;
using nInvoices.Application.Services;
using nInvoices.Application.Compliance.EInvoice;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Application.Compliance.Spain;
using nInvoices.Core.Compliance;

namespace nInvoices.Application.Compliance;

public static class ComplianceExtensions
{
    /// <summary>
    /// Registers the country modules, the registry and the gate. A new country is one more
    /// <c>AddSingleton&lt;ICountryComplianceModule, ...&gt;</c> line here. The host binds
    /// <c>ComplianceOptions</c> from the "Compliance" section.
    /// </summary>
    public static IServiceCollection AddCompliance(this IServiceCollection services)
    {
        services.AddSingleton<SpainComplianceModule>();
        services.AddSingleton<ICountryComplianceModule>(sp => sp.GetRequiredService<SpainComplianceModule>());
        services.AddSingleton<IEInvoiceFormat, FacturaeFormat>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IComplianceRegistry, ComplianceRegistry>();
        services.AddScoped<IComplianceGate, ComplianceGate>();
        services.AddScoped<ICustomerCompliance, CustomerCompliance>();
        services.AddScoped<ITaxCompliance, TaxCompliance>();
        services.AddScoped<IEInvoiceDocumentFactory, EInvoiceDocumentFactory>();
        services.AddScoped<SigningCertificateLoader>();
        services.AddScoped<IEInvoiceService, EInvoiceService>();
        services.AddScoped<IEInvoiceDeliveryService, EInvoiceDeliveryService>();
        services.AddSingleton<IEInvoiceChannel, FaceChannel>();

        services.AddScoped<IVerifactuService, VerifactuService>();
        services.AddScoped<IVerifactuSubmitter, VerifactuSubmitter>();
        services.AddScoped<IInvoiceLifecycleStep, VerifactuLifecycleStep>();
        services.AddScoped<IInvoiceLifecycleStep, EInvoiceDeliveryLifecycleStep>();
        services.AddScoped<IInvoiceTemplateModelContributor, VerifactuTemplateContributor>();
        return services;
    }
}
