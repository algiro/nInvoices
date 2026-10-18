using System.Globalization;
using System.Xml.Linq;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;

namespace nInvoices.Countries.Spain.Verifactu;

/// <param name="Sequence">The record the problem is in (0 for the chain as a whole).</param>
public sealed record ChainProblem(long Sequence, string Message);

public sealed record ChainReport(int Records, IReadOnlyList<ChainProblem> Problems)
{
    public bool IsIntact => Problems.Count == 0;
}

/// <summary>
/// Checks that a chain is what it claims to be: records numbered without gaps, each one pointing at the
/// hash of the one before, every hash being the one its own fields give, the stored XML saying the same,
/// and time not running backwards. A record changed after the fact, or one removed or slipped in, shows up here.
/// </summary>
public static class VerifactuChainVerifier
{
    public static ChainReport Verify(IAppText text, IReadOnlyList<VerifactuRecord> chain)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(chain);
        var problems = new List<ChainProblem>();
        VerifactuRecord? before = null;
        DateTimeOffset? lastTime = null;

        foreach (var (record, index) in chain.OrderBy(r => r.Sequence).Select((r, i) => (r, i)))
        {
            if (record.Sequence != index + 1)
                problems.Add(new ChainProblem(record.Sequence, text.Translate("compliance.verifactu.chain.gap", ("sequence", record.Sequence), ("expected", index + 1))));

            var expectedPrevious = before?.Hash ?? "";
            if (record.PreviousHash != expectedPrevious)
                problems.Add(new ChainProblem(record.Sequence, text.Translate("compliance.verifactu.chain.previousHash")));

            var recomputed = record.Kind == VerifactuRecordKind.Issued
                ? VerifactuHash.Issued(record.IssuerTaxId, record.InvoiceNumber, record.IssueDate, record.InvoiceType,
                    record.TotalTax, record.TotalAmount, record.PreviousHash, record.GeneratedAt)
                : VerifactuHash.Cancelled(record.IssuerTaxId, record.InvoiceNumber, record.IssueDate, record.PreviousHash, record.GeneratedAt);
            if (!string.Equals(recomputed, record.Hash, StringComparison.Ordinal))
                problems.Add(new ChainProblem(record.Sequence, text.Translate("compliance.verifactu.chain.hashMismatch")));

            CheckXml(record, problems, text);

            if (DateTimeOffset.TryParse(record.GeneratedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                if (lastTime is not null && time < lastTime)
                    problems.Add(new ChainProblem(record.Sequence, text.Translate("compliance.verifactu.chain.timeBackwards")));
                lastTime = time;
            }
            else
            {
                problems.Add(new ChainProblem(record.Sequence, text.Translate("compliance.verifactu.chain.timestampInvalid")));
            }

            before = record;
        }

        return new ChainReport(chain.Count, problems);
    }

    /// <summary>The XML that is sent must say the same as the fields that were hashed.</summary>
    private static void CheckXml(VerifactuRecord record, List<ChainProblem> problems, IAppText text)
    {
        try
        {
            var xml = XElement.Parse(record.Xml);
            var sf = VerifactuXml.Sf;
            var huella = xml.Element(sf + "Huella")?.Value;
            var stamp = xml.Element(sf + "FechaHoraHusoGenRegistro")?.Value;
            var id = xml.Element(sf + "IDFactura");
            var number = id?.Element(sf + (record.Kind == VerifactuRecordKind.Issued ? "NumSerieFactura" : "NumSerieFacturaAnulada"))?.Value;

            if (huella != record.Hash || stamp != record.GeneratedAt || number != record.InvoiceNumber)
                problems.Add(new ChainProblem(record.Sequence, text.Translate("compliance.verifactu.chain.xmlMismatch")));
        }
        catch (System.Xml.XmlException)
        {
            problems.Add(new ChainProblem(record.Sequence, text.Translate("compliance.verifactu.chain.xmlUnreadable")));
        }
    }
}
