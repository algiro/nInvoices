namespace nInvoices.Core.Interfaces;

/// <summary>
/// Service interface for PDF export functionality.
/// Part of Core layer - provides abstraction for PDF generation.
/// Follows Dependency Inversion Principle - high-level modules don't depend on low-level PDF library.
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// Generates a PDF document from invoice data.
    /// Returns the PDF as a byte array for flexible delivery (file, stream, response).
    /// </summary>
    /// <param name="invoice">The invoice to export</param>
    /// <returns>PDF document as byte array</returns>
    byte[] GenerateInvoicePdf(Entities.Invoice invoice);
    
    /// <summary>
    /// Generates a worked days calendar PDF for a monthly invoice.
    /// Useful for timesheet documentation.
    /// </summary>
    /// <param name="invoice">The monthly invoice with worked days</param>
    /// <returns>PDF document as byte array</returns>
    byte[] GenerateWorkedDaysCalendarPdf(Entities.Invoice invoice);
}
