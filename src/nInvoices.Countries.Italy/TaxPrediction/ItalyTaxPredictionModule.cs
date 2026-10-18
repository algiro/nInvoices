using nInvoices.Core.Compliance;
using nInvoices.Core.TaxPrediction;
using static nInvoices.Countries.Italy.TaxPrediction.ItalianTaxRules;

namespace nInvoices.Countries.Italy.TaxPrediction;

/// <summary>
/// Italy, for a professional in the Gestione Separata INPS: the regime forfettario (imposta sostitutiva and INPS,
/// in two installments and a balance) or the regime ordinario semplificato (the same for IRPEF, plus the
/// quarterly IVA liquidations).
/// </summary>
public sealed class ItalyTaxPredictionModule : ITaxPredictionModule
{
    public const string ForfettarioRegime = "forfettario";
    public const string OrdinarioRegime = "ordinario";

    public const string CoefficientKey = "coefficient";
    public const string StartupKey = "startup";
    public const string InpsRateKey = "inpsRate";
    public const string RegionalRateKey = "regionalRate";
    public const string MunicipalRateKey = "municipalRate";
    public const string WorkDeductionKey = "workDeduction";
    public const string PreviousTaxKey = "previousYearTax";
    public const string PreviousContributionsKey = "previousYearContributions";

    public const string SubstituteTaxCode = "IS";
    public const string IrpefCode = "IRPEF";
    public const string InpsCode = "INPS";

    // The periods payments are recorded under: the four IVA quarters, then the installments and the balance
    private static readonly InstallmentPeriods Installments = new(5, 6, 7);

    public string CountryCode => ItalyComplianceModule.CountryCodeValue;

    public string DisplayName => "Italy";

    public string Currency => "EUR";

    public string IndirectTaxName => "IVA";

    private static readonly ComplianceField InpsRateField = new(
        InpsRateKey,
        "INPS contribution rate (%)",
        Required: true,
        Help: "26.07 for a professional in the Gestione Separata without another pension. Enter your fund's rate if you belong to a cassa professionale.");

    private static readonly ComplianceField PreviousTaxField = new(
        PreviousTaxKey,
        "Last year's tax (€, optional)",
        Help: "From last year's Modello Redditi: the installments are then based on it (metodo storico). Left empty, they are based on this year's estimate (metodo previsionale).");

    private static readonly ComplianceField PreviousContributionsField = new(
        PreviousContributionsKey,
        "Last year's INPS contributions (€, optional)",
        Help: "The INPS installments are then 40% and 40% of it; left empty, of this year's estimate.");

    public IReadOnlyList<TaxRegime> Regimes { get; } =
    [
        new TaxRegime(
            ForfettarioRegime,
            "Regime forfettario",
            [
                new ComplianceField(
                    CoefficientKey,
                    "Profitability coefficient (by ATECO code)",
                    ComplianceFieldType.Choice,
                    Required: true,
                    Help: "The share of your revenue that is taxed, set by the group of your ATECO activity code.",
                    Options:
                    [
                        new ComplianceFieldOption("78", "78% · Professional, scientific, technical, health, education, finance"),
                        new ComplianceFieldOption("67", "67% · Other economic activities"),
                        new ComplianceFieldOption("86", "86% · Construction and real estate"),
                        new ComplianceFieldOption("62", "62% · Street trade of other goods, trade intermediaries"),
                        new ComplianceFieldOption("54", "54% · Street trade of food and drinks"),
                        new ComplianceFieldOption("40", "40% · Trade, accommodation and food services")
                    ]),
                new ComplianceField(
                    StartupKey,
                    "New activity: 5% instead of 15% (first five years)",
                    ComplianceFieldType.Boolean,
                    Help: "For the first five years of an activity that is really new (art. 1, comma 65, L. 190/2014)."),
                InpsRateField,
                PreviousTaxField,
                PreviousContributionsField
            ],
            "Imposta sostitutiva and INPS: two installments (30 June, 30 November) and the balance the next June. No IVA.",
            new Dictionary<string, string>
            {
                [CoefficientKey] = "78",
                [InpsRateKey] = GestioneSeparataRate.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }),
        new TaxRegime(
            OrdinarioRegime,
            "Regime ordinario (contabilità semplificata)",
            [
                InpsRateField,
                new ComplianceField(RegionalRateKey, "Addizionale regionale (%)", Help: "Your region's rate, e.g. 1.23."),
                new ComplianceField(MunicipalRateKey, "Addizionale comunale (%)", Help: "Your municipality's rate, e.g. 0.8."),
                new ComplianceField(
                    WorkDeductionKey,
                    "Apply the self-employment deduction (detrazione lavoro autonomo)",
                    ComplianceFieldType.Boolean,
                    Help: "Art. 13 TUIR: up to €1,265, falling to nothing at €50,000 of income."),
                PreviousTaxField,
                PreviousContributionsField
            ],
            "IVA every quarter; IRPEF and INPS in two installments (30 June, 30 November) and the balance the next June.",
            new Dictionary<string, string>
            {
                [InpsRateKey] = GestioneSeparataRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [WorkDeductionKey] = "true"
            })
    ];

    /// <summary>A contribution (rivalsa INPS, cassa) when the tax says so; otherwise IVA.</summary>
    public string? IndirectTaxKind(IReadOnlyDictionary<string, string> taxValues)
    {
        ArgumentNullException.ThrowIfNull(taxValues);
        return ItalyComplianceModule.IsContribution(taxValues) ? ItalyComplianceModule.Contribution : ItalyComplianceModule.Iva;
    }

    public TaxYearPrediction Predict(TaxPredictionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.RegimeId is not (ForfettarioRegime or OrdinarioRegime))
            throw new ArgumentException($"\"{input.RegimeId}\" is not an Italian tax regime", nameof(input));

        var warnings = new List<string>();
        var values = input.Values;
        var year = input.Year;
        var inpsRate = Number(values, InpsRateKey, "the INPS rate", warnings) ?? GestioneSeparataRate;
        var previousTax = Number(values, PreviousTaxKey, "last year's tax", warnings);
        var previousContributions = Number(values, PreviousContributionsKey, "last year's INPS contributions", warnings);

        IncomeTaxYear income;
        string taxCode, taxName;
        if (input.RegimeId == ForfettarioRegime)
        {
            var coefficient = Number(values, CoefficientKey, "the coefficient", warnings) ?? 78m;
            income = ForfettarioCalculator.Calculate(year, input.Income, coefficient, values.GetValueOrDefault(StartupKey) == "true", inpsRate);
            (taxCode, taxName) = (SubstituteTaxCode, "Imposta sostitutiva");

            warnings.Add("Revenue counts when invoiced; the regime forfettario counts it when collected (principio di cassa).");
            if (income.Revenue > ForfettarioImmediateExit)
                warnings.Add($"Revenue above €{ForfettarioImmediateExit:N0}: the regime forfettario stops applying during the year, and IVA is due from the invoice that crosses it.");
            else if (income.Revenue > ForfettarioRevenueLimit)
                warnings.Add($"Revenue above €{ForfettarioRevenueLimit:N0}: next year you leave the regime forfettario.");
            if (input.Expenses.Any(e => e.Date.Year == year))
                warnings.Add("Expenses are not deducted in the regime forfettario: the coefficient stands for them.");
        }
        else
        {
            var surcharges = (Number(values, RegionalRateKey, "the addizionale regionale", warnings) ?? 0m)
                + (Number(values, MunicipalRateKey, "the addizionale comunale", warnings) ?? 0m);
            income = OrdinarioCalculator.Calculate(year, input.Income, input.Expenses, inpsRate, surcharges, values.GetValueOrDefault(WorkDeductionKey) == "true");
            (taxCode, taxName) = (IrpefCode, "IRPEF");
            warnings.Add("Not estimated: the December IVA installment (acconto IVA) and the installment of the addizionale comunale.");
        }

        warnings.Add("INPS contributions are deducted in the year they are paid; the estimate deducts this year's own.");

        var tax = InstallmentPlan.Calculate(taxCode, taxName, year, income.Tax.Amount, income.Tax.Lines, previousTax, isInps: false,
            Paid(input.Payments, taxCode), Installments);
        var inps = InstallmentPlan.Calculate(InpsCode, "INPS", year, income.Contributions.Amount, income.Contributions.Lines, previousContributions, isInps: true,
            Paid(input.Payments, InpsCode), Installments);

        var first = new TaxPeriodPrediction(Installments.First, "1st installment", new DateOnly(year, 1, 1), new DateOnly(year, 5, 31), [tax.First, inps.First]);
        var second = new TaxPeriodPrediction(Installments.Second, "2nd installment", new DateOnly(year, 6, 1), new DateOnly(year, 10, 31), [tax.Second, inps.Second]);
        var balance = new TaxPeriodPrediction(Installments.Balance, "Balance", new DateOnly(year, 11, 1), new DateOnly(year, 12, 31), [tax.Balance, inps.Balance]);

        if (input.RegimeId == ForfettarioRegime)
            return new TaxYearPrediction(year, [first, second, balance], warnings);

        // In the order of their deadlines: Q1 (May), 1st installment (June), Q2 (August), Q3 and the 2nd installment (November), Q4 (March)
        var iva = IvaLiquidationCalculator.Calculate(year, input.Income, input.Expenses, Paid(input.Payments, IvaLiquidationCalculator.Code));
        TaxPeriodPrediction Quarter(int q) => new(q, $"Q{q}", QuarterStart(year, q), QuarterEnd(year, q), [iva[q - 1]]);

        return new TaxYearPrediction(year, [Quarter(1), first, Quarter(2), Quarter(3), second, Quarter(4), balance], warnings);
    }

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
