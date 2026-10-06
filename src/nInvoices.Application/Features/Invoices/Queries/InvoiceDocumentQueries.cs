using Mediator;
using Microsoft.Extensions.Logging;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Queries;

/// <summary>The invoice as a PDF: its rendered HTML, or the built-in layout when it has none.</summary>
public sealed record GetInvoicePdfQuery(long InvoiceId) : IRequest<DownloadFile>;

/// <summary>The calendar of worked days of a monthly invoice, as a PDF.</summary>
public sealed record GetWorkedDaysCalendarPdfQuery(long InvoiceId) : IRequest<DownloadFile>;

/// <summary>The monthly report (timesheet) of a monthly invoice, rendered with its template, as a PDF.</summary>
public sealed record GetMonthlyReportPdfQuery(long InvoiceId) : IRequest<DownloadFile>;

/// <summary>Renders the invoice again with the customer's current active template.</summary>
public sealed record RegenerateInvoiceHtmlCommand(long InvoiceId) : IRequest<Unit>;

/// <summary>
/// Checks that the monthly report of an invoice renders with the current template. Reports are not
/// stored (they are rendered on each download), so there is nothing to regenerate.
/// </summary>
public sealed record VerifyMonthlyReportQuery(long InvoiceId) : IRequest<Unit>;

/// <summary>
/// Produces the documents of one invoice. Throws <see cref="NotFoundException"/> when the invoice
/// (or its customer) doesn't exist and <see cref="DomainException"/> when the document doesn't apply.
/// </summary>
public sealed class InvoiceDocumentHandlers :
    IRequestHandler<GetInvoicePdfQuery, DownloadFile>,
    IRequestHandler<GetWorkedDaysCalendarPdfQuery, DownloadFile>,
    IRequestHandler<GetMonthlyReportPdfQuery, DownloadFile>,
    IRequestHandler<RegenerateInvoiceHtmlCommand, Unit>,
    IRequestHandler<VerifyMonthlyReportQuery, Unit>
{
    private const string CalendarOnlyMonthly = "Calendar export is only available for monthly invoices";
    private const string ReportOnlyMonthly = "Monthly reports are only available for monthly invoices";

    private readonly IRepository<Invoice> _invoices;
    private readonly IRepository<Customer> _customers;
    private readonly IPdfExportService _pdfExport;
    private readonly IHtmlToPdfConverter _htmlToPdf;
    private readonly IMonthlyReportGenerationService _monthlyReports;
    private readonly IInvoiceGenerationService _generation;
    private readonly ILogger<InvoiceDocumentHandlers> _logger;

    public InvoiceDocumentHandlers(
        IRepository<Invoice> invoices,
        IRepository<Customer> customers,
        IPdfExportService pdfExport,
        IHtmlToPdfConverter htmlToPdf,
        IMonthlyReportGenerationService monthlyReports,
        IInvoiceGenerationService generation,
        ILogger<InvoiceDocumentHandlers> logger)
    {
        _invoices = invoices;
        _customers = customers;
        _pdfExport = pdfExport;
        _htmlToPdf = htmlToPdf;
        _monthlyReports = monthlyReports;
        _generation = generation;
        _logger = logger;
    }

    public async ValueTask<DownloadFile> Handle(GetInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await InvoiceAsync(request.InvoiceId, cancellationToken);
        return new DownloadFile($"Invoice-{invoice.Number}.pdf", await _pdfExport.GenerateInvoicePdfAsync(invoice, cancellationToken));
    }

    public async ValueTask<DownloadFile> Handle(GetWorkedDaysCalendarPdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await MonthlyInvoiceAsync(request.InvoiceId, CalendarOnlyMonthly, cancellationToken);
        var customer = await CustomerAsync(invoice, cancellationToken);
        return new DownloadFile(
            $"Calendar-{invoice.Year}-{invoice.Month:00}-{customer.Name}.pdf",
            _pdfExport.GenerateWorkedDaysCalendarPdf(invoice));
    }

    public async ValueTask<DownloadFile> Handle(GetMonthlyReportPdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await MonthlyInvoiceAsync(request.InvoiceId, ReportOnlyMonthly, cancellationToken);
        var customer = await CustomerAsync(invoice, cancellationToken);

        var html = await _monthlyReports.GenerateReportHtmlAsync(invoice, customer, cancellationToken);
        var pdf = await _htmlToPdf.ConvertAsync(html, cancellationToken);
        return new DownloadFile($"MonthlyReport-{invoice.Year}-{invoice.Month:00}-{customer.Name}.pdf", pdf);
    }

    public async ValueTask<Unit> Handle(RegenerateInvoiceHtmlCommand request, CancellationToken cancellationToken)
    {
        await _generation.RegenerateInvoiceHtmlAsync(request.InvoiceId, cancellationToken);
        _logger.LogInformation("Invoice {InvoiceId} HTML re-rendered with the current template", request.InvoiceId);
        return Unit.Value;
    }

    public async ValueTask<Unit> Handle(VerifyMonthlyReportQuery request, CancellationToken cancellationToken)
    {
        var invoice = await MonthlyInvoiceAsync(request.InvoiceId, ReportOnlyMonthly, cancellationToken);
        var customer = await CustomerAsync(invoice, cancellationToken);

        _ = await _monthlyReports.GenerateReportHtmlAsync(invoice, customer, cancellationToken);
        _logger.LogInformation("Monthly report of invoice {InvoiceId} renders with the current template", request.InvoiceId);
        return Unit.Value;
    }

    private async Task<Invoice> InvoiceAsync(long id, CancellationToken cancellationToken) =>
        await _invoices.GetByIdAsync(id, cancellationToken) ?? throw NotFoundException.For("Invoice", id);

    private async Task<Invoice> MonthlyInvoiceAsync(long id, string notMonthly, CancellationToken cancellationToken)
    {
        var invoice = await InvoiceAsync(id, cancellationToken);
        if (invoice.Type != InvoiceType.Monthly)
            throw new DomainException(notMonthly);
        return invoice;
    }

    private async Task<Customer> CustomerAsync(Invoice invoice, CancellationToken cancellationToken) =>
        await _customers.GetByIdAsync(invoice.CustomerId, cancellationToken) ?? throw NotFoundException.For("Customer", invoice.CustomerId);
}
