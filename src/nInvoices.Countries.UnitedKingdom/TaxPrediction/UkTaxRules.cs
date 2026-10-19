using System.Globalization;

namespace nInvoices.Countries.UnitedKingdom.TaxPrediction;

/// <summary>
/// The rates, bands and dates of a United Kingdom sole trader's taxes, by tax year (named by the year it starts
/// in: 2026 is 2026/27). When the law changes, the value depends on the year here and nowhere else. Years outside
/// the known ones use the nearest (the estimate says so).
/// </summary>
public static class UkTaxRules
{
    public const int FirstKnownYear = 2024;
    public const int LastKnownYear = 2026;

    /// <summary>The last tax year whose Scottish bands are known; later years use them.</summary>
    public const int LastKnownScottishYear = 2025;

    public const decimal PersonalAllowance = 12_570m;

    /// <summary>Above this income the personal allowance shrinks by £1 for every £2.</summary>
    public const decimal AllowanceTaperThreshold = 100_000m;

    /// <summary>Class 4 National Insurance: the lower and upper profits limits, and the rates below and above the upper.</summary>
    public const decimal Class4LowerLimit = 12_570m;
    public const decimal Class4UpperLimit = 50_270m;
    public const decimal Class4MainRate = 0.06m;
    public const decimal Class4UpperRate = 0.02m;

    /// <summary>The trading allowance: deducted instead of the actual expenses when larger.</summary>
    public const decimal TradingAllowance = 1_000m;

    /// <summary>No payments on account when last year's Self Assessment bill was below this.</summary>
    public const decimal PaymentsOnAccountThreshold = 1_000m;

    public const decimal VatStandardRate = 20m;

    /// <summary>Taxable turnover above which a business must register for VAT (from 1 April 2024).</summary>
    public const decimal VatRegistrationThreshold = 90_000m;

    /// <summary>The Flat Rate Scheme's rate for a limited cost trader.</summary>
    public const decimal LimitedCostTraderRate = 16.5m;

    public const decimal StudentLoanRate = 0.09m;
    public const decimal PostgraduateLoanRate = 0.06m;

    /// <summary>The tax year that starts in <paramref name="year"/>: 6 April to 5 April.</summary>
    public static (DateOnly Start, DateOnly End) TaxYear(int year) => (new DateOnly(year, 4, 6), new DateOnly(year + 1, 4, 5));

    public static string Label(int year) => $"{year}/{((year + 1) % 100).ToString("00", CultureInfo.InvariantCulture)}";

    private static int Known(int year) => Math.Clamp(year, FirstKnownYear, LastKnownYear);

    /// <summary>
    /// The bands of taxable income (after the personal allowance), as (upper limit, rate), the last unlimited.
    /// England, Wales and Northern Ireland: basic 20%, higher 40%, additional 45% above £125,140 (frozen to 2031).
    /// </summary>
    public static IReadOnlyList<(decimal UpTo, decimal Rate)> Bands(int year, bool scottish) => !scottish
        ? [(37_700m, 0.20m), (125_140m, 0.40m), (decimal.MaxValue, 0.45m)]
        : Math.Clamp(year, FirstKnownYear, LastKnownScottishYear) switch
        {
            // Scotland: starter, basic, intermediate, higher, advanced, top (thresholds less the personal allowance)
            2024 => [(2_306m, 0.19m), (13_991m, 0.20m), (31_092m, 0.21m), (62_430m, 0.42m), (125_140m, 0.45m), (decimal.MaxValue, 0.48m)],
            _ => [(2_827m, 0.19m), (14_921m, 0.20m), (31_092m, 0.21m), (62_430m, 0.42m), (125_140m, 0.45m), (decimal.MaxValue, 0.48m)]
        };

    /// <summary>The personal allowance of an income: £12,570, tapered away above £100,000.</summary>
    public static decimal AllowanceFor(decimal income) =>
        Math.Max(0m, PersonalAllowance - Math.Floor(Math.Max(0m, income - AllowanceTaperThreshold) / 2m));

    public static decimal IncomeTax(decimal taxable, int year, bool scottish)
    {
        var tax = 0m;
        var lower = 0m;
        foreach (var (upTo, rate) in Bands(year, scottish))
        {
            if (taxable <= lower)
                break;
            tax += (Math.Min(taxable, upTo) - lower) * rate;
            lower = upTo;
        }
        return Round(tax);
    }

    public static decimal Class4(decimal profit) =>
        Round(Math.Max(0m, Math.Min(profit, Class4UpperLimit) - Class4LowerLimit) * Class4MainRate
              + Math.Max(0m, profit - Class4UpperLimit) * Class4UpperRate);

    /// <summary>The repayment threshold of a student loan plan, by tax year; null for a plan not yet repaid that year.</summary>
    public static decimal? StudentLoanThreshold(string plan, int year) => (plan, Known(year)) switch
    {
        ("plan1", 2024) => 24_990m,
        ("plan1", 2025) => 26_065m,
        ("plan1", _) => 26_900m,
        ("plan2", 2024) => 27_295m,
        ("plan2", 2025) => 28_470m,
        ("plan2", _) => 29_385m,
        ("plan4", 2024) => 31_395m,
        ("plan4", 2025) => 32_745m,
        ("plan4", _) => 33_795m,
        // Plan 5 repayments start in April 2026
        ("plan5", 2024) => null,
        ("plan5", _) => 25_000m,
        ("postgraduate", _) => 21_000m,
        _ => null
    };

    /// <summary>What a plan collects on an income: its rate on the part above the threshold, in whole pounds.</summary>
    public static decimal StudentLoan(string plan, decimal income, int year) =>
        StudentLoanThreshold(plan, year) is { } threshold
            ? Math.Floor(Math.Max(0m, income - threshold) * (plan == "postgraduate" ? PostgraduateLoanRate : StudentLoanRate))
            : 0m;

    /// <summary>The first payment on account: 31 January in the tax year.</summary>
    public static DateOnly FirstPaymentOnAccount(int year) => new(year + 1, 1, 31);

    /// <summary>The second payment on account: 31 July after it.</summary>
    public static DateOnly SecondPaymentOnAccount(int year) => new(year + 1, 7, 31);

    /// <summary>The balancing payment, with the online return: 31 January after that.</summary>
    public static DateOnly BalancingPayment(int year) => new(year + 2, 1, 31);

    /// <summary>A VAT return and its payment: one calendar month and seven days after the period ends.</summary>
    public static DateOnly VatDeadline(DateOnly periodEnd) => periodEnd.AddDays(1).AddMonths(1).AddDays(6);

    /// <summary>A number the user typed in the settings, "1234.5" or "1,234.50"; null when empty or not a number.</summary>
    public static decimal? ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return decimal.TryParse(text.Trim().Replace("£", string.Empty, StringComparison.Ordinal), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
