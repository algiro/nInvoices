using System.Xml.Linq;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.ValueObjects;
using nInvoices.Countries.Italy;
using nInvoices.Countries.Italy.FatturaPA;
using Shouldly;
using static nInvoices.Application.Tests.Compliance.Italy.FatturaPaTestData;

namespace nInvoices.Application.Tests.Compliance.Italy;

[TestFixture]
public sealed class ItalianTaxIdTests
{
    [TestCase("12345678903", true)]
    [TestCase("IT12345678903", true)]
    [TestCase("123 456 789 03", true)]
    [TestCase("00743110157", true)]
    [TestCase("12345678901", false)]
    [TestCase("1234567890", false)]
    [TestCase("", false)]
    public void IsValidPartitaIva_ChecksTheCheckDigit(string taxId, bool valid) =>
        ItalianTaxId.IsValidPartitaIva(taxId).ShouldBe(valid);

    [TestCase("RSSMRA85T10A562S", true)]
    [TestCase("rssmra85t10a562s", true)]
    [TestCase("RSSMRA85T10A562T", false)]
    [TestCase("RSSMRA85T10A56", false)]
    [TestCase("12345678903", true)]
    [TestCase("12345678901", false)]
    public void IsValidCodiceFiscale_ChecksTheCheckCharacter(string code, bool valid) =>
        ItalianTaxId.IsValidCodiceFiscale(code).ShouldBe(valid);
}

[TestFixture]
public sealed class ItalyComplianceModuleTests
{
    private readonly ItalyComplianceModule _module = new();

    [Test]
    public void ValidateIssuer_ACompleteIndividual_IsAccepted() =>
        _module.ValidateIssuer(Issuer()).ShouldBeEmpty();

    [Test]
    public void ValidateIssuer_MissingRegimeAndBadVat_SaysSo()
    {
        var issuer = Issuer() with
        {
            TaxId = "12345678901",
            Values = new Dictionary<string, string>(Issuer().Values) { [ItalyComplianceModule.RegimeKey] = "" }
        };

        var fields = _module.ValidateIssuer(issuer).Select(i => i.Field).ToList();

        fields.ShouldContain("taxId");
        fields.ShouldContain(ItalyComplianceModule.RegimeKey);
    }

    [Test]
    public void ValidateIssuer_AnIndividualWithoutFiscalCode_IsRefused()
    {
        var values = new Dictionary<string, string>(Issuer().Values);
        values.Remove(ItalyComplianceModule.FiscalCodeKey);

        _module.ValidateIssuer(Issuer() with { Values = values })
            .ShouldContain(i => i.Field == ItalyComplianceModule.FiscalCodeKey);
    }

    [Test]
    public void ValidateIssuer_AddressWithoutProvinceCode_IsRefused()
    {
        var issuer = Issuer() with { Address = new Address("Via Roma", "10", "Milano", "2012", "Italy", "Milano") };

        _module.ValidateIssuer(issuer).Count(i => i.Field == "address").ShouldBe(2);
    }

    [Test]
    public void ValidateCustomer_ABadRecipientCode_IsRefused()
    {
        var customer = new CustomerProfile("Acme", "00743110157", ItalianCompany().Address,
            new Dictionary<string, string> { [ItalyComplianceModule.RecipientCodeKey] = "ABC" });

        _module.ValidateCustomer(customer).ShouldHaveSingleItem().Field.ShouldBe(ItalyComplianceModule.RecipientCodeKey);
    }
}

[TestFixture]
public sealed class ItalianInvoiceTaxesTests
{
    private static readonly IReadOnlyDictionary<string, string> Forfettario = Issuer().Values;

    [TestCase(77.47, false)]
    [TestCase(77.48, true)]
    public void StampDuty_IsDueAboveTheThreshold_WithoutIva(decimal lines, bool due) =>
        ItalianInvoiceTaxes.Of([], lines, Forfettario).StampDutyApplies.ShouldBe(due);

    [Test]
    public void StampDuty_IsNotDueWhenIvaIsCharged() =>
        ItalianInvoiceTaxes.Of([Iva(1000m)], 1000m, Issuer(ItalyComplianceModule.Ordinario).Values).StampDutyApplies.ShouldBeFalse();

    [Test]
    public void StampDuty_IsNotDueOnAnIntraEuSupply()
    {
        var zeroIva = new EInvoiceTax(EInvoiceTaxKind.Added, "IVA 0%", 0m, 1000m, 0m,
            new Dictionary<string, string> { [ItalyComplianceModule.NatureKey] = "N3.2" });

        var taxes = ItalianInvoiceTaxes.Of([zeroIva], 1000m, Issuer(ItalyComplianceModule.Ordinario).Values);

        taxes.Nature.ShouldBe("N3.2");
        taxes.StampDutyApplies.ShouldBeFalse();
    }

    [Test]
    public void Of_Forfettario_IsN22_AndTheContributionIsPartOfTheTaxableAmount()
    {
        var taxes = ItalianInvoiceTaxes.Of([Rivalsa()], 1000m, Forfettario);

        taxes.Iva.ShouldBeNull();
        taxes.Nature.ShouldBe(ItalyComplianceModule.NotSubjectOther);
        taxes.Contributions.ShouldHaveSingleItem();
        taxes.TaxableAmount.ShouldBe(1040m);
    }
}

[TestFixture]
public sealed class FatturaPaFormatTests
{
    private readonly FatturaPaFormat _format = new(new ItalyComplianceModule());

    private XElement Build(EInvoiceDocument document)
    {
        _format.Validate(document).ShouldBeEmpty();
        var artifact = _format.Build(document, null);
        artifact.ContentType.ShouldBe("application/xml");
        artifact.FileExtension.ShouldBe("xml");
        return XDocument.Parse(System.Text.Encoding.UTF8.GetString(artifact.Content)).Root!;
    }

    private static string Value(XElement root, string path) =>
        path.Split('/').Aggregate(root, (e, name) => e.Element(name) ?? throw new InvalidOperationException($"{name} missing in {e.Name}")).Value;

    [Test]
    public void Build_Forfettario_WritesTheRegime_TheBollo_TheRivalsa_AndN22()
    {
        var root = Build(Forfettario());

        root.Name.ShouldBe(FatturaPaXmlBuilder.Ns + "FatturaElettronica");
        root.Attribute("versione")!.Value.ShouldBe("FPR12");
        Value(root, "FatturaElettronicaHeader/DatiTrasmissione/CodiceDestinatario").ShouldBe("ABC1234");
        Value(root, "FatturaElettronicaHeader/CedentePrestatore/DatiAnagrafici/RegimeFiscale").ShouldBe("RF19");
        Value(root, "FatturaElettronicaHeader/CedentePrestatore/DatiAnagrafici/Anagrafica/Cognome").ShouldBe("Rossi");
        Value(root, "FatturaElettronicaHeader/CessionarioCommittente/DatiAnagrafici/IdFiscaleIVA/IdCodice").ShouldBe("00743110157");

        const string general = "FatturaElettronicaBody/DatiGenerali/DatiGeneraliDocumento";
        Value(root, $"{general}/DatiBollo/ImportoBollo").ShouldBe("2.00");
        Value(root, $"{general}/DatiCassaPrevidenziale/TipoCassa").ShouldBe("TC22");
        Value(root, $"{general}/DatiCassaPrevidenziale/ImportoContributoCassa").ShouldBe("40.00");
        Value(root, $"{general}/DatiCassaPrevidenziale/Natura").ShouldBe("N2.2");
        Value(root, $"{general}/ImportoTotaleDocumento").ShouldBe("1040.00");

        Value(root, "FatturaElettronicaBody/DatiBeniServizi/DettaglioLinee/AliquotaIVA").ShouldBe("0.00");
        Value(root, "FatturaElettronicaBody/DatiBeniServizi/DettaglioLinee/Natura").ShouldBe("N2.2");
        Value(root, "FatturaElettronicaBody/DatiBeniServizi/DatiRiepilogo/ImponibileImporto").ShouldBe("1040.00");
        Value(root, "FatturaElettronicaBody/DatiBeniServizi/DatiRiepilogo/RiferimentoNormativo").ShouldBe(FatturaPaXmlBuilder.ForfettarioReference);
        Value(root, "FatturaElettronicaBody/DatiPagamento/DettaglioPagamento/DataScadenzaPagamento").ShouldBe("2026-11-15");
    }

    [Test]
    public void Build_Ordinario_WritesIvaOnFeesPlusRivalsa_AndTheRitenuta()
    {
        var root = Build(Ordinario());
        const string general = "FatturaElettronicaBody/DatiGenerali/DatiGeneraliDocumento";

        Value(root, $"{general}/DatiRitenuta/TipoRitenuta").ShouldBe("RT01");
        Value(root, $"{general}/DatiRitenuta/ImportoRitenuta").ShouldBe("208.00");
        Value(root, $"{general}/DatiRitenuta/CausalePagamento").ShouldBe("A");
        Value(root, $"{general}/DatiCassaPrevidenziale/AliquotaIVA").ShouldBe("22.00");
        Value(root, $"{general}/DatiCassaPrevidenziale/Ritenuta").ShouldBe("SI");
        root.Descendants("DatiBollo").ShouldBeEmpty();
        // The document's total includes IVA and is before the ritenuta; the payment is net of it
        Value(root, $"{general}/ImportoTotaleDocumento").ShouldBe("1268.80");
        Value(root, $"{general}/Causale").ShouldBe("Pagamento a 30 giorni");
        Value(root, "FatturaElettronicaBody/DatiBeniServizi/DettaglioLinee/Ritenuta").ShouldBe("SI");
        Value(root, "FatturaElettronicaBody/DatiBeniServizi/DatiRiepilogo/Imposta").ShouldBe("228.80");
        Value(root, "FatturaElettronicaBody/DatiBeniServizi/DatiRiepilogo/EsigibilitaIVA").ShouldBe("I");
        Value(root, "FatturaElettronicaBody/DatiPagamento/DettaglioPagamento/ImportoPagamento").ShouldBe("1060.80");
    }

    [Test]
    public void Build_ACustomerAbroad_IsReachedOutsideTheSdi()
    {
        var root = Build(Forfettario(GermanCompany()));

        Value(root, "FatturaElettronicaHeader/DatiTrasmissione/CodiceDestinatario").ShouldBe(FatturaPaXmlBuilder.ForeignRecipient);
        Value(root, "FatturaElettronicaHeader/CessionarioCommittente/DatiAnagrafici/IdFiscaleIVA/IdPaese").ShouldBe("DE");
        Value(root, "FatturaElettronicaHeader/CessionarioCommittente/DatiAnagrafici/IdFiscaleIVA/IdCodice").ShouldBe("123456789");
        Value(root, "FatturaElettronicaHeader/CessionarioCommittente/Sede/CAP").ShouldBe("00000");
        root.Descendants("Provincia").Count().ShouldBe(1); // the issuer's only
    }

    [Test]
    public void Build_ACustomerWithAPec_GetsTheZeroCodeAndThePec()
    {
        var buyer = ItalianCompany(new Dictionary<string, string> { [ItalyComplianceModule.PecKey] = "acme@pec.it" });

        var root = Build(Forfettario(buyer));

        Value(root, "FatturaElettronicaHeader/DatiTrasmissione/CodiceDestinatario").ShouldBe(FatturaPaXmlBuilder.NoRecipientCode);
        Value(root, "FatturaElettronicaHeader/DatiTrasmissione/PECDestinatario").ShouldBe("acme@pec.it");
    }

    [Test]
    public void Validate_IvaNotOnTheRivalsa_AsksToMakeItPartOfTheTaxableAmount()
    {
        var document = Ordinario() with { Taxes = [Rivalsa(), Iva(1000m), Ritenuta(1040m)], Total = 1000m + 40m + 220m - 208m };

        _format.Validate(document).ShouldContain(i => i.Message.Contains("Part of the taxable amount"));
    }

    [Test]
    public void Validate_OrdinarioWithoutIva_AsksForIt()
    {
        var document = Ordinario() with { Taxes = [], Total = 1000m };

        _format.Validate(document).ShouldContain(i => i.Message.Contains("needs its IVA"));
    }

    [Test]
    public void Validate_TwoIvaTaxes_IsRefused()
    {
        var document = Ordinario() with { Taxes = [Iva(1000m), Iva(1000m)], Total = 1440m };

        _format.Validate(document).ShouldContain(i => i.Message.Contains("one IVA rate"));
    }

    [Test]
    public void Validate_AnItalianCustomerWithoutTaxId_IsRefused()
    {
        var buyer = ItalianCompany() with { TaxId = "" };

        _format.Validate(Forfettario(buyer)).ShouldContain(i => i.Message.Contains("Partita IVA"));
    }

    [Test]
    public void IsMandatoryFor_EveryCustomer() =>
        _format.IsMandatoryFor(GermanCompany()).ShouldBeTrue();
}
