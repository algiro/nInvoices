using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Infrastructure.DataPortability;

/// <summary>
/// Customers with their rates, taxes, templates, projects, worked days and unbilled expenses.
/// Worked days and invoices refer to rates by position, so rates travel in id order.
/// </summary>
internal sealed class CustomerPortability
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger _logger;

    public CustomerPortability(ApplicationDbContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Export all customers with their rates, taxes, templates, projects, worked days and unbilled expenses.
    /// </summary>
    public async Task<List<CustomerExportDto>> ExportAsync(CancellationToken cancellationToken)
    {
        var customers = await _context.Customers
            .Include(c => c.Rates)
            .Include(c => c.Taxes)
            .Include(c => c.Templates)
            .Include(c => c.EmailTemplates)
            .Include(c => c.Projects)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var workDaysByCustomer = (await _context.WorkDays
                .Include(w => w.Projects).ThenInclude(p => p.Project)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .GroupBy(w => w.CustomerId)
            .ToDictionary(g => g.Key, g => g.OrderBy(w => w.Date).ToList());

        var unbilledByCustomer = (await _context.Expenses
                .Where(e => e.InvoiceId == null)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.CustomerId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Date).ToList());

        var monthlyTemplates = await _context.MonthlyReportTemplates
            .Where(mt => mt.CustomerId != null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var templatesByCustomer = monthlyTemplates
            .GroupBy(mt => mt.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var exported = customers.Select(c =>
        {
            // Rates in id order: worked days and invoices refer to them by position
            var rates = c.Rates.OrderBy(r => r.Id).ToList();
            return new CustomerExportDto(
                c.Name,
                c.FiscalId,
                new AddressDto(c.Address.Street, c.Address.HouseNumber, c.Address.City,
                    c.Address.ZipCode, c.Address.Country, c.Address.State),
                c.CreatedAt,
                rates.Select(r => new RateExportDto(r.Type, new MoneyDto(r.Price.Amount, r.Price.Currency), r.CreatedAt, r.Name, r.IsActive)).ToList(),
                c.Taxes.Select(t => new TaxExportDto(t.TaxId, t.Description, t.HandlerId, t.Rate,
                    t.ApplicationType, FindTaxIdByPk(c.Taxes, t.AppliedToTaxId), t.Order, t.IsActive, t.CreatedAt,
                    t.ComplianceValues)).ToList(),
                c.Templates.Select(t => new InvoiceTemplateExportDto(t.InvoiceType, t.Name, t.Content, t.IsActive, t.CreatedAt)).ToList(),
                (templatesByCustomer.GetValueOrDefault(c.Id) ?? [])
                    .Select(mt => new MonthlyReportTemplateExportDto(mt.InvoiceType, mt.Name, mt.Content, mt.IsActive, mt.CreatedAt)).ToList(),
                c.Email,
                c.CcEmails,
                c.EmailTemplates.Select(et => new EmailTemplateExportDto(et.Name, et.Subject, et.Body, et.IsActive, et.CreatedAt)).ToList(),
                c.HolidayCountry,
                c.Locale,
                c.Projects.OrderBy(p => p.Name).Select(p => new ProjectExportDto(p.Name, p.IsActive)).ToList(),
                (workDaysByCustomer.GetValueOrDefault(c.Id) ?? []).Select(w => new WorkDayExportDto(
                    w.Date,
                    w.DayType,
                    w.HoursWorked,
                    w.Notes,
                    IndexOf(rates, w.RateId),
                    w.Projects.Select(p => new WorkDayProjectExportDto(p.Project.Name, p.Hours)).ToList())).ToList(),
                c.ComplianceValues,
                (unbilledByCustomer.GetValueOrDefault(c.Id) ?? []).Select(PortableData.ToExpenseDto).ToList());
        }).ToList();

        _logger.LogInformation("Exported {Count} customers", exported.Count);

        return exported;
    }

    /// <summary>
    /// Import customers with their rates, taxes, templates, and monthly report templates.
    /// Customers are matched by FiscalId - existing customers are skipped.
    /// </summary>
    public async Task<(int Imported, int Skipped, List<string> Errors)> ImportAsync(
        IEnumerable<CustomerExportDto> customers,
        CancellationToken cancellationToken)
    {
        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        // Fiscal ids are encrypted, so they are compared here rather than in SQL
        var existingFiscalIds = (await _context.Customers.AsNoTracking()
                .Select(c => c.FiscalId)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var customerData in customers)
        {
            try
            {
                if (!existingFiscalIds.Add(customerData.FiscalId))
                {
                    skipped++;
                    continue;
                }

                var address = new Address(
                    customerData.Address.Street,
                    customerData.Address.HouseNumber,
                    customerData.Address.City,
                    customerData.Address.ZipCode,
                    customerData.Address.Country,
                    customerData.Address.State);

                var customer = string.IsNullOrWhiteSpace(customerData.Locale)
                    ? new Customer(customerData.Name, customerData.FiscalId, address)
                    : new Customer(customerData.Name, customerData.FiscalId, address, customerData.Locale);
                customer.SetContact(customerData.Email, customerData.CcEmails);
                customer.SetHolidayCountry(customerData.HolidayCountry);
                foreach (var (country, values) in ByCountry(customerData.ComplianceValues))
                    customer.SetComplianceValues(country, values);
                await _context.Customers.AddAsync(customer, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                // Import rates, in the exported order: worked days refer to them by position
                var rates = new List<Rate>();
                foreach (var rateData in customerData.Rates)
                {
                    var rate = new Rate(customer.Id, rateData.Type, new Money(rateData.Price.Amount, rateData.Price.Currency));
                    rate.SetName(rateData.Name);
                    rate.IsActive = rateData.IsActive;
                    await _context.Rates.AddAsync(rate, cancellationToken);
                    rates.Add(rate);
                }

                // Import taxes - need to handle compound tax references
                var taxIdMap = new Dictionary<string, long>();
                foreach (var taxData in customerData.Taxes.OrderBy(t => t.Order))
                {
                    var tax = new Tax(customer.Id, taxData.TaxId, taxData.Description,
                        taxData.HandlerId, taxData.Rate, taxData.ApplicationType, taxData.Order);
                    if (taxData.AppliedToTaxId is not null && taxIdMap.TryGetValue(taxData.AppliedToTaxId, out var mappedId))
                        tax.SetCompoundTax(mappedId);
                    if (!taxData.IsActive)
                        tax.IsActive = false;
                    foreach (var (country, values) in ByCountry(taxData.ComplianceValues))
                        tax.SetComplianceValues(country, values);
                    await _context.Taxes.AddAsync(tax, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    taxIdMap[taxData.TaxId] = tax.Id;
                }

                // Import invoice templates
                foreach (var templateData in customerData.InvoiceTemplates)
                {
                    var template = new InvoiceTemplate(customer.Id, templateData.InvoiceType, templateData.Name, templateData.Content);
                    if (templateData.IsActive)
                        template.Activate();
                    await _context.InvoiceTemplates.AddAsync(template, cancellationToken);
                }

                // Import monthly report templates
                foreach (var templateData in customerData.MonthlyReportTemplates)
                {
                    var template = new MonthlyReportTemplate(customer.Id, templateData.Name, templateData.Content, templateData.InvoiceType);
                    if (templateData.IsActive)
                        template.Activate();
                    await _context.MonthlyReportTemplates.AddAsync(template, cancellationToken);
                }

                // Import email templates (absent from exports made before they existed)
                foreach (var templateData in customerData.EmailTemplates ?? [])
                {
                    var template = new EmailTemplate(customer.Id, templateData.Name, templateData.Subject, templateData.Body);
                    if (templateData.IsActive)
                        template.Activate();
                    await _context.EmailTemplates.AddAsync(template, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);

                await ImportProjectsAndWorkDaysAsync(customer, customerData, rates, cancellationToken);

                foreach (var expenseData in customerData.UnbilledExpenses ?? [])
                {
                    await _context.Expenses.AddAsync(new Expense
                    {
                        CustomerId = customer.Id,
                        Description = expenseData.Description,
                        Amount = new Money(expenseData.Amount, expenseData.Currency),
                        Date = expenseData.Date
                    }, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Customer '{customerData.Name}' ({customerData.FiscalId}): {ex.Message}");
                _logger.LogError(ex, "Failed to import customer {FiscalId}", customerData.FiscalId);
            }
        }

        return (imported, skipped, errors);
    }

    private async Task ImportProjectsAndWorkDaysAsync(
        Customer customer,
        CustomerExportDto customerData,
        IReadOnlyList<Rate> rates,
        CancellationToken cancellationToken)
    {
        var projects = new Dictionary<string, Project>(StringComparer.Ordinal);
        foreach (var projectData in customerData.Projects ?? [])
        {
            var project = new Project(customer.Id, projectData.Name);
            if (!projectData.IsActive)
                project.Deactivate();
            if (projects.TryAdd(project.Name, project))
                await _context.Projects.AddAsync(project, cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var dayData in customerData.WorkDays ?? [])
        {
            var day = new WorkDay(customer.Id, dayData.Date, dayData.DayType, dayData.HoursWorked, dayData.Notes)
            {
                RateId = dayData.RateIndex is { } index && index >= 0 && index < rates.Count ? rates[index].Id : null
            };
            foreach (var split in dayData.Projects)
            {
                if (projects.TryGetValue(split.ProjectName.Trim(), out var project))
                    day.Projects.Add(new WorkDayProject(project, split.Hours));
            }
            await _context.WorkDays.AddAsync(day, cancellationToken);
        }
    }

    private static int? IndexOf(IReadOnlyList<Rate> rates, long? rateId)
    {
        if (rateId is not { } id)
            return null;
        for (var i = 0; i < rates.Count; i++)
        {
            if (rates[i].Id == id)
                return i;
        }
        return null;
    }

    /// <summary>Splits a "COUNTRY.field" map into one map of fields per country.</summary>
    private static IEnumerable<(string Country, IReadOnlyDictionary<string, string> Values)> ByCountry(
        IReadOnlyDictionary<string, string>? values) =>
        (values ?? new Dictionary<string, string>())
            .Where(v => v.Key.IndexOf('.') > 0)
            .GroupBy(v => v.Key[..v.Key.IndexOf('.')])
            .Select(g => (g.Key, (IReadOnlyDictionary<string, string>)g.ToDictionary(v => v.Key[(g.Key.Length + 1)..], v => v.Value)));

    private static string? FindTaxIdByPk(ICollection<Tax> taxes, long? pk) =>
        pk.HasValue ? taxes.FirstOrDefault(t => t.Id == pk.Value)?.TaxId : null;
}
