using System.Text;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Countries.Germany;
using nInvoices.Countries.Germany.XRechnung;
using Shouldly;
using static nInvoices.Application.Tests.Compliance.Germany.XRechnungTestData;
using nInvoices.Application.Tests.TestDoubles;

namespace nInvoices.Application.Tests.Compliance.Germany;

/// <summary>
/// The XRechnung files against what a recipient checks: the UBL 2.1 schema, always; the EN 16931 and XRechnung
/// 3.0 Schematron rules through KoSIT's validator, when Java and the validator jar are there (else skipped).
/// </summary>
[TestFixture]
public sealed class XRechnungValidationTests
{
    private static readonly XRechnungFormat Format = new(new GermanyComplianceModule(), TestText.Instance);

    private static readonly Dictionary<string, EInvoiceDocument> Documents = new()
    {
        ["standard-19"] = Invoice(Issuer(), GermanCustomer(), [Vat(19m, 1000m)]),
        ["kleinunternehmer"] = Invoice(Issuer(smallBusiness: true), GermanCustomer(), []),
        // Without a USt-IdNr, the Steuernummer identifies the seller (BR-CO-26)
        ["kleinunternehmer-without-ust-idnr"] = Invoice(WithoutVatId(Issuer(smallBusiness: true)), GermanCustomer(), []),
        ["reverse-charge-ae"] = Invoice(Issuer(), FrenchCustomer(), [ZeroVat(GermanyComplianceModule.ReverseCharge)]),
        ["not-subject-o"] = Invoice(Issuer(), FrenchCustomer(), [ZeroVat(GermanyComplianceModule.NotSubject)])
    };

    private static EInvoiceTax ZeroVat(string category) =>
        Vat(0m, 1000m, new Dictionary<string, string> { [GermanyComplianceModule.CategoryKey] = category });

    private static IssuerProfile WithoutVatId(IssuerProfile issuer) =>
        issuer with { Values = new Dictionary<string, string>(issuer.Values) { [GermanyComplianceModule.VatIdKey] = "" } };

    private static IEnumerable<string> DocumentNames() => Documents.Keys;

    private static byte[] Build(string name)
    {
        Format.Validate(Documents[name]).ShouldBeEmpty();
        return Format.Build(Documents[name], null).Content;
    }

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [TestCaseSource(nameof(DocumentNames))]
    public void Build_IsValidAgainstTheUbl21Schema(string name) =>
        XRechnungValidation.SchemaErrors(Build(name)).ShouldBeEmpty();

    [Test]
    public void SchemaErrors_ElementsOutOfOrder_AreRefused()
    {
        var xml = Encoding.UTF8.GetString(Build("standard-19"));
        var currency = "<cbc:DocumentCurrencyCode>EUR</cbc:DocumentCurrencyCode>";
        var broken = xml.Replace(currency, string.Empty, StringComparison.Ordinal)
            .Replace("<cbc:InvoiceTypeCode>", currency + "<cbc:InvoiceTypeCode>", StringComparison.Ordinal);
        broken.ShouldNotBe(xml);

        XRechnungValidation.SchemaErrors(Encoding.UTF8.GetBytes(broken)).ShouldNotBeEmpty();
    }

    [Test]
    public async Task Build_PassesTheKositValidator_EN16931AndXRechnungRules()
    {
        var jar = Validator();
        var invoices = Documents.Keys.ToDictionary(n => n, Build);
        // A file breaking an XRechnung rule (BR-DE-15, the buyer reference) shows the rules really run
        invoices["broken"] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(invoices["standard-19"])
            .Replace("<cbc:BuyerReference>2026-042</cbc:BuyerReference>", string.Empty, StringComparison.Ordinal));

        var results = await XRechnungValidation.SchematronErrorsAsync(jar, invoices, Token);

        results["broken"].ShouldContain(e => e.Contains("BR-DE-15"));
        results.Remove("broken");
        results.Where(r => r.Value.Count > 0)
            .Select(r => $"{r.Key}:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", r.Value)}")
            .ShouldBeEmpty();
    }

    private static string Validator()
    {
        var jar = XRechnungValidation.ValidatorJar(out var why);
        if (jar is not null)
            return jar;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(XRechnungValidation.RequireValidatorEnvironmentVariable)))
            Assert.Fail(why);
        Assert.Ignore(why);
        return string.Empty;
    }
}
