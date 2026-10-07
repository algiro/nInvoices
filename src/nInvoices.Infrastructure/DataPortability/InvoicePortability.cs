using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Infrastructure.DataPortability;

/// <summary>Invoices, as they were issued: status, amounts and rendered document are restored as exported.</summary>
internal sealed class InvoicePortability
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger _logger;

    public InvoicePortability(ApplicationDbContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Export invoices with optional filters.
    /// </summary>
    public async Task<DataExportDto> ExportAsync(
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
        var ratesByCustomer = await PortableData.RatesInIdOrderAsync(_context, customerIds, cancellationToken);

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
            i.Expenses.Select(PortableData.ToExpenseDto).ToList(),
            i.TaxLines.Select(tl => new InvoiceTaxLineExportDto(
                tl.TaxId, tl.Description, tl.Rate, tl.BaseAmount.Amount, tl.TaxAmount.Amount, tl.Order)).ToList(),
            i.Hours,
            PortableData.IndexOf(ratesByCustomer.GetValueOrDefault(i.CustomerId) ?? [], i.RateId)
        )).ToList();

        _logger.LogInformation("Exported {Count} invoices", exported.Count);

        return new DataExportDto("1.0", DateTime.UtcNow, null, exported);
    }

    /// <summary>
    /// Import invoices. Customers must already exist (matched by FiscalId).
    /// </summary>
    public async Task<ImportResultDto> ImportAsync(
        DataExportDto data,
        CancellationToken cancellationToken)
    {
        var customerCache = await _context.Customers
            .AsNoTracking()
            .ToDictionaryAsync(c => c.FiscalId, c => c.Id, cancellationToken);
        var ratesByCustomer = await PortableData.RatesInIdOrderAsync(_context, customerCache.Values.ToList(), cancellationToken);

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
                invoice.RateId = PortableData.RateAt(ratesByCustomer.GetValueOrDefault(customerId) ?? [], invoiceData.RateIndex);

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
}
