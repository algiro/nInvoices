using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Application.Features.Invoices;
using nInvoices.Application.Services;
using nInvoices.Core.Compliance;
using nInvoices.Core.TaxPrediction;
using nInvoices.Countries.UnitedKingdom.TaxPrediction;
using nInvoices.Infrastructure.Countries;

namespace nInvoices.Countries.UnitedKingdom;

/// <summary>
/// The United Kingdom: VAT invoice rules (the VAT number, why a 0% line charges none) and the estimate of a sole
/// trader's Self Assessment and VAT, over the tax year from 6 April to 5 April.
/// </summary>
public sealed class UnitedKingdomCountry : ICountryPlugin
{
    public const string Code = "GB";

    public string CountryCode => Code;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Invoicing rules
        services.AddSingleton<UkComplianceModule>();
        services.AddSingleton<ICountryComplianceModule>(sp => sp.GetRequiredService<UkComplianceModule>());
        services.AddScoped<IInvoiceLifecycleStep, UkVatInvoiceStep>();
        services.AddScoped<IInvoiceTemplateModelContributor, UkTemplateContributor>();

        // Tax prediction
        services.AddSingleton<ITaxPredictionModule, UkTaxPredictionModule>();
    }
}
