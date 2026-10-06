using nInvoices.Core.Entities;

namespace nInvoices.Application.Services;

/// <summary>
/// The PDF documents of an invoice. The one place that decides how an invoice becomes a PDF, so the
/// download, the zip and the email attachment are the same file.
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// The invoice as a PDF: its rendered template converted by <see cref="IHtmlToPdfConverter"/> (a
    /// headless browser, so this is real I/O), or the built-in layout when it has no rendered content.
    /// </summary>
    Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice, CancellationToken cancellationToken = default);

    /// <summary>
    /// The calendar of worked days of a monthly invoice, in the built-in layout. Drawn in memory, so
    /// synchronous.
    /// </summary>
    byte[] GenerateWorkedDaysCalendarPdf(Invoice invoice);
}
