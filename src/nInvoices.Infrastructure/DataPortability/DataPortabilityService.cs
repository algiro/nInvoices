using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.ImportExport;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Infrastructure.DataPortability;

/// <summary>
/// Exports and imports the user's data as JSON, for backups and moving between installations:
/// customers (with rates, taxes, templates, projects, worked days and unbilled expenses), invoices,
/// and settings and assets. Imports keep what the user already has and report per item what was
/// imported, skipped or failed. Works on the DbContext directly: it is bulk data transfer.
/// </summary>
public sealed class DataPortabilityService : IDataPortability
{
    private readonly ApplicationDbContext _context;
    private readonly IComplianceRegistry _compliance;
    private readonly ILogger<DataPortabilityService> _logger;

    public DataPortabilityService(
        ApplicationDbContext context,
        IComplianceRegistry compliance,
        ILogger<DataPortabilityService> logger)
    {
        _context = context;
        _compliance = compliance;
        _logger = logger;
    }

    /// <summary>
    /// Export all customers with their rates, taxes, templates, projects, worked days and unbilled expenses.
    /// </summary>
    public async Task<DataExportDto> ExportCustomersAsync(CancellationToken cancellationToken)
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
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Shared templates have no customer: they are exported on their own
        var templatesByCustomer = monthlyTemplates
            .Where(mt => mt.CustomerId.HasValue)
            .GroupBy(mt => mt.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sharedInvoiceTemplates = await _context.InvoiceTemplates
            .Where(t => t.CustomerId == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var sharedEmailTemplates = await _context.EmailTemplates
            .Where(t => t.CustomerId == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var shared = new SharedTemplatesExportDto(
            sharedInvoiceTemplates.Select(t => new InvoiceTemplateExportDto(t.InvoiceType, t.Name, t.Content, t.IsActive, t.CreatedAt)).ToList(),
            monthlyTemplates.Where(mt => mt.CustomerId is null)
                .Select(mt => new MonthlyReportTemplateExportDto(mt.InvoiceType, mt.Name, mt.Content, mt.IsActive, mt.CreatedAt)).ToList(),
            sharedEmailTemplates.Select(et => new EmailTemplateExportDto(et.Name, et.Subject, et.Body, et.IsActive, et.CreatedAt)).ToList());

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
                (unbilledByCustomer.GetValueOrDefault(c.Id) ?? []).Select(ToExpenseDto).ToList());
        }).ToList();

        _logger.LogInformation("Exported {Count} customers", exported.Count);

        return new DataExportDto("1.0", DateTime.UtcNow, exported, null, shared);
    }

    /// <summary>
    /// Export invoices with optional filters.
    /// </summary>
    public async Task<DataExportDto> ExportInvoicesAsync(
        int? year,
        int? month,
        long? customerId,
        CancellationToken cancellationToken)
    {
        var query = _context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Expenses)
            .Include(i => i.TaxLines)
            .AsNoTracking()
            .AsQueryable();

        if (year.HasValue)
            query = query.Where(i => i.Year == year.Value);
        if (month.HasValue)
            query = query.Where(i => i.Month == month.Value);
        if (customerId.HasValue)
            query = query.Where(i => i.CustomerId == customerId.Value);

        var invoices = await query.ToListAsync(cancellationToken);

        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var ratesByCustomer = await RatesInIdOrderAsync(customerIds, cancellationToken);

        var exported = invoices.Select(i => new InvoiceExportDto(
            i.Customer.FiscalId,
            i.Type,
            i.Number.Value,
            i.IssueDate,
            i.DueDate,
            i.WorkedDays,
            i.Year,
            i.Month,
            new MoneyDto(i.Subtotal.Amount, i.Subtotal.Currency),
            new MoneyDto(i.TotalExpenses.Amount, i.TotalExpenses.Currency),
            new MoneyDto(i.TotalTaxes.Amount, i.TotalTaxes.Currency),
            new MoneyDto(i.Total.Amount, i.Total.Currency),
            i.Status,
            i.RenderedContent,
            i.Notes,
            i.CreatedAt,
            i.Expenses.Select(ToExpenseDto).ToList(),
            i.TaxLines.Select(tl => new InvoiceTaxLineExportDto(
                tl.TaxId, tl.Description, tl.Rate, tl.BaseAmount.Amount, tl.TaxAmount.Amount, tl.Order)).ToList(),
            i.Hours,
            IndexOf(ratesByCustomer.GetValueOrDefault(i.CustomerId) ?? [], i.RateId)
        )).ToList();

        _logger.LogInformation("Exported {Count} invoices", exported.Count);

        return new DataExportDto("1.0", DateTime.UtcNow, null, exported);
    }

    /// <summary>
    /// Import customers with their rates, taxes, templates, and monthly report templates.
    /// Customers are matched by FiscalId - existing customers are skipped.
    /// </summary>
    public async Task<ImportResultDto> ImportCustomersAsync(
        DataExportDto data,
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

        foreach (var customerData in data.Customers ?? [])
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

        if (data.SharedTemplates is not null)
        {
            try
            {
                var (sharedImported, sharedSkipped) = await ImportSharedTemplatesAsync(data.SharedTemplates, cancellationToken);
                imported += sharedImported;
                skipped += sharedSkipped;
            }
            catch (Exception ex)
            {
                errors.Add($"Shared templates: {ex.Message}");
                _logger.LogError(ex, "Failed to import the shared templates");
            }
        }

        _logger.LogInformation("Import complete: {Imported} imported, {Skipped} skipped, {Errors} errors",
            imported, skipped, errors.Count);

        return new ImportResultDto(imported, skipped, errors);
    }

    /// <summary>
    /// Adds the shared templates that are not there yet (same kind, type and name); an imported
    /// template is only activated when the user has no active shared one of its kind and type.
    /// </summary>
    private async Task<(int Imported, int Skipped)> ImportSharedTemplatesAsync(
        SharedTemplatesExportDto shared,
        CancellationToken cancellationToken)
    {
        var imported = 0;
        var skipped = 0;

        var invoiceTemplates = await _context.InvoiceTemplates.Where(t => t.CustomerId == null).ToListAsync(cancellationToken);
        foreach (var data in shared.InvoiceTemplates)
        {
            if (invoiceTemplates.Any(t => t.InvoiceType == data.InvoiceType && t.Name == data.Name))
            {
                skipped++;
                continue;
            }

            var template = new InvoiceTemplate(null, data.InvoiceType, data.Name, data.Content);
            if (data.IsActive && !invoiceTemplates.Any(t => t.InvoiceType == data.InvoiceType && t.IsActive))
                template.Activate();
            invoiceTemplates.Add(template);
            await _context.InvoiceTemplates.AddAsync(template, cancellationToken);
            imported++;
        }

        var reportTemplates = await _context.MonthlyReportTemplates.Where(t => t.CustomerId == null).ToListAsync(cancellationToken);
        foreach (var data in shared.MonthlyReportTemplates)
        {
            if (reportTemplates.Any(t => t.Name == data.Name))
            {
                skipped++;
                continue;
            }

            var template = new MonthlyReportTemplate(null, data.Name, data.Content, data.InvoiceType);
            if (data.IsActive && !reportTemplates.Any(t => t.IsActive))
                template.Activate();
            reportTemplates.Add(template);
            await _context.MonthlyReportTemplates.AddAsync(template, cancellationToken);
            imported++;
        }

        var emailTemplates = await _context.EmailTemplates.Where(t => t.CustomerId == null).ToListAsync(cancellationToken);
        foreach (var data in shared.EmailTemplates)
        {
            if (emailTemplates.Any(t => t.Name == data.Name))
            {
                skipped++;
                continue;
            }

            var template = new EmailTemplate(null, data.Name, data.Subject, data.Body);
            if (data.IsActive && !emailTemplates.Any(t => t.IsActive))
                template.Activate();
            emailTemplates.Add(template);
            await _context.EmailTemplates.AddAsync(template, cancellationToken);
            imported++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (imported, skipped);
    }

    /// <summary>
    /// Import invoices. Customers must already exist (matched by FiscalId).
    /// </summary>
    public async Task<ImportResultDto> ImportInvoicesAsync(
        DataExportDto data,
        CancellationToken cancellationToken)
    {
        var customerCache = await _context.Customers
            .AsNoTracking()
            .ToDictionaryAsync(c => c.FiscalId, c => c.Id, cancellationToken);
        var ratesByCustomer = await RatesInIdOrderAsync(customerCache.Values.ToList(), cancellationToken);

        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var invoiceData in data.Invoices ?? [])
        {
            try
            {
                if (!customerCache.TryGetValue(invoiceData.CustomerFiscalId, out var customerId))
                {
                    errors.Add($"Invoice '{invoiceData.InvoiceNumber}': Customer with FiscalId '{invoiceData.CustomerFiscalId}' not found");
                    continue;
                }

                // Check for duplicate invoice number
                var exists = await _context.Invoices
                    .AnyAsync(i => i.Number.Value == invoiceData.InvoiceNumber, cancellationToken);
                if (exists)
                {
                    skipped++;
                    continue;
                }

                var currency = invoiceData.Subtotal.Currency;
                var invoice = new Invoice(
                    customerId,
                    new InvoiceNumber(invoiceData.InvoiceNumber),
                    invoiceData.Type,
                    invoiceData.IssueDate,
                    new Money(invoiceData.Subtotal.Amount, currency),
                    currency);

                invoice.DueDate = invoiceData.DueDate;
                invoice.WorkedDays = invoiceData.WorkedDays;
                invoice.Year = invoiceData.Year;
                invoice.Month = invoiceData.Month;
                // The status and amounts it was exported with, as they were
                invoice.RestoreImported(
                    invoiceData.Status,
                    new Money(invoiceData.TotalExpenses.Amount, currency),
                    new Money(invoiceData.TotalTaxes.Amount, currency),
                    new Money(invoiceData.Total.Amount, currency));
                invoice.RenderedContent = invoiceData.RenderedContent;
                invoice.Notes = invoiceData.Notes;
                invoice.Hours = invoiceData.Hours;
                invoice.RateId = RateAt(ratesByCustomer.GetValueOrDefault(customerId) ?? [], invoiceData.RateIndex);

                await _context.Invoices.AddAsync(invoice, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                // Import expenses
                foreach (var expenseData in invoiceData.Expenses)
                {
                    var expense = new Expense
                    {
                        CustomerId = customerId,
                        InvoiceId = invoice.Id,
                        Description = expenseData.Description,
                        Amount = new Money(expenseData.Amount, expenseData.Currency),
                        Date = expenseData.Date
                    };
                    await _context.Expenses.AddAsync(expense, cancellationToken);
                }

                // Import tax lines
                foreach (var taxLineData in invoiceData.TaxLines)
                {
                    var taxLine = new InvoiceTaxLine
                    {
                        InvoiceId = invoice.Id,
                        TaxId = taxLineData.TaxId,
                        Description = taxLineData.Description,
                        Rate = taxLineData.Rate,
                        BaseAmount = new Money(taxLineData.BaseAmount, currency),
                        TaxAmount = new Money(taxLineData.TaxAmount, currency),
                        Order = taxLineData.Order
                    };
                    await _context.InvoiceTaxLines.AddAsync(taxLine, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Invoice '{invoiceData.InvoiceNumber}': {ex.Message}");
                _logger.LogError(ex, "Failed to import invoice {InvoiceNumber}", invoiceData.InvoiceNumber);
            }
        }

        _logger.LogInformation("Invoice import complete: {Imported} imported, {Skipped} skipped, {Errors} errors",
            imported, skipped, errors.Count);

        return new ImportResultDto(imported, skipped, errors);
    }

    /// <summary>
    /// Export the user's settings and assets: invoice numbering, images used by templates, the
    /// holiday calendars they changed and their e-invoicing settings (without the signing certificate).
    /// </summary>
    public async Task<DataExportDto> ExportSettingsAsync(CancellationToken cancellationToken)
    {
        var sequence = await _context.InvoiceSequences.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var images = await _context.ImageAssets.AsNoTracking().OrderBy(i => i.Id).ToListAsync(cancellationToken);
        var calendars = await _context.HolidayCalendars.Include(c => c.Rules).AsNoTracking()
            .OrderBy(c => c.CountryCode).ToListAsync(cancellationToken);
        var compliance = await _context.ComplianceSettings.AsNoTracking()
            .OrderBy(c => c.CountryCode).ToListAsync(cancellationToken);

        var settings = new UserSettingsExportDto(
            sequence is null ? null : new InvoiceNumberingExportDto(sequence.CurrentValue, sequence.NumberFormat),
            images.Select(i => new ImageAssetExportDto(i.Alias, i.FileName, i.ContentType, i.Base64Data, i.FileSize)).ToList(),
            calendars.Select(c => new HolidayCalendarExportDto(c.CountryCode, c.Rules.OrderBy(r => r.Id).Select(r =>
                new HolidayRuleExportDto(r.Name, r.Kind, r.Month, r.Day, r.EasterOffset, r.Weekday, r.Occurrence,
                    r.FromYear, r.ToYear, r.IsActive)).ToList())).ToList(),
            compliance.Select(c => new ComplianceSettingsExportDto(
                c.CountryCode,
                c.IsEnabled,
                c.LegalName,
                c.TaxId,
                c.Address is { } a ? new AddressDto(a.Street, a.HouseNumber, a.City, a.ZipCode, a.Country, a.State) : null,
                c.Values)).ToList());

        return new DataExportDto("1.0", DateTime.UtcNow, null, null, Settings: settings);
    }

    /// <summary>
    /// Import settings and assets. What the user already has is kept: an image with the same name, a
    /// calendar for the same country, e-invoicing settings for the same country. The invoice sequence
    /// only moves forward, so restoring never hands out a number twice.
    /// </summary>
    public async Task<ImportResultDto> ImportSettingsAsync(
        DataExportDto data,
        CancellationToken cancellationToken)
    {
        // Present: checked by ImportSettingsCommandValidator
        var settings = data.Settings!;

        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        async Task Try(string what, Func<Task<bool>> import)
        {
            try
            {
                if (await import())
                    imported++;
                else
                    skipped++;
            }
            catch (Exception ex)
            {
                // Whatever this item staged is dropped, so the next save doesn't retry it
                _context.ChangeTracker.Clear();
                errors.Add($"{what}: {ex.Message}");
                _logger.LogError(ex, "Failed to import {What}", what);
            }
        }

        if (settings.InvoiceNumbering is { } numbering)
            await Try("Invoice numbering", () => ImportNumberingAsync(numbering, cancellationToken));

        var aliases = (await _context.ImageAssets.AsNoTracking().Select(i => i.Alias).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var image in settings.Images ?? [])
        {
            await Try($"Image '{image.Alias}'", async () =>
            {
                if (!aliases.Add(image.Alias))
                    return false;
                await _context.ImageAssets.AddAsync(
                    new ImageAsset(image.Alias, image.FileName, image.ContentType, image.Base64Data, image.FileSize), cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        var countries = (await _context.HolidayCalendars.AsNoTracking().Select(c => c.CountryCode).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var calendarData in settings.HolidayCalendars ?? [])
        {
            await Try($"Holiday calendar {calendarData.CountryCode}", async () =>
            {
                var calendar = new HolidayCalendar(calendarData.CountryCode);
                if (!countries.Add(calendar.CountryCode))
                    return false;
                foreach (var r in calendarData.Rules)
                {
                    var rule = HolidayRule.Create(r.Name, r.Kind, r.Month, r.Day, r.EasterOffset, r.Weekday, r.Occurrence, r.FromYear, r.ToYear);
                    if (!r.IsActive)
                        rule.Deactivate();
                    calendar.Rules.Add(rule);
                }
                await _context.HolidayCalendars.AddAsync(calendar, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        var regimes = (await _context.ComplianceSettings.AsNoTracking().Select(c => c.CountryCode).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var complianceData in settings.Compliance ?? [])
        {
            await Try($"E-invoicing settings {complianceData.CountryCode}", async () =>
            {
                var compliance = new ComplianceSettings(complianceData.CountryCode);
                if (!regimes.Add(compliance.CountryCode))
                    return false;
                var address = complianceData.Address is { } a
                    ? new Address(a.Street, a.HouseNumber, a.City, a.ZipCode, a.Country, a.State)
                    : null;
                // Turned on only where this server offers the country's rules
                var enabled = complianceData.IsEnabled && _compliance.Find(compliance.CountryCode) is not null;
                compliance.Update(enabled, complianceData.LegalName, complianceData.TaxId, address, complianceData.Values);
                await _context.ComplianceSettings.AddAsync(compliance, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        _logger.LogInformation("Settings import complete: {Imported} imported, {Skipped} skipped, {Errors} errors",
            imported, skipped, errors.Count);

        return new ImportResultDto(imported, skipped, errors);
    }

    private async Task<bool> ImportNumberingAsync(InvoiceNumberingExportDto numbering, CancellationToken cancellationToken)
    {
        var sequence = await _context.InvoiceSequences.FirstOrDefaultAsync(cancellationToken);
        if (sequence is null)
        {
            sequence = new InvoiceSequence(Math.Max(1, numbering.NextNumber));
            sequence.SetNumberFormat(numbering.NumberFormat);
            await _context.InvoiceSequences.AddAsync(sequence, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        var changed = false;
        if (sequence.NumberFormat is null && !string.IsNullOrWhiteSpace(numbering.NumberFormat))
        {
            sequence.SetNumberFormat(numbering.NumberFormat);
            changed = true;
        }
        if (numbering.NextNumber > sequence.CurrentValue)
        {
            sequence.SetValue(numbering.NextNumber);
            changed = true;
        }
        if (changed)
            await _context.SaveChangesAsync(cancellationToken);
        return changed;
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

    private async Task<Dictionary<long, List<long>>> RatesInIdOrderAsync(IReadOnlyCollection<long> customerIds, CancellationToken cancellationToken) =>
        (await _context.Rates.AsNoTracking()
            .Where(r => customerIds.Contains(r.CustomerId))
            .Select(r => new { r.Id, r.CustomerId })
            .ToListAsync(cancellationToken))
        .GroupBy(r => r.CustomerId)
        .ToDictionary(g => g.Key, g => g.Select(r => r.Id).Order().ToList());

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

    private static int? IndexOf(List<long> rateIds, long? rateId) =>
        rateId is { } id && rateIds.IndexOf(id) is var index and >= 0 ? index : null;

    private static long? RateAt(List<long> rateIds, int? index) =>
        index is { } i && i >= 0 && i < rateIds.Count ? rateIds[i] : null;

    private static ExpenseDto ToExpenseDto(Expense e) => new()
    {
        Description = e.Description,
        Amount = e.Amount.Amount,
        Currency = e.Amount.Currency,
        Date = e.Date
    };

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
