using System.IO.Compression;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed class GetInvoiceDocumentsZipQueryHandler : IRequestHandler<GetInvoiceDocumentsZipQuery, DownloadFile>
{
    /// <summary>PDFs are rendered one by one in a browser, so a download is kept to a sensible size.</summary>
    public const int MaxInvoices = 100;

    private const string ProblemsFileName = "NOT-INCLUDED.txt";

    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IInvoiceEmailComposer _documents;
    private readonly ILogger<GetInvoiceDocumentsZipQueryHandler> _logger;

    public GetInvoiceDocumentsZipQueryHandler(
        IInvoiceRepository invoiceRepository,
        IRepository<Customer> customerRepository,
        IInvoiceEmailComposer documents,
        ILogger<GetInvoiceDocumentsZipQueryHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _customerRepository = customerRepository;
        _documents = documents;
        _logger = logger;
    }

    public async Task<DownloadFile> Handle(GetInvoiceDocumentsZipQuery request, CancellationToken cancellationToken)
    {
        var ids = request.InvoiceIds.Distinct().ToList();
        if (ids.Count == 0)
            throw new ArgumentException("Select at least one invoice");
        if (ids.Count > MaxInvoices)
            throw new ArgumentException($"At most {MaxInvoices} invoices can be downloaded at once");

        var invoices = (await _invoiceRepository.GetByIdsAsync(ids, cancellationToken))
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.Id)
            .ToList();
        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = (await _customerRepository.FindAsync(c => customerIds.Contains(c.Id), cancellationToken))
            .ToDictionary(c => c.Id);

        var problems = ids
            .Where(id => invoices.All(i => i.Id != id))
            .Select(id => $"Invoice {id}: not found")
            .ToList();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var invoice in invoices)
            {
                if (!customers.TryGetValue(invoice.CustomerId, out var customer))
                {
                    problems.Add($"{invoice.Number}: customer not found");
                    continue;
                }

                try
                {
                    var files = await _documents.BuildAttachmentsAsync(invoice, customer, request.IncludeMonthlyReports, cancellationToken);
                    foreach (var file in files)
                        await AddAsync(zip, UniqueName(file.FileName, usedNames), file.Content, cancellationToken);
                }
                catch (InvoiceEmailException ex)
                {
                    _logger.LogWarning(ex, "Documents of invoice {InvoiceId} left out of the zip", invoice.Id);
                    problems.Add($"{invoice.Number}: {ex.Message}");
                }
            }

            if (problems.Count > 0)
            {
                var text = "These invoices are not in the zip:" + Environment.NewLine + string.Join(Environment.NewLine, problems);
                await AddAsync(zip, ProblemsFileName, Encoding.UTF8.GetBytes(text), cancellationToken);
            }
        }

        return new DownloadFile($"Invoices-{DateTime.Now:yyyy-MM-dd-HHmm}.zip", buffer.ToArray());
    }

    private static async Task AddAsync(ZipArchive zip, string name, byte[] content, CancellationToken cancellationToken)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await stream.WriteAsync(content, cancellationToken);
    }

    /// <summary>Two documents with the same name (e.g. two invoices of one month) get "(2)", "(3)"…</summary>
    private static string UniqueName(string name, HashSet<string> used)
    {
        var candidate = name;
        for (var n = 2; !used.Add(candidate); n++)
            candidate = $"{Path.GetFileNameWithoutExtension(name)} ({n}){Path.GetExtension(name)}";
        return candidate;
    }
}
