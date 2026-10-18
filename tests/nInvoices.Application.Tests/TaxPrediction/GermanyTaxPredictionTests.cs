using nInvoices.Core.TaxPrediction;
using nInvoices.Countries.Germany.TaxPrediction;
using Shouldly;
using static nInvoices.Countries.Germany.TaxPrediction.GermanyTaxPredictionModule;
using nInvoices.Application.Tests.TestDoubles;

namespace nInvoices.Application.Tests.TaxPrediction;

[TestFixture]
public sealed class GermanyTaxPredictionTests
{
    private static readonly GermanyTaxPredictionModule Module = new(TestText.Instance);

    // 60,000 invoiced in February with 19% VAT; 10,000 of expenses in April with 1,900 of input VAT
    private static TaxPredictionInput Input(string regime, Dictionary<string, string>? values = null, IReadOnlyList<RecordedTaxPayment>? payments = null) => new(
        2026,
        regime,
        values ?? new Dictionary<string, string> { [VatPeriodKey] = Quarterly, [InsuranceKey] = "8000", [HebesatzKey] = "400" },
        [new TaxableIncome(new DateOnly(2026, 2, 15), 60_000m, [new ChargedTax(VatKind, 19m, 11_400m)])],
        [new DeductibleExpense(new DateOnly(2026, 4, 10), 10_000m, 1_900m)],
        payments ?? []);

    private static TaxFormPrediction Form(TaxYearPrediction prediction, int period, string code) =>
        prediction.Periods.Single(p => p.Period == period).Forms.Single(f => f.Code == code);

    [TestCase(12_348, 2026, 0)]
    [TestCase(17_799, 2026, 1_034)]
    [TestCase(30_000, 2026, 4_217)]
    [TestCase(69_878, 2026, 18_213)]
    [TestCase(30_000, 2025, 4_303)]
    [TestCase(300_000, 2026, 115_529)]
    public void IncomeTax_FollowsTheTariffOfTheYear(decimal taxable, int year, decimal expected) =>
        GermanTaxRules.IncomeTax(taxable, year).ShouldBe(expected);

    [TestCase(20_000, 0)]
    [TestCase(25_000, 553.35)]
    [TestCase(60_000, 3_300)]
    public void SolidaritySurcharge_IsPhasedInAboveTheExemption(decimal incomeTax, decimal expected) =>
        GermanTaxRules.SolidaritySurcharge(incomeTax, 2026).ShouldBe(expected);

    [Test]
    public void Freiberufler_EstimatesTheIncomeTax_AndSpreadsItOverFourPrepayments()
    {
        var prediction = Module.Predict(Input(FreelancerRegime));

        // Profit 50,000; taxable 50,000 − 8,000 − 36 = 41,964; Einkommensteuer 2026: 7,838
        var balance = Form(prediction, 21, IncomeTaxCode);
        balance.Lines.Single(l => l.Label.StartsWith("Taxable income", StringComparison.Ordinal)).Amount.ShouldBe(41_964m);
        balance.Lines.Single(l => l.Label == "Tax of the year").Amount.ShouldBe(7_838m);
        Form(prediction, 13, IncomeTaxCode).Estimated.ShouldBe(1_959.50m);
        balance.Estimated.ShouldBe(0m);
        prediction.Periods.ShouldNotContain(p => p.Forms.Any(f => f.Code == TradeTaxCode));
    }

    [Test]
    public void Freiberufler_QuarterlyVat_PaysQ1_AndQ2IsARefund()
    {
        var prediction = Module.Predict(Input(FreelancerRegime));

        Form(prediction, 1, "USt").Estimated.ShouldBe(11_400m);
        Form(prediction, 1, "USt").Deadline.ShouldBe(new DateOnly(2026, 4, 10));
        var q2 = Form(prediction, 2, "USt");
        q2.Estimated.ShouldBe(0m);
        q2.Lines.Single(l => l.Box == "83").Amount.ShouldBe(-1_900m);
        // 10 October 2026 is a Saturday
        Form(prediction, 3, "USt").Deadline.ShouldBe(new DateOnly(2026, 10, 12));

        // In the order of their deadlines
        prediction.Periods.Select(p => p.Period).ShouldBe([13, 1, 14, 2, 15, 3, 16, 4, 21]);
    }

    [Test]
    public void Gewerbe_AddsTheGewerbesteuer_CreditedAgainstTheIncomeTax()
    {
        var prediction = Module.Predict(Input(TradeRegime));

        // 50,000 − 24,500 = 25,500 × 3.5% = 892.50; × 400% = 3,570; credited up to 4 × 892.50 = 3,570
        Form(prediction, 21, TradeTaxCode).Lines.Single(l => l.Label.StartsWith("Gewerbesteuer at", StringComparison.Ordinal)).Amount.ShouldBe(3_570m);
        Form(prediction, 21, IncomeTaxCode).Lines.Single(l => l.Label == "Tax of the year").Amount.ShouldBe(4_268m);
        Form(prediction, 17, TradeTaxCode).Estimated.ShouldBe(892.50m);
        Form(prediction, 17, TradeTaxCode).Deadline.ShouldBe(new DateOnly(2026, 2, 16)); // 15 February 2026 is a Sunday
    }

    [Test]
    public void TheAssessedPrepayment_IsUsed_AndOverpaymentIsARefund()
    {
        var values = new Dictionary<string, string> { [VatPeriodKey] = Quarterly, [InsuranceKey] = "8000", [PrepaymentKey] = "2000" };

        var prediction = Module.Predict(Input(FreelancerRegime, values));

        Form(prediction, 13, IncomeTaxCode).Estimated.ShouldBe(2_000m);
        var balance = Form(prediction, 21, IncomeTaxCode);
        balance.Estimated.ShouldBe(0m);
        balance.Lines[^1].Amount.ShouldBe(-162m);
    }

    [Test]
    public void ARecordedPrepayment_ReplacesTheEstimateInTheBalance()
    {
        var prediction = Module.Predict(Input(FreelancerRegime, payments: [new RecordedTaxPayment(13, IncomeTaxCode, 1_000m)]));

        // 7,838 − 1,000 − 3 × 1,959.50 = 959.50
        Form(prediction, 21, IncomeTaxCode).Estimated.ShouldBe(959.50m);
    }

    [Test]
    public void Kleinunternehmer_HasNoVatReturns_AndAboveTheLimitIsWarned()
    {
        var values = new Dictionary<string, string> { [SmallBusinessKey] = "true" };
        var input = Input(FreelancerRegime, values) with
        {
            Income = [new TaxableIncome(new DateOnly(2026, 2, 15), 120_000m, [])]
        };

        var prediction = Module.Predict(input);

        prediction.Periods.ShouldNotContain(p => p.Forms.Any(f => f.Code == "USt"));
        prediction.Warnings.ShouldContain(w => w.Contains("no longer a Kleinunternehmer"));
    }

    [Test]
    public void MonthlyVat_HasTwelveReturns()
    {
        var values = new Dictionary<string, string> { [VatPeriodKey] = Monthly, [ExtensionKey] = "true" };

        var prediction = Module.Predict(Input(FreelancerRegime, values));

        prediction.Periods.Count(p => p.Forms.Any(f => f.Code == "USt")).ShouldBe(12);
        // February's return with the extension: 10 April
        Form(prediction, 2, "USt").Deadline.ShouldBe(new DateOnly(2026, 4, 10));
        prediction.Warnings.ShouldContain(w => w.Contains("Sondervorauszahlung"));
    }
}
