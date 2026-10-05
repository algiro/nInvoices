using FluentValidation;
using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed class PreviewInvoiceDraftQueryHandler : IRequestHandler<PreviewInvoiceDraftQuery, InvoiceDraftPreviewDto>
{
    private readonly IInvoiceGenerationService _invoiceGenerationService;
    private readonly IMonthlyReportGenerationService _monthlyReportGenerationService;
    private readonly IValidator<GenerateInvoiceDto> _validator;

    public PreviewInvoiceDraftQueryHandler(
        IInvoiceGenerationService invoiceGenerationService,
        IMonthlyReportGenerationService monthlyReportGenerationService,
        IValidator<GenerateInvoiceDto> validator)
    {
        _invoiceGenerationService = invoiceGenerationService;
        _monthlyReportGenerationService = monthlyReportGenerationService;
        _validator = validator;
    }

    public async ValueTask<InvoiceDraftPreviewDto> Handle(PreviewInvoiceDraftQuery request, CancellationToken cancellationToken)
    {
        // The wizard previews while the user is still filling it in: input that generating would
        // reject is shown as the preview's problems, not returned as an error
        var validation = await _validator.ValidateAsync(request.Invoice, cancellationToken);
        if (!validation.IsValid)
        {
            var problems = validation.Errors.Select(e => e.ErrorMessage).Distinct().ToList();
            return new InvoiceDraftPreviewDto(null, null, problems, null, null, null, null, null, null, []);
        }

        InvoiceDraft draft;
        try
        {
            draft = await _invoiceGenerationService.PreviewInvoiceAsync(request.Invoice, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new InvoiceDraftPreviewDto(null, null, [Reason(ex)], null, null, null, null, null, null, []);
        }

        string? timesheetHtml = null;
        string? timesheetError = null;
        if (draft.Invoice.Type == InvoiceType.Monthly)
        {
            try
            {
                timesheetHtml = await _monthlyReportGenerationService.PreviewReportHtmlAsync(
                    draft.Invoice, draft.Customer, draft.WorkDays, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                timesheetError = Reason(ex);
            }
        }

        var invoice = draft.Invoice;
        return new InvoiceDraftPreviewDto(
            invoice.Number.ToString(),
            invoice.RenderedContent,
            [],
            timesheetHtml,
            timesheetError,
            ToDto(invoice.Subtotal),
            ToDto(invoice.TotalExpenses),
            ToDto(invoice.TotalTaxes),
            ToDto(invoice.Total),
            invoice.TaxLines
                .OrderBy(t => t.Order)
                .Select(t => new InvoiceDraftTaxLineDto(t.Description, t.Rate, ToDto(t.TaxAmount)))
                .ToList());
    }

    private static MoneyDto ToDto(Money money) => new(money.Amount, money.Currency);

    // The renderer wraps Scriban's runtime error (e.g. an unknown function) in its own exception
    private static string Reason(InvalidOperationException ex) => ex.InnerException?.Message ?? ex.Message;
}
