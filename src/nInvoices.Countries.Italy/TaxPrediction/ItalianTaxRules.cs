using System.Globalization;

namespace nInvoices.Countries.Italy.TaxPrediction;

/// <summary>
/// The rates, limits and dates of the Italian taxes of a freelancer. When the law changes, the value depends on
/// the year here and nowhere else.
/// </summary>
public static class ItalianTaxRules
{
    /// <summary>The general IVA rate, used to self-charge a reverse-charge expense.</summary>
    public const decimal IvaGeneralRate = 22m;

    /// <summary>Imposta sostitutiva of the regime forfettario (art. 1 c. 64 L. 190/2014).</summary>
    public const decimal ForfettarioRate = 0.15m;

    /// <summary>The reduced rate of the first five years of a new activity (art. 1 c. 65).</summary>
    public const decimal ForfettarioStartupRate = 0.05m;

    /// <summary>Revenue above which the regime forfettario no longer applies from the next year.</summary>
    public const decimal ForfettarioRevenueLimit = 85_000m;

    /// <summary>Revenue above which it stops applying during the year itself.</summary>
    public const decimal ForfettarioImmediateExit = 100_000m;

    /// <summary>Gestione Separata INPS, a professional without another pension fund: the default rate.</summary>
    public const decimal GestioneSeparataRate = 26.07m;

    /// <summary>The 1% interest a quarterly IVA filer adds to each payment (art. 7 DPR 542/1999).</summary>
    public const decimal QuarterlyIvaInterest = 0.01m;

    /// <summary>No income-tax installment is due when the base is at most this.</summary>
    public const decimal NoInstallmentLimit = 51.65m;

    /// <summary>Up to this, the installment is paid in one go in November; above, 40% in June and 60% in November.</summary>
    public const decimal SingleInstallmentLimit = 257.52m;

    public const decimal FirstInstallmentShare = 0.40m;
    public const decimal SecondInstallmentShare = 0.60m;

    /// <summary>The Gestione Separata pays 80% of the base on account: 40% in June, 40% in November.</summary>
    public const decimal InpsInstallmentShare = 0.40m;

    /// <summary>IRPEF brackets: (upper limit, rate), the last without a limit.</summary>
    public static IReadOnlyList<(decimal UpTo, decimal Rate)> IrpefBrackets(int year) => year switch
    {
        // Legge di Bilancio 2026: the second bracket from 35% to 33%
        >= 2026 => [(28_000m, 0.23m), (50_000m, 0.33m), (decimal.MaxValue, 0.43m)],
        // 2024 and 2025: the first two brackets merged
        >= 2024 => [(28_000m, 0.23m), (50_000m, 0.35m), (decimal.MaxValue, 0.43m)],
        _ => [(15_000m, 0.23m), (28_000m, 0.25m), (50_000m, 0.35m), (decimal.MaxValue, 0.43m)]
    };

    public static decimal Irpef(decimal taxable, int year)
    {
        var tax = 0m;
        var lower = 0m;
        foreach (var (upTo, rate) in IrpefBrackets(year))
        {
            if (taxable <= lower)
                break;
            tax += (Math.Min(taxable, upTo) - lower) * rate;
            lower = upTo;
        }
        return Round(tax);
    }

    /// <summary>
    /// The deduction of self-employment income (art. 13 c. 5 and 5-bis TUIR): €1,265 up to €5,500, falling to
    /// nothing at €50,000, plus €50 between €25,000 and €35,000.
    /// </summary>
    public static decimal SelfEmploymentDeduction(decimal income)
    {
        var deduction = income switch
        {
            <= 5_500m => 1_265m,
            <= 28_000m => 500m + 765m * (28_000m - income) / 22_500m,
            <= 50_000m => 500m * (50_000m - income) / 22_000m,
            _ => 0m
        };
        if (income is > 25_000m and <= 35_000m)
            deduction += 50m;
        return Round(Math.Max(0m, deduction));
    }

    /// <summary>16 May, 20 August, 16 November; the fourth quarter is settled in the annual return, 16 March.</summary>
    public static DateOnly IvaDeadline(int year, int quarter) => NextWorkingDay(quarter switch
    {
        1 => new DateOnly(year, 5, 16),
        2 => new DateOnly(year, 8, 20),
        3 => new DateOnly(year, 11, 16),
        _ => new DateOnly(year + 1, 3, 16)
    });

    /// <summary>The balance of the year and the first installment: 30 June.</summary>
    public static DateOnly JuneDeadline(int year) => NextWorkingDay(new DateOnly(year, 6, 30));

    /// <summary>The second installment: 30 November.</summary>
    public static DateOnly NovemberDeadline(int year) => NextWorkingDay(new DateOnly(year, 11, 30));

    public static DateOnly QuarterStart(int year, int quarter) => new(year, (quarter - 1) * 3 + 1, 1);

    public static DateOnly QuarterEnd(int year, int quarter) => QuarterStart(year, quarter).AddMonths(3).AddDays(-1);

    /// <summary>A deadline on a weekend moves to the Monday (national holidays aside).</summary>
    public static DateOnly NextWorkingDay(DateOnly day) => day.DayOfWeek switch
    {
        DayOfWeek.Saturday => day.AddDays(2),
        DayOfWeek.Sunday => day.AddDays(1),
        _ => day
    };

    /// <summary>The two income-tax installments for a base (the year's tax, or last year's): June and November.</summary>
    public static (decimal June, decimal November) TaxInstallments(decimal basis) => basis switch
    {
        <= NoInstallmentLimit => (0m, 0m),
        <= SingleInstallmentLimit => (0m, Round(basis)),
        _ => (Round(basis * FirstInstallmentShare), Round(basis * SecondInstallmentShare))
    };

    /// <summary>A number the user typed in the settings, "1234.5" or "1234,5"; null when empty or not a number.</summary>
    public static decimal? ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>Rounds to the cent, as the F24 does.</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
