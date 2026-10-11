using nInvoices.Application.TaxPrediction.Spain;
using nInvoices.Core.TaxPrediction;
using Shouldly;
using static nInvoices.Application.TaxPrediction.Spain.SpainTaxPredictionModule;

namespace nInvoices.Application.Tests.TaxPrediction;

/// <summary>
/// The Spanish quarterly estimate on plain values. The fixture is a made-up half-year of an autónomo
/// in the Canary Islands, shaped like a real one (the real figures, checked against what was actually
/// paid, are kept private): monthly invoices to an EU business (reverse charge), two Canarian invoices
/// with IGIC and IRPF, small expenses and the social security fee. The settings are those of a gestor
/// who applies the 5% hard-to-justify expenses and doesn't subtract the customers' withholdings.
/// Yes/no settings are on only when "true", as the settings page stores them.
/// </summary>
[TestFixture]
public sealed class SpainTaxPredictionTests
{
    private static readonly SpainTaxPredictionModule Module = new();

    private static TaxableIncome ReverseCharge(int month, int day, decimal amount) =>
        new(new DateOnly(2026, month, day), amount, []);

    /// <summary>A Canarian customer: 7% IGIC added, 15% IRPF withheld.</summary>
    private static TaxableIncome Canarian(int month, int day, decimal amount) =>
        new(new DateOnly(2026, month, day), amount, [new ChargedTax("IGIC", 7m, amount * 0.07m)], amount * 0.15m);

    private static DeductibleExpense Cost(int month, decimal amount, decimal igic = 0m, bool reverseCharge = false, decimal percent = 100m) =>
        new(new DateOnly(2026, month, 15), amount, igic, reverseCharge, percent);

    private static readonly TaxableIncome[] HalfYearIncome =
    [
        ReverseCharge(1, 31, 4000m),
        ReverseCharge(2, 28, 4000m),
        ReverseCharge(3, 31, 4000m),
        Canarian(3, 31, 3000m),
        ReverseCharge(4, 30, 4200m),
        Canarian(4, 30, 1000m),
        ReverseCharge(5, 31, 4000m),
        ReverseCharge(6, 30, 3600m)
    ];

    private static readonly DeductibleExpense[] HalfYearExpenses =
    [
        Cost(1, 12.40m), Cost(2, 250.00m), Cost(3, 18.60m),
        Cost(4, 30.00m), Cost(5, 40.00m), Cost(6, 25.00m),
        // The Social Security fee (cuota de autónomo)
        .. Enumerable.Range(1, 6).Select(month => Cost(month, 300.00m))
    ];

    private static readonly Dictionary<string, string> GestorSettings = new()
    {
        [TerritoryKey] = CanaryIslands,
        [DifficultExpensesKey] = "true"
    };

    private static Dictionary<string, string> GestorSettingsWith(string key, string? value)
    {
        var values = new Dictionary<string, string>(GestorSettings);
        if (value is null)
            values.Remove(key);
        else
            values[key] = value;
        return values;
    }

    private static TaxYearPrediction Predict(
        IReadOnlyDictionary<string, string>? values = null,
        IReadOnlyList<TaxableIncome>? income = null,
        IReadOnlyList<DeductibleExpense>? expenses = null,
        params RecordedTaxPayment[] payments) =>
        Module.Predict(new TaxPredictionInput(
            2026,
            SimplifiedDirectEstimation,
            values ?? GestorSettings,
            income ?? HalfYearIncome,
            expenses ?? HalfYearExpenses,
            payments));

    private static TaxFormPrediction Form(TaxYearPrediction prediction, int quarter, string code) =>
        prediction.Periods[quarter - 1].Forms.Single(f => f.Code == code);

    private static decimal Box(TaxFormPrediction form, string box) =>
        form.Lines.Single(l => l.Box == box).Amount;

    [Test]
    public void Modelo130_Q1_IsTwentyPercentOfTheNetIncome()
    {
        var q1 = Form(Predict(), 1, "130");

        Box(q1, "01").ShouldBe(15000.00m);
        Box(q1, "02").ShouldBe(1871.95m);  // 1,181.00 recorded + 690.95 (5% of 13,819.00)
        Box(q1, "03").ShouldBe(13128.05m);
        Box(q1, "06").ShouldBe(0m);
        q1.Estimated.ShouldBe(2625.61m);
        q1.Deadline.ShouldBe(new DateOnly(2026, 4, 20));
    }

    [Test]
    public void Modelo130_Q2_StartsFromTheRecordedQ1Payment()
    {
        var q2 = Form(Predict(payments: new RecordedTaxPayment(1, "130", 2625.00m)), 2, "130");

        Box(q2, "01").ShouldBe(27800.00m);
        Box(q2, "03").ShouldBe(24342.80m);  // 27,800 − 2,176.00 recorded − 1,281.20 (5%)
        Box(q2, "05").ShouldBe(2625.00m);
        q2.Estimated.ShouldBe(2243.56m);    // 20% = 4,868.56
        q2.Deadline.ShouldBe(new DateOnly(2026, 7, 20));
    }

    [Test]
    public void Modelo130_WithoutARecordedPayment_UsesTheEstimateOfTheEarlierQuarter()
    {
        var prediction = Predict();

        Box(Form(prediction, 2, "130"), "05").ShouldBe(2625.61m);
    }

    [Test]
    public void Modelo130_RecordedPayment_IsShownNextToTheEstimate()
    {
        var q1 = Form(Predict(payments: new RecordedTaxPayment(1, "130", 2625.00m)), 1, "130");

        q1.Paid.ShouldBe(2625.00m);
        Form(Predict(), 1, "130").Paid.ShouldBeNull();
    }

    [Test]
    public void Modelo420_Quarters_AreTheIgicCharged()
    {
        var prediction = Predict(payments: new RecordedTaxPayment(1, "420", 210m));

        Form(prediction, 1, "420").Estimated.ShouldBe(210m);
        Form(prediction, 1, "420").Paid.ShouldBe(210m);
        Form(prediction, 2, "420").Estimated.ShouldBe(70m);
        prediction.Periods[0].Forms.Select(f => f.Code).ShouldBe(["130", "420"]);
    }

    [Test]
    public void Modelo130_DeductingWithholdings_SubtractsBox06()
    {
        var q1 = Form(Predict(GestorSettingsWith(DeductWithholdingsKey, "true")), 1, "130");

        Box(q1, "06").ShouldBe(450m);
        q1.Estimated.ShouldBe(2175.61m);
    }

    [Test]
    public void Modelo130_SettingsLeftEmpty_ApplyNeither()
    {
        var q1 = Form(Predict(new Dictionary<string, string> { [TerritoryKey] = CanaryIslands }), 1, "130");

        q1.Estimated.ShouldBe(2763.80m);
    }

    [Test]
    public void Modelo130_RegimeDefaults_ApplyBoth()
    {
        var defaults = new Dictionary<string, string>(Module.Regimes.Single().Defaults!) { [TerritoryKey] = CanaryIslands };

        Form(Predict(defaults), 1, "130").Estimated.ShouldBe(2175.61m);
    }

    [Test]
    public void Modelo130_WithoutDifficultExpenses_TaxesTheWholeNetIncome()
    {
        var values = GestorSettingsWith(DifficultExpensesKey, null);

        var q1 = Form(Predict(values), 1, "130");

        Box(q1, "02").ShouldBe(1181.00m);
        q1.Estimated.ShouldBe(2763.80m);  // 20% of 13,819.00
        q1.Lines.ShouldNotContain(l => l.Label.Contains("hard-to-justify"));
    }

    [Test]
    public void Modelo130_DifficultExpenses_AreCappedAt2000AYear()
    {
        var q1 = Form(Predict(income: [ReverseCharge(1, 31, 100000m)], expenses: []), 1, "130");

        Box(q1, "02").ShouldBe(2000m);
        q1.Estimated.ShouldBe(19600m);
    }

    [Test]
    public void Modelo130_QuarterBelowWhatWasPaid_IsZero_AndTheNextOneCatchesUp()
    {
        var income = new[] { ReverseCharge(1, 31, 10000m), ReverseCharge(3, 31, 10000m) };
        var expenses = new[] { Cost(2, 6000m) };
        var values = GestorSettingsWith(DifficultExpensesKey, null);

        var prediction = Predict(values, income, expenses, new RecordedTaxPayment(1, "130", 3000m));

        var q2 = Form(prediction, 2, "130");
        Box(q2, "07").ShouldBe(-200m);    // 20% of 14,000 = 2,800, 3,000 already paid
        q2.Estimated.ShouldBe(0m);
        Box(Form(prediction, 3, "130"), "05").ShouldBe(3000m);
    }

    [Test]
    public void Modelo130_NetLoss_HasNothingToPay()
    {
        var q1 = Form(Predict(income: [ReverseCharge(1, 31, 1000m)], expenses: [Cost(1, 3000m)]), 1, "130");

        Box(q1, "03").ShouldBe(-2000m);
        Box(q1, "04").ShouldBe(0m);
        q1.Estimated.ShouldBe(0m);
    }

    [Test]
    public void Modelo130_PartlyDeductibleExpense_CountsItsShare()
    {
        var values = GestorSettingsWith(DifficultExpensesKey, null);

        var q1 = Form(Predict(values, [ReverseCharge(1, 31, 1000m)], [Cost(1, 200m, percent: 30m)]), 1, "130");

        Box(q1, "02").ShouldBe(60m);
    }

    [Test]
    public void Modelo130_OtherYears_AreIgnored()
    {
        var income = new[] { ReverseCharge(1, 31, 1000m), new TaxableIncome(new DateOnly(2025, 12, 31), 50000m, []) };

        Box(Form(Predict(income: income, expenses: []), 1, "130"), "01").ShouldBe(1000m);
    }

    [Test]
    public void Modelo420_ForeignServiceWithoutIgic_IsSelfChargedAndDeducted_NettingToZero()
    {
        var q1 = Form(Predict(income: [], expenses: [Cost(2, 100m, reverseCharge: true)]), 1, "420");

        q1.Lines.Single(l => l.Label.StartsWith("Self-charged", StringComparison.Ordinal)).Amount.ShouldBe(7m);
        q1.Lines.Single(l => l.Label.Contains("deductible")).Amount.ShouldBe(7m);
        q1.Estimated.ShouldBe(0m);
    }

    [Test]
    public void Modelo420_IgicPaidOnExpenses_IsDeducted()
    {
        var q1 = Form(Predict(income: [Canarian(3, 31, 3000m)], expenses: [Cost(3, 100m, igic: 7m)]), 1, "420");

        q1.Estimated.ShouldBe(203m);
    }

    [Test]
    public void Modelo420_NegativeQuarter_CarriesTheCreditForward()
    {
        var income = new[] { Canarian(5, 31, 1000m) };   // 70 IGIC in Q2
        var expenses = new[] { Cost(1, 1000m, igic: 100m) };  // 100 IGIC deductible in Q1

        var prediction = Predict(income: income, expenses: expenses);

        Form(prediction, 1, "420").Estimated.ShouldBe(0m);
        Form(prediction, 2, "420").Lines.Single(l => l.Label.StartsWith("Credit", StringComparison.Ordinal)).Amount.ShouldBe(100m);
        Form(prediction, 2, "420").Estimated.ShouldBe(0m);
        Form(prediction, 3, "420").Lines.Single(l => l.Label.StartsWith("Credit", StringComparison.Ordinal)).Amount.ShouldBe(30m);
    }

    [Test]
    public void Modelo303_Mainland_SettlesIva_AndWarnsAboutIgicInvoices()
    {
        var values = new Dictionary<string, string> { [TerritoryKey] = Mainland };
        var income = new[]
        {
            new TaxableIncome(new DateOnly(2026, 2, 10), 1000m, [new ChargedTax("IVA", 21m, 210m)], 150m),
            Canarian(2, 11, 100m)
        };

        var prediction = Predict(values, income, [Cost(2, 100m, igic: 21m)]);

        var q1 = Form(prediction, 1, "303");
        q1.Name.ShouldContain("IVA");
        q1.Estimated.ShouldBe(189m);
        prediction.Warnings.ShouldHaveSingleItem().ShouldContain("1 invoice(s)");
    }

    [TestCase(CanaryIslands, "420")]
    [TestCase(Mainland, "303")]
    public void IndirectReturn_TaxThatDoesntSayWhichItIs_CountsAsTheTerritorysOwn(string territory, string code)
    {
        var values = new Dictionary<string, string> { [TerritoryKey] = territory };
        var income = new[] { new TaxableIncome(new DateOnly(2026, 3, 31), 3000m, [new ChargedTax(null, 7m, 210m)], 450m) };

        var prediction = Predict(values, income, []);

        Form(prediction, 1, code).Estimated.ShouldBe(210m);
        prediction.Warnings.ShouldBeEmpty();
    }

    [TestCase("IGIC", "IGIC")]
    [TestCase("IVA", "IVA")]
    [TestCase("", null)]
    public void IndirectTaxKind_IsWhatTheTaxSays_OrNull(string taxType, string? expected)
    {
        var values = taxType.Length == 0 ? new Dictionary<string, string>() : new Dictionary<string, string> { ["taxType"] = taxType };

        Module.IndirectTaxKind(values).ShouldBe(expected);
    }

    [Test]
    public void Predict_WithoutTerritory_EstimatesModelo130Only_AndSaysWhy()
    {
        var prediction = Predict(new Dictionary<string, string>());

        prediction.Periods.ShouldAllBe(p => p.Forms.Count == 1 && p.Forms[0].Code == "130");
        prediction.Warnings.ShouldHaveSingleItem().ShouldContain("Canary Islands");
    }

    [Test]
    public void Predict_UnknownRegime_Throws()
    {
        var input = new TaxPredictionInput(2026, "modulos", GestorSettings, [], [], []);

        Should.Throw<ArgumentException>(() => Module.Predict(input));
    }

    [TestCase(2026, 1, "2026-04-20")]
    [TestCase(2026, 3, "2026-10-20")]
    [TestCase(2026, 4, "2027-02-01")]  // 30 January 2027 is a Saturday
    [TestCase(2025, 3, "2025-10-20")]
    [TestCase(2024, 2, "2024-07-22")]  // 20 July 2024 is a Saturday
    public void Deadline_IsThe20thOrThe30thOfJanuary_MovedOffWeekends(int year, int quarter, string expected)
    {
        SpanishTaxRules.Deadline(year, quarter).ShouldBe(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test]
    public void Periods_AreTheFourQuarters()
    {
        var periods = Predict().Periods;

        periods.Select(p => p.Name).ShouldBe(["Q1", "Q2", "Q3", "Q4"]);
        periods[3].Start.ShouldBe(new DateOnly(2026, 10, 1));
        periods[3].End.ShouldBe(new DateOnly(2026, 12, 31));
    }
}
