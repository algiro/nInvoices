using Microsoft.Extensions.Options;
using nInvoices.Application.DTOs;
using nInvoices.Application.Models;
using nInvoices.Core.Configuration;
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
    
    Task RegenerateInvoiceHtmlAsync(
        long invoiceId,
        CancellationToken cancellationToken = default);
}

public sealed class InvoiceGenerationService : IInvoiceGenerationService
{
    private readonly IRepository<InvoiceTemplate> _templateRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Rate> _rateRepository;
    private readonly IRepository<Tax> _taxRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRepository<WorkDay> _workDayRepository;
    private readonly IRepository<InvoiceSequence> _sequenceRepository;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IHtmlToPdfConverter _htmlToPdfConverter;
    private readonly ITaxCalculationService _taxCalculationService;
    private readonly InvoiceSettings _invoiceSettings;
    private readonly IUnitOfWork _unitOfWork;

    public InvoiceGenerationService(
        IRepository<InvoiceTemplate> templateRepository,
        IRepository<Customer> customerRepository,
        IRepository<Rate> rateRepository,
        IRepository<Tax> taxRepository,
        IInvoiceRepository invoiceRepository,
        IRepository<WorkDay> workDayRepository,
        IRepository<InvoiceSequence> sequenceRepository,
        ITemplateRenderer templateRenderer,
        IHtmlToPdfConverter htmlToPdfConverter,
        ITaxCalculationService taxCalculationService,
        IOptions<InvoiceSettings> invoiceSettings,
        IUnitOfWork unitOfWork)
    {
        _templateRepository = templateRepository;
        _customerRepository = customerRepository;
        _rateRepository = rateRepository;
        _taxRepository = taxRepository;
        _invoiceRepository = invoiceRepository;
        _workDayRepository = workDayRepository;
        _sequenceRepository = sequenceRepository;
        _templateRenderer = templateRenderer;
        _htmlToPdfConverter = htmlToPdfConverter;
        _taxCalculationService = taxCalculationService;
        _invoiceSettings = invoiceSettings.Value;
        _unitOfWork = unitOfWork;
    }

    public async Task<Invoice> GenerateInvoiceAsync(
        GenerateInvoiceDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var template = await GetTemplateAsync(dto.CustomerId, dto.InvoiceType, cancellationToken);
        var rate = await GetRateAsync(dto.CustomerId, dto.InvoiceType, cancellationToken);
        var customerTaxes = await GetCustomerTaxesAsync(dto.CustomerId, cancellationToken);

        // Validate hourly rate requirements
        if (rate.Type == RateType.Hourly && dto.InvoiceType == InvoiceType.Monthly)
        {
            if (dto.WorkDays == null || !dto.WorkDays.Any())
                throw new InvalidOperationException("Work days are required for hourly rate invoices.");
            
            var workedDaysWithoutHours = dto.WorkDays
                .Where(wd => wd.DayType == DayType.Worked && !wd.HoursWorked.HasValue)
                .ToList();
            
            if (workedDaysWithoutHours.Any())
                throw new InvalidOperationException($"Hours worked must be specified for all worked days when using hourly rates. Missing hours for {workedDaysWithoutHours.Count} day(s).");
        }

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
            // Count only "Worked" days for invoice calculation
            var workedDaysCount = dto.WorkDays.Count(wd => wd.DayType == DayType.Worked);
            invoice.SetMonthlyInvoiceDetails(dto.Year!.Value, dto.Month!.Value, workedDaysCount);
            
            // Store the template ID if provided
            if (dto.MonthlyReportTemplateId.HasValue)
            {
                invoice.MonthlyReportTemplateId = dto.MonthlyReportTemplateId.Value;
            }
            
            // Clear existing work days for this month and save new ones
            await ClearAndSaveWorkDaysAsync(dto.CustomerId, dto.Year!.Value, dto.Month!.Value, dto.WorkDays, cancellationToken);
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

        var templateModel = BuildTemplateModel(invoice, customer, dto, rate, dto.WorkDays);
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

    public async Task RegenerateInvoiceHtmlAsync(
        long invoiceId,
        CancellationToken cancellationToken = default)
    {
        // Use the specialized repository method to eagerly load related entities
        var invoice = await _invoiceRepository.GetByIdWithRelatedAsync(invoiceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice {invoiceId} not found");

        var customer = await _customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {invoice.CustomerId} not found");

        var template = await GetTemplateAsync(invoice.CustomerId, invoice.Type, cancellationToken);
        var rate = await GetRateAsync(invoice.CustomerId, invoice.Type, cancellationToken);

        // Load saved work days from the database for per-day line items
        ICollection<WorkDayDto>? workDayDtos = null;
        if (invoice.Year.HasValue && invoice.Month.HasValue)
        {
            var startDate = new DateOnly(invoice.Year.Value, invoice.Month.Value, 1);
            var endDate = new DateOnly(invoice.Year.Value, invoice.Month.Value, DateTime.DaysInMonth(invoice.Year.Value, invoice.Month.Value));
            var savedWorkDays = await _workDayRepository.FindAsync(
                wd => wd.CustomerId == invoice.CustomerId && wd.Date >= startDate && wd.Date <= endDate,
                cancellationToken);
            workDayDtos = savedWorkDays
                .Select(wd => new WorkDayDto(wd.Date, wd.DayType, wd.HoursWorked, wd.Notes))
                .ToList();
        }

        // Rebuild the template model from the existing invoice data
        var dto = new GenerateInvoiceDto
        {
            CustomerId = invoice.CustomerId,
            InvoiceType = invoice.Type,
            IssueDate = invoice.IssueDate,
            Year = invoice.Year,
            Month = invoice.Month,
            WorkDays = workDayDtos,
            Expenses = invoice.Expenses.Select(e => new ExpenseDto
            {
                Date = e.Date,
                Description = e.Description,
                Amount = e.Amount.Amount,
                Currency = e.Amount.Currency
            }).ToList()
        };

        var templateModel = BuildTemplateModel(invoice, customer, dto, rate, workDayDtos);
        var renderedHtml = await _templateRenderer.RenderAsync(template.Content, templateModel, cancellationToken);

        invoice.SetRenderedContent(renderedHtml);
        await _invoiceRepository.UpdateAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
        // For Monthly invoices, prefer Daily rate (daily rate × worked days)
        // Fall back to Monthly rate for fixed monthly billing, or Hourly rate (hourly rate × hours worked)
        var preferredRateType = invoiceType switch
        {
            InvoiceType.Monthly => RateType.Daily,
            InvoiceType.OneTime => RateType.Daily,
            _ => throw new ArgumentException($"Unsupported invoice type: {invoiceType}", nameof(invoiceType))
        };

        var rates = await _rateRepository.FindAsync(
            r => r.CustomerId == customerId && r.Type == preferredRateType,
            cancellationToken);

        var rate = rates.FirstOrDefault();
        if (rate == null && invoiceType == InvoiceType.Monthly)
        {
            // Fall back to Monthly rate
            rates = await _rateRepository.FindAsync(
                r => r.CustomerId == customerId && r.Type == RateType.Monthly,
                cancellationToken);
            rate = rates.FirstOrDefault();
            
            // If still no rate, try Hourly rate
            if (rate == null)
            {
                rates = await _rateRepository.FindAsync(
                    r => r.CustomerId == customerId && r.Type == RateType.Hourly,
                    cancellationToken);
                rate = rates.FirstOrDefault();
            }
        }

        if (rate == null)
            throw new InvalidOperationException($"No active rate found for customer {customerId}. Please add a Daily, Monthly, or Hourly rate.");

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
            // Fixed monthly rate
            InvoiceType.Monthly when rate.Type == RateType.Monthly =>
                rate.Price,
            
            // Hourly rate: sum of (hourly_rate × hours_worked per day)
            InvoiceType.Monthly when rate.Type == RateType.Hourly && dto.WorkDays != null =>
                new Money(
                    rate.Price.Amount * dto.WorkDays
                        .Where(wd => wd.DayType == DayType.Worked && wd.HoursWorked.HasValue)
                        .Sum(wd => wd.HoursWorked!.Value),
                    rate.Price.Currency),
            
            // Daily rate: daily_rate × number of worked days
            InvoiceType.Monthly when dto.WorkDays != null => 
                new Money(rate.Price.Amount * dto.WorkDays.Count(wd => wd.DayType == DayType.Worked), rate.Price.Currency),
            
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
        // Get or create the singleton sequence record (ID = 1)
        var sequence = await _sequenceRepository.GetByIdAsync(1, cancellationToken);
        if (sequence == null)
        {
            sequence = new InvoiceSequence(1);
            await _sequenceRepository.AddAsync(sequence, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Atomically increment the sequence
        var sequenceNumber = sequence.Increment();
        await _sequenceRepository.UpdateAsync(sequence, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Use configured pattern from appsettings
        var pattern = _invoiceSettings.NumberFormat;
        var date = issueDate.ToDateTime(TimeOnly.MinValue);

        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        var customerCode = customer?.FiscalId;

        return InvoiceNumber.Generate(pattern, sequenceNumber, date, customerCode);
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
        Rate rate,
        IEnumerable<WorkDayDto>? workDays = null)
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
            Date = invoice.IssueDate.ToDateTime(TimeOnly.MinValue),
            DueDate = invoice.DueDate?.ToDateTime(TimeOnly.MinValue),
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

            LineItems = BuildLineItems(invoice, dto, rate, workDays, customer.Locale),
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
    /// For Daily/Hourly rates: one line item per worked day (date as description).
    /// For Monthly rates: a single consolidated line.
    /// Expenses are added as separate line items.
    /// </summary>
    private List<LineItemTemplateModel> BuildLineItems(
        Invoice invoice,
        GenerateInvoiceDto dto,
        Rate rate,
        IEnumerable<WorkDayDto>? workDays,
        string locale = "en-US")
    {
        var lineItems = new List<LineItemTemplateModel>();

        if (dto.InvoiceType == InvoiceType.Monthly && invoice.WorkedDays.HasValue)
        {
            var workedDaysList = workDays?
                .Where(wd => wd.DayType == DayType.Worked)
                .OrderBy(wd => wd.Date)
                .ToList();

            if (workedDaysList is { Count: > 0 } && rate.Type is RateType.Daily or RateType.Hourly)
            {
                // One line item per worked day
                foreach (var wd in workedDaysList)
                {
                    var culture = System.Globalization.CultureInfo.GetCultureInfo(locale);
                    var description = wd.Date.ToString("d", culture);
                    var quantity = rate.Type == RateType.Hourly ? wd.HoursWorked ?? 0m : 1m;
                    var amount = rate.Price.Amount * quantity;

                    lineItems.Add(new LineItemTemplateModel
                    {
                        Description = description,
                        Quantity = quantity,
                        Rate = rate.Price.Amount,
                        Amount = amount
                    });
                }
            }
            else
            {
                // Monthly rate: single consolidated line item
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
        }
        else if (dto.InvoiceType == InvoiceType.OneTime)
        {
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
    
    /// <summary>
    /// Clears all work days for the specified month and saves new ones.
    /// This ensures the calendar is accurate for the month being invoiced.
    /// </summary>
    private async Task ClearAndSaveWorkDaysAsync(
        long customerId,
        int year,
        int month,
        ICollection<WorkDayDto> workDayDtos,
        CancellationToken cancellationToken)
    {
        if (workDayDtos == null || workDayDtos.Count == 0)
            return;

        // Delete all work days for this customer in this month
        var startDate = new DateOnly(year, month, 1);
        var endDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        
        var existingWorkDays = await _workDayRepository.FindAsync(
            wd => wd.CustomerId == customerId && wd.Date >= startDate && wd.Date <= endDate,
            cancellationToken);

        foreach (var existing in existingWorkDays)
        {
            await _workDayRepository.DeleteAsync(existing, cancellationToken);
        }

        // Insert new work days
        foreach (var dto in workDayDtos)
        {
            var workDay = new WorkDay(customerId, dto.Date, dto.DayType, dto.HoursWorked, dto.Notes);
            await _workDayRepository.AddAsync(workDay, cancellationToken);
        }
    }
    
    /// <summary>
    /// Saves or updates work days for a customer.
    /// Existing work days for the same dates are updated with new day types.
    /// </summary>
    private async Task SaveWorkDaysAsync(
        long customerId,
        ICollection<WorkDayDto> workDayDtos,
        CancellationToken cancellationToken)
    {
        if (workDayDtos == null || workDayDtos.Count == 0)
            return;

        var dates = workDayDtos.Select(wd => wd.Date).ToList();
        var existingWorkDays = await _workDayRepository.FindAsync(
            wd => wd.CustomerId == customerId && dates.Contains(wd.Date),
            cancellationToken);

        var existingDict = existingWorkDays.ToDictionary(wd => wd.Date);

        foreach (var dto in workDayDtos)
        {
            if (existingDict.TryGetValue(dto.Date, out var existing))
            {
                // Update existing work day
                existing.UpdateDayType(dto.DayType);
                existing.UpdateHoursWorked(dto.HoursWorked);
                if (dto.Notes != null)
                {
                    existing.UpdateNotes(dto.Notes);
                }
                await _workDayRepository.UpdateAsync(existing, cancellationToken);
            }
            else
            {
                // Create new work day
                var workDay = new WorkDay(customerId, dto.Date, dto.DayType, dto.HoursWorked, dto.Notes);
                await _workDayRepository.AddAsync(workDay, cancellationToken);
            }
        }
    }
}
