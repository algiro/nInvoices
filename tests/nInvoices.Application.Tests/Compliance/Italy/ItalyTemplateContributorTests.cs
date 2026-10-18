using Moq;
using nInvoices.Application.Compliance;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using nInvoices.Countries.Italy;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance.Italy;

[TestFixture]
public sealed class ItalyTemplateContributorTests
{
    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static ItalyTemplateContributor Contributor(bool italyOn, string regime, params Tax[] taxes)
    {
        var gate = new Mock<IComplianceGate>();
        var settings = new ComplianceSettings(ItalyComplianceModule.CountryCodeValue);
        settings.Update(true, "Mario Rossi", FatturaPaTestData.IssuerVat, null,
            new Dictionary<string, string> { [ItalyComplianceModule.RegimeKey] = regime });
        gate.Setup(g => g.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(italyOn ? [new ActiveCompliance(new ItalyComplianceModule(TestText.Instance), settings)] : []);

        var repository = new InMemoryRepository<Tax>();
        foreach (var tax in taxes)
            repository.AddAsync(tax).GetAwaiter().GetResult();
        return new ItalyTemplateContributor(gate.Object, repository);
    }

    private static Invoice Invoice(decimal subtotal, params InvoiceTaxLine[] lines)
    {
        var invoice = new Invoice(1, new InvoiceNumber("2026/12"), InvoiceType.OneTime, new DateOnly(2026, 10, 15), new Money(subtotal, "EUR"), "EUR");
        foreach (var line in lines)
            invoice.TaxLines.Add(line);
        invoice.AddTaxes(new Money(lines.Sum(l => l.TaxAmount.Amount), "EUR"));
        return invoice;
    }

    private static InvoiceTaxLine Line(string taxId, decimal rate, decimal baseAmount, decimal amount, int order) =>
        new(taxId, taxId, rate, new Money(baseAmount, "EUR"), new Money(amount, "EUR"), order);

    [Test]
    public async Task Contribute_WithoutTheItalianRules_GivesNothing()
    {
        var result = await Contributor(false, ItalyComplianceModule.Forfettario).ContributeAsync(Invoice(1000m), Token);

        result.ShouldBeNull();
    }

    [Test]
    public async Task Contribute_Forfettario_GivesTheNote_TheBollo_AndTheRivalsa()
    {
        var rivalsa = new Tax(1, "INPS", "Rivalsa INPS 4%", "PERCENTAGE", 4m, TaxApplicationType.OnSubtotal) { AddsToTaxableBase = true };
        rivalsa.SetComplianceValues(ItalyComplianceModule.CountryCodeValue, FatturaPaTestData.RivalsaValues);

        var model = (await Contributor(true, ItalyComplianceModule.Forfettario, rivalsa)
                .ContributeAsync(Invoice(1000m, Line("INPS", 4m, 1000m, 40m, 0)), Token))
            .ShouldBeOfType<ItalyTemplateModel>();

        model.IsApplicable.ShouldBeTrue();
        model.IsForfettario.ShouldBeTrue();
        model.LegalNote.ShouldContain("190/2014");
        model.StampDuty.Applies.ShouldBeTrue();
        model.StampDuty.Amount.ShouldBe(2m);
        model.Contribution.Applies.ShouldBeTrue();
        model.Contribution.Amount.ShouldBe(40m);
        model.Withholding.Applies.ShouldBeFalse();
        model.NetToPay.ShouldBe(1040m);
    }

    [Test]
    public async Task Contribute_Ordinario_GivesTheRitenuta_AndNoBollo()
    {
        var model = (await Contributor(true, ItalyComplianceModule.Ordinario)
                .ContributeAsync(Invoice(1000m, Line("IVA", 22m, 1000m, 220m, 0), Line("RA", -20m, 1000m, -200m, 1)), Token))
            .ShouldBeOfType<ItalyTemplateModel>();

        model.IsForfettario.ShouldBeFalse();
        model.LegalNote.ShouldBeEmpty();
        model.StampDuty.Applies.ShouldBeFalse();
        model.Withholding.Amount.ShouldBe(200m);
        model.Withholding.Rate.ShouldBe(20m);
        model.NetToPay.ShouldBe(1020m);
    }

    [Test]
    public void EmptyAndSample_HaveEveryPart()
    {
        var contributor = Contributor(false, ItalyComplianceModule.Forfettario);

        contributor.Empty.ShouldBeOfType<ItalyTemplateModel>().StampDuty.Applies.ShouldBeFalse();
        contributor.Sample.ShouldBeOfType<ItalyTemplateModel>().StampDuty.Applies.ShouldBeTrue();
    }
}
