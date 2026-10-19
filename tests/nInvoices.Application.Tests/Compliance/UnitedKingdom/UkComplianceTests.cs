using Moq;
using nInvoices.Application.Compliance;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;
using nInvoices.Core.ValueObjects;
using nInvoices.Countries.UnitedKingdom;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance.UnitedKingdom;

[TestFixture]
public sealed class UkTaxIdTests
{
    [TestCase("GB980780684", true)]
    [TestCase("GB 980 7806 84", true)]
    [TestCase("123456782", true)]
    [TestCase("123456727", true)]
    [TestCase("980780684001", true)]
    [TestCase("GB980780685", false)]
    [TestCase("GB98078068", false)]
    public void IsValidVatNumber_ChecksBothSchemes(string vatNumber, bool valid) =>
        UkTaxId.IsValidVatNumber(vatNumber).ShouldBe(valid);

    [TestCase("1234567890", true)]
    [TestCase("12345 67890", true)]
    [TestCase("123456789", false)]
    public void IsPlausibleUtr_IsTenDigits(string utr, bool plausible) => UkTaxId.IsPlausibleUtr(utr).ShouldBe(plausible);

    [TestCase("SW1A 1AA", true)]
    [TestCase("ec1a1bb", true)]
    [TestCase("M1 1AE", true)]
    [TestCase("GIR 0AA", true)]
    [TestCase("12345", false)]
    [TestCase("SW1A 1A", false)]
    public void IsValidPostcode_FollowsTheUkFormat(string postcode, bool valid) => UkTaxId.IsValidPostcode(postcode).ShouldBe(valid);
}

[TestFixture]
public sealed class UkComplianceModuleTests
{
    private readonly UkComplianceModule _module = new(TestText.Instance);

    private static IssuerProfile Issuer(bool registered, string taxId) => new(
        "Jane Smith",
        taxId,
        new Address("High Street", "1", "London", "SW1A 1AA", "United Kingdom"),
        new Dictionary<string, string> { [UkComplianceModule.VatRegisteredKey] = registered ? "true" : "" });

    [Test]
    public void ValidateIssuer_VatRegisteredWithItsNumber_IsAccepted() =>
        _module.ValidateIssuer(Issuer(true, "GB980780684")).ShouldBeEmpty();

    [Test]
    public void ValidateIssuer_NotRegisteredWithAUtr_IsAccepted() =>
        _module.ValidateIssuer(Issuer(false, "1234567890")).ShouldBeEmpty();

    [Test]
    public void ValidateIssuer_RegisteredWithAUtr_AsksForTheVatNumber() =>
        _module.ValidateIssuer(Issuer(true, "1234567890")).ShouldHaveSingleItem().Field.ShouldBe("taxId");

    [Test]
    public void ValidateIssuer_BadPostcode_IsRefused()
    {
        var issuer = Issuer(false, "1234567890") with { Address = new Address("High Street", "1", "London", "12345", "United Kingdom") };

        _module.ValidateIssuer(issuer).ShouldHaveSingleItem().Field.ShouldBe("address");
    }

    [Test]
    public void Fields_AreTranslated()
    {
        _module.Fields.Concat(_module.TaxFields).ShouldAllBe(f => !f.Label.StartsWith("compliance.", StringComparison.Ordinal));
        _module.DisplayName.ShouldBe("United Kingdom");
    }
}

[TestFixture]
public sealed class UkInvoiceRulesTests
{
    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static (Mock<IComplianceGate> Gate, InMemoryRepository<Tax> Taxes) Setup(bool registered, string? treatment = null)
    {
        var settings = new ComplianceSettings(UkComplianceModule.CountryCodeValue);
        settings.Update(true, "Jane Smith", registered ? "GB980780684" : "1234567890", null,
            new Dictionary<string, string> { [UkComplianceModule.VatRegisteredKey] = registered ? "true" : "", [UkComplianceModule.TradingNameKey] = "Smith Consulting" });
        var gate = new Mock<IComplianceGate>();
        gate.Setup(g => g.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActiveCompliance(new UkComplianceModule(TestText.Instance), settings)]);

        var taxes = new InMemoryRepository<Tax>();
        var tax = new Tax(1, "VAT", "VAT", "PERCENTAGE", 0m, TaxApplicationType.OnSubtotal);
        if (treatment is not null)
            tax.SetComplianceValues(UkComplianceModule.CountryCodeValue, new Dictionary<string, string> { [UkComplianceModule.TreatmentKey] = treatment });
        taxes.AddAsync(tax).GetAwaiter().GetResult();
        return (gate, taxes);
    }

    private static Invoice Invoice(decimal rate)
    {
        var invoice = new Invoice(1, new InvoiceNumber("INV-1"), InvoiceType.OneTime, new DateOnly(2026, 10, 15), new Money(1000m, "GBP"), "GBP");
        invoice.TaxLines.Add(new InvoiceTaxLine("VAT", "VAT", rate, new Money(1000m, "GBP"), new Money(1000m * rate / 100m, "GBP"), 0));
        invoice.AddTaxes(new Money(1000m * rate / 100m, "GBP"));
        return invoice;
    }

    private static Customer Customer() =>
        new("Acme Ltd", "GB123456782", new Address("King Street", "2", "Manchester", "M1 1AE", "United Kingdom"));

    [Test]
    public async Task Finalizing_NotRegisteredButChargingVat_IsRefused()
    {
        var (gate, taxes) = Setup(registered: false);

        await Should.ThrowAsync<DomainException>(
            new UkVatInvoiceStep(gate.Object, taxes, TestText.Instance).OnFinalizingAsync(Invoice(20m), Customer(), Token));
    }

    [Test]
    public async Task Finalizing_ZeroVatWithoutAReason_IsRefused_AndWithOne_IsAccepted()
    {
        var (gate, taxes) = Setup(registered: true);
        await Should.ThrowAsync<DomainException>(
            new UkVatInvoiceStep(gate.Object, taxes, TestText.Instance).OnFinalizingAsync(Invoice(0m), Customer(), Token));

        var (gate2, taxes2) = Setup(registered: true, UkComplianceModule.OutsideScope);
        (await new UkVatInvoiceStep(gate2.Object, taxes2, TestText.Instance).OnFinalizingAsync(Invoice(0m), Customer(), Token)).ShouldBeFalse();
    }

    [Test]
    public async Task Template_RegisteredOutsideTheScope_GivesTheNumberAndTheNote()
    {
        var (gate, taxes) = Setup(registered: true, UkComplianceModule.OutsideScope);

        var model = (await new UkTemplateContributor(gate.Object, taxes).ContributeAsync(Invoice(0m), Token)).ShouldBeOfType<UkTemplateModel>();

        model.IsVatRegistered.ShouldBeTrue();
        model.VatNumber.ShouldBe("GB 980 7806 84");
        model.TradingName.ShouldBe("Smith Consulting");
        model.HasVatNote.ShouldBeTrue();
        model.VatNote.ShouldBe("Outside the scope of UK VAT.");
    }

    [Test]
    public async Task Template_StandardRatedInvoice_HasNoNote()
    {
        var (gate, taxes) = Setup(registered: true);

        var model = (await new UkTemplateContributor(gate.Object, taxes).ContributeAsync(Invoice(20m), Token)).ShouldBeOfType<UkTemplateModel>();

        model.HasVatNote.ShouldBeFalse();
    }
}
