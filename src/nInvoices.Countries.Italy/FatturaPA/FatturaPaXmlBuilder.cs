using System.Globalization;
using System.Xml.Linq;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Countries.Italy.FatturaPA;

/// <summary>
/// Writes a FatturaPA 1.2 (FPR12) document, following the Agenzia delle Entrate technical specifications: the
/// root element is in the FatturaPA namespace, everything inside it unqualified.
/// </summary>
public static class FatturaPaXmlBuilder
{
    public static readonly XNamespace Ns = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";

    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    /// <summary>The code of a recipient abroad, who receives the invoice outside the SDI.</summary>
    public const string ForeignRecipient = "XXXXXXX";

    /// <summary>The code for a recipient reached by PEC, or a consumer who finds it in their tax area.</summary>
    public const string NoRecipientCode = "0000000";

    /// <summary>The tax id the SDI accepts for a customer abroad without one (a consumer).</summary>
    private const string ForeignWithoutTaxId = "99999999999";

    /// <summary>What the summary of a forfettario invoice cites as the reason it charges no IVA.</summary>
    public const string ForfettarioReference = "Operazione in franchigia da IVA, art. 1 commi 54-89 L. 190/2014";

    public static XDocument Build(EInvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var linesAmount = document.Lines.Sum(l => Round2(l.Amount));
        var taxes = ItalianInvoiceTaxes.Of(document.Taxes, linesAmount, document.Issuer.Values);

        var root = new XElement(Ns + "FatturaElettronica",
            new XAttribute("versione", "FPR12"),
            new XAttribute(XNamespace.Xmlns + "p", Ns),
            new XAttribute(XNamespace.Xmlns + "ds", Ds),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
            Header(document),
            Body(document, taxes));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    /// <summary>The buyer's codice fiscale: given on the customer, or their tax id when it is one.</summary>
    public static string? BuyerFiscalCode(EInvoiceParty buyer)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        if (buyer.Values.TryGetValue(ItalyComplianceModule.FiscalCodeKey, out var code) && !string.IsNullOrWhiteSpace(code))
            return code.Trim().ToUpperInvariant();
        var taxId = ItalianTaxId.Normalize(buyer.TaxId);
        return taxId.Length == 16 ? taxId : null;
    }

    private static XElement Header(EInvoiceDocument document)
    {
        var issuer = document.Issuer;
        var values = issuer.Values;
        var vat = ItalianTaxId.Normalize(issuer.TaxId);
        var fiscalCode = values.GetValueOrDefault(ItalyComplianceModule.FiscalCodeKey)?.Trim().ToUpperInvariant();
        var buyer = document.Buyer;
        var buyerCountry = CountryCodes.FromName(buyer.Address.Country) ?? ItalyComplianceModule.CountryCodeValue;
        var isItalianBuyer = buyerCountry == ItalyComplianceModule.CountryCodeValue;

        var recipientCode = buyer.Values.GetValueOrDefault(ItalyComplianceModule.RecipientCodeKey)?.Trim().ToUpperInvariant();
        var pec = buyer.Values.GetValueOrDefault(ItalyComplianceModule.PecKey)?.Trim();
        var code = !isItalianBuyer ? ForeignRecipient : string.IsNullOrEmpty(recipientCode) ? NoRecipientCode : recipientCode;

        var transmission = new XElement("DatiTrasmissione",
            new XElement("IdTrasmittente",
                new XElement("IdPaese", ItalyComplianceModule.CountryCodeValue),
                new XElement("IdCodice", string.IsNullOrEmpty(fiscalCode) ? vat : fiscalCode)),
            new XElement("ProgressivoInvio", Progressive(document.Number)),
            new XElement("FormatoTrasmissione", "FPR12"),
            new XElement("CodiceDestinatario", code),
            code == NoRecipientCode && !string.IsNullOrEmpty(pec) ? new XElement("PECDestinatario", pec) : null);

        var isIndividual = values.GetValueOrDefault(ItalyComplianceModule.PersonTypeKey) == ItalyComplianceModule.Individual;
        var supplier = new XElement("CedentePrestatore",
            new XElement("DatiAnagrafici",
                new XElement("IdFiscaleIVA",
                    new XElement("IdPaese", ItalyComplianceModule.CountryCodeValue),
                    new XElement("IdCodice", vat)),
                string.IsNullOrEmpty(fiscalCode) ? null : new XElement("CodiceFiscale", fiscalCode),
                new XElement("Anagrafica", isIndividual
                    ?
                    [
                        new XElement("Nome", Cut(values.GetValueOrDefault(ItalyComplianceModule.FirstNameKey), 60)),
                        new XElement("Cognome", Cut(values.GetValueOrDefault(ItalyComplianceModule.LastNameKey), 60))
                    ]
                    : new object[] { new XElement("Denominazione", Cut(issuer.LegalName, 80)) }),
                new XElement("RegimeFiscale", values.GetValueOrDefault(ItalyComplianceModule.RegimeKey))),
            Sede(issuer.Address!, ItalyComplianceModule.CountryCodeValue));

        var buyerVat = isItalianBuyer ? ItalianTaxId.Normalize(buyer.TaxId) : ForeignTaxId(buyer.TaxId, buyerCountry);
        var hasItalianVat = isItalianBuyer && ItalianTaxId.IsValidPartitaIva(buyerVat);
        var buyerFiscalCode = isItalianBuyer ? BuyerFiscalCode(buyer) : null;

        var customer = new XElement("CessionarioCommittente",
            new XElement("DatiAnagrafici",
                hasItalianVat || !isItalianBuyer
                    ? new XElement("IdFiscaleIVA",
                        new XElement("IdPaese", buyerCountry),
                        new XElement("IdCodice", buyerVat))
                    : null,
                buyerFiscalCode is null ? null : new XElement("CodiceFiscale", buyerFiscalCode),
                new XElement("Anagrafica", new XElement("Denominazione", Cut(buyer.Name, 80)))),
            Sede(buyer.Address, buyerCountry));

        return new XElement("FatturaElettronicaHeader", transmission, supplier, customer);
    }

    private static XElement Body(EInvoiceDocument document, ItalianInvoiceTaxes taxes)
    {
        var withheld = taxes.Withholdings.Sum(w => w.Amount);
        var hasWithholding = taxes.Withholdings.Count > 0;
        var ivaRate = taxes.IvaRate;
        var nature = ivaRate == 0 ? taxes.Nature : null;
        var linesAmount = document.Lines.Sum(l => Round2(l.Amount));

        var general = new XElement("DatiGeneraliDocumento",
            new XElement("TipoDocumento", "TD01"),
            new XElement("Divisa", document.Currency),
            new XElement("Data", Date(document.IssueDate)),
            new XElement("Numero", document.Number),
            taxes.Withholdings.Select(w => new XElement("DatiRitenuta",
                new XElement("TipoRitenuta", w.Values?.GetValueOrDefault(ItalyComplianceModule.WithholdingTypeKey) ?? "RT01"),
                new XElement("ImportoRitenuta", Amount(w.Amount)),
                new XElement("AliquotaRitenuta", Amount(w.Rate)),
                new XElement("CausalePagamento", w.Values?.GetValueOrDefault(ItalyComplianceModule.PaymentReasonKey) ?? "A"))),
            taxes.StampDutyApplies
                ? new XElement("DatiBollo",
                    new XElement("BolloVirtuale", "SI"),
                    new XElement("ImportoBollo", Amount(ItalianInvoiceTaxes.StampDuty)))
                : null,
            taxes.Contributions.Select(c => new XElement("DatiCassaPrevidenziale",
                new XElement("TipoCassa", c.Values?.GetValueOrDefault(ItalyComplianceModule.FundTypeKey) ?? ItalyComplianceModule.InpsFund),
                new XElement("AlCassa", Amount(c.Rate)),
                new XElement("ImportoContributoCassa", Amount(c.Amount)),
                new XElement("ImponibileCassa", Amount(c.TaxableBase)),
                new XElement("AliquotaIVA", Amount(ivaRate)),
                // The contribution is subject to the ritenuta when the ritenuta was calculated on it too
                taxes.Withholdings.Any(w => w.TaxableBase > linesAmount + 0.005m) ? new XElement("Ritenuta", "SI") : null,
                nature is null ? null : new XElement("Natura", nature))),
            new XElement("ImportoTotaleDocumento", Amount(document.Total + withheld)),
            Reasons(document.Notes));

        var lines = document.Lines.Select((line, index) => new XElement("DettaglioLinee",
            new XElement("NumeroLinea", index + 1),
            new XElement("Descrizione", Cut(line.Description, 1000)),
            new XElement("Quantita", Precise(line.Quantity)),
            new XElement("PrezzoUnitario", Precise(line.UnitPrice)),
            new XElement("PrezzoTotale", Amount(line.Amount)),
            new XElement("AliquotaIVA", Amount(ivaRate)),
            hasWithholding ? new XElement("Ritenuta", "SI") : null,
            nature is null ? null : new XElement("Natura", nature)));

        var summary = new XElement("DatiRiepilogo",
            new XElement("AliquotaIVA", Amount(ivaRate)),
            nature is null ? null : new XElement("Natura", nature),
            new XElement("ImponibileImporto", Amount(taxes.TaxableAmount)),
            new XElement("Imposta", Amount(taxes.Iva?.Amount ?? 0m)),
            taxes.ChargesIva ? new XElement("EsigibilitaIVA", "I") : null,
            nature == ItalyComplianceModule.NotSubjectOther ? new XElement("RiferimentoNormativo", ForfettarioReference) : null);

        var payment = new XElement("DatiPagamento",
            new XElement("CondizioniPagamento", "TP02"),
            new XElement("DettaglioPagamento",
                new XElement("ModalitaPagamento", "MP05"),
                document.DueDate is { } due ? new XElement("DataScadenzaPagamento", Date(due)) : null,
                new XElement("ImportoPagamento", Amount(document.Total))));

        return new XElement("FatturaElettronicaBody",
            new XElement("DatiGenerali", general),
            new XElement("DatiBeniServizi", lines, summary),
            payment);
    }

    private static XElement Sede(Address address, string country)
    {
        var isItaly = country == ItalyComplianceModule.CountryCodeValue;
        return new XElement("Sede",
            new XElement("Indirizzo", Cut(address.Street, 60)),
            string.IsNullOrWhiteSpace(address.HouseNumber) ? null : new XElement("NumeroCivico", Cut(address.HouseNumber, 8)),
            // Abroad the CAP is "00000": the SDI only checks Italian ones
            new XElement("CAP", isItaly ? address.ZipCode.Trim() : "00000"),
            new XElement("Comune", Cut(address.City, 60)),
            isItaly && !string.IsNullOrWhiteSpace(address.State) ? new XElement("Provincia", address.State.Trim().ToUpperInvariant()) : null,
            new XElement("Nazione", country));
    }

    private static IEnumerable<XElement> Reasons(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            yield break;
        var text = notes.Trim();
        for (var i = 0; i < text.Length; i += 200)
            yield return new XElement("Causale", text.Substring(i, Math.Min(200, text.Length - i)));
    }

    // A foreign VAT number without its country prefix ("DE123..." is IdPaese DE, IdCodice 123...)
    private static string ForeignTaxId(string taxId, string country)
    {
        var id = new string(taxId.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        if (id.StartsWith(country, StringComparison.Ordinal))
            id = id[country.Length..];
        return id.Length == 0 ? ForeignWithoutTaxId : Cut(id, 28);
    }

    // [A-Za-z0-9]{1,10}, unique per transmitter: the invoice number's letters and digits, the last ten
    private static string Progressive(string number)
    {
        var id = new string(number.Where(char.IsAsciiLetterOrDigit).ToArray());
        return id.Length == 0 ? "1" : id[Math.Max(0, id.Length - 10)..];
    }

    private static string Cut(string? text, int max)
    {
        var value = (text ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }

    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Amount(decimal value) => Round2(value).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Precise(decimal value) => value.ToString("0.00######", CultureInfo.InvariantCulture);

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
