using nInvoices.Application.Localization;
using nInvoices.Core.Compliance;
using nInvoices.Core.TaxPrediction;
using static nInvoices.Countries.UnitedKingdom.TaxPrediction.UkTaxRules;

namespace nInvoices.Countries.UnitedKingdom.TaxPrediction;

/// <summary>
/// The United Kingdom, for a sole trader filing Self Assessment: Income Tax (the rest of the UK's bands or
/// Scotland's), Class 4 National Insurance and student loan repayments, paid in two payments on account and a
/// balancing payment; VAT returns every quarter, or the Flat Rate Scheme, unless not VAT registered. The tax year
/// runs from 6 April to 5 April.
/// </summary>
public sealed class UkTaxPredictionModule : ITaxPredictionModule
{
    public const string SoleTraderRegime = "soleTrader";

    public const string VatSchemeKey = "vatScheme";
    public const string NotRegistered = "none";
    public const string Standard = "standard";
    public const string FlatRate = "flatRate";
    public const string StaggerKey = "stagger";
    public const string FlatRateKey = "flatRatePercent";
    public const string FirstYearDiscountKey = "firstYearDiscount";
    public const string ScottishKey = "scottish";
    public const string StudentLoanKey = "studentLoan";
    public const string PostgraduateLoanKey = "postgraduateLoan";
    public const string TradingAllowanceKey = "tradingAllowance";
    public const string PreviousYearTaxKey = "previousYearTax";

    /// <summary>The indirect tax, as <see cref="ChargedTax.Kind"/> names it.</summary>
    public const string VatKind = "VAT";

    public const string SelfAssessmentCode = "SA";
    public const string VatCode = "VAT";

    // The periods payments are recorded under: the four VAT quarters, then Self Assessment's three payments
    private const int FirstPaymentPeriod = 5;
    private const int SecondPaymentPeriod = 6;
    private const int BalancingPeriod = 7;

    private readonly IAppText _text;

    public UkTaxPredictionModule(IAppText text)
    {
        _text = text;
    }

    public string CountryCode => UkComplianceModule.CountryCodeValue;

    public string DisplayName => _text.Translate("countries.GB");

    public string Currency => "GBP";

    public string IndirectTaxName => "VAT";

    public TaxYear TaxYearOf(int year)
    {
        var (start, end) = UkTaxRules.TaxYear(year);
        return new TaxYear(year, start, end, Label(year));
    }

    // Built on each read, in the language of the request
    public IReadOnlyList<TaxRegime> Regimes =>
    [
        new TaxRegime(
            SoleTraderRegime,
            T("taxPrediction.uk.regime.name"),
            [
                new(VatSchemeKey, T("taxPrediction.uk.fields.vatScheme.label"), ComplianceFieldType.Choice, Required: true,
                    Help: T("taxPrediction.uk.fields.vatScheme.help"),
                    Options:
                    [
                        new(NotRegistered, T("taxPrediction.uk.fields.vatScheme.none")),
                        new(Standard, T("taxPrediction.uk.fields.vatScheme.standard")),
                        new(FlatRate, T("taxPrediction.uk.fields.vatScheme.flatRate"))
                    ]),
                new(StaggerKey, T("taxPrediction.uk.fields.stagger.label"), ComplianceFieldType.Choice,
                    Help: T("taxPrediction.uk.fields.stagger.help"),
                    Options:
                    [
                        new("1", T("taxPrediction.uk.fields.stagger.1")),
                        new("2", T("taxPrediction.uk.fields.stagger.2")),
                        new("3", T("taxPrediction.uk.fields.stagger.3"))
                    ]),
                new(FlatRateKey, T("taxPrediction.uk.fields.flatRate.label"), Help: T("taxPrediction.uk.fields.flatRate.help")),
                new(FirstYearDiscountKey, T("taxPrediction.uk.fields.firstYearDiscount.label"), ComplianceFieldType.Boolean,
                    Help: T("taxPrediction.uk.fields.firstYearDiscount.help")),
                new(ScottishKey, T("taxPrediction.uk.fields.scottish.label"), ComplianceFieldType.Boolean,
                    Help: T("taxPrediction.uk.fields.scottish.help")),
                new(StudentLoanKey, T("taxPrediction.uk.fields.studentLoan.label"), ComplianceFieldType.Choice,
                    Options:
                    [
                        new("none", T("taxPrediction.uk.fields.studentLoan.none")),
                        new("plan1", T("taxPrediction.uk.fields.studentLoan.plan1")),
                        new("plan2", T("taxPrediction.uk.fields.studentLoan.plan2")),
                        new("plan4", T("taxPrediction.uk.fields.studentLoan.plan4")),
                        new("plan5", T("taxPrediction.uk.fields.studentLoan.plan5"))
                    ]),
                new(PostgraduateLoanKey, T("taxPrediction.uk.fields.postgraduateLoan.label"), ComplianceFieldType.Boolean),
                new(TradingAllowanceKey, T("taxPrediction.uk.fields.tradingAllowance.label"), ComplianceFieldType.Boolean,
                    Help: T("taxPrediction.uk.fields.tradingAllowance.help")),
                new(PreviousYearTaxKey, T("taxPrediction.uk.fields.previousYearTax.label"), Help: T("taxPrediction.uk.fields.previousYearTax.help"))
            ],
            T("taxPrediction.uk.regime.help"),
            new Dictionary<string, string>
            {
                [VatSchemeKey] = NotRegistered,
                [StaggerKey] = "1",
                [StudentLoanKey] = "none",
                [FlatRateKey] = "16.5"
            })
    ];

    public string? IndirectTaxKind(IReadOnlyDictionary<string, string> taxValues)
    {
        ArgumentNullException.ThrowIfNull(taxValues);
        return VatKind;
    }

    public TaxYearPrediction Predict(TaxPredictionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.RegimeId != SoleTraderRegime)
            throw new ArgumentException($"\"{input.RegimeId}\" is not a United Kingdom tax regime", nameof(input));

        var year = input.Year;
        var values = input.Values;
        var taxYear = TaxYearOf(year);
        var warnings = new List<string>();
        var scheme = values.GetValueOrDefault(VatSchemeKey) ?? NotRegistered;
        var scottish = values.GetValueOrDefault(ScottishKey) == "true";

        var income = input.Income.Where(i => taxYear.Contains(i.Date)).ToList();
        var expenses = input.Expenses.Where(e => taxYear.Contains(e.Date)).ToList();
        var turnover = Round(income.Sum(i => i.Base));

        if (year < FirstKnownYear || year > LastKnownYear)
            warnings.Add(T("taxPrediction.uk.warnings.unknownYear", ("year", Label(year)), ("known", Label(Math.Clamp(year, FirstKnownYear, LastKnownYear)))));
        if (scottish && year > LastKnownScottishYear)
            warnings.Add(T("taxPrediction.uk.warnings.scottishBands", ("year", Label(year)), ("known", Label(LastKnownScottishYear))));
        warnings.Add(T("taxPrediction.uk.warnings.cashBasis"));
        warnings.Add(T("taxPrediction.uk.warnings.otherIncome"));
        if (scheme == NotRegistered && turnover > VatRegistrationThreshold)
            warnings.Add(T("taxPrediction.uk.warnings.mustRegister", ("threshold", VatRegistrationThreshold.ToString("N0", System.Globalization.CultureInfo.CurrentCulture))));

        var periods = new List<(DateOnly Deadline, TaxPeriodPrediction Period)>();

        // VAT, every quarter of the stagger
        var flatRateSurplus = 0m;
        if (scheme is Standard or FlatRate)
        {
            var stagger = int.TryParse(values.GetValueOrDefault(StaggerKey), out var s) && s is >= 1 and <= 3 ? s : 1;
            var flatRate = (Number(values, FlatRateKey, "taxPrediction.uk.fields.flatRate.label", warnings) ?? LimitedCostTraderRate)
                - (values.GetValueOrDefault(FirstYearDiscountKey) == "true" ? 1m : 0m);
            var paid = Paid(input.Payments, VatCode);

            for (var quarter = 1; quarter <= 4; quarter++)
            {
                // Stagger 1's quarters start in April, stagger 2's in May, stagger 3's in June
                var start = new DateOnly(year, 3 + stagger, 1).AddMonths((quarter - 1) * 3);
                var end = start.AddMonths(3).AddDays(-1);
                var form = scheme == Standard
                    ? StandardReturn(quarter, start, end, input, paid)
                    : FlatRateReturn(quarter, start, end, input, flatRate, paid, ref flatRateSurplus);
                periods.Add((form.Deadline, new TaxPeriodPrediction(quarter, T("taxPrediction.common.quarter", ("quarter", quarter)), start, end, [form])));
            }
        }

        // Self Assessment
        var gross = Round(expenses.Sum(e => (scheme == FlatRate ? e.Base + e.IndirectTax : e.Base) * e.DeductiblePercent / 100m));
        var allowance = values.GetValueOrDefault(TradingAllowanceKey) == "true" && TradingAllowance > gross;
        var deductions = allowance ? TradingAllowance : gross;
        // Under the Flat Rate Scheme the VAT kept (charged less paid) is income too
        var profit = Math.Max(0m, turnover + flatRateSurplus - deductions);
        var personalAllowance = AllowanceFor(profit);
        var taxable = Math.Max(0m, profit - personalAllowance);
        var incomeTax = IncomeTax(taxable, year, scottish);
        var class4 = Class4(profit);
        var plan = values.GetValueOrDefault(StudentLoanKey) ?? "none";
        var studentLoan = StudentLoan(plan, profit, year)
            + (values.GetValueOrDefault(PostgraduateLoanKey) == "true" ? StudentLoan("postgraduate", profit, year) : 0m);
        var withheld = Round(income.Sum(i => i.Withheld));
        var total = Math.Max(0m, incomeTax + class4 + studentLoan - withheld);

        var lines = new List<TaxFormLine>
        {
            new(null, T("taxPrediction.uk.lines.turnover"), turnover)
        };
        if (scheme == FlatRate)
            lines.Add(new(null, T("taxPrediction.uk.lines.flatRateSurplus"), flatRateSurplus));
        lines.Add(new(null, T(allowance ? "taxPrediction.uk.lines.tradingAllowance" : "taxPrediction.uk.lines.expenses"), deductions));
        lines.Add(new(null, T("taxPrediction.uk.lines.profit"), profit));
        lines.Add(new(null, T("taxPrediction.uk.lines.personalAllowance"), personalAllowance));
        lines.Add(new(null, T("taxPrediction.common.taxableIncome"), taxable));
        lines.Add(new(null, T(scottish ? "taxPrediction.uk.lines.incomeTaxScottish" : "taxPrediction.uk.lines.incomeTax"), incomeTax));
        lines.Add(new(null, T("taxPrediction.uk.lines.class4"), class4));
        if (studentLoan > 0 || plan != "none")
            lines.Add(new(null, T("taxPrediction.uk.lines.studentLoan"), studentLoan));
        if (withheld > 0)
            lines.Add(new(null, T("taxPrediction.uk.lines.withheld"), withheld));
        lines.Add(new(null, T("taxPrediction.common.taxOfTheYear"), total));

        // Payments on account: half each of last year's bill (or this year's estimate), none below £1,000;
        // student loans are not paid on account
        var previous = Number(values, PreviousYearTaxKey, "taxPrediction.uk.fields.previousYearTax.label", warnings);
        var basis = Math.Max(0m, previous ?? incomeTax + class4 - withheld);
        var onAccount = basis < PaymentsOnAccountThreshold ? 0m : Round(basis / 2m);
        var paidSa = Paid(input.Payments, SelfAssessmentCode);
        decimal? paidFirst = paidSa.TryGetValue(FirstPaymentPeriod, out var a) ? a : null;
        decimal? paidSecond = paidSa.TryGetValue(SecondPaymentPeriod, out var b) ? b : null;
        decimal? paidBalance = paidSa.TryGetValue(BalancingPeriod, out var c) ? c : null;
        var basisLine = new TaxFormLine(null, T(previous is null ? "taxPrediction.uk.lines.basisEstimate" : "taxPrediction.uk.lines.basisLastYear"), basis);
        var halfLine = new TaxFormLine(null, T(basis < PaymentsOnAccountThreshold ? "taxPrediction.uk.lines.noPaymentsOnAccount" : "taxPrediction.uk.lines.half"), onAccount);

        var first = new TaxFormPrediction(SelfAssessmentCode, T("taxPrediction.uk.forms.firstPayment"), FirstPaymentOnAccount(year), onAccount, paidFirst, [basisLine, halfLine]);
        var second = new TaxFormPrediction(SelfAssessmentCode, T("taxPrediction.uk.forms.secondPayment"), SecondPaymentOnAccount(year), onAccount, paidSecond, [basisLine, halfLine]);

        var paidOnAccount = (paidFirst ?? onAccount) + (paidSecond ?? onAccount);
        var balance = total - paidOnAccount;
        var balanceLines = lines.ToList();
        balanceLines.Add(new(null, T("taxPrediction.uk.lines.paidOnAccount"), paidOnAccount));
        balanceLines.Add(new(null, T(balance >= 0 ? "taxPrediction.uk.lines.balanceToPay" : "taxPrediction.uk.lines.refund"), balance));
        var balancing = new TaxFormPrediction(SelfAssessmentCode, T("taxPrediction.uk.forms.balancing"), BalancingPayment(year), Math.Max(0m, balance), paidBalance, balanceLines);

        periods.Add((first.Deadline, new TaxPeriodPrediction(FirstPaymentPeriod, T("taxPrediction.uk.periods.firstPayment"), taxYear.Start, new DateOnly(year, 12, 31), [first])));
        periods.Add((second.Deadline, new TaxPeriodPrediction(SecondPaymentPeriod, T("taxPrediction.uk.periods.secondPayment"), new DateOnly(year + 1, 1, 1), new DateOnly(year + 1, 6, 30), [second])));
        periods.Add((balancing.Deadline, new TaxPeriodPrediction(BalancingPeriod, T("taxPrediction.uk.periods.balancing"), taxYear.Start, taxYear.End, [balancing])));

        return new TaxYearPrediction(year, periods.OrderBy(p => p.Deadline).ThenBy(p => p.Period.Period).Select(p => p.Period).ToList(), warnings);
    }

    private TaxFormPrediction StandardReturn(int quarter, DateOnly start, DateOnly end, TaxPredictionInput input, IReadOnlyDictionary<int, decimal> paid)
    {
        var costs = input.Expenses.Where(e => e.Date >= start && e.Date <= end).ToList();
        var charged = Round(input.Income.Where(i => i.Date >= start && i.Date <= end).SelectMany(i => i.Taxes)
            .Where(t => t.Kind is null or VatKind).Sum(t => t.Amount));
        // A supplier abroad charged no VAT: the user accounts for it and reclaims it in the same return
        var reverse = Round(costs.Where(e => e.ReverseCharge).Sum(e => e.Base * VatStandardRate / 100m));
        var reclaimed = Round(costs.Sum(e => (e.ReverseCharge ? e.Base * VatStandardRate / 100m : e.IndirectTax) * e.DeductiblePercent / 100m));
        var due = charged + reverse - reclaimed;
        decimal? recorded = paid.TryGetValue(quarter, out var amount) ? amount : null;

        return new TaxFormPrediction(VatCode, T("taxPrediction.uk.forms.vatReturn", ("quarter", quarter)), VatDeadline(end), Math.Max(0m, due), recorded,
        [
            new("1", T("taxPrediction.uk.lines.vatOnSales"), charged),
            new("2", T("taxPrediction.uk.lines.vatReverseCharge"), reverse),
            new("4", T("taxPrediction.uk.lines.vatReclaimed"), reclaimed),
            new("5", T(due >= 0 ? "taxPrediction.uk.lines.vatToPay" : "taxPrediction.uk.lines.vatRepayment"), due)
        ]);
    }

    private TaxFormPrediction FlatRateReturn(
        int quarter, DateOnly start, DateOnly end, TaxPredictionInput input, decimal rate, IReadOnlyDictionary<int, decimal> paid, ref decimal surplus)
    {
        var invoiced = input.Income.Where(i => i.Date >= start && i.Date <= end).ToList();
        var charged = Round(invoiced.SelectMany(i => i.Taxes).Where(t => t.Kind is null or VatKind).Sum(t => t.Amount));
        var inclusive = Round(invoiced.Sum(i => i.Base)) + charged;
        var due = Round(inclusive * rate / 100m);
        surplus += charged - due;
        decimal? recorded = paid.TryGetValue(quarter, out var amount) ? amount : null;

        return new TaxFormPrediction(VatCode, T("taxPrediction.uk.forms.vatReturn", ("quarter", quarter)), VatDeadline(end), due, recorded,
        [
            new("6", T("taxPrediction.uk.lines.flatRateTurnover"), inclusive),
            new(null, T("taxPrediction.uk.lines.flatRateApplied", ("rate", rate)), due),
            new(null, T("taxPrediction.uk.lines.vatCharged"), charged),
            new("5", T("taxPrediction.uk.lines.vatToPay"), due)
        ]);
    }

    private decimal? Number(IReadOnlyDictionary<string, string> values, string key, string labelKey, List<string> warnings)
    {
        var text = values.GetValueOrDefault(key);
        var number = ParseAmount(text);
        if (number is null && !string.IsNullOrWhiteSpace(text))
            warnings.Add(T("taxPrediction.common.notANumber", ("text", text), ("what", T(labelKey))));
        return number;
    }

    private static IReadOnlyDictionary<int, decimal> Paid(IReadOnlyList<RecordedTaxPayment> payments, string form) =>
        payments.Where(p => p.Form == form).GroupBy(p => p.Period).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

    private string T(string key, params (string Name, object? Value)[] args) => _text.Translate(key, args);
}
