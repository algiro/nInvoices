using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

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
}

public sealed class InvoiceGenerationService : IInvoiceGenerationService
{
    private readonly IRepository<InvoiceTemplate> _templateRepository;
    private readonly IRepository<Rate> _rateRepository;
    private readonly IRepository<Tax> _taxRepository;
    private readonly IRepository<Invoice> _invoiceRepository;
    private readonly ITemplateEngine _templateEngine;
    private readonly ITaxCalculationService _taxCalculationService;

    public InvoiceGenerationService(
        IRepository<InvoiceTemplate> templateRepository,
        IRepository<Rate> rateRepository,
        IRepository<Tax> taxRepository,
        IRepository<Invoice> invoiceRepository,
        ITemplateEngine templateEngine,
        ITaxCalculationService taxCalculationService)
    {
        _templateRepository = templateRepository;
        _rateRepository = rateRepository;
        _taxRepository = taxRepository;
        _invoiceRepository = invoiceRepository;
        _templateEngine = templateEngine;
        _taxCalculationService = taxCalculationService;
    }

    public async Task<Invoice> GenerateInvoiceAsync(
        GenerateInvoiceDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var template = await GetTemplateAsync(dto.CustomerId, dto.InvoiceType, cancellationToken);
        var rate = await GetRateAsync(dto.CustomerId, dto.InvoiceType, cancellationToken);
        var customerTaxes = await GetCustomerTaxesAsync(dto.CustomerId, cancellationToken);

        var subtotal = CalculateSubtotal(dto, rate);
        var expensesTotal = CalculateExpensesTotal(dto.Expenses);
        var invoiceNumber = await GenerateInvoiceNumberAsync(
            dto.CustomerId,
            dto.InvoiceType,
            dto.IssueDate,
            cancellationToken);

        var (totalTax, taxLines) = _taxCalculationService.CalculateTaxes(customerTaxes, subtotal + expensesTotal);

        var invoice = new Invoice(
            dto.CustomerId,
            invoiceNumber,
            dto.InvoiceType,
            dto.IssueDate,
            subtotal,
            rate.Price.Currency);

        if (dto.InvoiceType == InvoiceType.Monthly && dto.WorkDays != null)
        {
            invoice.SetMonthlyInvoiceDetails(dto.Year!.Value, dto.Month!.Value, dto.WorkDays.Count);
        }

        if (dto.Expenses != null && dto.Expenses.Count > 0)
        {
            foreach (var expenseDto in dto.Expenses)
            {
                var expense = new Expense(
                    dto.CustomerId,
                    expenseDto.Date,
                    expenseDto.Description,
                    new Money(expenseDto.Amount, expenseDto.Currency));
                invoice.Expenses.Add(expense);
            }

            invoice.AddExpenses(expensesTotal);
        }

        foreach (var taxLine in taxLines)
        {
            invoice.TaxLines.Add(taxLine);
        }

        invoice.AddTaxes(totalTax);

        var templateData = BuildTemplateData(invoice, dto, rate);
        var renderedContent = _templateEngine.Render(template.Content, templateData);

        invoice.SetRenderedContent(renderedContent);

        return invoice;
    }

    private async Task<InvoiceTemplate> GetTemplateAsync(
        long customerId,
        InvoiceType invoiceType,
        CancellationToken cancellationToken)
    {
        var templates = await _templateRepository.FindAsync(
            t => t.CustomerId == customerId && t.InvoiceType == invoiceType && t.IsActive,
            cancellationToken);

        var template = templates.FirstOrDefault();
        if (template == null)
            throw new InvalidOperationException($"No active template found for customer {customerId} and type {invoiceType}");

        return template;
    }

    private async Task<Rate> GetRateAsync(
        long customerId,
        InvoiceType invoiceType,
        CancellationToken cancellationToken)
    {
        var rateType = invoiceType switch
        {
            InvoiceType.Monthly => RateType.Monthly,
            InvoiceType.OneTime => RateType.Daily,
            _ => throw new ArgumentException($"Unsupported invoice type: {invoiceType}", nameof(invoiceType))
        };

        var rates = await _rateRepository.FindAsync(
            r => r.CustomerId == customerId && r.Type == rateType,
            cancellationToken);

        var rate = rates.FirstOrDefault();
        if (rate == null)
            throw new InvalidOperationException($"No {rateType} rate found for customer {customerId}");

        return rate;
    }

    private async Task<IEnumerable<Tax>> GetCustomerTaxesAsync(
        long customerId,
        CancellationToken cancellationToken)
    {
        return await _taxRepository.FindAsync(
            t => t.CustomerId == customerId && t.IsActive,
            cancellationToken);
    }

    private Money CalculateSubtotal(GenerateInvoiceDto dto, Rate rate)
    {
        return dto.InvoiceType switch
        {
            InvoiceType.Monthly when dto.WorkDays != null => 
                new Money(rate.Price.Amount * dto.WorkDays.Count, rate.Price.Currency),
            InvoiceType.OneTime => rate.Price,
            _ => throw new InvalidOperationException($"Cannot calculate subtotal for invoice type {dto.InvoiceType}")
        };
    }

    private Money CalculateExpensesTotal(ICollection<ExpenseDto>? expenses)
    {
        if (expenses == null || expenses.Count == 0)
            return Money.Zero("EUR");

        var firstExpense = expenses.First();
        var currency = firstExpense.Currency;
        var total = expenses.Sum(e => e.Amount);

        return new Money(total, currency);
    }

    private async Task<InvoiceNumber> GenerateInvoiceNumberAsync(
        long customerId,
        InvoiceType invoiceType,
        DateOnly issueDate,
        CancellationToken cancellationToken)
    {
        var year = issueDate.Year;
        var month = issueDate.Month;

        var existingInvoices = await _invoiceRepository.FindAsync(
            i => i.CustomerId == customerId && i.Type == invoiceType && i.Year == year,
            cancellationToken);

        var sequenceNumber = existingInvoices.Count() + 1;

        var pattern = "INV-{YEAR}-{MONTH:00}-{NUMBER:000}";
        var date = issueDate.ToDateTime(TimeOnly.MinValue);

        return InvoiceNumber.Generate(pattern, sequenceNumber, date, customerId.ToString());
    }

    private Dictionary<string, object> BuildTemplateData(
        Invoice invoice,
        GenerateInvoiceDto dto,
        Rate rate)
    {
        var data = new Dictionary<string, object>
        {
            ["InvoiceNumber"] = invoice.Number.ToString(),
            ["Date"] = invoice.IssueDate.ToString("yyyy-MM-dd"),
            ["IssueDate"] = invoice.IssueDate.ToString("yyyy-MM-dd"),
            ["DueDate"] = invoice.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ["Subtotal"] = invoice.Subtotal.ToString(),
            ["TotalExpenses"] = invoice.TotalExpenses.ToString(),
            ["TotalExcludingExpenses"] = invoice.Subtotal.ToString(),
            ["TotalIncludingExpenses"] = (invoice.Subtotal + invoice.TotalExpenses).ToString(),
            ["TotalTaxes"] = invoice.TotalTaxes.ToString(),
            ["Total"] = invoice.Total.ToString(),
            ["Currency"] = rate.Price.Currency,
            ["Notes"] = invoice.Notes ?? string.Empty
        };

        if (dto.InvoiceType == InvoiceType.Monthly && invoice.WorkedDays.HasValue)
        {
            data["WorkedDays"] = invoice.WorkedDays.Value;
            data["Year"] = invoice.Year ?? DateTime.UtcNow.Year;
            data["Month"] = invoice.Month ?? DateTime.UtcNow.Month;
            data["MonthNumber"] = invoice.Month ?? DateTime.UtcNow.Month;
            data["MonthlyRate"] = rate.Price.ToString();

            if (invoice.Month.HasValue && invoice.Year.HasValue)
            {
                var monthDescriptions = new Dictionary<string, string>
                {
                    ["EN"] = new DateTime(invoice.Year.Value, invoice.Month.Value, 1)
                        .ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("en-US")),
                    ["ES"] = new DateTime(invoice.Year.Value, invoice.Month.Value, 1)
                        .ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("es-ES")),
                    ["IT"] = new DateTime(invoice.Year.Value, invoice.Month.Value, 1)
                        .ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("it-IT")),
                    ["FR"] = new DateTime(invoice.Year.Value, invoice.Month.Value, 1)
                        .ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")),
                    ["DE"] = new DateTime(invoice.Year.Value, invoice.Month.Value, 1)
                        .ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))
                };

                data["MonthDescription"] = monthDescriptions;
            }
        }

        return data;
    }
}
