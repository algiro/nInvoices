using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Infrastructure.DataPortability;

/// <summary>
/// The tax prediction's data, exported with the settings: the tax profile, business expenses (one-off
/// and recurring) and recorded tax payments.
/// </summary>
internal sealed class TaxRecordsPortability
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger _logger;

    public TaxRecordsPortability(ApplicationDbContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TaxRecordsExportDto> ExportAsync(CancellationToken cancellationToken)
    {
        var profile = await _context.TaxProfiles.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var country = await _context.UserCountries.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var expenses = await _context.BusinessExpenses.AsNoTracking().OrderBy(e => e.Date).ThenBy(e => e.Id).ToListAsync(cancellationToken);
        var recurring = await _context.RecurringExpenses.AsNoTracking().OrderBy(e => e.FromMonth).ThenBy(e => e.Id).ToListAsync(cancellationToken);
        var payments = await _context.TaxPayments.AsNoTracking()
            .OrderBy(p => p.Year).ThenBy(p => p.Period).ThenBy(p => p.Form).ToListAsync(cancellationToken);

        return new TaxRecordsExportDto(
            profile is null ? null : new TaxProfileExportDto(profile.CountryCode, profile.RegimeId, profile.Values),
            expenses.Select(e => new BusinessExpenseExportDto(
                e.Date, e.Description, e.Supplier, e.Amount.Amount, e.Amount.Currency,
                e.IndirectTax, e.ReverseCharge, e.Category, e.DeductiblePercent)).ToList(),
            recurring.Select(e => new RecurringExpenseExportDto(
                e.Description, e.Amount.Amount, e.Amount.Currency, e.Category, e.FromMonth, e.ToMonth)).ToList(),
            payments.Select(p => new TaxPaymentExportDto(
                p.Year, p.Period, p.Form, p.Amount.Amount, p.Amount.Currency, p.PaidOn)).ToList(),
            country?.CountryCode);
    }

    /// <summary>
    /// Imports what the user doesn't have yet: the country and the profile if they have none, an expense unless one with
    /// the same date, description and amount exists, a recurring expense unless one with the same
    /// description, start and amount exists, a payment unless the form and period already have one.
    /// </summary>
    public async Task<ImportResultDto> ImportAsync(TaxRecordsExportDto data, CancellationToken cancellationToken)
    {
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

        if (data.CountryCode is { } countryCode)
        {
            await Try("Country", async () =>
            {
                if (await _context.UserCountries.AnyAsync(cancellationToken))
                    return false;
                await _context.UserCountries.AddAsync(new UserCountry(countryCode), cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        if (data.Profile is { } profile)
        {
            await Try("Tax profile", async () =>
            {
                if (await _context.TaxProfiles.AnyAsync(cancellationToken))
                    return false;
                await _context.TaxProfiles.AddAsync(new TaxProfile(profile.CountryCode, profile.RegimeId, profile.Values), cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        // Encrypted columns: compared in memory
        var expenses = (await _context.BusinessExpenses.AsNoTracking().ToListAsync(cancellationToken))
            .Select(e => (e.Date, e.Description, e.Amount))
            .ToHashSet();
        foreach (var e in data.Expenses ?? [])
        {
            await Try($"Expense '{e.Description}' of {e.Date:yyyy-MM-dd}", async () =>
            {
                var amount = new Money(e.Amount, e.Currency);
                if (!expenses.Add((e.Date, e.Description.Trim(), amount)))
                    return false;
                await _context.BusinessExpenses.AddAsync(new BusinessExpense(
                    e.Date, e.Description, amount, e.Category, e.IndirectTax, e.ReverseCharge, e.DeductiblePercent, e.Supplier), cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        var recurring = (await _context.RecurringExpenses.AsNoTracking().ToListAsync(cancellationToken))
            .Select(e => (e.Description, e.FromMonth, e.Amount))
            .ToHashSet();
        foreach (var e in data.RecurringExpenses ?? [])
        {
            await Try($"Recurring expense '{e.Description}'", async () =>
            {
                var expense = new RecurringExpense(e.Description, new Money(e.Amount, e.Currency), e.Category, e.FromMonth, e.ToMonth);
                if (!recurring.Add((expense.Description, expense.FromMonth, expense.Amount)))
                    return false;
                await _context.RecurringExpenses.AddAsync(expense, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        var payments = (await _context.TaxPayments.AsNoTracking().Select(p => new { p.Year, p.Period, p.Form }).ToListAsync(cancellationToken))
            .Select(p => (p.Year, p.Period, p.Form))
            .ToHashSet();
        foreach (var p in data.Payments ?? [])
        {
            await Try($"Tax payment {p.Form} {p.Year}/{p.Period}", async () =>
            {
                var payment = new TaxPayment(p.Year, p.Period, p.Form, new Money(p.Amount, p.Currency), p.PaidOn);
                if (!payments.Add((payment.Year, payment.Period, payment.Form)))
                    return false;
                await _context.TaxPayments.AddAsync(payment, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        return new ImportResultDto(imported, skipped, errors);
    }
}
