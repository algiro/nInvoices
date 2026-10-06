using QuestPDF.Fluent;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;

namespace nInvoices.Infrastructure.PdfExport;

/// <summary>
/// The PDF documents of an invoice: its rendered template through <see cref="IHtmlToPdfConverter"/>,
/// or the built-in QuestPDF layouts.
/// </summary>
public sealed class PdfExportService : IPdfExportService
{
    private readonly IHtmlToPdfConverter _htmlToPdfConverter;

    public PdfExportService(IHtmlToPdfConverter htmlToPdfConverter)
    {
        _htmlToPdfConverter = htmlToPdfConverter;
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        // The invoice as the user's template rendered it; the built-in layout only when there is none
        if (!string.IsNullOrWhiteSpace(invoice.RenderedContent))
            return await _htmlToPdfConverter.ConvertAsync(invoice.RenderedContent, cancellationToken);

        return new InvoicePdfDocument(invoice).GeneratePdf();
    }

    public byte[] GenerateWorkedDaysCalendarPdf(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        if (invoice.Type != InvoiceType.Monthly)
            throw new DomainException("Calendar export is only available for monthly invoices");

        return new WorkedDaysCalendarDocument(invoice).GeneratePdf();
    }
}
