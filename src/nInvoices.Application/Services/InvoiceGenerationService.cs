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

    /// <summary>
    /// Builds and renders the invoice exactly as <see cref="GenerateInvoiceAsync"/> would, without
    /// side effects: work days are not saved and projects are not created (the number shown is
    /// the next one; generating does not take it either, finalizing does). Throws <see cref="InvalidOperationException"/>
    /// when the invoice could not be generated (no rate, no active template, a template error).
    /// </summary>
    Task<InvoiceDraft> PreviewInvoiceAsync(
        GenerateInvoiceDto dto,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateInvoicePdfAsync(
        long invoiceId,
        CancellationToken cancellationToken = default);
    
    Task RegenerateInvoiceHtmlAsync(
        long invoiceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// An invoice built in memory and not persisted, with the customer and the work days it was
/// built from (project allocations merged per day), so the timesheet can be rendered from them.
/// </summary>
public sealed record InvoiceDraft(Invoice Invoice, Customer Customer, IReadOnlyCollection<WorkDayDto> WorkDays);

public sealed class InvoiceGenerationService : IInvoiceGenerationService
{
    private readonly IRepository<InvoiceTemplate> _templateRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Rate> _rateRepository;
    private readonly IRepository<Tax> _taxRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IWorkDayRepository _workDayRepository;
    private readonly IProjectResolver _projectResolver;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IHtmlToPdfConverter _htmlToPdfConverter;
    private readonly ITaxCalculationService _taxCalculationService;
    private readonly IInvoiceNumbering _numbering;
    private readonly IUnitOfWork _unitOfWork;

    public InvoiceGenerationService(
        IRepository<InvoiceTemplate> templateRepository,
        IRepository<Customer> customerRepository,
        IRepository<Rate> rateRepository,
        IRepository<Tax> taxRepository,
        IInvoiceRepository invoiceRepository,
        IWorkDayRepository workDayRepository,
        IProjectResolver projectResolver,
        ITemplateRenderer templateRenderer,
        IHtmlToPdfConverter htmlToPdfConverter,
        ITaxCalculationService taxCalculationService,
        IInvoiceNumbering numbering,
        IUnitOfWork unitOfWork)
    {
        _templateRepository = templateRepository;
        _customerRepository = customerRepository;
        _rateRepository = rateRepository;
        _taxRepository = taxRepository;
        _invoiceRepository = invoiceRepository;
        _workDayRepository = workDayRepository;
        _projectResolver = projectResolver;
        _templateRenderer = templateRenderer;
        _htmlToPdfConverter = htmlToPdfConverter;
        _taxCalculationService = taxCalculationService;
        _numbering = numbering;
        _unitOfWork = unitOfWork;
    }

    public async Task<Invoice> GenerateInvoiceAsync(
        GenerateInvoiceDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var draft = await BuildInvoiceAsync(dto, persist: true, cancellationToken);
        return draft.Invoice;
    }

    public Task<InvoiceDraft> PreviewInvoiceAsync(
        GenerateInvoiceDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return BuildInvoiceAsync(dto, persist: false, cancellationToken);
    }

    /// <summary>
    /// Builds and renders the invoice. With <paramref name="persist"/> the month's work days are
    /// replaced (staged, committed by the caller); without it nothing is written, which is what
    /// the preview needs. Either way the number is the next one, not yet taken from the sequence.
    /// </summary>
    private async Task<InvoiceDraft> BuildInvoiceAsync(
        GenerateInvoiceDto dto,
        bool persist,
        CancellationToken cancellationToken)
    {
        var template = await GetTemplateAsync(dto.CustomerId, dto.InvoiceType, cancellationToken);
        var rate = await GetRateAsync(dto.CustomerId, dto.InvoiceType, dto.RateId, cancellationToken);
        var customerTaxes = await GetCustomerTaxesAsync(dto.CustomerId, cancellationToken);

        // The rates of the invoice: its default one, and any a worked day chose for itself
        var rates = await LoadDayRatesAsync(dto.CustomerId, rate, dto.InvoiceType == InvoiceType.Monthly ? dto.WorkDays : null, cancellationToken);

        // Validate hourly rate requirements
        if (dto.InvoiceType == InvoiceType.Monthly)
        {
            if (rate.Type == RateType.Hourly && (dto.WorkDays == null || !dto.WorkDays.Any()))
                throw new InvalidOperationException("Work days are required for hourly rate invoices.");

            rates.Validate(dto.WorkDays ?? []);

            // Every day billed by the hour needs its hours
            var workedDaysWithoutHours = (dto.WorkDays ?? [])
                .Where(wd => wd.DayType == DayType.Worked && !rates.IsFixedMonthly && rates.For(wd).Type == RateType.Hourly && GetDayHours(wd) <= 0)
                .ToList();

            if (workedDaysWithoutHours.Any())
                throw new InvalidOperationException(
                    $"Hours must be specified for all worked days when using hourly rates " +
                    $"(via a project allocation or the day's hours). Missing hours for {workedDaysWithoutHours.Count} day(s).");
        }

        if (rate.Type == RateType.Hourly && dto.InvoiceType == InvoiceType.OneTime && dto.Hours is not > 0)
            throw new InvalidOperationException("Hours are required for a one-time invoice with an hourly rate.");

        var subtotal = CalculateSubtotal(dto, rates);
        var expensesTotal = CalculateExpensesTotal(dto.Expenses, rate.Price.Currency);
        var customer = await _customerRepository.GetByIdAsync(dto.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {dto.CustomerId} not found");
        // A draft does not use up a number: it shows the next one, which is only taken when the
        // invoice is finalized
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
            workDays = persist
                ? await ClearAndSaveWorkDaysAsync(
                    dto.CustomerId, dto.Year!.Value, dto.Month!.Value, workDays, cancellationToken)
                : MergeAllocations(workDays);
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
        var templateModel = BuildTemplateModel(invoice, customer, dto, rates, workDays);
        var renderedHtml = await _templateRenderer.RenderAsync(template.Content, templateModel, cancellationToken);

        invoice.SetRenderedContent(renderedHtml);

        return new InvoiceDraft(invoice, customer, workDays?.ToList() ?? []);
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
        var rate = await GetRateAsync(invoice.CustomerId, invoice.Type, invoice.RateId, cancellationToken);

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
                        .ToList(),
                    wd.RateId))
                .ToList();
        }

        // Rebuild the template model from the existing invoice data
        var dto = new GenerateInvoiceDto
        {
            CustomerId = invoice.CustomerId,
            InvoiceType = invoice.Type,
            IssueDate = invoice.IssueDate,
            RateId = invoice.RateId,
            Hours = invoice.Hours,
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

        var rates = await LoadDayRatesAsync(invoice.CustomerId, rate, workDayDtos, cancellationToken);
        var templateModel = BuildTemplateModel(invoice, customer, dto, rates, workDayDtos);
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
        // The customer's own active template, else the shared one
        var templates = await _templateRepository.FindAsync(
            t => (t.CustomerId == customerId || t.CustomerId == null) && t.InvoiceType == invoiceType && t.IsActive,
            cancellationToken);

        var template = ScopedTemplates.PickEffective(templates, customerId);
        if (template == null)
            throw new InvalidOperationException($"No active template found for customer {customerId} and type {invoiceType}");

        return template;
    }

    private async Task<Rate> GetRateAsync(
        long customerId,
        InvoiceType invoiceType,
        long? rateId,
        CancellationToken cancellationToken)
    {
        if (invoiceType is not (InvoiceType.Monthly or InvoiceType.OneTime))
            throw new ArgumentException($"Unsupported invoice type: {invoiceType}", nameof(invoiceType));

        // The rate chosen for the invoice, which must be one of the customer's
        if (rateId.HasValue)
        {
            var chosen = (await _rateRepository.FindAsync(
                r => r.Id == rateId.Value && r.CustomerId == customerId,
                cancellationToken)).FirstOrDefault();

            return chosen
                ?? throw new InvalidOperationException($"Rate {rateId.Value} not found for customer {customerId}.");
        }

        // Otherwise prefer the Daily rate (daily rate × worked days), then the Monthly rate
        // (fixed monthly billing), then the Hourly rate (hourly rate × hours)
        var rates = await _rateRepository.FindAsync(
            r => r.CustomerId == customerId && r.Type == RateType.Daily,
            cancellationToken);

        var rate = rates.FirstOrDefault();
        if (rate == null)
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

    /// <summary>
    /// The default rate plus the rates the worked days chose for themselves, loaded from the
    /// customer's rates.
    /// </summary>
    /// <exception cref="InvalidOperationException">A day chose a rate that is not the customer's.</exception>
    private async Task<DayRates> LoadDayRatesAsync(
        long customerId,
        Rate defaultRate,
        IEnumerable<WorkDayDto>? workDays,
        CancellationToken cancellationToken)
    {
        var ids = DayRates.RequestedIds(workDays, defaultRate);
        if (ids.Count == 0)
            return new DayRates(defaultRate, []);

        var found = (await _rateRepository.FindAsync(
            r => r.CustomerId == customerId && ids.Contains(r.Id),
            cancellationToken)).ToList();

        var missing = ids.Except(found.Select(r => r.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Rate {missing[0]} not found for customer {customerId}.");

        return new DayRates(defaultRate, found);
    }

    private Money CalculateSubtotal(GenerateInvoiceDto dto, DayRates rates)
    {
        var rate = rates.Default;

        return dto.InvoiceType switch
        {
            // Fixed monthly rate
            InvoiceType.Monthly when rate.Type == RateType.Monthly =>
                rate.Price,

            // Each worked day at its own rate: an hourly rate bills the day's hours (from its
            // project allocations when present, otherwise HoursWorked), a daily rate bills
            // the day (partial days counted as hoursWorked/8). Quantities are added up per rate
            // first, so a single rate gives exactly rate × total.
            InvoiceType.Monthly when dto.WorkDays != null =>
                SumWorkedDays(dto.WorkDays, rates),

            // One-time: hours × the hourly rate, or the rate's price as a fixed amount
            InvoiceType.OneTime when rate.Type == RateType.Hourly =>
                new Money(rate.Price.Amount * (dto.Hours ?? 0m), rate.Price.Currency),

            InvoiceType.OneTime => rate.Price,
            _ => throw new InvalidOperationException($"Cannot calculate subtotal for invoice type {dto.InvoiceType}")
        };
    }

    private static Money SumWorkedDays(IEnumerable<WorkDayDto> workDays, DayRates rates)
    {
        var amount = workDays
            .Where(wd => wd.DayType == DayType.Worked)
            .GroupBy(wd => rates.For(wd))
            .Sum(group => group.Key.Price.Amount * group.Sum(wd => group.Key.Type == RateType.Hourly
                ? GetDayHours(wd)
                : (wd.HoursWorked ?? 8m) / 8m));

        return new Money(amount, rates.Default.Price.Currency);
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

    /// <summary>
    /// Builds the template model for rendering.
    /// This model is passed to Scriban and all properties become template variables.
    /// Following the Builder pattern for complex object construction.
    /// </summary>
    private InvoiceTemplateModel BuildTemplateModel(
        Invoice invoice,
        Customer customer,
        GenerateInvoiceDto dto,
        DayRates rates,
        IEnumerable<WorkDayDto>? workDays = null)
    {
        var rate = rates.Default;

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
            Locale = customer.Locale,

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

            LineItems = BuildLineItems(invoice, dto, rates, workDays, customer.Locale),
            ProjectSummary = BuildProjectSummary(workDays, rates),
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
    /// Days billed at different rates get their own lines, each saying which rate it is.
    /// For Monthly rates: a single consolidated line.
    /// Expenses are added as separate line items.
    /// </summary>
    private List<LineItemTemplateModel> BuildLineItems(
        Invoice invoice,
        GenerateInvoiceDto dto,
        DayRates rates,
        IEnumerable<WorkDayDto>? workDays,
        string locale = "en-US")
    {
        var rate = rates.Default;
        var lineItems = new List<LineItemTemplateModel>();

        if (dto.InvoiceType == InvoiceType.Monthly && invoice.WorkedDays.HasValue)
        {
            var workedDaysList = workDays?
                .Where(wd => wd.DayType == DayType.Worked)
                .OrderBy(wd => wd.Date)
                .ToList();

            var perDayRate = rate.Type is RateType.Daily or RateType.Hourly;

            if (workedDaysList is { Count: > 0 } && perDayRate)
            {
                // Days at the same rate are billed together, in the order the rates first appear
                var groups = workedDaysList.GroupBy(wd => rates.For(wd).Id).Select(g => g.ToList()).ToList();
                var mixed = groups.Count > 1;

                foreach (var days in groups)
                {
                    var groupRate = rates.For(days[0]);
                    var lines = BuildRateLineItems(days, groupRate, locale);

                    // With several rates on one invoice, say which rate each line is billed at
                    lineItems.AddRange(mixed
                        ? lines.Select(l => l with { Description = $"{l.Description} - {DayRates.Label(groupRate)}" })
                        : lines);
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
            var hourly = rate.Type == RateType.Hourly && dto.Hours.HasValue;
            lineItems.Add(new LineItemTemplateModel
            {
                Description = "Professional Services",
                Quantity = hourly ? dto.Hours!.Value : 1,
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

    /// <summary>The lines for worked days billed at one daily or hourly rate: per project, or per day.</summary>
    private static List<LineItemTemplateModel> BuildRateLineItems(List<WorkDayDto> days, Rate rate, string locale)
    {
        var hasAllocations = days.Any(wd => wd.Projects is { Count: > 0 });
        if (hasAllocations)
        {
            // One line item per project (time grouped across the month)
            return BuildProjectLineItems(days, rate).ToList();
        }

        // One line item per worked day
        var culture = System.Globalization.CultureInfo.GetCultureInfo(locale);
        return days
            .Select(wd =>
            {
                var quantity = rate.Type == RateType.Hourly ? GetDayHours(wd) : 1m;
                return new LineItemTemplateModel
                {
                    Description = wd.Date.ToString("d", culture),
                    Quantity = quantity,
                    Rate = rate.Price.Amount,
                    Amount = rate.Price.Amount * quantity
                };
            })
            .ToList();
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
        DayRates rates)
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
            var rate = rates.For(wd);

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
                    Amount = rates.IsFixedMonthly ? null : acc.Amount
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
            var workDay = new WorkDay(customerId, dto.Date, dto.DayType, dto.HoursWorked, dto.Notes)
            {
                RateId = dto.DayType == DayType.Worked ? dto.RateId : null
            };
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

    /// <summary>
    /// The in-memory counterpart of <see cref="ClearAndSaveWorkDaysAsync"/> for previews: merges
    /// duplicate references to the same project within a day, without resolving or creating
    /// projects, so names keep the spelling typed on the calendar.
    /// </summary>
    private static List<WorkDayDto> MergeAllocations(IEnumerable<WorkDayDto> workDays) =>
        workDays
            .Select(wd => wd.Projects is { Count: > 0 }
                ? wd with
                {
                    Projects = wd.Projects
                        .Where(p => p.Hours > 0)
                        .GroupBy(p => (p.ProjectName ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                        .Select(g => new WorkDayProjectDto(g.Key, g.Sum(p => p.Hours), g.First().ProjectId))
                        .ToList()
                }
                : wd with { Projects = null })
            .ToList();
}
