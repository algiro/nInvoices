using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.ValueObjects;
using nInvoices.Countries.Italy;

namespace nInvoices.Application.Tests.Compliance.Italy;

internal static class FatturaPaTestData
{
    public const string IssuerVat = "12345678903";
    public const string IssuerFiscalCode = "RSSMRA85T10A562S";

    public static IssuerProfile Issuer(string regime = ItalyComplianceModule.Forfettario) => new(
        "Mario Rossi",
        IssuerVat,
        new Address("Via Roma", "10", "Milano", "20121", "Italy", "MI"),
        new Dictionary<string, string>
        {
            [ItalyComplianceModule.RegimeKey] = regime,
            [ItalyComplianceModule.PersonTypeKey] = ItalyComplianceModule.Individual,
            [ItalyComplianceModule.FirstNameKey] = "Mario",
            [ItalyComplianceModule.LastNameKey] = "Rossi",
            [ItalyComplianceModule.FiscalCodeKey] = IssuerFiscalCode
        });

    public static EInvoiceParty ItalianCompany(IReadOnlyDictionary<string, string>? values = null) => new(
        "Acme Srl",
        "IT00743110157",
        new Address("Corso Italia", "5", "Torino", "10121", "Italia", "TO"),
        values ?? new Dictionary<string, string> { [ItalyComplianceModule.RecipientCodeKey] = "ABC1234" });

    public static EInvoiceParty GermanCompany() => new(
        "Beispiel GmbH",
        "DE123456789",
        new Address("Hauptstraße", "1", "Berlin", "10115", "Germany", null),
        new Dictionary<string, string>());

    public static readonly IReadOnlyDictionary<string, string> RivalsaValues =
        new Dictionary<string, string> { [ItalyComplianceModule.TaxKindKey] = ItalyComplianceModule.Contribution };

    public static EInvoiceTax Rivalsa(decimal lines = 1000m) =>
        new(EInvoiceTaxKind.Added, "Rivalsa INPS 4%", 4m, lines, lines * 0.04m, RivalsaValues);

    public static EInvoiceTax Iva(decimal taxableBase) =>
        new(EInvoiceTaxKind.Added, "IVA 22%", 22m, taxableBase, Math.Round(taxableBase * 0.22m, 2));

    public static EInvoiceTax Ritenuta(decimal taxableBase) =>
        new(EInvoiceTaxKind.Withheld, "Ritenuta d'acconto 20%", 20m, taxableBase, Math.Round(taxableBase * 0.2m, 2));

    /// <summary>A forfettario invoice: 1,000 of fees and the rivalsa INPS, no IVA.</summary>
    public static EInvoiceDocument Forfettario(EInvoiceParty? buyer = null) => new(
        "2026/12",
        new DateOnly(2026, 10, 15),
        new DateOnly(2026, 11, 15),
        "EUR",
        Issuer(),
        buyer ?? ItalianCompany(),
        [new EInvoiceLine("Consulenza ottobre", 10m, 100m, 1000m)],
        [Rivalsa()],
        1040m,
        null,
        "it");

    /// <summary>A regime ordinario invoice: fees, rivalsa INPS, IVA and ritenuta on fees + rivalsa.</summary>
    public static EInvoiceDocument Ordinario() => new(
        "2026/13",
        new DateOnly(2026, 10, 15),
        null,
        "EUR",
        Issuer(ItalyComplianceModule.Ordinario),
        ItalianCompany(),
        [new EInvoiceLine("Consulenza ottobre", 10m, 100m, 1000m)],
        [Rivalsa(), Iva(1040m), Ritenuta(1040m)],
        1000m + 40m + 228.80m - 208m,
        "Pagamento a 30 giorni",
        "it");
}
