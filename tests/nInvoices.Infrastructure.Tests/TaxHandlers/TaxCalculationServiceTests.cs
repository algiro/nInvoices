using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.TaxHandlers;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.TaxHandlers;

[TestFixture]
public sealed class TaxCalculationServiceTests
{
    private static readonly TaxCalculationService Service = new([new PercentageTaxHandler()]);

    private static Tax Percentage(long id, string code, decimal rate, int order) =>
        new(1, code, code, "PERCENTAGE", rate, TaxApplicationType.OnSubtotal, order) { Id = id };

    [Test]
    public void CalculateTaxes_RoundsEachTaxToCents_SoTheTotalIsTheSumOfTheLines()
    {
        // 21% of 8100.55 is 1701.1155 and 15% is 1215.0825: printed as 1701.12 and -1215.08
        var (total, lines) = Service.CalculateTaxes(
            [Percentage(1, "VAT", 21m, 0), Percentage(2, "IRPF", -15m, 1)],
            new Money(8100.55m, "EUR"));

        var amounts = lines.Select(l => l.TaxAmount.Amount).ToList();
        amounts.ShouldBe([1701.12m, -1215.08m]);
        total.Amount.ShouldBe(486.04m);
        (8100.55m + total.Amount).ShouldBe(8100.55m + amounts.Sum());
    }

    [Test]
    public void CalculateTaxes_HalfAUnitRoundsAwayFromZero()
    {
        var (_, lines) = Service.CalculateTaxes([Percentage(1, "VAT", 10m, 0)], new Money(0.05m, "EUR"));

        lines.Single().TaxAmount.Amount.ShouldBe(0.01m); // 0.005
    }

    [Test]
    public void CalculateTaxes_TaxOnTaxUsesTheRoundedBase()
    {
        var vat = Percentage(1, "VAT", 21m, 0);
        var surcharge = new Tax(1, "SUR", "SUR", "PERCENTAGE", 5.2m, TaxApplicationType.OnTax, 1) { Id = 2, AppliedToTaxId = 1 };

        var (_, taxLines) = Service.CalculateTaxes([vat, surcharge], new Money(8100.55m, "EUR"));
        var lines = taxLines.ToList();

        lines[0].TaxAmount.Amount.ShouldBe(1701.12m);
        lines[1].BaseAmount.Amount.ShouldBe(1701.12m);
        lines[1].TaxAmount.Amount.ShouldBe(88.46m); // 5.2% of 1701.12 = 88.45824
    }

    [Test]
    public void CalculateTaxes_NoTaxes_IsZero()
    {
        var (total, lines) = Service.CalculateTaxes([], new Money(100m, "EUR"));

        total.Amount.ShouldBe(0m);
        lines.ShouldBeEmpty();
    }
}
