using System.Reflection;
using nInvoices.Application.Compliance.Spain.Verifactu;
using nInvoices.Core.Entities;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance.Spain;

[TestFixture]
public sealed class VerifactuChainTests
{
    private static readonly VerifactuSystem System = new("Producer SL", "B12345674", "nInvoices", "NI", "1.0.0", "1", false);

    /// <summary>A chain of issued records built the way the service builds them.</summary>
    private static List<VerifactuRecord> Chain(int length)
    {
        var chain = new List<VerifactuRecord>();
        var previous = "";
        VerifactuRecord? before = null;

        for (var i = 1; i <= length; i++)
        {
            var number = $"26-10-{i:000}";
            var stamp = $"2026-10-03T11:{i:00}:00+02:00";
            var hash = VerifactuHash.Issued("12345678Z", number, "03-10-2026", "F1", "21.00", "121.00", previous, stamp);
            var data = new VerifactuInvoiceData(
                "12345678Z", "Ana", number, new DateOnly(2026, 10, 3), "F1", "Servicios",
                new VerifactuRecipient("Cliente", "A58818501", null, null, null),
                [new VerifactuBreakdown("S1", 21m, 100m, 21m)], 21m, 121m);
            var xml = VerifactuXml.Issued(
                data, System,
                before is null ? null : new VerifactuPrevious(before.IssuerTaxId, before.InvoiceNumber, before.IssueDate, before.Hash),
                stamp, hash).ToString(System_Xml.None);

            before = new VerifactuRecord(i, VerifactuRecordKind.Issued, i, "12345678Z", number, "03-10-2026", "F1", "21.00", "121.00", previous, stamp, hash, xml);
            chain.Add(before);
            previous = hash;
        }

        return chain;
    }

    // System.Xml.Linq.SaveOptions without a using that clashes with the VerifactuSystem field name above
    private static class System_Xml
    {
        public const global::System.Xml.Linq.SaveOptions None = global::System.Xml.Linq.SaveOptions.DisableFormatting;
    }

    /// <summary>Changes a field of a saved record, as someone with database access might.</summary>
    private static void Tamper(VerifactuRecord record, string property, string value) =>
        typeof(VerifactuRecord).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(record, value);

    [Test]
    public void Verify_AnIntactChain_HasNoProblems()
    {
        var report = VerifactuChainVerifier.Verify(Chain(5));

        report.Records.ShouldBe(5);
        report.IsIntact.ShouldBeTrue();
    }

    [Test]
    public void Verify_EmptyChain_IsIntact() =>
        VerifactuChainVerifier.Verify([]).IsIntact.ShouldBeTrue();

    [Test]
    public void Verify_ARecordWhoseAmountWasChanged_IsCaught()
    {
        var chain = Chain(4);
        Tamper(chain[1], nameof(VerifactuRecord.TotalAmount), "1.00");

        var report = VerifactuChainVerifier.Verify(chain);

        report.IsIntact.ShouldBeFalse();
        report.Problems.ShouldContain(p => p.Sequence == 2 && p.Message.Contains("changed"));
    }

    [Test]
    public void Verify_ARecordWhoseHashWasRewrittenToo_BreaksTheNextLink()
    {
        // Someone alters a record and recomputes its own hash: the record after it no longer matches
        var chain = Chain(3);
        Tamper(chain[1], nameof(VerifactuRecord.TotalAmount), "1.00");
        Tamper(chain[1], nameof(VerifactuRecord.Hash),
            VerifactuHash.Issued("12345678Z", "26-10-002", "03-10-2026", "F1", "21.00", "1.00", chain[1].PreviousHash, chain[1].GeneratedAt));

        var report = VerifactuChainVerifier.Verify(chain);

        report.Problems.ShouldContain(p => p.Sequence == 3 && p.Message.Contains("does not point"));
    }

    [Test]
    public void Verify_ARemovedRecord_IsCaught()
    {
        var chain = Chain(4);
        chain.RemoveAt(1);

        var report = VerifactuChainVerifier.Verify(chain);

        report.Problems.ShouldContain(p => p.Message.Contains("missing"));
        report.Problems.ShouldContain(p => p.Message.Contains("does not point"));
    }

    [Test]
    public void Verify_AnXmlThatSaysSomethingElse_IsCaught()
    {
        var chain = Chain(2);
        Tamper(chain[0], nameof(VerifactuRecord.Xml), chain[0].Xml.Replace(chain[0].Hash, new string('A', 64)));

        VerifactuChainVerifier.Verify(chain).Problems.ShouldContain(p => p.Sequence == 1 && p.Message.Contains("XML"));
    }

    [Test]
    public void Verify_TimeRunningBackwards_IsCaught()
    {
        var chain = Chain(3);
        var stamp = "2026-10-03T10:00:00+02:00";
        Tamper(chain[2], nameof(VerifactuRecord.GeneratedAt), stamp);
        Tamper(chain[2], nameof(VerifactuRecord.Hash),
            VerifactuHash.Issued("12345678Z", "26-10-003", "03-10-2026", "F1", "21.00", "121.00", chain[2].PreviousHash, stamp));

        VerifactuChainVerifier.Verify(chain).Problems.ShouldContain(p => p.Sequence == 3 && p.Message.Contains("before"));
    }

    [Test]
    public void Verify_RecordsGivenOutOfOrder_AreStillJudgedByTheirPosition() =>
        VerifactuChainVerifier.Verify(Chain(4).AsEnumerable().Reverse().ToList()).IsIntact.ShouldBeTrue();
}
