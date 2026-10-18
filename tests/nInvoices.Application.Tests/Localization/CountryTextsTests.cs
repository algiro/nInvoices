using System.Globalization;
using System.Text.RegularExpressions;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Compliance;
using nInvoices.Core.TaxPrediction;
using nInvoices.Countries.Germany;
using nInvoices.Countries.Germany.TaxPrediction;
using nInvoices.Countries.Italy;
using nInvoices.Countries.Italy.TaxPrediction;
using nInvoices.Countries.Spain;
using nInvoices.Countries.Spain.TaxPrediction;
using Shouldly;

namespace nInvoices.Application.Tests.Localization;

/// <summary>
/// What the countries describe (their fields, regimes and tax estimates) comes out in the user's language. Some keys
/// are put together at run time (an option's, a month's), which <see cref="AppTextTests"/> can't see: a key missing
/// from the texts would reach the user as the key itself.
/// </summary>
[TestFixture]
public sealed partial class CountryTextsTests
{
    private static readonly ICountryComplianceModule[] Compliance =
    [
        new SpainComplianceModule(TestText.Instance),
        new ItalyComplianceModule(TestText.Instance),
        new GermanyComplianceModule(TestText.Instance)
    ];

    private static readonly ITaxPredictionModule[] Taxes =
    [
        new SpainTaxPredictionModule(TestText.Instance),
        new ItalyTaxPredictionModule(TestText.Instance),
        new GermanyTaxPredictionModule(TestText.Instance)
    ];

    [TestCase("en")]
    [TestCase("it")]
    public void Texts_AreAllTranslated(string language)
    {
        var texts = InCulture(language, AllTexts);

        texts.Count.ShouldBeGreaterThan(300);
        texts.Where(t => Key().IsMatch(t) || t.Contains('{', StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Test]
    public void Texts_InItalian_AreItalian()
    {
        var spain = new SpainComplianceModule(TestText.Instance);

        InCulture("it", () => spain.Fields.Single(f => f.Key == SpainComplianceModule.PersonTypeKey).Label).ShouldBe("Tipo di emittente");
        InCulture("it", () => spain.DisplayName).ShouldBe("Spagna");
        InCulture("it", () => Taxes[1].Predict(Input(Taxes[1].Regimes[0], numbers: "1")).Periods[0].Name).ShouldBe("1° acconto");
    }

    private static List<string> AllTexts()
    {
        var texts = new List<string>();
        foreach (var module in Compliance)
        {
            texts.Add(module.DisplayName);
            foreach (var field in module.Fields.Concat(module.CustomerFields).Concat(module.TaxFields))
                texts.AddRange(FieldTexts(field));
        }

        foreach (var module in Taxes)
        {
            texts.Add(module.DisplayName);
            foreach (var regime in module.Regimes)
            {
                texts.Add(regime.Name);
                texts.AddRange(regime.Fields.SelectMany(FieldTexts));
                if (regime.Help is not null)
                    texts.Add(regime.Help);

                // Once with numbers, once with text where numbers go (the "not a number" warnings)
                foreach (var numbers in new[] { "1", "x" })
                {
                    var prediction = module.Predict(Input(regime, numbers));
                    texts.AddRange(prediction.Warnings);
                    foreach (var period in prediction.Periods)
                    {
                        texts.Add(period.Name);
                        foreach (var form in period.Forms)
                        {
                            texts.Add(form.Name);
                            texts.AddRange(form.Lines.Select(l => l.Label));
                        }
                    }
                }
            }
        }
        return texts;
    }

    private static IEnumerable<string> FieldTexts(ComplianceField field)
    {
        yield return field.Label;
        if (field.Help is not null)
            yield return field.Help;
        foreach (var option in field.Options ?? [])
            yield return option.Label;
    }

    // Every choice its first option, every box ticked, numbers where text goes; monthly VAT returns, for the month names
    private static TaxPredictionInput Input(TaxRegime regime, string numbers)
    {
        var values = new Dictionary<string, string>();
        foreach (var field in regime.Fields)
        {
            values[field.Key] = field.Type switch
            {
                ComplianceFieldType.Boolean => "true",
                ComplianceFieldType.Choice => field.Options![0].Value,
                _ => numbers
            };
        }
        values[GermanyTaxPredictionModule.VatPeriodKey] = GermanyTaxPredictionModule.Monthly;
        values[GermanyTaxPredictionModule.SmallBusinessKey] = "false";

        return new TaxPredictionInput(
            2026,
            regime.Id,
            values,
            [new TaxableIncome(new DateOnly(2026, 2, 15), 120_000m, [new ChargedTax(null, 21m, 25_200m)], 1_000m)],
            [new DeductibleExpense(new DateOnly(2026, 4, 10), 1_000m, 210m), new DeductibleExpense(new DateOnly(2026, 5, 10), 500m, ReverseCharge: true)],
            []);
    }

    private static T InCulture<T>(string language, Func<T> read)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        try
        {
            return read();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [GeneratedRegex(@"^(compliance|countries|taxPrediction)\.[A-Za-z0-9_.]+$")]
    private static partial Regex Key();
}
