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
    private readonly IWorkDayRepository _workDayRepository;
    private readonly IRepository<InvoiceSequence> _sequenceRepository;
    private readonly IProjectResolver _projectResolver;
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
        IWorkDayRepository workDayRepository,
        IRepository<InvoiceSequence> sequenceRepository,
        IProjectResolver projectResolver,
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
        _projectResolver = projectResolver;
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
                .Where(wd => wd.DayType == DayType.Worked && GetDayHours(wd) <= 0)
                .ToList();

            if (workedDaysWithoutHours.Any())
                throw new InvalidOperationException(
                    $"Hours must be specified for all worked days when using hourly rates " +
                    $"(via a project allocation or the day's hours). Missing hours for {workedDaysWithoutHours.Count} day(s).");
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

        var workDays = dto.WorkDays;

        if (dto.InvoiceType == InvoiceType.Monthly && workDays != null)
        {
            // Count only "Worked" days for invoice calculation
            var workedDaysCount = workDays.Count(wd => wd.DayType == DayType.Worked);
            invoice.SetMonthlyInvoiceDetails(dto.Year!.Value, dto.Month!.Value, workedDaysCount);

            // Store the template ID if provided
            if (dto.MonthlyReportTemplateId.HasValue)
            {
                invoice.MonthlyReportTemplateId = dto.MonthlyReportTemplateId.Value;
            }

            // Clear existing work days for this month and save new ones.
            // Returns the work days with project names normalized to their canonical form.
            workDays = await ClearAndSaveWorkDaysAsync(
                dto.CustomerId, dto.Year!.Value, dto.Month!.Value, workDays, cancellationToken);
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

        var templateModel = BuildTemplateModel(invoice, customer, dto, rate, workDays);
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

        // Load saved work days (with project allocations) from the database for line items
        ICollection<WorkDayDto>? workDayDtos = null;
        if (invoice.Year.HasValue && invoice.Month.HasValue)
        {
            var savedWorkDays = await _workDayRepository.GetByCustomerAndMonthAsync(
                invoice.CustomerId, invoice.Year.Value, invoice.Month.Value, cancellationToken);
            workDayDtos = savedWorkDays
                .Select(wd => new WorkDayDto(
                    wd.Date,
                    wd.DayType,
                    wd.HoursWorked,
                    wd.Notes,
                    wd.Projects
                        .Select(p => new WorkDayProjectDto(p.Project.Name, p.Hours, p.ProjectId))
                        .ToList()))
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
            
            // Hourly rate: sum of (hourly_rate × hours per day), hours taken from
            // project allocations when present, otherwise the day's HoursWorked
            InvoiceType.Monthly when rate.Type == RateType.Hourly && dto.WorkDays != null =>
                new Money(
                    rate.Price.Amount * dto.WorkDays
                        .Where(wd => wd.DayType == DayType.Worked)
                        .Sum(GetDayHours),
                    rate.Price.Currency),
            
            // Daily rate: daily_rate × effective days (partial days counted as hoursWorked/8)
            InvoiceType.Monthly when dto.WorkDays != null =>
                new Money(
                    rate.Price.Amount * dto.WorkDays
                        .Where(wd => wd.DayType == DayType.Worked)
                        .Sum(wd => (wd.HoursWorked ?? 8m) / 8m),
                    rate.Price.Currency),
            
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
            ProjectSummary = BuildProjectSummary(workDays, rate),
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
            TotalExpenses = totalExpenses,
            WorkedDayItems = dto.InvoiceType == InvoiceType.Monthly && workDays != null
                ? workDays
                    .Where(wd => wd.DayType == DayType.Worked)
                    .OrderBy(wd => wd.Date)
                    .Select(wd => new WorkedDayTemplateModel
                    {
                        Date = wd.Date.ToString("d", System.Globalization.CultureInfo.GetCultureInfo(customer.Locale)),
                        Hours = wd.HoursWorked ?? 8m
                    })
                    .ToList()
                : []
        };

        return model;
    }

    /// <summary>
    /// Hours recorded for a work day: the sum of its project allocations when present,
    /// otherwise the day-level <see cref="WorkDayDto.HoursWorked"/> (0 when neither is set).
    /// </summary>
    private static decimal GetDayHours(WorkDayDto workDay)
    {
        if (workDay.Projects is { Count: > 0 })
            return workDay.Projects.Where(p => p.Hours > 0).Sum(p => p.Hours);

        return workDay.HoursWorked ?? 0m;
    }

    /// <summary>
    /// Builds line items for the template.
    /// For Daily/Hourly rates with project allocations: one line item per project.
    /// For Daily/Hourly rates without allocations: one line item per worked day (date as description).
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

            var perDayRate = rate.Type is RateType.Daily or RateType.Hourly;
            var hasAllocations = workedDaysList?.Any(wd => wd.Projects is { Count: > 0 }) == true;

            if (workedDaysList is { Count: > 0 } && perDayRate && hasAllocations)
            {
                // One line item per project (time grouped across the month)
                lineItems.AddRange(BuildProjectLineItems(workedDaysList, rate));
            }
            else if (workedDaysList is { Count: > 0 } && perDayRate)
            {
                // One line item per worked day
                foreach (var wd in workedDaysList)
                {
                    var culture = System.Globalization.CultureInfo.GetCultureInfo(locale);
                    var description = wd.Date.ToString("d", culture);
                    var quantity = rate.Type == RateType.Hourly ? GetDayHours(wd) : 1m;
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
    /// One billed line per project. For hourly rates the quantity is total hours;
    /// for daily rates each day is split across its projects pro-rata by hours, so the
    /// quantities still sum to the worked-day count. Worked days with no allocation are
    /// grouped into a single "Unassigned" line.
    /// </summary>
    private static IEnumerable<LineItemTemplateModel> BuildProjectLineItems(
        List<WorkDayDto> workedDays,
        Rate rate)
    {
        var order = new List<string>();
        var byProject = new Dictionary<string, (decimal Hours, decimal Days, decimal Amount)>(StringComparer.OrdinalIgnoreCase);
        var unassignedDays = 0m;
        var unassignedHours = 0m;

        foreach (var wd in workedDays)
        {
            var allocations = wd.Projects?.Where(p => p.Hours > 0).ToList() ?? [];

            if (allocations.Count == 0)
            {
                unassignedDays += 1m;
                unassignedHours += GetDayHours(wd);
                continue;
            }

            var dayHours = allocations.Sum(a => a.Hours);

            foreach (var allocation in allocations)
            {
                var name = allocation.ProjectName.Trim();
                if (!byProject.TryGetValue(name, out var acc))
                {
                    order.Add(name);
                    acc = (0m, 0m, 0m);
                }

                var dayFraction = dayHours > 0 ? allocation.Hours / dayHours : 0m;
                var amount = rate.Type == RateType.Hourly
                    ? rate.Price.Amount * allocation.Hours
                    : rate.Price.Amount * dayFraction;

                byProject[name] = (
                    acc.Hours + allocation.Hours,
                    acc.Days + dayFraction,
                    acc.Amount + amount);
            }
        }

        foreach (var name in order)
        {
            var acc = byProject[name];
            yield return new LineItemTemplateModel
            {
                Description = name,
                Quantity = rate.Type == RateType.Hourly ? acc.Hours : decimal.Round(acc.Days, 3),
                Rate = rate.Price.Amount,
                Amount = acc.Amount
            };
        }

        if (unassignedDays > 0m || unassignedHours > 0m)
        {
            yield return new LineItemTemplateModel
            {
                Description = "Unassigned",
                Quantity = rate.Type == RateType.Hourly ? unassignedHours : unassignedDays,
                Rate = rate.Price.Amount,
                Amount = rate.Type == RateType.Hourly
                    ? rate.Price.Amount * unassignedHours
                    : rate.Price.Amount * unassignedDays
            };
        }
    }

    /// <summary>
    /// Per-project totals for the billed month. <see cref="ProjectSummaryTemplateModel.Amount"/>
    /// is null for a flat monthly rate (time is not billed per project).
    /// </summary>
    private static List<ProjectSummaryTemplateModel> BuildProjectSummary(
        IEnumerable<WorkDayDto>? workDays,
        Rate rate)
    {
        if (workDays is null)
            return [];

        var order = new List<string>();
        var byProject = new Dictionary<string, (decimal Hours, HashSet<DateOnly> Days, decimal Amount)>(StringComparer.OrdinalIgnoreCase);

        foreach (var wd in workDays.Where(wd => wd.DayType == DayType.Worked))
        {
            var allocations = wd.Projects?.Where(p => p.Hours > 0).ToList() ?? [];
            if (allocations.Count == 0)
                continue;

            var dayHours = allocations.Sum(a => a.Hours);

            foreach (var allocation in allocations)
            {
                var name = allocation.ProjectName.Trim();
                if (!byProject.TryGetValue(name, out var acc))
                {
                    order.Add(name);
                    acc = (0m, new HashSet<DateOnly>(), 0m);
                }

                var amount = rate.Type switch
                {
                    RateType.Hourly => rate.Price.Amount * allocation.Hours,
                    RateType.Daily => dayHours > 0 ? rate.Price.Amount * (allocation.Hours / dayHours) : 0m,
                    _ => 0m
                };

                acc.Days.Add(wd.Date);
                byProject[name] = (acc.Hours + allocation.Hours, acc.Days, acc.Amount + amount);
            }
        }

        return order
            .Select(name =>
            {
                var acc = byProject[name];
                return new ProjectSummaryTemplateModel
                {
                    Name = name,
                    TotalHours = acc.Hours,
                    WorkedDays = acc.Days.Count,
                    Amount = rate.Type == RateType.Monthly ? null : acc.Amount
                };
            })
            .ToList();
    }

    /// <summary>
    /// Clears all work days for the specified month and saves new ones, resolving each
    /// project allocation (creating projects typed on the calendar for the first time).
    /// Returns the work days with project names normalized to their canonical form and
    /// day hours set to the sum of their allocations.
    /// </summary>
    private async Task<ICollection<WorkDayDto>> ClearAndSaveWorkDaysAsync(
        long customerId,
        int year,
        int month,
        ICollection<WorkDayDto> workDayDtos,
        CancellationToken cancellationToken)
    {
        if (workDayDtos == null || workDayDtos.Count == 0)
            return workDayDtos ?? [];

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

        var normalized = new List<WorkDayDto>(workDayDtos.Count);

        foreach (var dto in workDayDtos)
        {
            var workDay = new WorkDay(customerId, dto.Date, dto.DayType, dto.HoursWorked, dto.Notes);
            List<WorkDayProjectDto>? normalizedAllocations = null;

            if (dto.Projects is { Count: > 0 })
            {
                normalizedAllocations = [];
                var totalHours = 0m;

                // Merge duplicate references to the same project within a single day
                var groups = dto.Projects
                    .Where(p => p.Hours > 0)
                    .GroupBy(p => new { p.ProjectId, Name = (p.ProjectName ?? string.Empty).Trim() });

                foreach (var group in groups)
                {
                    var hours = group.Sum(p => p.Hours);
                    var project = await _projectResolver.ResolveOrCreateAsync(
                        customerId, group.Key.ProjectId, group.Key.Name, cancellationToken);

                    workDay.Projects.Add(new WorkDayProject(project, hours));
                    normalizedAllocations.Add(new WorkDayProjectDto(
                        project.Name, hours, project.Id > 0 ? project.Id : null));
                    totalHours += hours;
                }

                if (totalHours > 0m)
                    workDay.HoursWorked = totalHours;
            }

            await _workDayRepository.AddAsync(workDay, cancellationToken);
            normalized.Add(dto with { Projects = normalizedAllocations });
        }

        return normalized;
    }
}
