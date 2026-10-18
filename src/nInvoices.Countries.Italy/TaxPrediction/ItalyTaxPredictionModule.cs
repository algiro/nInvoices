using System.Globalization;
using nInvoices.Application.Localization;
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

    private readonly IAppText _text;

    public ItalyTaxPredictionModule(IAppText text)
    {
        _text = text;
    }

    public string CountryCode => ItalyComplianceModule.CountryCodeValue;

    public string DisplayName => _text.Translate("countries.IT");

    public string Currency => "EUR";

    public string IndirectTaxName => "IVA";

    private ComplianceField InpsRateField => new(
        InpsRateKey,
        T("taxPrediction.italy.fields.inpsRate.label"),
        Required: true,
        Help: T("taxPrediction.italy.fields.inpsRate.help"));

    private ComplianceField PreviousTaxField => new(
        PreviousTaxKey,
        T("taxPrediction.italy.fields.previousTax.label"),
        Help: T("taxPrediction.italy.fields.previousTax.help"));

    private ComplianceField PreviousContributionsField => new(
        PreviousContributionsKey,
        T("taxPrediction.italy.fields.previousContributions.label"),
        Help: T("taxPrediction.italy.fields.previousContributions.help"));

    // Built on each read, in the language of the request
    public IReadOnlyList<TaxRegime> Regimes =>
    [
        new TaxRegime(
            ForfettarioRegime,
            "Regime forfettario",
            [
                new ComplianceField(
                    CoefficientKey,
                    T("taxPrediction.italy.fields.coefficient.label"),
                    ComplianceFieldType.Choice,
                    Required: true,
                    Help: T("taxPrediction.italy.fields.coefficient.help"),
                    Options:
                    [
                        new ComplianceFieldOption("78", T("taxPrediction.italy.fields.coefficient.c78")),
                        new ComplianceFieldOption("67", T("taxPrediction.italy.fields.coefficient.c67")),
                        new ComplianceFieldOption("86", T("taxPrediction.italy.fields.coefficient.c86")),
                        new ComplianceFieldOption("62", T("taxPrediction.italy.fields.coefficient.c62")),
                        new ComplianceFieldOption("54", T("taxPrediction.italy.fields.coefficient.c54")),
                        new ComplianceFieldOption("40", T("taxPrediction.italy.fields.coefficient.c40"))
                    ]),
                new ComplianceField(
                    StartupKey,
                    T("taxPrediction.italy.fields.startup.label"),
                    ComplianceFieldType.Boolean,
                    Help: T("taxPrediction.italy.fields.startup.help")),
                InpsRateField,
                PreviousTaxField,
                PreviousContributionsField
            ],
            T("taxPrediction.italy.regimes.forfettario.help"),
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
                new ComplianceField(RegionalRateKey, "Addizionale regionale (%)", Help: T("taxPrediction.italy.fields.regionalRate.help")),
                new ComplianceField(MunicipalRateKey, "Addizionale comunale (%)", Help: T("taxPrediction.italy.fields.municipalRate.help")),
                new ComplianceField(
                    WorkDeductionKey,
                    T("taxPrediction.italy.fields.workDeduction.label"),
                    ComplianceFieldType.Boolean,
                    Help: T("taxPrediction.italy.fields.workDeduction.help")),
                PreviousTaxField,
                PreviousContributionsField
            ],
            T("taxPrediction.italy.regimes.ordinario.help"),
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
        var inpsRate = Number(values, InpsRateKey, "taxPrediction.italy.what.inpsRate", warnings) ?? GestioneSeparataRate;
        var previousTax = Number(values, PreviousTaxKey, "taxPrediction.italy.what.previousTax", warnings);
        var previousContributions = Number(values, PreviousContributionsKey, "taxPrediction.italy.what.previousContributions", warnings);

        IncomeTaxYear income;
        string taxCode, taxName;
        if (input.RegimeId == ForfettarioRegime)
        {
            var coefficient = Number(values, CoefficientKey, "taxPrediction.italy.what.coefficient", warnings) ?? 78m;
            income = ForfettarioCalculator.Calculate(year, input.Income, coefficient, values.GetValueOrDefault(StartupKey) == "true", inpsRate, _text);
            (taxCode, taxName) = (SubstituteTaxCode, "Imposta sostitutiva");

            warnings.Add(T("taxPrediction.italy.warnings.forfettarioCash"));
            if (income.Revenue > ForfettarioImmediateExit)
                warnings.Add(_text.Translate("taxPrediction.italy.warnings.forfettarioImmediateExit", ("limit", ForfettarioImmediateExit.ToString("N0", CultureInfo.CurrentCulture))));
            else if (income.Revenue > ForfettarioRevenueLimit)
                warnings.Add(_text.Translate("taxPrediction.italy.warnings.forfettarioLimit", ("limit", ForfettarioRevenueLimit.ToString("N0", CultureInfo.CurrentCulture))));
            if (input.Expenses.Any(e => e.Date.Year == year))
                warnings.Add(T("taxPrediction.italy.warnings.forfettarioExpenses"));
        }
        else
        {
            var surcharges = (Number(values, RegionalRateKey, "taxPrediction.italy.what.regionalRate", warnings) ?? 0m)
                + (Number(values, MunicipalRateKey, "taxPrediction.italy.what.municipalRate", warnings) ?? 0m);
            income = OrdinarioCalculator.Calculate(year, input.Income, input.Expenses, inpsRate, surcharges, values.GetValueOrDefault(WorkDeductionKey) == "true", _text);
            (taxCode, taxName) = (IrpefCode, "IRPEF");
            warnings.Add(T("taxPrediction.italy.warnings.ordinarioNotEstimated"));
        }

        warnings.Add(T("taxPrediction.italy.warnings.inpsDeducted"));

        var tax = InstallmentPlan.Calculate(taxCode, taxName, year, income.Tax.Amount, income.Tax.Lines, previousTax, isInps: false,
            Paid(input.Payments, taxCode), Installments, _text);
        var inps = InstallmentPlan.Calculate(InpsCode, "INPS", year, income.Contributions.Amount, income.Contributions.Lines, previousContributions, isInps: true,
            Paid(input.Payments, InpsCode), Installments, _text);

        var first = new TaxPeriodPrediction(Installments.First, T("taxPrediction.italy.periods.firstInstallment"), new DateOnly(year, 1, 1), new DateOnly(year, 5, 31), [tax.First, inps.First]);
        var second = new TaxPeriodPrediction(Installments.Second, T("taxPrediction.italy.periods.secondInstallment"), new DateOnly(year, 6, 1), new DateOnly(year, 10, 31), [tax.Second, inps.Second]);
        var balance = new TaxPeriodPrediction(Installments.Balance, T("taxPrediction.italy.periods.balance"), new DateOnly(year, 11, 1), new DateOnly(year, 12, 31), [tax.Balance, inps.Balance]);

        if (input.RegimeId == ForfettarioRegime)
            return new TaxYearPrediction(year, [first, second, balance], warnings);

        // In the order of their deadlines: Q1 (May), 1st installment (June), Q2 (August), Q3 and the 2nd installment (November), Q4 (March)
        var iva = IvaLiquidationCalculator.Calculate(year, input.Income, input.Expenses, Paid(input.Payments, IvaLiquidationCalculator.Code), _text);
        TaxPeriodPrediction Quarter(int q) => new(q, _text.Translate("taxPrediction.common.quarter", ("quarter", q)), QuarterStart(year, q), QuarterEnd(year, q), [iva[q - 1]]);

        return new TaxYearPrediction(year, [Quarter(1), first, Quarter(2), Quarter(3), second, Quarter(4), balance], warnings);
    }

    /// <param name="what">The key of what the value is ("the INPS rate"), for the warning when it is not a number.</param>
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
