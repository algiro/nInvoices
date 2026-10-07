using nInvoices.Application.DTOs;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Services.InvoiceGeneration;

/// <summary>
/// The arithmetic of a new invoice: what a generation request must contain to be billable and the
/// amounts it bills. Pure: no repositories, so every rule can be tested with plain values.
/// </summary>
public static class InvoiceCalculator
{
    /// <summary>
    /// Checks the request carries what its rates need: work days (with their hours) for an hourly
    /// monthly invoice, hours for an hourly one-time invoice, and valid per-day rate choices.
    /// </summary>
    /// <exception cref="DomainException">The request can't be billed as it is.</exception>
    public static void EnsureBillable(GenerateInvoiceDto dto, DayRates rates)
    {
        var rate = rates.Default;

        if (dto.InvoiceType == InvoiceType.Monthly)
        {
            if (rate.Type == RateType.Hourly && (dto.WorkDays == null || !dto.WorkDays.Any()))
                throw new DomainException("Work days are required for hourly rate invoices.");

            rates.Validate(dto.WorkDays ?? []);

            // Every day billed by the hour needs its hours
            var workedDaysWithoutHours = (dto.WorkDays ?? [])
                .Where(wd => wd.DayType == DayType.Worked && !rates.IsFixedMonthly && rates.For(wd).Type == RateType.Hourly && DayHours(wd) <= 0)
                .ToList();

            if (workedDaysWithoutHours.Any())
                throw new DomainException(
                    $"Hours must be specified for all worked days when using hourly rates " +
                    $"(via a project allocation or the day's hours). Missing hours for {workedDaysWithoutHours.Count} day(s).");
        }

        if (rate.Type == RateType.Hourly && dto.InvoiceType == InvoiceType.OneTime && dto.Hours is not > 0)
            throw new DomainException("Hours are required for a one-time invoice with an hourly rate.");
    }

    public static Money Subtotal(GenerateInvoiceDto dto, DayRates rates)
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

    /// <summary>The expenses added up, in the first expense's currency; zero in <paramref name="defaultCurrency"/> without any.</summary>
    public static Money ExpensesTotal(ICollection<ExpenseDto>? expenses, string defaultCurrency)
    {
        if (expenses == null || expenses.Count == 0)
            return Money.Zero(defaultCurrency);

        return new Money(expenses.Sum(e => e.Amount), expenses.First().Currency);
    }

    /// <summary>
    /// Hours recorded for a work day: the sum of its project allocations when present,
    /// otherwise the day-level <see cref="WorkDayDto.HoursWorked"/> (0 when neither is set).
    /// </summary>
    public static decimal DayHours(WorkDayDto workDay)
    {
        if (workDay.Projects is { Count: > 0 })
            return workDay.Projects.Where(p => p.Hours > 0).Sum(p => p.Hours);

        return workDay.HoursWorked ?? 0m;
    }

    /// <summary>
    /// Merges duplicate references to the same project within a day, without resolving or creating
    /// projects, so names keep the spelling typed on the calendar (the in-memory counterpart of
    /// <see cref="IInvoiceWorkDays.ReplaceMonthAsync"/>, for previews).
    /// </summary>
    public static List<WorkDayDto> MergeAllocations(IEnumerable<WorkDayDto> workDays) =>
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

    private static Money SumWorkedDays(IEnumerable<WorkDayDto> workDays, DayRates rates)
    {
        var amount = workDays
            .Where(wd => wd.DayType == DayType.Worked)
            .GroupBy(wd => rates.For(wd))
            .Sum(group => group.Key.Price.Amount * group.Sum(wd => group.Key.Type == RateType.Hourly
                ? DayHours(wd)
                : (wd.HoursWorked ?? 8m) / 8m));

        return new Money(amount, rates.Default.Price.Currency);
    }
}
