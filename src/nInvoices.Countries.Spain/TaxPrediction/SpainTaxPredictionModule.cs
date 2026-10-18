using nInvoices.Application.Localization;
using nInvoices.Countries.Spain;
using nInvoices.Core.Compliance;
using nInvoices.Core.TaxPrediction;

namespace nInvoices.Countries.Spain.TaxPrediction;

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

    private readonly IAppText _text;

    public SpainTaxPredictionModule(IAppText text)
    {
        _text = text;
    }

    public string CountryCode => CountryCodeValue;

    public string DisplayName => _text.Translate("countries.ES");

    public string Currency => "EUR";

    public string IndirectTaxName => "IVA / IGIC";

    // Built on each read, in the language of the request
    public IReadOnlyList<TaxRegime> Regimes =>
    [
        new TaxRegime(
            SimplifiedDirectEstimation,
            "Autónomo · estimación directa simplificada",
            [
                new ComplianceField(
                    TerritoryKey,
                    T("taxPrediction.spain.fields.territory.label"),
                    ComplianceFieldType.Choice,
                    Required: true,
                    Help: T("taxPrediction.spain.fields.territory.help"),
                    Options:
                    [
                        new ComplianceFieldOption(CanaryIslands, T("taxPrediction.spain.fields.territory.canarias")),
                        new ComplianceFieldOption(Mainland, T("taxPrediction.spain.fields.territory.mainland"))
                    ]),
                new ComplianceField(
                    DifficultExpensesKey,
                    T("taxPrediction.spain.fields.difficultExpenses.label"),
                    ComplianceFieldType.Boolean,
                    Help: T("taxPrediction.spain.fields.difficultExpenses.help")),
                new ComplianceField(
                    DeductWithholdingsKey,
                    T("taxPrediction.spain.fields.deductWithholdings.label"),
                    ComplianceFieldType.Boolean,
                    Help: T("taxPrediction.spain.fields.deductWithholdings.help"))
            ],
            T("taxPrediction.spain.regimes.eds.help"),
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
            warnings.Add(T("taxPrediction.spain.warnings.chooseTerritory"));
        }
        else
        {
            var other = input.Income
                .Where(i => i.Date.Year == input.Year)
                .Count(i => i.Taxes.Any(t => t.Kind is not null && t.Kind != indirect.Kind && t.Amount != 0));
            if (other > 0)
                warnings.Add(_text.Translate("taxPrediction.spain.warnings.otherTax", ("count", other), ("tax", indirect.Kind), ("form", indirect.Name)));
        }

        var form130 = Modelo130Calculator.Calculate(
            input.Year,
            input.Income,
            input.Expenses,
            Paid(input.Payments, Modelo130Calculator.Code),
            Flag(input.Values, DifficultExpensesKey),
            Flag(input.Values, DeductWithholdingsKey),
            _text);

        var indirectForms = indirect is null
            ? null
            : IndirectTaxCalculator.Calculate(indirect, input.Year, input.Income, input.Expenses, Paid(input.Payments, indirect.Code), _text);

        var periods = Enumerable.Range(1, 4)
            .Select(quarter => new TaxPeriodPrediction(
                quarter,
                _text.Translate("taxPrediction.common.quarter", ("quarter", quarter)),
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

    private string T(string key) => _text.Translate(key);

    private static bool Flag(IReadOnlyDictionary<string, string> values, string key) =>
        values.GetValueOrDefault(key) == "true";
}
