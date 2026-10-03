using nInvoices.Application.Models;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Compliance.Spain.Verifactu;

/// <summary>
/// What a template needs to print the Verifactu parts of an invoice: the QR code (as an SVG and as a data
/// URI), the text that must come before and after it, and the position of the record in the chain.
/// Available to templates as <c>compliance.verifactu</c>, always: when the invoice has no record it is empty
/// and <c>isRecorded</c> is false, so a template can print its parts without checking first.
/// </summary>
public sealed record VerifactuTemplateModel
{
    /// <summary>
    /// True when the invoice has its Verifactu record, and so a QR code. Always present, and false when it
    /// has not (a draft, or a user without Verifactu), in which case everything below is empty.
    /// </summary>
    public bool IsRecorded { get; init; }

    public string QrUrl { get; init; } = string.Empty;
    public string QrSvg { get; init; } = string.Empty;
    public string QrDataUri { get; init; } = string.Empty;

    /// <summary>The text that must be shown above the QR code.</summary>
    public string Heading { get; init; } = VerifactuQr.Heading;

    /// <summary>The phrase that must be shown below the QR code.</summary>
    public string Legend { get; init; } = VerifactuQr.Legend;

    public long Sequence { get; init; }
    public string Hash { get; init; } = string.Empty;

    /// <summary>The invoice was cancelled: the authority no longer finds it, so no QR code should be printed.</summary>
    public bool IsCancelled { get; init; }
}

public sealed class VerifactuTemplateContributor : IInvoiceTemplateModelContributor
{
    public const string Key = "verifactu";

    private readonly IVerifactuService _verifactu;

    public VerifactuTemplateContributor(IVerifactuService verifactu)
    {
        _verifactu = verifactu;
    }

    public async Task<InvoiceTemplateModel> ContributeAsync(Invoice invoice, InvoiceTemplateModel model, CancellationToken cancellationToken)
    {
        if (invoice.Id == 0)
            return model;

        var records = await _verifactu.GetRecordsAsync(invoice.Id, cancellationToken);
        var issued = records.FirstOrDefault(r => r.Kind == VerifactuRecordKind.Issued);
        if (issued is null)
            return model;

        var url = _verifactu.QrUrl(issued);
        var contribution = new VerifactuTemplateModel
        {
            IsRecorded = true,
            QrUrl = url,
            QrSvg = VerifactuQr.Svg(url),
            QrDataUri = VerifactuQr.SvgDataUri(url),
            Sequence = issued.Sequence,
            Hash = issued.Hash,
            IsCancelled = records.Any(r => r.Kind == VerifactuRecordKind.Cancelled)
        };

        return model with { Compliance = new Dictionary<string, object>(model.Compliance) { [Key] = contribution } };
    }
}
