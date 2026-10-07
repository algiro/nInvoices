using nInvoices.Application.DTOs;
using nInvoices.Application.Models;
using nInvoices.Application.Services.InvoiceGeneration;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using nInvoices.Application.Exceptions;
using nInvoices.Core.Exceptions;

namespace nInvoices.Application.Services;

/// <summary>
/// Service responsible for invoice generation orchestration.
/// Implements the Facade pattern to simplify complex invoice generation workflow.
/// Coordinates: template selection, rate calculation, tax application, and rendering.
/// </summary>
public interface IInvoiceGenerationService
{
    Task<Invoice> GenerateInvoiceAsync(
        GenerateInvoiceDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds and renders the invoice exactly as <see cref="GenerateInvoiceAsync"/> would, without
    /// side effects: work days are not saved and projects are not created (the number shown is
    /// the next one; generating does not take it either, finalizing does). Throws <see cref="InvalidOperationException"/>
    /// when the invoice could not be generated (no rate, no active template, a template error).
    /// </summary>
    Task<InvoiceDraft> PreviewInvoiceAsync(
        GenerateInvoiceDto dto,
        CancellationToken cancellationToken = default);

    Task RegenerateInvoiceHtmlAsync(
        long invoiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The model the invoice renders from (customer, line items, taxes), rebuilt from what is saved.
    /// Throws <see cref="KeyNotFoundException"/> when the invoice does not exist.
    /// </summary>
    Task<InvoiceTemplateModel> BuildTemplateModelAsync(
        long invoiceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// An invoice built in memory and not persisted, with the customer and the work days it was
/// built from (project allocations merged per day), so the timesheet can be rendered from them.
/// </summary>
public sealed record InvoiceDraft(Invoice Invoice, Customer Customer, IReadOnlyCollection<WorkDayDto> WorkDays);

/// <summary>
/// Generates invoices: picks the template and the rates, has <see cref="InvoiceCalculator"/> work
/// out the amounts and <see cref="InvoiceTemplateModelBuilder"/> the model, and renders it.
/// Generating and previewing share all of it; only generating saves the month's work days.
/// </summary>
public sealed class InvoiceGenerationService : IInvoiceGenerationService
{
    private readonly IRepository<InvoiceTemplate> _templateRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Tax> _taxRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IInvoiceRateResolver _rates;
    private readonly IInvoiceWorkDays _workDays;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly ITaxCalculationService _taxCalculationService;
    private readonly IInvoiceNumbering _numbering;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadOnlyList<IInvoiceTemplateModelContributor> _contributors;

    public InvoiceGenerationService(
        IRepository<InvoiceTemplate> templateRepository,
        IRepository<Customer> customerRepository,
        IRepository<Tax> taxRepository,
        IInvoiceRepository invoiceRepository,
        IInvoiceRateResolver rates,
        IInvoiceWorkDays workDays,
        ITemplateRenderer templateRenderer,
        ITaxCalculationService taxCalculationService,
        IInvoiceNumbering numbering,
        IUnitOfWork unitOfWork,
        IEnumerable<IInvoiceTemplateModelContributor>? contributors = null)
    {
        _contributors = contributors?.ToList() ?? [];
        _templateRepository = templateRepository;
        _customerRepository = customerRepository;
        _taxRepository = taxRepository;
        _invoiceRepository = invoiceRepository;
        _rates = rates;
        _workDays = workDays;
        _templateRenderer = templateRenderer;
        _taxCalculationService = taxCalculationService;
        _numbering = numbering;
        _unitOfWork = unitOfWork;
    }

    public async Task<Invoice> GenerateInvoiceAsync(
        GenerateInvoiceDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var built = await BuildAsync(dto, cancellationToken);

        // The month's work days are replaced (staged, committed by the caller), with project names
        // in their canonical form
        var workDays = dto.InvoiceType == InvoiceType.Monthly && dto.WorkDays != null
            ? await _workDays.ReplaceMonthAsync(dto.CustomerId, dto.Year!.Value, dto.Month!.Value, dto.WorkDays, cancellationToken)
            : dto.WorkDays;

        return (await RenderAsync(built, workDays, cancellationToken)).Invoice;
    }

    public async Task<InvoiceDraft> PreviewInvoiceAsync(
        GenerateInvoiceDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var built = await BuildAsync(dto, cancellationToken);

        // Nothing is written: allocations are only merged, as they would be once saved
        var workDays = dto.InvoiceType == InvoiceType.Monthly && dto.WorkDays != null
            ? InvoiceCalculator.MergeAllocations(dto.WorkDays)
            : dto.WorkDays;

        return await RenderAsync(built, workDays, cancellationToken);
    }

    public async Task RegenerateInvoiceHtmlAsync(
        long invoiceId,
        CancellationToken cancellationToken = default)
    {
        var (invoice, templateModel) = await RebuildTemplateModelAsync(invoiceId, cancellationToken);

        var template = await GetTemplateAsync(invoice.CustomerId, invoice.Type, cancellationToken);
        var renderedHtml = await _templateRenderer.RenderAsync(template.Content, templateModel, cancellationToken);

        invoice.SetRenderedContent(renderedHtml);
        await _invoiceRepository.UpdateAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<InvoiceTemplateModel> BuildTemplateModelAsync(
        long invoiceId,
        CancellationToken cancellationToken = default)
    {
        var (_, templateModel) = await RebuildTemplateModelAsync(invoiceId, cancellationToken);
        return templateModel;
    }

    /// <summary>A new invoice with its amounts, before its work days are settled and it is rendered.</summary>
    private sealed record BuiltInvoice(Invoice Invoice, Customer Customer, DayRates Rates, InvoiceTemplate Template);

    /// <summary>
    /// Builds the invoice in memory. Its number is the next one, not yet taken from the sequence:
    /// a draft does not use up a number, finalizing does.
    /// </summary>
    private async Task<BuiltInvoice> BuildAsync(GenerateInvoiceDto dto, CancellationToken cancellationToken)
    {
        var template = await GetTemplateAsync(dto.CustomerId, dto.InvoiceType, cancellationToken);
        var rates = await _rates.ResolveAsync(dto.CustomerId, dto.InvoiceType, dto.RateId, dto.WorkDays, cancellationToken);
        var customerTaxes = await _taxRepository.FindAsync(t => t.CustomerId == dto.CustomerId && t.IsActive, cancellationToken);

        InvoiceCalculator.EnsureBillable(dto, rates);

        var rate = rates.Default;
        var subtotal = InvoiceCalculator.Subtotal(dto, rates);
        var expensesTotal = InvoiceCalculator.ExpensesTotal(dto.Expenses, rate.Price.Currency);
        var customer = await _customerRepository.GetByIdAsync(dto.CustomerId, cancellationToken)
            ?? throw new NotFoundException($"Customer {dto.CustomerId} not found");
        var invoiceNumber = await _numbering.PeekAsync(customer, dto.IssueDate, cancellationToken);

        var (totalTax, taxLines) = _taxCalculationService.CalculateTaxes(customerTaxes, subtotal + expensesTotal);

        var invoice = new Invoice(
            dto.CustomerId,
            invoiceNumber,
            dto.InvoiceType,
            dto.IssueDate,
            subtotal,
            rate.Price.Currency)
        {
            RateId = dto.RateId,
            Hours = rate.Type == RateType.Hourly && dto.InvoiceType == InvoiceType.OneTime ? dto.Hours : null
        };

        if (dto.InvoiceType == InvoiceType.Monthly && dto.WorkDays != null)
        {
            // Count only "Worked" days for invoice calculation
            var workedDaysCount = dto.WorkDays.Count(wd => wd.DayType == DayType.Worked);
            invoice.SetMonthlyInvoiceDetails(dto.Year!.Value, dto.Month!.Value, workedDaysCount);

            if (dto.MonthlyReportTemplateId.HasValue)
                invoice.MonthlyReportTemplateId = dto.MonthlyReportTemplateId.Value;
        }

        if (dto.Expenses is { Count: > 0 })
        {
            foreach (var expenseDto in dto.Expenses)
            {
                invoice.Expenses.Add(new Expense(
                    dto.CustomerId,
                    expenseDto.Date,
                    expenseDto.Description,
                    new Money(expenseDto.Amount, expenseDto.Currency)));
            }

            invoice.AddExpenses(expensesTotal);
        }

        foreach (var taxLine in taxLines)
            invoice.TaxLines.Add(taxLine);

        invoice.AddTaxes(totalTax);

        return new BuiltInvoice(invoice, customer, rates, template);
    }

    private async Task<InvoiceDraft> RenderAsync(BuiltInvoice built, ICollection<WorkDayDto>? workDays, CancellationToken cancellationToken)
    {
        var model = InvoiceTemplateModelBuilder.Build(built.Invoice, built.Customer, built.Rates, workDays);
        built.Invoice.SetRenderedContent(await _templateRenderer.RenderAsync(built.Template.Content, model, cancellationToken));

        return new InvoiceDraft(built.Invoice, built.Customer, workDays?.ToList() ?? []);
    }

    /// <summary>The model an existing invoice renders from, rebuilt from the saved invoice, work days and rates.</summary>
    private async Task<(Invoice Invoice, InvoiceTemplateModel Model)> RebuildTemplateModelAsync(
        long invoiceId,
        CancellationToken cancellationToken)
    {
        // Use the specialized repository method to eagerly load related entities
        var invoice = await _invoiceRepository.GetByIdWithRelatedAsync(invoiceId, cancellationToken)
            ?? throw new NotFoundException($"Invoice {invoiceId} not found");

        var customer = await _customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken)
            ?? throw new NotFoundException($"Customer {invoice.CustomerId} not found");

        // The saved work days (with project allocations) the line items are built from
        var workDays = invoice is { Year: { } year, Month: { } month }
            ? await _workDays.LoadMonthAsync(invoice.CustomerId, year, month, cancellationToken)
            : null;

        var rates = await _rates.ResolveAsync(invoice.CustomerId, invoice.Type, invoice.RateId, workDays, cancellationToken);
        var templateModel = InvoiceTemplateModelBuilder.Build(invoice, customer, rates, workDays);
        foreach (var contributor in _contributors)
            templateModel = await contributor.ContributeAsync(invoice, templateModel, cancellationToken);
        return (invoice, templateModel);
    }

    private async Task<InvoiceTemplate> GetTemplateAsync(
        long customerId,
        InvoiceType invoiceType,
        CancellationToken cancellationToken)
    {
        // The customer's own active template, else the shared one
        var templates = await _templateRepository.FindAsync(
            t => (t.CustomerId == customerId || t.CustomerId == null) && t.InvoiceType == invoiceType && t.IsActive,
            cancellationToken);

        return ScopedTemplates.PickEffective(templates, customerId)
            ?? throw new DomainException($"No active template found for customer {customerId} and type {invoiceType}");
    }
}
