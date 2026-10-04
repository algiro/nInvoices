namespace nInvoices.Application.Compliance.Spain.Verifactu;

/// <summary>Who the invoice is addressed to, as the record identifies them.</summary>
/// <param name="Nif">A Spanish tax id; null for a customer abroad.</param>
/// <param name="CountryCode">ISO alpha-2, for a customer abroad.</param>
/// <param name="IdType">AEAT id type for a customer abroad: 02 (EU VAT number), 04 (id in the country of residence).</param>
/// <param name="Id">The id of a customer abroad.</param>
public sealed record VerifactuRecipient(string Name, string? Nif, string? CountryCode, string? IdType, string? Id);

/// <summary>One line of the tax breakdown (DetalleDesglose).</summary>
/// <param name="Operation">AEAT qualification: S1 (taxed), S2 (reverse charge), N1/N2 (not subject), E1..E8 (exempt).</param>
/// <param name="Igic">The tax is IGIC (Canary Islands) instead of IVA.</param>
public sealed record VerifactuBreakdown(string Operation, decimal Rate, decimal Base, decimal Tax, bool Igic = false);

/// <summary>What the record of an issued invoice is made from.</summary>
public sealed record VerifactuInvoiceData(
    string IssuerTaxId,
    string IssuerName,
    string InvoiceNumber,
    DateOnly IssueDate,
    string InvoiceType,
    string Description,
    VerifactuRecipient Recipient,
    IReadOnlyList<VerifactuBreakdown> Breakdown,
    decimal TotalTax,
    decimal TotalAmount);

/// <summary>The billing system the records name (SistemaInformatico).</summary>
public sealed record VerifactuSystem(
    string ProducerName,
    string ProducerTaxId,
    string SystemName,
    string SystemId,
    string Version,
    string InstallationNumber,
    bool MultipleTaxpayers);

/// <summary>The record before, which the new one is chained to; null for the first record.</summary>
public sealed record VerifactuPrevious(string IssuerTaxId, string InvoiceNumber, string IssueDate, string Hash);
