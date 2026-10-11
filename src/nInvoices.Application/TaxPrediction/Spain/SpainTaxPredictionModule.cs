using nInvoices.Application.Compliance.Spain;
using nInvoices.Core.Compliance;
using nInvoices.Core.TaxPrediction;

namespace nInvoices.Application.TaxPrediction.Spain;

/// <summary>
/// Spain, for an autónomo in estimación directa simplificada: each quarter Modelo 130 (income tax on
/// account) and Modelo 420 (IGIC, Canary Islands) or 303 (IVA, elsewhere).
/// </summary>
public sealed class SpainTaxPredictionModule : ITaxPredictionModule
{
    public const string CountryCodeValue = "ES";

    /// <summary>Regime: estimación directa simplificada.</summary>
    public const string SimplifiedDirectEstimation = "eds";

    public const string TerritoryKey = "territory";
    public const string CanaryIslands = "canarias";
    public const string Mainland = "mainland";

    /// <summary>Setting: apply the hard-to-justify expenses deduction ("true"; on for a new profile).</summary>
    public const string DifficultExpensesKey = "difficultExpenses";

    /// <summary>Setting: subtract the customers' withholdings in Modelo 130 ("true"; on for a new profile).</summary>
    public const string DeductWithholdingsKey = "deductWithholdings";

    public string CountryCode => CountryCodeValue;

    public string DisplayName => "Spain";

    public string Currency => "EUR";

    public IReadOnlyList<TaxRegime> Regimes { get; } =
    [
        new TaxRegime(
            SimplifiedDirectEstimation,
            "Autónomo · estimación directa simplificada",
            [
                new ComplianceField(
                    TerritoryKey,
                    "Where you pay indirect tax",
                    ComplianceFieldType.Choice,
                    Required: true,
                    Help: "The Canary Islands have IGIC, filed with Modelo 420; elsewhere it is IVA, filed with Modelo 303.",
                    Options:
                    [
                        new ComplianceFieldOption(CanaryIslands, "Canary Islands (IGIC, Modelo 420)"),
                        new ComplianceFieldOption(Mainland, "Mainland and Balearic Islands (IVA, Modelo 303)")
                    ]),
                new ComplianceField(
                    DifficultExpensesKey,
                    "Deduct hard-to-justify expenses (gastos de difícil justificación)",
                    ComplianceFieldType.Boolean,
                    Help: "5% of the net income, at most €2,000 a year. Most autónomos and gestores apply it."),
                new ComplianceField(
                    DeductWithholdingsKey,
                    "Subtract what customers withheld in Modelo 130",
                    ComplianceFieldType.Boolean,
                    Help: "The law allows subtracting the IRPF your customers withheld (box 06). Untick it if your gestor leaves it out: you then get it back in the annual return.")
            ],
            "Quarterly Modelo 130 and Modelo 420 or 303.",
            new Dictionary<string, string>
            {
                [DifficultExpensesKey] = "true",
                [DeductWithholdingsKey] = "true"
            })
    ];

    /// <summary>
    /// IGIC or IVA when the tax says so (its "Which tax is this?" in the Spanish invoicing rules);
    /// otherwise null, and it is counted as the tax of the user's territory: IGIC in the Canary Islands.
    /// </summary>
    public string? IndirectTaxKind(IReadOnlyDictionary<string, string> taxValues)
    {
        ArgumentNullException.ThrowIfNull(taxValues);
        return taxValues.GetValueOrDefault(SpainComplianceModule.TaxTypeKey) switch
        {
            SpainComplianceModule.Igic => SpainComplianceModule.Igic,
            SpainComplianceModule.Iva => SpainComplianceModule.Iva,
            _ => null
        };
    }

    public TaxYearPrediction Predict(TaxPredictionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.RegimeId != SimplifiedDirectEstimation)
            throw new ArgumentException($"\"{input.RegimeId}\" is not a Spanish tax regime", nameof(input));

        var warnings = new List<string>();
        var indirect = input.Values.GetValueOrDefault(TerritoryKey) switch
        {
            CanaryIslands => IndirectTaxReturn.Igic420,
            Mainland => IndirectTaxReturn.Iva303,
            _ => null
        };

        if (indirect is null)
        {
            warnings.Add("Choose where you pay indirect tax (Canary Islands or elsewhere in Spain) to estimate IGIC or IVA.");
        }
        else
        {
            var other = input.Income
                .Where(i => i.Date.Year == input.Year)
                .Count(i => i.Taxes.Any(t => t.Kind is not null && t.Kind != indirect.Kind && t.Amount != 0));
            if (other > 0)
                warnings.Add($"{other} invoice(s) charge a tax other than {indirect.Kind}: it is not counted in {indirect.Name}.");
        }

        var form130 = Modelo130Calculator.Calculate(
            input.Year,
            input.Income,
            input.Expenses,
            Paid(input.Payments, Modelo130Calculator.Code),
            Flag(input.Values, DifficultExpensesKey),
            Flag(input.Values, DeductWithholdingsKey));

        var indirectForms = indirect is null
            ? null
            : IndirectTaxCalculator.Calculate(indirect, input.Year, input.Income, input.Expenses, Paid(input.Payments, indirect.Code));

        var periods = Enumerable.Range(1, 4)
            .Select(quarter => new TaxPeriodPrediction(
                quarter,
                $"Q{quarter}",
                SpanishTaxRules.QuarterStart(input.Year, quarter),
                SpanishTaxRules.QuarterEnd(input.Year, quarter),
                indirectForms is null ? [form130[quarter - 1]] : [form130[quarter - 1], indirectForms[quarter - 1]]))
            .ToList();

        return new TaxYearPrediction(input.Year, periods, warnings);
    }

    private static IReadOnlyDictionary<int, decimal> Paid(IReadOnlyList<RecordedTaxPayment> payments, string form) =>
        payments
            .Where(p => p.Form == form)
            .GroupBy(p => p.Period)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

    private static bool Flag(IReadOnlyDictionary<string, string> values, string key) =>
        values.GetValueOrDefault(key) == "true";
}
