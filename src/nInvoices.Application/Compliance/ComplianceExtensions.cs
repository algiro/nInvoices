using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using nInvoices.Application.Compliance.Spain.Facturae;
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
        services.AddScoped<IEInvoiceDocumentFactory, EInvoiceDocumentFactory>();
        services.AddScoped<IEInvoiceService, EInvoiceService>();
        return services;
    }
}
