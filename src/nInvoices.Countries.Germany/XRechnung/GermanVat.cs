using nInvoices.Core.Compliance.EInvoice;

namespace nInvoices.Countries.Germany.XRechnung;

/// <summary>
/// The VAT of an invoice as EN 16931 sees it: one category and rate for every line (each tax applies to the
/// whole invoice, so an invoice has at most one), with the exemption reason a 0% category must give.
/// </summary>
/// <param name="Vat">The VAT tax, null when there is none (a Kleinunternehmer).</param>
/// <param name="OtherVat">Further VAT taxes, which one invoice cannot have here.</param>
/// <param name="Category">S, Z, E, AE, K, G or O; null when a 0% VAT doesn't say which.</param>
/// <param name="ExemptionReason">The text a 0% category prints (BT-120); null when none is needed or known.</param>
/// <param name="ExemptionCode">The VATEX code (BT-121), when the category has one.</param>
public sealed record GermanVat(
    EInvoiceTax? Vat,
    IReadOnlyList<EInvoiceTax> OtherVat,
    string? Category,
    string? ExemptionReason,
    string? ExemptionCode,
    IReadOnlyList<EInvoiceTax> Withholdings)
{
    public decimal Rate => Vat?.Rate ?? 0m;

    public decimal Amount => Vat?.Amount ?? 0m;

    public static GermanVat Of(IReadOnlyList<EInvoiceTax> taxes, IReadOnlyDictionary<string, string> issuerValues)
    {
        ArgumentNullException.ThrowIfNull(taxes);
        ArgumentNullException.ThrowIfNull(issuerValues);

        var vat = taxes.Where(t => t.Kind == EInvoiceTaxKind.Added).ToList();
        var withholdings = taxes.Where(t => t.Kind == EInvoiceTaxKind.Withheld).ToList();
        var first = vat.FirstOrDefault();

        string? category;
        if (first is { Rate: > 0 })
            category = GermanyComplianceModule.Standard;
        else if (first?.Values?.GetValueOrDefault(GermanyComplianceModule.CategoryKey) is { Length: > 0 } chosen)
            category = chosen;
        else
            category = GermanyComplianceModule.IsSmallBusiness(issuerValues) ? GermanyComplianceModule.Exempt : null;

        var note = first?.Values?.GetValueOrDefault(GermanyComplianceModule.ExemptionNoteKey);
        var (reason, code) = category switch
        {
            GermanyComplianceModule.Exempt => (
                string.IsNullOrWhiteSpace(note)
                    ? GermanyComplianceModule.IsSmallBusiness(issuerValues) ? GermanyComplianceModule.SmallBusinessNote : null
                    : note.Trim(),
                (string?)null),
            GermanyComplianceModule.ReverseCharge => (Or(note, GermanyComplianceModule.ReverseChargeNote), "VATEX-EU-AE"),
            GermanyComplianceModule.IntraCommunity => (Or(note, "Steuerfreie innergemeinschaftliche Lieferung (§ 4 Nr. 1b UStG)"), "VATEX-EU-IC"),
            GermanyComplianceModule.Export => (Or(note, "Steuerfreie Ausfuhrlieferung (§ 4 Nr. 1a UStG)"), "VATEX-EU-G"),
            GermanyComplianceModule.NotSubject => (Or(note, "Nicht im Inland steuerbare Leistung"), "VATEX-EU-O"),
            _ => ((string?)null, (string?)null)
        };

        return new GermanVat(first, vat.Skip(1).ToList(), category, reason, code, withholdings);
    }

    private static string Or(string? note, string fallback) => string.IsNullOrWhiteSpace(note) ? fallback : note.Trim();
}
