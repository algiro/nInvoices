using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Services;

/// <summary>
/// Service responsible for invoice generation orchestration.
/// Brings together: templates, rates, taxes, and rendering.
/// Follows the Facade pattern - simplifies complex subsystem interactions.
/// </summary>
public interface IInvoiceGenerationService
{
    Task<Invoice> GenerateInvoiceAsync(
        Customer customer,
        InvoiceType invoiceType,
        int year,
        int month,
        IEnumerable<WorkDayDto> workDays,
        IEnumerable<ExpenseDto> expenses,
        string invoiceNumberFormat,
        CancellationToken cancellationToken = default);
}

public sealed class InvoiceGenerationService : IInvoiceGenerationService
{
    private readonly IRepository<InvoiceTemplate> _templateRepository;
    private readonly IRepository<Rate> _rateRepository;
    private readonly IRepository<Tax> _taxRepository;
    private readonly ITemplateEngine _templateEngine;
    private readonly ITaxCalculationService _taxCalculationService;

    public InvoiceGenerationService(
        IRepository<InvoiceTemplate> templateRepository,
        IRepository<Rate> rateRepository,
        IRepository<Tax> taxRepository,
        ITemplateEngine templateEngine,
        ITaxCalculationService taxCalculationService)
    {
        _templateRepository = templateRepository;
        _rateRepository = rateRepository;
        _taxRepository = taxRepository;
        _templateEngine = templateEngine;
        _taxCalculationService = taxCalculationService;
    }

    public async Task<Invoice> GenerateInvoiceAsync(
        Customer customer,
        InvoiceType invoiceType,
        int year,
        int month,
        IEnumerable<WorkDayDto> workDays,
        IEnumerable<ExpenseDto> expenses,
        string invoiceNumberFormat,
        CancellationToken cancellationToken = default)
    {
        var template = await FindTemplateAsync(customer.Id, invoiceType, cancellationToken);
        if (template == null)
            throw new InvalidOperationException($"No active template found for customer {customer.Id} and type {invoiceType}");

        var rate = await FindApplicableRateAsync(customer.Id, invoiceType, cancellationToken);
        if (rate == null)
            throw new InvalidOperationException($"No rate found for customer {customer.Id}");

        var subtotal = CalculateSubtotal(workDays, rate);
        var totalExpenses = CalculateTotalExpenses(expenses);

        var invoiceNumber = GenerateInvoiceNumber(invoiceNumberFormat, year, month, customer);
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var invoice = new Invoice(
            customer.Id,
            invoiceType,
            invoiceNumber,
            issueDate,
            subtotal,
            totalExpenses);

        foreach (var expense in expenses)
        {
            var expenseEntity = new Expense(
                invoice.Id,
                expense.Description,
                new Money(expense.Amount.Amount, expense.Amount.Currency));
            invoice.Expenses.Add(expenseEntity);
        }

        foreach (var workDay in workDays)
        {
            var workDayEntity = new WorkDay(invoice.Id, workDay.Date, workDay.Notes);
            invoice.WorkDays.Add(workDayEntity);
        }

        var taxes = await FindApplicableTaxesAsync(customer.Id, cancellationToken);
        var (totalTaxes, taxLines) = _taxCalculationService.CalculateTaxes(taxes, subtotal);

        foreach (var taxLine in taxLines)
        {
            invoice.TaxLines.Add(taxLine);
        }

        invoice.RecalculateTotals();

        var templateData = BuildTemplateData(invoice, customer, workDays, expenses, year, month);
        var renderedContent = _templateEngine.Render(template.Content, templateData);
        invoice.RenderedContent = renderedContent;

        return invoice;
    }

    private async Task<InvoiceTemplate?> FindTemplateAsync(
        long customerId,
        InvoiceType invoiceType,
        CancellationToken cancellationToken)
    {
        var templates = await _templateRepository.FindAsync(
            t => t.CustomerId == customerId && t.InvoiceType == invoiceType && t.IsActive,
            cancellationToken);
        return templates.FirstOrDefault();
    }

    private async Task<Rate?> FindApplicableRateAsync(
        long customerId,
        InvoiceType invoiceType,
        CancellationToken cancellationToken)
    {
        var rateType = invoiceType switch
        {
            InvoiceType.Monthly => RateType.Monthly,
            InvoiceType.Daily => RateType.Daily,
            InvoiceType.Hourly => RateType.Hourly,
            _ => RateType.OneTime
        };

        var rates = await _rateRepository.FindAsync(
            r => r.CustomerId == customerId && r.Type == rateType,
            cancellationToken);
        return rates.FirstOrDefault();
    }

    private async Task<List<Tax>> FindApplicableTaxesAsync(
        long customerId,
        CancellationToken cancellationToken)
    {
        var taxes = await _taxRepository.FindAsync(
            t => t.CustomerId == customerId && t.IsActive,
            cancellationToken);
        return taxes.OrderBy(t => t.Order).ToList();
    }

    private static Money CalculateSubtotal(IEnumerable<WorkDayDto> workDays, Rate rate)
    {
        var workedDaysCount = workDays.Count();
        
        return rate.Type switch
        {
            RateType.Daily => rate.Price * workedDaysCount,
            RateType.Monthly => rate.Price,
            RateType.Hourly => rate.Price * (workedDaysCount * 8),
            _ => rate.Price
        };
    }

    private static Money CalculateTotalExpenses(IEnumerable<ExpenseDto> expenses)
    {
        if (!expenses.Any())
            return new Money(0, "USD");

        var first = expenses.First();
        var total = new Money(0, first.Amount.Currency);

        foreach (var expense in expenses)
        {
            total += new Money(expense.Amount.Amount, expense.Amount.Currency);
        }

        return total;
    }

    private static string GenerateInvoiceNumber(
        string format,
        int year,
        int month,
        Customer customer)
    {
        return format
            .Replace("{YEAR}", year.ToString())
            .Replace("{MONTH}", month.ToString("D2"))
            .Replace("{NUMBER:000}", "001")
            .Replace("{CUSTOMER}", customer.Name.Replace(" ", ""));
    }

    private static Dictionary<string, object?> BuildTemplateData(
        Invoice invoice,
        Customer customer,
        IEnumerable<WorkDayDto> workDays,
        IEnumerable<ExpenseDto> expenses,
        int year,
        int month)
    {
        var monthNames = new Dictionary<string, string>
        {
            ["EN"] = new DateTime(year, month, 1).ToString("MMMM", new System.Globalization.CultureInfo("en-US")),
            ["ES"] = new DateTime(year, month, 1).ToString("MMMM", new System.Globalization.CultureInfo("es-ES")),
            ["FR"] = new DateTime(year, month, 1).ToString("MMMM", new System.Globalization.CultureInfo("fr-FR")),
            ["IT"] = new DateTime(year, month, 1).ToString("MMMM", new System.Globalization.CultureInfo("it-IT")),
            ["DE"] = new DateTime(year, month, 1).ToString("MMMM", new System.Globalization.CultureInfo("de-DE"))
        };

        return new Dictionary<string, object?>
        {
            ["InvoiceNumber"] = invoice.InvoiceNumber,
            ["IssueDate"] = invoice.IssueDate.ToString("yyyy-MM-dd"),
            ["DueDate"] = invoice.DueDate?.ToString("yyyy-MM-dd"),
            ["Customer"] = new Dictionary<string, object?>
            {
                ["Name"] = customer.Name,
                ["FiscalId"] = customer.FiscalId,
                ["Address"] = new Dictionary<string, object?>
                {
                    ["Street"] = customer.Address.Street,
                    ["HouseNumber"] = customer.Address.HouseNumber,
                    ["City"] = customer.Address.City,
                    ["ZipCode"] = customer.Address.ZipCode,
                    ["Country"] = customer.Address.Country,
                    ["State"] = customer.Address.State
                }
            },
            ["WorkedDays"] = workDays.Count(),
            ["MonthNumber"] = month,
            ["Year"] = year,
            ["MonthDescription"] = monthNames,
            ["Subtotal"] = $"{invoice.Subtotal.Amount:F2} {invoice.Subtotal.Currency}",
            ["TotalExpenses"] = $"{invoice.TotalExpenses.Amount:F2} {invoice.TotalExpenses.Currency}",
            ["TotalTaxes"] = $"{invoice.TotalTaxes.Amount:F2} {invoice.TotalTaxes.Currency}",
            ["Total"] = $"{invoice.Total.Amount:F2} {invoice.Total.Currency}",
            ["Expenses"] = expenses.Select(e => new Dictionary<string, object?>
            {
                ["Description"] = e.Description,
                ["Amount"] = $"{e.Amount.Amount:F2} {e.Amount.Currency}"
            }).ToList()
        };
    }
}