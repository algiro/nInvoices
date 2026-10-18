using System.Globalization;
using nInvoices.Core.Compliance;
using nInvoices.Core.TaxPrediction;
using static nInvoices.Countries.Germany.TaxPrediction.GermanTaxRules;

namespace nInvoices.Countries.Germany.TaxPrediction;

/// <summary>
/// Germany, for a self-employed person with an Einnahmenüberschussrechnung: as a Freiberufler (Einkommensteuer,
/// Soli, Kirchensteuer) or a Gewerbetreibender (plus the Gewerbesteuer); VAT returns monthly or quarterly
/// unless a Kleinunternehmer; the quarterly prepayments the Finanzamt set, and the balance of the year.
/// </summary>
public sealed class GermanyTaxPredictionModule : ITaxPredictionModule
{
    public const string FreelancerRegime = "freiberufler";
    public const string TradeRegime = "gewerbe";

    public const string SmallBusinessKey = "smallBusiness";
    public const string VatPeriodKey = "vatPeriod";
    public const string Monthly = "monthly";
    public const string Quarterly = "quarterly";
    public const string ExtensionKey = "extension";
    public const string ChurchTaxKey = "churchTax";
    public const string InsuranceKey = "deductibleInsurance";
    public const string PrepaymentKey = "prepayment";
    public const string HebesatzKey = "hebesatz";
    public const string TradeTaxPrepaymentKey = "tradeTaxPrepayment";

    /// <summary>The indirect tax, as <see cref="ChargedTax.Kind"/> names it.</summary>
    public const string VatKind = "USt";

    public const string IncomeTaxCode = "ESt";
    public const string TradeTaxCode = "GewSt";

    // The periods payments are recorded under: the VAT returns' (1-12), then the prepayments and the balance
    private const int IncomeTaxPeriod = 13;
    private const int TradeTaxPeriod = 17;
    private const int BalancePeriod = 21;

    public string CountryCode => "DE";

    public string DisplayName => "Germany";

    public string Currency => "EUR";

    public string IndirectTaxName => "USt (MwSt)";

    private static IReadOnlyList<ComplianceField> CommonFields(bool trade)
    {
        var fields = new List<ComplianceField>
        {
            new(SmallBusinessKey, "Kleinunternehmer (§ 19 UStG): no VAT returns", ComplianceFieldType.Boolean,
                Help: "Below €25,000 of revenue last year and €100,000 this year you may charge no VAT."),
            new(VatPeriodKey, "VAT returns (Umsatzsteuer-Voranmeldung)", ComplianceFieldType.Choice,
                Help: "Quarterly is the rule when last year's VAT was at most €9,000; monthly above.",
                Options: [new(Quarterly, "Quarterly"), new(Monthly, "Monthly")]),
            new(ExtensionKey, "Dauerfristverlängerung: one month more for each return", ComplianceFieldType.Boolean),
            new(ChurchTaxKey, "Kirchensteuer", ComplianceFieldType.Choice,
                Options: [new("0", "None"), new("8", "8% (Bavaria, Baden-Württemberg)"), new("9", "9% (other Länder)")]),
            new(InsuranceKey, "Deductible insurance per year (€)",
                Help: "Health, care and pension contributions you deduct as Vorsorgeaufwendungen (only the deductible part)."),
            new(PrepaymentKey, "Einkommensteuer-Vorauszahlung per quarter (€)",
                Help: "From your Vorauszahlungsbescheid, Soli and Kirchensteuer included. Left empty, each quarter is a quarter of this year's estimate.")
        };
        if (trade)
        {
            fields.Add(new(HebesatzKey, "Hebesatz of your municipality (%)", Required: true, Help: "E.g. 400; Berlin 410, Munich 490."));
            fields.Add(new(TradeTaxPrepaymentKey, "Gewerbesteuer-Vorauszahlung per quarter (€)",
                Help: "From your municipality's Bescheid. Left empty, a quarter of this year's estimate."));
        }
        return fields;
    }

    private static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        [VatPeriodKey] = Quarterly,
        [ChurchTaxKey] = "0"
    };

    public IReadOnlyList<TaxRegime> Regimes { get; } =
    [
        new TaxRegime(FreelancerRegime, "Freiberufler (liberal profession)", CommonFields(trade: false),
            "Einkommensteuer, Soli and Kirchensteuer: four prepayments and the balance; VAT returns unless a Kleinunternehmer.",
            Defaults),
        new TaxRegime(TradeRegime, "Gewerbetreibender (trade)", CommonFields(trade: true),
            "As a Freiberufler, plus the Gewerbesteuer (partly credited against the Einkommensteuer).",
            new Dictionary<string, string>(Defaults) { [HebesatzKey] = "400" })
    ];

    public string? IndirectTaxKind(IReadOnlyDictionary<string, string> taxValues)
    {
        ArgumentNullException.ThrowIfNull(taxValues);
        return VatKind;
    }

    public TaxYearPrediction Predict(TaxPredictionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.RegimeId is not (FreelancerRegime or TradeRegime))
            throw new ArgumentException($"\"{input.RegimeId}\" is not a German tax regime", nameof(input));

        var year = input.Year;
        var values = input.Values;
        var warnings = new List<string>();
        var isTrade = input.RegimeId == TradeRegime;
        var isSmallBusiness = values.GetValueOrDefault(SmallBusinessKey) == "true";

        var hebesatz = isTrade ? Number(values, HebesatzKey, "the Hebesatz", warnings) ?? 400m : (decimal?)null;
        var insurance = Number(values, InsuranceKey, "the deductible insurance", warnings) ?? 0m;
        var church = Number(values, ChurchTaxKey, "the Kirchensteuer", warnings) ?? 0m;
        var prepayment = Number(values, PrepaymentKey, "the Vorauszahlung", warnings);
        var tradePrepayment = isTrade ? Number(values, TradeTaxPrepaymentKey, "the Gewerbesteuer-Vorauszahlung", warnings) : null;

        if (year < FirstKnownYear || year > LastKnownYear)
            warnings.Add($"The Einkommensteuer of {year} is estimated with the tariff of {Math.Clamp(year, FirstKnownYear, LastKnownYear)}.");
        warnings.Add("Revenue and expenses count when invoiced; the Einnahmenüberschussrechnung counts them when paid.");
        warnings.Add("Single assessment (Grundtarif): a joint assessment with a spouse (Splitting) is not estimated.");

        var taxes = GermanIncomeTaxCalculator.Calculate(year, input.Income, input.Expenses, hebesatz, insurance, church);

        if (isSmallBusiness && taxes.Revenue > SmallBusinessCurrentYearLimit)
            warnings.Add($"Revenue above €{SmallBusinessCurrentYearLimit:N0}: you are no longer a Kleinunternehmer, and VAT is due from the invoice that crossed it.");
        if (isTrade)
            warnings.Add("Gewerbesteuer additions and deductions (Hinzurechnungen, Kürzungen) are not estimated.");

        var periods = new List<(DateOnly Deadline, TaxPeriodPrediction Period)>();

        // VAT returns
        if (!isSmallBusiness)
        {
            var monthly = values.GetValueOrDefault(VatPeriodKey) == Monthly;
            var extension = values.GetValueOrDefault(ExtensionKey) == "true";
            if (monthly && extension)
                warnings.Add("The Sondervorauszahlung that a monthly filer pays for the Dauerfristverlängerung is not estimated.");
            foreach (var (period, name, start, end, form) in VatReturnCalculator.Calculate(
                         year, input.Income, input.Expenses, monthly, extension, Paid(input.Payments, VatReturnCalculator.Code)))
                periods.Add((form.Deadline, new TaxPeriodPrediction(period, name, start, end, [form])));
        }

        // Prepayments: what the Finanzamt set, or a quarter of the estimate
        var paidIncomeTax = Paid(input.Payments, IncomeTaxCode);
        var paidTradeTax = Paid(input.Payments, TradeTaxCode);
        var onAccountIncome = 0m;
        var onAccountTrade = 0m;
        for (var quarter = 1; quarter <= 4; quarter++)
        {
            var incomeDeadline = IncomeTaxPrepaymentDeadline(year, quarter);
            var incomeAmount = Round(prepayment ?? taxes.IncomeTax.Amount / 4m);
            decimal? incomePaid = paidIncomeTax.TryGetValue(IncomeTaxPeriod + quarter - 1, out var a) ? a : null;
            onAccountIncome += incomePaid ?? incomeAmount;

            // The window ends with the month before the deadline's: the payment is then due until the 10th
            var incomeForm = new TaxFormPrediction(IncomeTaxCode, $"Einkommensteuer-Vorauszahlung · Q{quarter}", incomeDeadline, incomeAmount, incomePaid,
            [
                prepayment is null
                    ? new TaxFormLine(null, "A quarter of this year's estimate", incomeAmount)
                    : new TaxFormLine(null, "As set in the Vorauszahlungsbescheid", incomeAmount)
            ]);
            periods.Add((incomeDeadline, new TaxPeriodPrediction(
                IncomeTaxPeriod + quarter - 1, $"ESt prepayment Q{quarter}", WindowStart(year, quarter, 3, 10), new DateOnly(year, quarter * 3, 1).AddDays(-1), [incomeForm])));

            if (isTrade)
            {
                var tradeDeadline = TradeTaxPrepaymentDeadline(year, quarter);
                var tradeAmount = Round(tradePrepayment ?? taxes.TradeTax.Amount / 4m);
                decimal? tradePaid = paidTradeTax.TryGetValue(TradeTaxPeriod + quarter - 1, out var b) ? b : null;
                onAccountTrade += tradePaid ?? tradeAmount;

                var tradeForm = new TaxFormPrediction(TradeTaxCode, $"Gewerbesteuer-Vorauszahlung · Q{quarter}", tradeDeadline, tradeAmount, tradePaid,
                [
                    tradePrepayment is null
                        ? new TaxFormLine(null, "A quarter of this year's estimate", tradeAmount)
                        : new TaxFormLine(null, "As set in the Bescheid", tradeAmount)
                ]);
                periods.Add((tradeDeadline, new TaxPeriodPrediction(
                    TradeTaxPeriod + quarter - 1, $"GewSt prepayment Q{quarter}", WindowStart(year, quarter, 2, 15), new DateOnly(year, quarter * 3 - 1, 1).AddDays(-1), [tradeForm])));
            }
        }

        // The balance of the year, after the annual returns
        var balanceForms = new List<TaxFormPrediction> { Balance(IncomeTaxCode, "Einkommensteuer", year, taxes.IncomeTax, onAccountIncome, paidIncomeTax) };
        if (isTrade)
            balanceForms.Add(Balance(TradeTaxCode, "Gewerbesteuer", year, taxes.TradeTax, onAccountTrade, paidTradeTax));
        warnings.Add("The balance is due about a month after the assessment (Bescheid); its date is the filing deadline of the returns.");
        periods.Add((AnnualReturnDeadline(year), new TaxPeriodPrediction(
            BalancePeriod, "Annual returns", new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), balanceForms)));

        return new TaxYearPrediction(year, periods.OrderBy(p => p.Deadline).ThenBy(p => p.Period.Period).Select(p => p.Period).ToList(), warnings);
    }

    private static TaxFormPrediction Balance(string code, string name, int year, GermanYearAmount due, decimal onAccount, IReadOnlyDictionary<int, decimal> paid)
    {
        var balance = due.Amount - onAccount;
        var lines = due.Lines.ToList();
        lines.Add(new TaxFormLine(null, "Paid on account (Vorauszahlungen)", onAccount));
        lines.Add(new TaxFormLine(null, balance >= 0 ? "Balance to pay (Abschlusszahlung)" : "Refund (Erstattung)", balance));
        decimal? recorded = paid.TryGetValue(BalancePeriod, out var amount) ? amount : null;
        return new TaxFormPrediction(code, $"{name} · balance of the year", AnnualReturnDeadline(year), Math.Max(0m, balance), recorded, lines);
    }

    // The day after the previous prepayment of the same kind (the year's first starts on 1 January)
    private static DateOnly WindowStart(int year, int quarter, int monthInQuarter, int day) =>
        quarter == 1 ? new DateOnly(year, 1, 1) : new DateOnly(year, (quarter - 1) * 3 - (3 - monthInQuarter), day).AddDays(1);

    private static decimal? Number(IReadOnlyDictionary<string, string> values, string key, string what, List<string> warnings)
    {
        var text = values.GetValueOrDefault(key);
        var number = ParseAmount(text);
        if (number is null && !string.IsNullOrWhiteSpace(text))
            warnings.Add($"\"{text}\" is not a number: {what} is left out. Correct it in Settings.");
        return number;
    }

    private static IReadOnlyDictionary<int, decimal> Paid(IReadOnlyList<RecordedTaxPayment> payments, string form) =>
        payments
            .Where(p => p.Form == form)
            .GroupBy(p => p.Period)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
}
