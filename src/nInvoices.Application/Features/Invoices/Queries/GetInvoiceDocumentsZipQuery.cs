using Mediator;

namespace nInvoices.Application.Features.Invoices.Queries;

/// <summary>A file to download: a document of an invoice, or several in a zip.</summary>
public sealed record DownloadFile(string FileName, byte[] Content, string ContentType = DownloadFile.Pdf)
{
    public const string Pdf = "application/pdf";
    public const string Zip = "application/zip";
}

/// <summary>
/// The PDFs of several invoices (and, optionally, the timesheets of the monthly ones) in one zip.
/// An invoice whose documents can't be produced is listed in a text file inside the zip instead.
/// </summary>
public sealed record GetInvoiceDocumentsZipQuery(IReadOnlyList<long> InvoiceIds, bool IncludeMonthlyReports) : IRequest<DownloadFile>;
