using System.Globalization;
using nInvoices.Application.Localization;
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

    private readonly IAppText _text;

    public GermanyTaxPredictionModule(IAppText text)
    {
        _text = text;
    }

    public string CountryCode => "DE";

    public string DisplayName => _text.Translate("countries.DE");

    public string Currency => "EUR";

    public string IndirectTaxName => "USt (MwSt)";

    private List<ComplianceField> CommonFields(bool trade)
    {
        var fields = new List<ComplianceField>
        {
            new(SmallBusinessKey, T("taxPrediction.germany.fields.smallBusiness.label"), ComplianceFieldType.Boolean,
                Help: T("taxPrediction.germany.fields.smallBusiness.help")),
            new(VatPeriodKey, T("taxPrediction.germany.fields.vatPeriod.label"), ComplianceFieldType.Choice,
                Help: T("taxPrediction.germany.fields.vatPeriod.help"),
                Options: [new(Quarterly, T("taxPrediction.germany.fields.vatPeriod.quarterly")), new(Monthly, T("taxPrediction.germany.fields.vatPeriod.monthly"))]),
            new(ExtensionKey, T("taxPrediction.germany.fields.extension.label"), ComplianceFieldType.Boolean),
            new(ChurchTaxKey, "Kirchensteuer", ComplianceFieldType.Choice,
                Options: [new("0", T("taxPrediction.germany.fields.churchTax.none")), new("8", T("taxPrediction.germany.fields.churchTax.rate8")), new("9", T("taxPrediction.germany.fields.churchTax.rate9"))]),
            new(InsuranceKey, T("taxPrediction.germany.fields.insurance.label"),
                Help: T("taxPrediction.germany.fields.insurance.help")),
            new(PrepaymentKey, T("taxPrediction.germany.fields.prepayment.label"),
                Help: T("taxPrediction.germany.fields.prepayment.help"))
        };
        if (trade)
        {
            fields.Add(new(HebesatzKey, T("taxPrediction.germany.fields.hebesatz.label"), Required: true, Help: T("taxPrediction.germany.fields.hebesatz.help")));
            fields.Add(new(TradeTaxPrepaymentKey, T("taxPrediction.germany.fields.tradeTaxPrepayment.label"),
                Help: T("taxPrediction.germany.fields.tradeTaxPrepayment.help")));
        }
        return fields;
    }

    private static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        [VatPeriodKey] = Quarterly,
        [ChurchTaxKey] = "0"
    };

    // Built on each read, in the language of the request
    public IReadOnlyList<TaxRegime> Regimes =>
    [
        new TaxRegime(FreelancerRegime, T("taxPrediction.germany.regimes.freiberufler.name"), CommonFields(trade: false),
            T("taxPrediction.germany.regimes.freiberufler.help"),
            Defaults),
        new TaxRegime(TradeRegime, T("taxPrediction.germany.regimes.gewerbe.name"), CommonFields(trade: true),
            T("taxPrediction.germany.regimes.gewerbe.help"),
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

        var hebesatz = isTrade ? Number(values, HebesatzKey, "taxPrediction.germany.what.hebesatz", warnings) ?? 400m : (decimal?)null;
        var insurance = Number(values, InsuranceKey, "taxPrediction.germany.what.insurance", warnings) ?? 0m;
        var church = Number(values, ChurchTaxKey, "taxPrediction.germany.what.churchTax", warnings) ?? 0m;
        var prepayment = Number(values, PrepaymentKey, "taxPrediction.germany.what.prepayment", warnings);
        var tradePrepayment = isTrade ? Number(values, TradeTaxPrepaymentKey, "taxPrediction.germany.what.tradeTaxPrepayment", warnings) : null;

        if (year < FirstKnownYear || year > LastKnownYear)
            warnings.Add(_text.Translate("taxPrediction.germany.warnings.tariffYear", ("year", year), ("tariffYear", Math.Clamp(year, FirstKnownYear, LastKnownYear))));
        warnings.Add(T("taxPrediction.germany.warnings.cashBasis"));
        warnings.Add(T("taxPrediction.germany.warnings.singleAssessment"));

        var taxes = GermanIncomeTaxCalculator.Calculate(year, input.Income, input.Expenses, hebesatz, insurance, church, _text);

        if (isSmallBusiness && taxes.Revenue > SmallBusinessCurrentYearLimit)
            warnings.Add(_text.Translate("taxPrediction.germany.warnings.smallBusinessExceeded", ("limit", SmallBusinessCurrentYearLimit.ToString("N0", CultureInfo.CurrentCulture))));
        if (isTrade)
            warnings.Add(T("taxPrediction.germany.warnings.tradeTaxAdjustments"));

        var periods = new List<(DateOnly Deadline, TaxPeriodPrediction Period)>();

        // VAT returns
        if (!isSmallBusiness)
        {
            var monthly = values.GetValueOrDefault(VatPeriodKey) == Monthly;
            var extension = values.GetValueOrDefault(ExtensionKey) == "true";
            if (monthly && extension)
                warnings.Add(T("taxPrediction.germany.warnings.specialPrepayment"));
            foreach (var (period, name, start, end, form) in VatReturnCalculator.Calculate(
                         year, input.Income, input.Expenses, monthly, extension, Paid(input.Payments, VatReturnCalculator.Code), _text))
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
            var incomeForm = new TaxFormPrediction(IncomeTaxCode, _text.Translate("taxPrediction.germany.incomePrepayment.form", ("quarter", quarter)), incomeDeadline, incomeAmount, incomePaid,
            [
                prepayment is null
                    ? new TaxFormLine(null, T("taxPrediction.germany.lines.quarterOfEstimate"), incomeAmount)
                    : new TaxFormLine(null, T("taxPrediction.germany.lines.asSetInPrepaymentNotice"), incomeAmount)
            ]);
            periods.Add((incomeDeadline, new TaxPeriodPrediction(
                IncomeTaxPeriod + quarter - 1, _text.Translate("taxPrediction.germany.incomePrepayment.period", ("quarter", quarter)), WindowStart(year, quarter, 3, 10), new DateOnly(year, quarter * 3, 1).AddDays(-1), [incomeForm])));

            if (isTrade)
            {
                var tradeDeadline = TradeTaxPrepaymentDeadline(year, quarter);
                var tradeAmount = Round(tradePrepayment ?? taxes.TradeTax.Amount / 4m);
                decimal? tradePaid = paidTradeTax.TryGetValue(TradeTaxPeriod + quarter - 1, out var b) ? b : null;
                onAccountTrade += tradePaid ?? tradeAmount;

                var tradeForm = new TaxFormPrediction(TradeTaxCode, _text.Translate("taxPrediction.germany.tradePrepayment.form", ("quarter", quarter)), tradeDeadline, tradeAmount, tradePaid,
                [
                    tradePrepayment is null
                        ? new TaxFormLine(null, T("taxPrediction.germany.lines.quarterOfEstimate"), tradeAmount)
                        : new TaxFormLine(null, T("taxPrediction.germany.lines.asSetInNotice"), tradeAmount)
                ]);
                periods.Add((tradeDeadline, new TaxPeriodPrediction(
                    TradeTaxPeriod + quarter - 1, _text.Translate("taxPrediction.germany.tradePrepayment.period", ("quarter", quarter)), WindowStart(year, quarter, 2, 15), new DateOnly(year, quarter * 3 - 1, 1).AddDays(-1), [tradeForm])));
            }
        }

        // The balance of the year, after the annual returns
        var balanceForms = new List<TaxFormPrediction> { Balance(IncomeTaxCode, "Einkommensteuer", year, taxes.IncomeTax, onAccountIncome, paidIncomeTax) };
        if (isTrade)
            balanceForms.Add(Balance(TradeTaxCode, "Gewerbesteuer", year, taxes.TradeTax, onAccountTrade, paidTradeTax));
        warnings.Add(T("taxPrediction.germany.warnings.balanceDue"));
        periods.Add((AnnualReturnDeadline(year), new TaxPeriodPrediction(
            BalancePeriod, T("taxPrediction.germany.periods.annualReturns"), new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), balanceForms)));

        return new TaxYearPrediction(year, periods.OrderBy(p => p.Deadline).ThenBy(p => p.Period.Period).Select(p => p.Period).ToList(), warnings);
    }

    private TaxFormPrediction Balance(string code, string name, int year, GermanYearAmount due, decimal onAccount, IReadOnlyDictionary<int, decimal> paid)
    {
        var balance = due.Amount - onAccount;
        var lines = due.Lines.ToList();
        lines.Add(new TaxFormLine(null, T("taxPrediction.germany.balance.paidOnAccount"), onAccount));
        lines.Add(new TaxFormLine(null, T(balance >= 0 ? "taxPrediction.germany.balance.toPay" : "taxPrediction.germany.refund"), balance));
        decimal? recorded = paid.TryGetValue(BalancePeriod, out var amount) ? amount : null;
        return new TaxFormPrediction(code, _text.Translate("taxPrediction.germany.balance.name", ("name", name)), AnnualReturnDeadline(year), Math.Max(0m, balance), recorded, lines);
    }

    // The day after the previous prepayment of the same kind (the year's first starts on 1 January)
    private static DateOnly WindowStart(int year, int quarter, int monthInQuarter, int day) =>
        quarter == 1 ? new DateOnly(year, 1, 1) : new DateOnly(year, (quarter - 1) * 3 - (3 - monthInQuarter), day).AddDays(1);

    /// <param name="what">The key of what the value is ("the Hebesatz"), for the warning when it is not a number.</param>
    private decimal? Number(IReadOnlyDictionary<string, string> values, string key, string what, List<string> warnings)
    {
        var text = values.GetValueOrDefault(key);
        var number = ParseAmount(text);
        if (number is null && !string.IsNullOrWhiteSpace(text))
            warnings.Add(_text.Translate("taxPrediction.common.notANumber", ("text", text), ("what", T(what))));
        return number;
    }

    private string T(string key) => _text.Translate(key);

    private static IReadOnlyDictionary<int, decimal> Paid(IReadOnlyList<RecordedTaxPayment> payments, string form) =>
        payments
            .Where(p => p.Form == form)
            .GroupBy(p => p.Period)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
}
