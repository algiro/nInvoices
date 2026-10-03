using System.Globalization;
using System.Xml.Linq;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Compliance.Spain.Verifactu;

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
    public static ChainReport Verify(IReadOnlyList<VerifactuRecord> chain)
    {
        var problems = new List<ChainProblem>();
        VerifactuRecord? before = null;
        DateTimeOffset? lastTime = null;

        foreach (var (record, index) in chain.OrderBy(r => r.Sequence).Select((r, i) => (r, i)))
        {
            if (record.Sequence != index + 1)
                problems.Add(new ChainProblem(record.Sequence, $"The chain jumps to record {record.Sequence} where {index + 1} was expected (a record is missing)"));

            var expectedPrevious = before?.Hash ?? "";
            if (record.PreviousHash != expectedPrevious)
                problems.Add(new ChainProblem(record.Sequence, "It does not point at the hash of the record before it"));

            var recomputed = record.Kind == VerifactuRecordKind.Issued
                ? VerifactuHash.Issued(record.IssuerTaxId, record.InvoiceNumber, record.IssueDate, record.InvoiceType,
                    record.TotalTax, record.TotalAmount, record.PreviousHash, record.GeneratedAt)
                : VerifactuHash.Cancelled(record.IssuerTaxId, record.InvoiceNumber, record.IssueDate, record.PreviousHash, record.GeneratedAt);
            if (!string.Equals(recomputed, record.Hash, StringComparison.Ordinal))
                problems.Add(new ChainProblem(record.Sequence, "Its hash does not match its contents: the record was changed"));

            CheckXml(record, problems);

            if (DateTimeOffset.TryParse(record.GeneratedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                if (lastTime is not null && time < lastTime)
                    problems.Add(new ChainProblem(record.Sequence, "It is dated before the record that precedes it"));
                lastTime = time;
            }
            else
            {
                problems.Add(new ChainProblem(record.Sequence, "Its time stamp is not valid"));
            }

            before = record;
        }

        return new ChainReport(chain.Count, problems);
    }

    /// <summary>The XML that is sent must say the same as the fields that were hashed.</summary>
    private static void CheckXml(VerifactuRecord record, List<ChainProblem> problems)
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
                problems.Add(new ChainProblem(record.Sequence, "Its XML does not say what was hashed"));
        }
        catch (System.Xml.XmlException)
        {
            problems.Add(new ChainProblem(record.Sequence, "Its XML cannot be read"));
        }
    }
}
