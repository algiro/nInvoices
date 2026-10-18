using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Application.Services;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.TaxPrediction;
using nInvoices.Countries.Germany.TaxPrediction;
using nInvoices.Countries.Germany.XRechnung;
using nInvoices.Infrastructure.Countries;

namespace nInvoices.Countries.Germany;

/// <summary>
/// Germany: the invoicing rules (§14 UStG, Kleinunternehmer, XRechnung) and the tax estimate (Einkommensteuer,
/// Soli, Kirchensteuer, Gewerbesteuer, Umsatzsteuer-Voranmeldung).
/// </summary>
public sealed class GermanyCountry : ICountryPlugin
{
    public const string Code = "DE";

    public string CountryCode => Code;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Invoicing rules
        services.AddSingleton<GermanyComplianceModule>();
        services.AddSingleton<ICountryComplianceModule>(sp => sp.GetRequiredService<GermanyComplianceModule>());
        services.AddSingleton<IEInvoiceFormat, XRechnungFormat>();
        services.AddScoped<IInvoiceTemplateModelContributor, GermanyTemplateContributor>();

        // Tax prediction
        services.AddSingleton<ITaxPredictionModule, GermanyTaxPredictionModule>();
    }
}
