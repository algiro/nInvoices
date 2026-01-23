using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.PdfExport;

/// <summary>
/// PDF export service implementation using QuestPDF.
/// Implements professional invoice and calendar PDF generation.
/// Follows Open/Closed Principle - extensible through inheritance or composition.
/// </summary>
public sealed class PdfExportService : IPdfExportService
{
    public PdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateInvoicePdf(Invoice invoice)
    {
        var document = new InvoicePdfDocument(invoice);
        return document.GeneratePdf();
    }

    public byte[] GenerateWorkedDaysCalendarPdf(Invoice invoice)
    {
        if (invoice.Type != Core.Enums.InvoiceType.Monthly)
            throw new InvalidOperationException("Calendar export is only available for monthly invoices");

        var document = new WorkedDaysCalendarDocument(invoice);
        return document.GeneratePdf();
    }
}
