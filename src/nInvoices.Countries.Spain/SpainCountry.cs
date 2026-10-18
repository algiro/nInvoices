using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Application.Features.Invoices;
using nInvoices.Application.Services;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.TaxPrediction;
using nInvoices.Countries.Spain.Face;
using nInvoices.Countries.Spain.Facturae;
using nInvoices.Countries.Spain.TaxPrediction;
using nInvoices.Countries.Spain.Verifactu;
using nInvoices.Infrastructure.Countries;

namespace nInvoices.Countries.Spain;

/// <summary>
/// Spain: the invoicing rules (issuer and customer data, Facturae e-invoices, delivery through FACe,
/// Verifactu records sent to the Tax Agency) and the quarterly tax estimate (Modelo 130, 303 / 420).
/// </summary>
public sealed class SpainCountry : ICountryPlugin
{
    public const string Code = "ES";

    /// <summary>The values of the <c>system</c> tag of the e-invoice metrics (<c>AppMetrics</c>) for Spain's services.</summary>
    public static class Systems
    {
        /// <summary>The Spanish Tax Agency (AEAT), through Verifactu.</summary>
        public const string Verifactu = "verifactu";

        /// <summary>FACe, the Spanish portal for invoices to public administrations.</summary>
        public const string Face = "face";
    }

    public string CountryCode => Code;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<VerifactuOptions>(configuration.GetSection(VerifactuOptions.SectionName));
        services.Configure<FaceOptions>(configuration.GetSection(FaceOptions.SectionName));

        // Invoicing rules
        services.AddSingleton<SpainComplianceModule>();
        services.AddSingleton<ICountryComplianceModule>(sp => sp.GetRequiredService<SpainComplianceModule>());
        services.AddSingleton<IEInvoiceFormat, FacturaeFormat>();
        services.AddSingleton<IEInvoiceChannel, FaceChannel>();

        // Verifactu: the chain, the step that records each invoice, its QR code on the document, and the
        // Tax Agency client with the worker that sends the waiting records (idle until set up)
        services.AddScoped<IVerifactuRecordRepository, VerifactuRecordRepository>();
        services.AddScoped<IVerifactuService, VerifactuService>();
        services.AddScoped<IVerifactuSubmitter, VerifactuSubmitter>();
        services.AddScoped<IInvoiceLifecycleStep, VerifactuLifecycleStep>();
        services.AddScoped<IInvoiceTemplateModelContributor, VerifactuTemplateContributor>();
        services.AddVerifactuSubmission();

        // FACe: the client that delivers invoices to public administrations (used when the user sends one)
        services.AddFace();

        // Tax prediction
        services.AddSingleton<ITaxPredictionModule, SpainTaxPredictionModule>();
    }
}
