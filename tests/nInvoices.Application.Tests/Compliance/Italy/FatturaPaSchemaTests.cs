using System.Text;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Countries.Italy;
using nInvoices.Countries.Italy.FatturaPA;
using Shouldly;
using static nInvoices.Application.Tests.Compliance.Italy.FatturaPaTestData;
using nInvoices.Application.Tests.TestDoubles;

namespace nInvoices.Application.Tests.Compliance.Italy;

/// <summary>The FatturaPA files against the official FPR12 schema (1.2.3), the first check the SDI makes.</summary>
[TestFixture]
public sealed class FatturaPaSchemaTests
{
    private readonly FatturaPaFormat _format = new(new ItalyComplianceModule(TestText.Instance), TestText.Instance);

    private static IEnumerable<TestCaseData> Documents()
    {
        yield return new TestCaseData(Forfettario()).SetName("Forfettario with rivalsa and bollo");
        yield return new TestCaseData(Ordinario()).SetName("Ordinario with IVA, rivalsa and ritenuta");
        yield return new TestCaseData(Forfettario(GermanCompany())).SetName("Customer abroad");
        yield return new TestCaseData(Forfettario(ItalianCompany(new Dictionary<string, string> { [ItalyComplianceModule.PecKey] = "acme@pec.it" })))
            .SetName("Customer with a PEC");
    }

    [TestCaseSource(nameof(Documents))]
    public void Build_IsValidAgainstTheFpr12Schema(EInvoiceDocument document)
    {
        var artifact = _format.Build(document, null);

        SchemaErrors(artifact.Content).ShouldBeEmpty();
    }

    [Test]
    public void SchemaErrors_AFileWithoutTheRecipientCode_IsRefused()
    {
        var xml = Encoding.UTF8.GetString(_format.Build(Forfettario(), null).Content);
        var broken = xml.Replace("<CodiceDestinatario>ABC1234</CodiceDestinatario>", string.Empty, StringComparison.Ordinal);
        broken.ShouldNotBe(xml);

        SchemaErrors(Encoding.UTF8.GetBytes(broken)).ShouldNotBeEmpty();
    }
}
