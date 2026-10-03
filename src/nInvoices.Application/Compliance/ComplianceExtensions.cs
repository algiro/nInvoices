using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<ICountryComplianceModule, SpainComplianceModule>();

        services.AddSingleton<IComplianceRegistry, ComplianceRegistry>();
        services.AddScoped<IComplianceGate, ComplianceGate>();
        return services;
    }
}
