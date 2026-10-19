using System.Globalization;

namespace nInvoices.Core.TaxPrediction;

/// <summary>
/// The days a country's tax year covers. Most countries tax the calendar year; the United Kingdom's runs from
/// 6 April to 5 April. A year is named by the calendar year it starts in.
/// </summary>
/// <param name="Year">The calendar year it starts in: what the user picks and payments are recorded under.</param>
/// <param name="Label">How it is shown, e.g. "2026" or "2026/27".</param>
public sealed record TaxYear(int Year, DateOnly Start, DateOnly End, string Label)
{
    public static TaxYear Calendar(int year) =>
        new(year, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), year.ToString(CultureInfo.InvariantCulture));

    public bool Contains(DateOnly day) => day >= Start && day <= End;
}
