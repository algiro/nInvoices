using nInvoices.Core.Compliance.EInvoice;

namespace nInvoices.Countries.Italy;

/// <summary>
/// The taxes of an invoice as Italian rules see them: its IVA (at most one rate: every tax applies to the
/// whole invoice), the pension contributions charged to the customer (rivalsa INPS, cassa), the ritenute, and
/// whether the €2 marca da bollo is due. One place for the FatturaPA and for what templates print.
/// </summary>
/// <param name="Iva">The IVA charged, null when there is none (a forfettario invoice).</param>
/// <param name="OtherIva">Further IVA taxes, which an Italian invoice cannot have (each line has one rate).</param>
/// <param name="Nature">Why no IVA is charged (natura), when the IVA is 0% or absent; null if unknown.</param>
/// <param name="TaxableAmount">The lines plus the contributions: what IVA is charged on.</param>
public sealed record ItalianInvoiceTaxes(
    EInvoiceTax? Iva,
    IReadOnlyList<EInvoiceTax> OtherIva,
    string? Nature,
    IReadOnlyList<EInvoiceTax> Contributions,
    IReadOnlyList<EInvoiceTax> Withholdings,
    decimal TaxableAmount)
{
    /// <summary>The marca da bollo, paid on documents above the threshold that charge no IVA.</summary>
    public const decimal StampDuty = 2m;

    /// <summary>The bollo is due above this amount (art. 13 of the tariff, DPR 642/1972), not at it.</summary>
    public const decimal StampDutyThreshold = 77.47m;

    /// <summary>
    /// The operations without IVA whose invoices carry the bollo: not subject (N2.x, forfettario and minimi
    /// included) and exempt (N4). Non-taxable exports and intra-EU supplies (N3.x) and reverse charge (N6.x) don't.
    /// </summary>
    private static readonly HashSet<string> StampDutyNatures = ["N2.1", "N2.2", "N4"];

    /// <summary>The rate printed on every line: the IVA's, or 0 when there is none.</summary>
    public decimal IvaRate => Iva?.Rate ?? 0m;

    public bool ChargesIva => Iva is { Amount: > 0 };

    /// <summary>Whether the €2 marca da bollo is due on this invoice.</summary>
    public bool StampDutyApplies =>
        !ChargesIva && Nature is not null && StampDutyNatures.Contains(Nature) && TaxableAmount > StampDutyThreshold;

    /// <param name="linesAmount">The lines before taxes.</param>
    /// <param name="issuerValues">The issuer's Italian settings: a forfettario or minimi regime charges no IVA (N2.2).</param>
    public static ItalianInvoiceTaxes Of(IReadOnlyList<EInvoiceTax> taxes, decimal linesAmount, IReadOnlyDictionary<string, string> issuerValues)
    {
        ArgumentNullException.ThrowIfNull(taxes);
        ArgumentNullException.ThrowIfNull(issuerValues);

        var added = taxes.Where(t => t.Kind == EInvoiceTaxKind.Added).ToList();
        var contributions = added.Where(t => ItalyComplianceModule.IsContribution(t.Values)).ToList();
        var iva = added.Where(t => !ItalyComplianceModule.IsContribution(t.Values)).ToList();
        var withholdings = taxes.Where(t => t.Kind == EInvoiceTaxKind.Withheld).ToList();

        var first = iva.FirstOrDefault();
        string? nature = null;
        if (first is null || first.Rate == 0)
        {
            nature = first?.Values?.GetValueOrDefault(ItalyComplianceModule.NatureKey);
            if (string.IsNullOrWhiteSpace(nature))
                nature = ItalyComplianceModule.ChargesNoIva(issuerValues) ? ItalyComplianceModule.NotSubjectOther : null;
        }

        return new ItalianInvoiceTaxes(
            first,
            iva.Skip(1).ToList(),
            nature,
            contributions,
            withholdings,
            linesAmount + contributions.Sum(c => c.Amount));
    }
}
