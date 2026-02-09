using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Api.Controllers;

/// <summary>
/// Import/Export controller for data migration and backups.
/// Exports/imports customers (with rates, taxes, templates) and invoices as JSON.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ImportExportController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ImportExportController> _logger;

    public ImportExportController(
        ApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<ImportExportController> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Export all customers with their rates, taxes, templates, and monthly report templates.
    /// </summary>
    [HttpGet("customers")]
    [ProducesResponseType(typeof(DataExportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DataExportDto>> ExportCustomers(CancellationToken cancellationToken)
    {
        var customers = await _context.Customers
            .Include(c => c.Rates)
            .Include(c => c.Taxes)
            .Include(c => c.Templates)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var monthlyTemplates = await _context.MonthlyReportTemplates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var templatesByCustomer = monthlyTemplates
            .GroupBy(mt => mt.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var exported = customers.Select(c => new CustomerExportDto(
            c.Name,
            c.FiscalId,
            new AddressDto(c.Address.Street, c.Address.HouseNumber, c.Address.City,
                c.Address.ZipCode, c.Address.Country, c.Address.State),
            c.CreatedAt,
            c.Rates.Select(r => new RateExportDto(r.Type, new MoneyDto(r.Price.Amount, r.Price.Currency), r.CreatedAt)).ToList(),
            c.Taxes.Select(t => new TaxExportDto(t.TaxId, t.Description, t.HandlerId, t.Rate,
                t.ApplicationType, FindTaxIdByPk(c.Taxes, t.AppliedToTaxId), t.Order, t.IsActive, t.CreatedAt)).ToList(),
            c.Templates.Select(t => new InvoiceTemplateExportDto(t.InvoiceType, t.Name, t.Content, t.IsActive, t.CreatedAt)).ToList(),
            (templatesByCustomer.GetValueOrDefault(c.Id) ?? [])
                .Select(mt => new MonthlyReportTemplateExportDto(mt.InvoiceType, mt.Name, mt.Content, mt.IsActive, mt.CreatedAt)).ToList()
        )).ToList();

        _logger.LogInformation("Exported {Count} customers", exported.Count);

        return Ok(new DataExportDto("1.0", DateTime.UtcNow, exported, null));
    }

    /// <summary>
    /// Export invoices with optional filters.
    /// </summary>
    [HttpGet("invoices")]
    [ProducesResponseType(typeof(DataExportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DataExportDto>> ExportInvoices(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] long? customerId,
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
            i.Expenses.Select(e => new ExpenseDto
            {
                Description = e.Description,
                Amount = e.Amount.Amount,
                Currency = e.Amount.Currency,
                Date = e.Date
            }).ToList(),
            i.TaxLines.Select(tl => new InvoiceTaxLineExportDto(
                tl.TaxId, tl.Description, tl.Rate, tl.BaseAmount.Amount, tl.TaxAmount.Amount, tl.Order)).ToList()
        )).ToList();

        _logger.LogInformation("Exported {Count} invoices", exported.Count);

        return Ok(new DataExportDto("1.0", DateTime.UtcNow, null, exported));
    }

    /// <summary>
    /// Import customers with their rates, taxes, templates, and monthly report templates.
    /// Customers are matched by FiscalId - existing customers are skipped.
    /// </summary>
    [HttpPost("customers")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResultDto>> ImportCustomers(
        [FromBody] DataExportDto data,
        CancellationToken cancellationToken)
    {
        if (data.Customers is null || data.Customers.Count == 0)
            return BadRequest(new { error = "No customers to import" });

        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var customerData in data.Customers)
        {
            try
            {
                var existing = await _context.Customers
                    .FirstOrDefaultAsync(c => c.FiscalId == customerData.FiscalId, cancellationToken);

                if (existing is not null)
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

                var customer = new Customer(customerData.Name, customerData.FiscalId, address);
                await _context.Customers.AddAsync(customer, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                // Import rates
                foreach (var rateData in customerData.Rates)
                {
                    var rate = new Rate(customer.Id, rateData.Type, new Money(rateData.Price.Amount, rateData.Price.Currency));
                    await _context.Rates.AddAsync(rate, cancellationToken);
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

                await _context.SaveChangesAsync(cancellationToken);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Customer '{customerData.Name}' ({customerData.FiscalId}): {ex.Message}");
                _logger.LogError(ex, "Failed to import customer {FiscalId}", customerData.FiscalId);
            }
        }

        _logger.LogInformation("Import complete: {Imported} imported, {Skipped} skipped, {Errors} errors",
            imported, skipped, errors.Count);

        return Ok(new ImportResultDto(imported, skipped, errors));
    }

    /// <summary>
    /// Import invoices. Customers must already exist (matched by FiscalId).
    /// </summary>
    [HttpPost("invoices")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResultDto>> ImportInvoices(
        [FromBody] DataExportDto data,
        CancellationToken cancellationToken)
    {
        if (data.Invoices is null || data.Invoices.Count == 0)
            return BadRequest(new { error = "No invoices to import" });

        var customerCache = await _context.Customers
            .AsNoTracking()
            .ToDictionaryAsync(c => c.FiscalId, c => c.Id, cancellationToken);

        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var invoiceData in data.Invoices)
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
                invoice.TotalExpenses = new Money(invoiceData.TotalExpenses.Amount, currency);
                invoice.TotalTaxes = new Money(invoiceData.TotalTaxes.Amount, currency);
                invoice.Total = new Money(invoiceData.Total.Amount, currency);
                invoice.RenderedContent = invoiceData.RenderedContent;
                invoice.Notes = invoiceData.Notes;
                invoice.Status = invoiceData.Status;

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

        return Ok(new ImportResultDto(imported, skipped, errors));
    }

    private static string? FindTaxIdByPk(ICollection<Tax> taxes, long? pk) =>
        pk.HasValue ? taxes.FirstOrDefault(t => t.Id == pk.Value)?.TaxId : null;
}

public sealed record ImportResultDto(int Imported, int Skipped, IReadOnlyList<string> Errors);
