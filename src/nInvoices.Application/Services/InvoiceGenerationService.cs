using nInvoices.Application.DTOs;
using nInvoices.Application.Models;
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
    
    Task<byte[]> GenerateInvoicePdfAsync(
        long invoiceId,
        CancellationToken cancellationToken = default);
}

public sealed class InvoiceGenerationService : IInvoiceGenerationService
{
    private readonly IRepository<InvoiceTemplate> _templateRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Rate> _rateRepository;
    private readonly IRepository<Tax> _taxRepository;
    private readonly IRepository<Invoice> _invoiceRepository;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IHtmlToPdfConverter _htmlToPdfConverter;
    private readonly ITaxCalculationService _taxCalculationService;

    public InvoiceGenerationService(
        IRepository<InvoiceTemplate> templateRepository,
        IRepository<Customer> customerRepository,
        IRepository<Rate> rateRepository,
        IRepository<Tax> taxRepository,
        IRepository<Invoice> invoiceRepository,
        ITemplateRenderer templateRenderer,
        IHtmlToPdfConverter htmlToPdfConverter,
        ITaxCalculationService taxCalculationService)
    {
        _templateRepository = templateRepository;
        _customerRepository = customerRepository;
        _rateRepository = rateRepository;
        _taxRepository = taxRepository;
        _invoiceRepository = invoiceRepository;
        _templateRenderer = templateRenderer;
        _htmlToPdfConverter = htmlToPdfConverter;
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
        var expensesTotal = CalculateExpensesTotal(dto.Expenses, rate.Price.Currency);
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

        // NEW: Use Scriban template renderer instead of old template engine
        var customer = await _customerRepository.GetByIdAsync(dto.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {dto.CustomerId} not found");

        var templateModel = BuildTemplateModel(invoice, customer, dto, rate);
        var renderedHtml = await _templateRenderer.RenderAsync(template.Content, templateModel, cancellationToken);

        invoice.SetRenderedContent(renderedHtml);

        return invoice;
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(
        long invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found");

        if (string.IsNullOrEmpty(invoice.RenderedContent))
        {
            throw new InvalidOperationException($"Invoice {invoiceId} has no rendered content. Generate invoice first.");
        }

        // NEW: Convert HTML to PDF using QuestPDF
        var pdfBytes = await _htmlToPdfConverter.ConvertAsync(invoice.RenderedContent, cancellationToken);
        
        return pdfBytes;
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
        // For Monthly invoices, we use Daily rate when tracking worked days
        // For OneTime invoices, we use Daily rate
        // Monthly rate would be used for fixed monthly billing without day tracking
        var rateType = invoiceType switch
        {
            InvoiceType.Monthly => RateType.Daily,  // Changed: Monthly invoices use daily rate × worked days
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

    private Money CalculateExpensesTotal(ICollection<ExpenseDto>? expenses, string defaultCurrency)
    {
        if (expenses == null || expenses.Count == 0)
            return Money.Zero(defaultCurrency);

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

    /// <summary>
    /// Builds the template model for rendering.
    /// This model is passed to Scriban and all properties become template variables.
    /// Following the Builder pattern for complex object construction.
    /// </summary>
    private InvoiceTemplateModel BuildTemplateModel(
        Invoice invoice,
        Customer customer,
        GenerateInvoiceDto dto,
        Rate rate)
    {
        // Calculate monthly-specific values upfront
        int? workedDays = null;
        int? monthNumber = null;
        string? monthDescription = null;
        decimal? monthlyRate = null;
        decimal? totalExpenses = null;

        if (dto.InvoiceType == InvoiceType.Monthly && invoice.WorkedDays.HasValue)
        {
            workedDays = invoice.WorkedDays.Value;
            monthNumber = invoice.Month ?? DateTime.UtcNow.Month;
            monthlyRate = rate.Price.Amount;
            totalExpenses = invoice.TotalExpenses.Amount;

            if (invoice.Month.HasValue && invoice.Year.HasValue)
            {
                var date = new DateTime(invoice.Year.Value, invoice.Month.Value, 1);
                monthDescription = date.ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
            }
        }

        // Build model with object initializer
        var model = new InvoiceTemplateModel
        {
            InvoiceNumber = invoice.Number.ToString(),
            InvoiceType = invoice.Type.ToString(),
            Date = invoice.IssueDate.ToString("yyyy-MM-dd"),
            DueDate = invoice.DueDate?.ToString("yyyy-MM-dd"),
            Currency = rate.Price.Currency,

            Customer = new CustomerTemplateModel
            {
                Name = customer.Name,
                FiscalId = customer.FiscalId,
                Address = new AddressTemplateModel
                {
                    Street = customer.Address.Street,
                    City = customer.Address.City,
                    PostalCode = customer.Address.ZipCode,  // ZipCode → PostalCode
                    Country = customer.Address.Country
                }
            },

            LineItems = BuildLineItems(invoice, dto, rate),
            Taxes = invoice.TaxLines.Select(t => new TaxTemplateModel
            {
                Description = t.Description,
                Rate = t.Rate,
                Amount = t.TaxAmount.Amount  // TaxAmount, not Amount
            }).ToList(),

            Subtotal = invoice.Subtotal.Amount,
            TotalTax = invoice.TotalTaxes.Amount,
            Total = invoice.Total.Amount,

            // Monthly invoice specific data
            WorkedDays = workedDays,
            MonthNumber = monthNumber,
            MonthDescription = monthDescription,
            MonthlyRate = monthlyRate,
            TotalExpenses = totalExpenses
        };

        return model;
    }

    /// <summary>
    /// Builds line items for the template.
    /// For monthly invoices with worked days, creates a single consolidated line.
    /// For one-time invoices, could be extended to support multiple line items.
    /// </summary>
    private List<LineItemTemplateModel> BuildLineItems(
        Invoice invoice,
        GenerateInvoiceDto dto,
        Rate rate)
    {
        var lineItems = new List<LineItemTemplateModel>();

        if (dto.InvoiceType == InvoiceType.Monthly && invoice.WorkedDays.HasValue)
        {
            // Single line item for monthly invoices
            var description = $"Professional Services - {invoice.WorkedDays.Value} days";
            if (invoice.Month.HasValue && invoice.Year.HasValue)
            {
                var date = new DateTime(invoice.Year.Value, invoice.Month.Value, 1);
                var monthName = date.ToString("MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
                description = $"Professional Services for {monthName}";
            }

            lineItems.Add(new LineItemTemplateModel
            {
                Description = description,
                Quantity = invoice.WorkedDays.Value,
                Rate = rate.Price.Amount,
                Amount = invoice.Subtotal.Amount
            });
        }
        else if (dto.InvoiceType == InvoiceType.OneTime)
        {
            // One-time invoice line item
            lineItems.Add(new LineItemTemplateModel
            {
                Description = "Professional Services",
                Quantity = 1,
                Rate = rate.Price.Amount,
                Amount = invoice.Subtotal.Amount
            });
        }

        // Add expenses as separate line items
        if (invoice.Expenses.Count > 0)
        {
            foreach (var expense in invoice.Expenses)
            {
                lineItems.Add(new LineItemTemplateModel
                {
                    Description = $"Expense: {expense.Description}",
                    Quantity = 1,
                    Rate = expense.Amount.Amount,
                    Amount = expense.Amount.Amount
                });
            }
        }

        return lineItems;
    }
}
