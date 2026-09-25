using MediatR;

namespace nInvoices.Application.Features.Invoices.Queries;

/// <summary>A zip file to download.</summary>
public sealed record DownloadFile(string FileName, byte[] Content);

/// <summary>
/// The PDFs of several invoices (and, optionally, the timesheets of the monthly ones) in one zip.
/// An invoice whose documents can't be produced is listed in a text file inside the zip instead.
/// </summary>
public sealed record GetInvoiceDocumentsZipQuery(IReadOnlyList<long> InvoiceIds, bool IncludeMonthlyReports) : IRequest<DownloadFile>;
