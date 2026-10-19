using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.TaxPrediction;
using nInvoices.Countries.UnitedKingdom.TaxPrediction;
using Shouldly;
using static nInvoices.Countries.UnitedKingdom.TaxPrediction.UkTaxPredictionModule;

namespace nInvoices.Application.Tests.TaxPrediction;

[TestFixture]
public sealed class UkTaxPredictionTests
{
    private static readonly UkTaxPredictionModule Module = new(TestText.Instance);

    // 60,000 invoiced in May 2026 (tax year 2026/27); 5,000 of expenses in June
    private static TaxPredictionInput Input(
        Dictionary<string, string>? values = null,
        decimal revenue = 60_000m,
        decimal vat = 0m,
        int year = 2026,
        IReadOnlyList<RecordedTaxPayment>? payments = null) => new(
        year,
        SoleTraderRegime,
        values ?? new Dictionary<string, string> { [VatSchemeKey] = NotRegistered },
        [
            new TaxableIncome(new DateOnly(year, 5, 10), revenue, vat > 0 ? [new ChargedTax(VatKind, 20m, vat)] : []),
            // March is the previous tax year
            new TaxableIncome(new DateOnly(year, 3, 31), 99_999m, [])
        ],
        [new DeductibleExpense(new DateOnly(year, 6, 1), 5_000m, vat > 0 ? 1_000m : 0m)],
        payments ?? []);

    private static TaxFormPrediction Form(TaxYearPrediction prediction, int period) =>
        prediction.Periods.Single(p => p.Period == period).Forms.Single();

    private static decimal Line(TaxFormPrediction form, string label) => form.Lines.Single(l => l.Label == label).Amount;

    [Test]
    public void TaxYear_RunsFromSixthOfAprilToFifthOfApril()
    {
        var year = Module.TaxYearOf(2026);

        (year.Start, year.End, year.Label).ShouldBe((new DateOnly(2026, 4, 6), new DateOnly(2027, 4, 5), "2026/27"));
    }

    [TestCase(5, 0)]
    [TestCase(6, 1_000)]
    public void AnInvoiceOnTheFifthOrSixthOfApril_FallsInTheTaxYearItStarts(int day, decimal counted)
    {
        var input = Input() with { Income = [new TaxableIncome(new DateOnly(2026, 4, day), 1_000m, [])], Expenses = [] };

        Line(Form(Module.Predict(input), 7), "Profit").ShouldBe(counted);
    }

    [Test]
    public void SoleTrader_IncomeTaxAndClass4_InTwoPaymentsOnAccountAndABalance()
    {
        var prediction = Module.Predict(Input());

        // Profit 55,000 (March's invoice is last tax year's); taxable 42,430:
        // 37,700 × 20% + 4,730 × 40% = 9,432; Class 4: 37,700 × 6% + 4,730 × 2% = 2,356.60
        var balancing = Form(prediction, 7);
        Line(balancing, "Profit").ShouldBe(55_000m);
        Line(balancing, "Income Tax").ShouldBe(9_432m);
        Line(balancing, "Class 4 National Insurance").ShouldBe(2_356.60m);
        Line(balancing, "Tax of the year").ShouldBe(11_788.60m);

        Form(prediction, 5).Estimated.ShouldBe(5_894.30m);
        Form(prediction, 5).Deadline.ShouldBe(new DateOnly(2027, 1, 31));
        Form(prediction, 6).Deadline.ShouldBe(new DateOnly(2027, 7, 31));
        balancing.Deadline.ShouldBe(new DateOnly(2028, 1, 31));
        balancing.Estimated.ShouldBe(0m);
        prediction.Periods.Select(p => p.Period).ShouldBe([5, 6, 7]);
    }

    [Test]
    public void HighIncome_TapersThePersonalAllowance()
    {
        var prediction = Module.Predict(Input(revenue: 115_000m));

        // Profit 110,000: allowance 12,570 − 5,000 = 7,570; taxable 102,430: 7,540 + 64,730 × 40% = 33,432
        var balancing = Form(prediction, 7);
        Line(balancing, "− Personal allowance").ShouldBe(7_570m);
        Line(balancing, "Income Tax").ShouldBe(33_432m);
        Line(balancing, "Class 4 National Insurance").ShouldBe(3_456.60m);
    }

    [Test]
    public void ScottishTaxpayer_UsesScotlandsBands()
    {
        var prediction = Module.Predict(Input(new Dictionary<string, string> { [VatSchemeKey] = NotRegistered, [ScottishKey] = "true" }, year: 2025));

        // 2025/26, taxable 42,430: 2,827 × 19% + 12,094 × 20% + 16,171 × 21% + 11,338 × 42% = 11,113.80
        Line(Form(prediction, 7), "Income Tax (Scottish rates)").ShouldBe(11_113.80m);
        prediction.Warnings.ShouldNotContain(w => w.Contains("Scotland"));

        Module.Predict(Input(new Dictionary<string, string> { [VatSchemeKey] = NotRegistered, [ScottishKey] = "true" }))
            .Warnings.ShouldContain(w => w.Contains("Scotland's bands for 2026/27"));
    }

    [Test]
    public void StudentLoans_AreInTheBalance_NotPaidOnAccount()
    {
        var values = new Dictionary<string, string> { [VatSchemeKey] = NotRegistered, [StudentLoanKey] = "plan2", [PostgraduateLoanKey] = "true" };

        var prediction = Module.Predict(Input(values));

        // Plan 2: (55,000 − 29,385) × 9% = 2,305 (whole pounds); postgraduate: 34,000 × 6% = 2,040
        var balancing = Form(prediction, 7);
        Line(balancing, "Student loan repayment").ShouldBe(4_345m);
        Form(prediction, 5).Estimated.ShouldBe(5_894.30m);
        balancing.Estimated.ShouldBe(4_345m);
    }

    [Test]
    public void TradingAllowance_ReplacesSmallExpenses_AndNoPaymentsOnAccountBelow1000()
    {
        var input = Input(new Dictionary<string, string> { [VatSchemeKey] = NotRegistered, [TradingAllowanceKey] = "true" }, revenue: 14_000m) with
        {
            Expenses = [new DeductibleExpense(new DateOnly(2026, 6, 1), 200m)]
        };

        var prediction = Module.Predict(input);

        // 14,000 − 1,000 = 13,000: 430 × 20% = 86 of Income Tax, 25.80 of Class 4: below £1,000, no payments on account
        var balancing = Form(prediction, 7);
        Line(balancing, "− Trading allowance").ShouldBe(1_000m);
        Line(balancing, "Income Tax").ShouldBe(86m);
        Form(prediction, 5).Estimated.ShouldBe(0m);
        balancing.Estimated.ShouldBe(111.80m);
    }

    [Test]
    public void LastYearsBill_SetsThePaymentsOnAccount_AndARecordedOneCounts()
    {
        var values = new Dictionary<string, string> { [VatSchemeKey] = NotRegistered, [PreviousYearTaxKey] = "8,000" };

        var prediction = Module.Predict(Input(values, payments: [new RecordedTaxPayment(5, SelfAssessmentCode, 3_000m)]));

        Form(prediction, 6).Estimated.ShouldBe(4_000m);
        // 11,788.60 − 3,000 − 4,000
        Form(prediction, 7).Estimated.ShouldBe(4_788.60m);
    }

    [Test]
    public void StandardVat_QuarterlyOnTheStagger_DueOneMonthAndSevenDaysAfter()
    {
        var prediction = Module.Predict(Input(new Dictionary<string, string> { [VatSchemeKey] = Standard, [StaggerKey] = "1" }, vat: 12_000m));

        var q1 = Form(prediction, 1);
        q1.Estimated.ShouldBe(11_000m);
        q1.Deadline.ShouldBe(new DateOnly(2026, 8, 7));
        prediction.Periods.Single(p => p.Period == 1).End.ShouldBe(new DateOnly(2026, 6, 30));
        Form(prediction, 4).Deadline.ShouldBe(new DateOnly(2027, 5, 7));
        // By deadline: the January payment on account comes before Q3's return (7 February)
        prediction.Periods.Select(p => p.Period).ShouldBe([1, 2, 5, 3, 4, 6, 7]);
    }

    [Test]
    public void StandardVat_Stagger3_StartsInJune()
    {
        var prediction = Module.Predict(Input(new Dictionary<string, string> { [VatSchemeKey] = Standard, [StaggerKey] = "3" }, vat: 12_000m));

        var q1 = prediction.Periods.Single(p => p.Period == 1);
        (q1.Start, q1.End).ShouldBe((new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31)));
        // May's invoice falls before stagger 3's first quarter
        q1.Forms.Single().Lines.Single(l => l.Box == "1").Amount.ShouldBe(0m);
    }

    [Test]
    public void FlatRateScheme_PaysAShareOfGrossTurnover_AndTheSurplusIsIncome()
    {
        var prediction = Module.Predict(Input(new Dictionary<string, string> { [VatSchemeKey] = FlatRate, [FlatRateKey] = "16.5" }, vat: 12_000m));

        // 72,000 × 16.5% = 11,880; the 120 kept is income; expenses count with their VAT: 60,000 + 120 − 6,000
        Form(prediction, 1).Estimated.ShouldBe(11_880m);
        var balancing = Form(prediction, 7);
        Line(balancing, "+ VAT kept under the Flat Rate Scheme").ShouldBe(120m);
        Line(balancing, "Profit").ShouldBe(54_120m);
    }

    [Test]
    public void NotRegistered_AboveTheThreshold_IsWarned() =>
        Module.Predict(Input(revenue: 95_000m)).Warnings.ShouldContain(w => w.Contains("register for VAT"));

    [Test]
    public void Regimes_AreTranslated() =>
        Module.Regimes.SelectMany(r => r.Fields).ShouldAllBe(f => !f.Label.StartsWith("taxPrediction.", StringComparison.Ordinal));
}
