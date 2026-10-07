using System.Globalization;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;

namespace nInvoices.Application.Services.InvoiceGeneration;

/// <summary>
/// The rates a monthly invoice is billed with: the invoice's default rate, and the rates worked
/// days chose for themselves (say some days at one hourly rate, some at another, some at the daily
/// rate). A day without a rate of its own uses the default.
/// </summary>
public sealed class DayRates
{
    private readonly IReadOnlyDictionary<long, Rate> _byId;

    public DayRates(Rate defaultRate, IEnumerable<Rate> dayRates)
    {
        Default = defaultRate;
        _byId = dayRates.Concat([defaultRate]).GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>The rate of days that don't choose one, and the one that sets the invoice's currency.</summary>
    public Rate Default { get; }

    /// <summary>A fixed monthly price: days are not priced one by one.</summary>
    public bool IsFixedMonthly => Default.Type == RateType.Monthly;

    /// <summary>The ids of the rates worked days ask for, other than the default; these have to be loaded.</summary>
    public static IReadOnlyCollection<long> RequestedIds(IEnumerable<WorkDayDto>? days, Rate defaultRate) =>
        (days ?? [])
            .Where(d => d.DayType == DayType.Worked && d.RateId.HasValue && d.RateId.Value != defaultRate.Id)
            .Select(d => d.RateId!.Value)
            .Distinct()
            .ToList();

    /// <summary>The rate this day is billed at.</summary>
    /// <exception cref="InvalidOperationException">The day chose a rate that is not one of the customer's.</exception>
    public Rate For(WorkDayDto day)
    {
        if (day.RateId is not long id)
            return Default;

        return _byId.TryGetValue(id, out var rate)
            ? rate
            : throw new DomainException($"Rate {id} not found for this customer ({day.Date:yyyy-MM-dd}).");
    }

    /// <summary>
    /// Checks the days' rate choices: a fixed monthly rate leaves no room for per-day rates, only
    /// daily and hourly rates can be chosen for a day, and one invoice bills in one currency.
    /// </summary>
    /// <exception cref="InvalidOperationException">A rate choice is not valid.</exception>
    public void Validate(IEnumerable<WorkDayDto> days)
    {
        var worked = days.Where(d => d.DayType == DayType.Worked).ToList();

        if (IsFixedMonthly)
        {
            if (worked.Any(d => d.RateId.HasValue && d.RateId != Default.Id))
                throw new DomainException("Days can't have their own rate when the invoice uses a fixed monthly rate.");
            return;
        }

        foreach (var day in worked)
        {
            var rate = For(day);
            if (rate.Type == RateType.Monthly)
                throw new DomainException($"A fixed monthly rate can't be used for a single day ({day.Date:yyyy-MM-dd}).");
            if (!string.Equals(rate.Price.Currency, Default.Price.Currency, StringComparison.OrdinalIgnoreCase))
                throw new DomainException(
                    $"An invoice is billed in one currency: {rate.Price.Currency} on {day.Date:yyyy-MM-dd} differs from {Default.Price.Currency}.");
        }
    }

    /// <summary>True when the worked days use more than one rate, so the invoice has to say which is which.</summary>
    public bool IsMixed(IEnumerable<WorkDayDto> days) =>
        !IsFixedMonthly && days.Where(d => d.DayType == DayType.Worked).Select(d => For(d).Id).Distinct().Count() > 1;

    /// <summary>A short label for a rate on the invoice: its name and price, or just the price.</summary>
    public static string Label(Rate rate)
    {
        var unit = rate.Type switch
        {
            RateType.Hourly => "h",
            RateType.Monthly => "month",
            _ => "day"
        };
        var price = $"{rate.Price.Amount.ToString("0.##", CultureInfo.InvariantCulture)} {rate.Price.Currency}/{unit}";

        return string.IsNullOrWhiteSpace(rate.Name) ? price : $"{rate.Name} ({price})";
    }
}
