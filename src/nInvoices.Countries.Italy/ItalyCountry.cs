using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Application.Services;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.TaxPrediction;
using nInvoices.Countries.Italy.FatturaPA;
using nInvoices.Countries.Italy.TaxPrediction;
using nInvoices.Infrastructure.Countries;

namespace nInvoices.Countries.Italy;

/// <summary>
/// Italy: the invoicing rules (issuer and customer data, the FatturaPA, the regime's notes, bollo, rivalsa and
/// ritenuta on the document) and the tax estimate (regime forfettario, regime ordinario semplificato).
/// </summary>
public sealed class ItalyCountry : ICountryPlugin
{
    public const string Code = "IT";

    public string CountryCode => Code;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Invoicing rules
        services.AddSingleton<ItalyComplianceModule>();
        services.AddSingleton<ICountryComplianceModule>(sp => sp.GetRequiredService<ItalyComplianceModule>());
        services.AddSingleton<IEInvoiceFormat, FatturaPaFormat>();
        services.AddScoped<IInvoiceTemplateModelContributor, ItalyTemplateContributor>();

        // Tax prediction
        services.AddSingleton<ITaxPredictionModule, ItalyTaxPredictionModule>();
    }
}
