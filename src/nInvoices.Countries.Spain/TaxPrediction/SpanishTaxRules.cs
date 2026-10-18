namespace nInvoices.Countries.Spain.TaxPrediction;

/// <summary>
/// The rates, limits and dates of the Spanish quarterly forms. When the law changes, the value
/// depends on the year here and nowhere else.
/// </summary>
public static class SpanishTaxRules
{
    /// <summary>Modelo 130: the share of the year-to-date net income paid on account (art. 110.1.a RIRPF).</summary>
    public const decimal Modelo130Rate = 0.20m;

    /// <summary>The yearly cap of the hard-to-justify expenses deduction (art. 30.2.4ª RIRPF).</summary>
    public const decimal DifficultExpensesCap = 2000m;

    /// <summary>General IGIC rate (Canary Islands).</summary>
    public const decimal IgicGeneralRate = 7m;

    /// <summary>General IVA rate (mainland and Balearic Islands).</summary>
    public const decimal IvaGeneralRate = 21m;

    /// <summary>
    /// Hard-to-justify expenses (gastos de difícil justificación) in estimación directa simplificada:
    /// a share of the net income, 7% in 2023 only, 5% otherwise.
    /// </summary>
    public static decimal DifficultExpensesRate(int year) => year == 2023 ? 0.07m : 0.05m;

    public static DateOnly QuarterStart(int year, int quarter) => new(year, (quarter - 1) * 3 + 1, 1);

    public static DateOnly QuarterEnd(int year, int quarter) => QuarterStart(year, quarter).AddMonths(3).AddDays(-1);

    /// <summary>
    /// The last day to file and pay a quarter's forms: the 20th of the month after it, or 30 January
    /// for the fourth quarter. A deadline on a weekend moves to the Monday (national holidays aside).
    /// </summary>
    public static DateOnly Deadline(int year, int quarter)
    {
        var day = quarter == 4 ? new DateOnly(year + 1, 1, 30) : new DateOnly(year, quarter * 3 + 1, 20);
        return day.DayOfWeek switch
        {
            DayOfWeek.Saturday => day.AddDays(2),
            DayOfWeek.Sunday => day.AddDays(1),
            _ => day
        };
    }

    /// <summary>Rounds to the cent, as the forms do.</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
