namespace nInvoices.Application.Services;

/// <summary>
/// Converts rendered HTML to PDF using QuestPDF.
/// Supports basic HTML tags and table layouts for invoice generation.
/// </summary>
public interface IHtmlToPdfConverter
{
    /// <summary>
    /// Converts HTML content to PDF bytes.
    /// </summary>
    /// <param name="html">Rendered HTML content (after template processing)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF document as byte array</returns>
    Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default);
}
