using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using nInvoices.Application.Compliance.Spain.Facturae;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Configuration;

namespace nInvoices.Application.Compliance.Spain.Face;

/// <summary>
/// FACe, the general entry point for invoices to Spanish public administrations: delivers the signed
/// Facturae of an invoice to a public body and reports where it stands there (registered, accounted,
/// paid, rejected...). Only invoices to public administrations go through it.
/// </summary>
public sealed class FaceChannel : IEInvoiceChannel
{
    public const string Id = "face";

    private readonly IFaceClient _client;
    private readonly FaceOptions _options;

    public FaceChannel(IFaceClient client, IOptions<FaceOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public string ChannelId => Id;

    public string CountryCode => SpainComplianceModule.CountryCodeValue;

    public string FormatId => FacturaeFormat.Id;

    public string DisplayName => "FACe";

    public string EnvironmentName => _options.IsProduction ? "Production" : "Test";

    public string? UnavailableReason
    {
        get
        {
            var missing = _options.Missing();
            return missing.Count == 0
                ? null
                : $"FACe is not set up on this server ({string.Join(", ", missing)} missing under {FaceOptions.SectionName})";
        }
    }

    public bool AppliesTo(EInvoiceParty buyer) => SpainComplianceModule.IsPublicAdministration(buyer.Values);

    public IReadOnlyList<ComplianceIssue> CheckReady(IssuerProfile issuer)
    {
        var email = issuer.Values.GetValueOrDefault(SpainComplianceModule.FaceEmailKey);
        return email is not null && EmailAddresses.IsValid(email)
            ? []
            : [new ComplianceIssue(SpainComplianceModule.FaceEmailKey, "Fill in the email for FACe notifications in the Spanish settings")];
    }

    public async Task<ChannelDelivery> SendAsync(EInvoiceShipment shipment, X509Certificate2 certificate, CancellationToken cancellationToken = default)
    {
        try
        {
            var invoice = await _client.SendInvoiceAsync(
                Target(), certificate, shipment.Issuer.Values[SpainComplianceModule.FaceEmailKey], shipment.FileName, shipment.Content, cancellationToken);
            return ToDelivery(invoice);
        }
        catch (FaceException ex)
        {
            throw new ChannelException(ex.Message, ex);
        }
    }

    public async Task<ChannelDelivery> RefreshAsync(string reference, X509Certificate2 certificate, CancellationToken cancellationToken = default)
    {
        try
        {
            return ToDelivery(await _client.GetInvoiceAsync(Target(), certificate, reference, cancellationToken));
        }
        catch (FaceException ex)
        {
            throw new ChannelException(ex.Message, ex);
        }
    }

    private FaceTarget Target() => new(_options.IsProduction);

    private static ChannelDelivery ToDelivery(FaceInvoice invoice) =>
        new(invoice.RegistryCode, invoice.StatusCode, invoice.StatusName,
            invoice.CancellationName ?? invoice.CancellationCode, invoice.RegisteredAt);
}
