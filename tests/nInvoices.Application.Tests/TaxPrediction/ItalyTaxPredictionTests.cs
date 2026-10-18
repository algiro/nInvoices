using nInvoices.Core.TaxPrediction;
using nInvoices.Countries.Italy;
using nInvoices.Countries.Italy.TaxPrediction;
using Shouldly;
using static nInvoices.Countries.Italy.TaxPrediction.ItalyTaxPredictionModule;

namespace nInvoices.Application.Tests.TaxPrediction;

[TestFixture]
public sealed class ItalyTaxPredictionTests
{
    private static readonly ItalyTaxPredictionModule Module = new();

    private static TaxPredictionInput Forfettario(
        Dictionary<string, string>? values = null,
        IReadOnlyList<RecordedTaxPayment>? payments = null,
        IReadOnlyList<DeductibleExpense>? expenses = null) => new(
        2026,
        ForfettarioRegime,
        values ?? new Dictionary<string, string> { [CoefficientKey] = "78", [InpsRateKey] = "26.07" },
        [new TaxableIncome(new DateOnly(2026, 3, 10), 40_000m, [])],
        expenses ?? [],
        payments ?? []);

    private static TaxFormPrediction Form(TaxYearPrediction prediction, int period, string code) =>
        prediction.Periods.Single(p => p.Period == period).Forms.Single(f => f.Code == code);

    [Test]
    public void Forfettario_TaxesTheCoefficientOfRevenue_LessInps()
    {
        var prediction = Module.Predict(Forfettario());

        // 40,000 × 78% = 31,200; INPS 26.07% = 8,133.84; 15% of 23,066.16 = 3,459.92
        var balance = Form(prediction, 7, SubstituteTaxCode);
        balance.Lines.Single(l => l.Box == "LM35").Amount.ShouldBe(8_133.84m);
        balance.Lines.Single(l => l.Box == "LM39").Amount.ShouldBe(3_459.92m);

        // Metodo previsionale: 40% and 60% of the estimate, nothing left for the balance
        Form(prediction, 5, SubstituteTaxCode).Estimated.ShouldBe(1_383.97m);
        Form(prediction, 6, SubstituteTaxCode).Estimated.ShouldBe(2_075.95m);
        balance.Estimated.ShouldBe(0m);

        // INPS: 40% + 40% on account, the remaining 20% in the balance
        Form(prediction, 5, InpsCode).Estimated.ShouldBe(3_253.54m);
        Form(prediction, 6, InpsCode).Estimated.ShouldBe(3_253.54m);
        Form(prediction, 7, InpsCode).Estimated.ShouldBe(1_626.76m);

        prediction.Periods.Select(p => p.Name).ShouldBe(["1st installment", "2nd installment", "Balance"]);
        Form(prediction, 7, SubstituteTaxCode).Deadline.ShouldBe(new DateOnly(2027, 6, 30));
    }

    [Test]
    public void Forfettario_NewActivity_Pays5Percent()
    {
        var prediction = Module.Predict(Forfettario(new Dictionary<string, string>
        {
            [CoefficientKey] = "78", [InpsRateKey] = "26.07", [StartupKey] = "true"
        }));

        Form(prediction, 7, SubstituteTaxCode).Lines.Single(l => l.Box == "LM39").Amount.ShouldBe(1_153.31m);
    }

    [Test]
    public void Forfettario_WithLastYearsTax_UsesTheMetodoStorico()
    {
        var prediction = Module.Predict(Forfettario(new Dictionary<string, string>
        {
            [CoefficientKey] = "78", [InpsRateKey] = "26.07", [PreviousTaxKey] = "1000,00"
        }));

        Form(prediction, 5, SubstituteTaxCode).Estimated.ShouldBe(400m);
        Form(prediction, 6, SubstituteTaxCode).Estimated.ShouldBe(600m);
        Form(prediction, 7, SubstituteTaxCode).Estimated.ShouldBe(2_459.92m);
    }

    [TestCase("50", 0, 0)]
    [TestCase("200", 0, 200)]
    public void Forfettario_SmallBasis_PaysLessOrNothingOnAccount(string previous, decimal june, decimal november)
    {
        var prediction = Module.Predict(Forfettario(new Dictionary<string, string>
        {
            [CoefficientKey] = "78", [InpsRateKey] = "26.07", [PreviousTaxKey] = previous
        }));

        Form(prediction, 5, SubstituteTaxCode).Estimated.ShouldBe(june);
        Form(prediction, 6, SubstituteTaxCode).Estimated.ShouldBe(november);
    }

    [Test]
    public void Forfettario_ARecordedInstallment_ReplacesTheEstimateInTheBalance()
    {
        var prediction = Module.Predict(Forfettario(payments: [new RecordedTaxPayment(5, SubstituteTaxCode, 1_500m)]));

        var june = Form(prediction, 5, SubstituteTaxCode);
        june.Paid.ShouldBe(1_500m);
        // 3,459.92 − 1,500 − 2,075.95 is a credit: nothing to pay
        Form(prediction, 7, SubstituteTaxCode).Estimated.ShouldBe(0m);
        Form(prediction, 7, SubstituteTaxCode).Lines[^1].Amount.ShouldBe(-116.03m);
    }

    [Test]
    public void Forfettario_CountsTheRivalsaAsRevenue_AndWarnsAboutExpenses()
    {
        var input = Forfettario(expenses: [new DeductibleExpense(new DateOnly(2026, 5, 1), 500m)]) with
        {
            Income = [new TaxableIncome(new DateOnly(2026, 3, 10), 1_000m, [new ChargedTax(ItalyComplianceModule.Contribution, 4m, 40m)])]
        };

        var prediction = Module.Predict(input);

        Form(prediction, 7, SubstituteTaxCode).Lines.Single(l => l.Box == "LM22").Amount.ShouldBe(1_040m);
        prediction.Warnings.ShouldContain(w => w.Contains("Expenses are not deducted"));
    }

    [Test]
    public void Forfettario_AboveTheLimit_Warns()
    {
        var input = Forfettario() with { Income = [new TaxableIncome(new DateOnly(2026, 3, 10), 90_000m, [])] };

        Module.Predict(input).Warnings.ShouldContain(w => w.Contains("next year you leave"));
    }

    [Test]
    public void Ordinario_IrpefOnIncomeLessExpensesAndInps_AndQuarterlyIva()
    {
        var input = new TaxPredictionInput(
            2026,
            OrdinarioRegime,
            new Dictionary<string, string> { [InpsRateKey] = "26.07", [WorkDeductionKey] = "true" },
            [
                new TaxableIncome(new DateOnly(2026, 2, 15), 50_000m,
                [
                    new ChargedTax(ItalyComplianceModule.Contribution, 4m, 2_000m),
                    new ChargedTax(ItalyComplianceModule.Iva, 22m, 11_440m)
                ])
            ],
            [new DeductibleExpense(new DateOnly(2026, 4, 10), 10_000m, 2_200m)],
            []);

        var prediction = Module.Predict(input);

        // 52,000 − 10,000 = 42,000; INPS 10,949.40; taxable 31,050.60
        // IRPEF 2026: 6,440 + 33% of 3,050.60 = 7,446.70; deduction 430.67 + 50 = 480.67
        var balance = Form(prediction, 7, IrpefCode);
        balance.Lines.Single(l => l.Box == "RN1").Amount.ShouldBe(31_050.60m);
        balance.Lines.Single(l => l.Box == "RN5").Amount.ShouldBe(7_446.70m);
        balance.Lines.Single(l => l.Box == "RN7").Amount.ShouldBe(480.67m);
        Form(prediction, 5, IrpefCode).Estimated.ShouldBe(2_786.41m);
        Form(prediction, 6, IrpefCode).Estimated.ShouldBe(4_179.62m);

        // Q1 pays the IVA with 1% interest; Q2's expense is a credit carried on
        Form(prediction, 1, IvaLiquidationCalculator.Code).Estimated.ShouldBe(11_554.40m);
        Form(prediction, 2, IvaLiquidationCalculator.Code).Estimated.ShouldBe(0m);
        Form(prediction, 3, IvaLiquidationCalculator.Code).Lines.Single(l => l.Box == "VP8").Amount.ShouldBe(2_200m);
        Form(prediction, 4, IvaLiquidationCalculator.Code).Deadline.ShouldBe(new DateOnly(2027, 3, 16));

        // In the order of the deadlines
        prediction.Periods.Select(p => p.Period).ShouldBe([1, 5, 2, 3, 6, 4, 7]);
    }

    [TestCase(2025, 7_507.71)]
    [TestCase(2026, 7_446.70)]
    public void Irpef_FollowsTheBracketsOfTheYear(int year, decimal expected) =>
        ItalianTaxRules.Irpef(31_050.60m, year).ShouldBe(expected);

    [TestCase(5_000, 1_265)]
    [TestCase(20_000, 772)]
    [TestCase(30_000, 504.55)]
    [TestCase(60_000, 0)]
    public void SelfEmploymentDeduction_FallsWithIncome(decimal income, decimal expected) =>
        ItalianTaxRules.SelfEmploymentDeduction(income).ShouldBe(expected);

    [Test]
    public void Predict_ANumberThatIsNot_Warns()
    {
        var prediction = Module.Predict(Forfettario(new Dictionary<string, string>
        {
            [CoefficientKey] = "78", [InpsRateKey] = "26.07", [PreviousTaxKey] = "lots"
        }));

        prediction.Warnings.ShouldContain(w => w.Contains("\"lots\" is not a number"));
    }

    [Test]
    public void IndirectTaxKind_TellsContributionsFromIva()
    {
        Module.IndirectTaxKind(new Dictionary<string, string> { [ItalyComplianceModule.TaxKindKey] = ItalyComplianceModule.Contribution })
            .ShouldBe(ItalyComplianceModule.Contribution);
        Module.IndirectTaxKind(new Dictionary<string, string>()).ShouldBe(ItalyComplianceModule.Iva);
    }
}
