namespace nInvoices.Core.Entities;

public enum VerifactuRecordKind
{
    /// <summary>The invoice was issued (registro de alta).</summary>
    Issued,

    /// <summary>The invoice was cancelled (registro de anulación).</summary>
    Cancelled
}

/// <summary>
/// One link of a user Verifactu chain: the record of an invoice issued or cancelled, with the hash
/// that ties it to the record before. Records are append-only: once saved they are never changed or
/// deleted (the database context refuses), which is what makes the chain tamper-evident. Where an
/// invoice stands with the tax authority is kept apart, in its own rows.
/// </summary>
public sealed class VerifactuRecord : OwnedEntityBase
{
    /// <summary>Position in the user chain, 1 for the first record; no gaps.</summary>
    public long Sequence { get; private set; }

    public VerifactuRecordKind Kind { get; private set; }

    /// <summary>The invoice; it cannot be deleted while a record refers to it.</summary>
    public long InvoiceId { get; private set; }

    // The fields below are the ones the hash is made of, kept exactly as they were hashed

    /// <summary>NIF of the issuer (IDEmisorFactura).</summary>
    public string IssuerTaxId { get; private set; } = string.Empty;

    /// <summary>Series and number of the invoice (NumSerieFactura).</summary>
    public string InvoiceNumber { get; private set; } = string.Empty;

    /// <summary>Issue date of the invoice as written in the record, DD-MM-YYYY.</summary>
    public string IssueDate { get; private set; } = string.Empty;

    /// <summary>Invoice type (F1...); empty for a cancellation.</summary>
    public string InvoiceType { get; private set; } = string.Empty;

    /// <summary>Total tax (CuotaTotal) as written, e.g. "12.35"; empty for a cancellation.</summary>
    public string TotalTax { get; private set; } = string.Empty;

    /// <summary>Total amount (ImporteTotal) as written, e.g. "123.45"; empty for a cancellation.</summary>
    public string TotalAmount { get; private set; } = string.Empty;

    /// <summary>Hash of the record before, or empty for the first one.</summary>
    public string PreviousHash { get; private set; } = string.Empty;

    /// <summary>When the record was generated, as written: ISO 8601 with offset (FechaHoraHusoGenRegistro).</summary>
    public string GeneratedAt { get; private set; } = string.Empty;

    /// <summary>SHA-256, 64 upper-case hexadecimal characters.</summary>
    public string Hash { get; private set; } = string.Empty;

    /// <summary>The record as the XML element sent to the tax authority.</summary>
    public string Xml { get; private set; } = string.Empty;

    public Invoice Invoice { get; set; } = null!;

    private VerifactuRecord() { }

    public VerifactuRecord(
        long sequence,
        VerifactuRecordKind kind,
        long invoiceId,
        string issuerTaxId,
        string invoiceNumber,
        string issueDate,
        string invoiceType,
        string totalTax,
        string totalAmount,
        string previousHash,
        string generatedAt,
        string hash,
        string xml)
    {
        Sequence = sequence;
        Kind = kind;
        InvoiceId = invoiceId;
        IssuerTaxId = issuerTaxId;
        InvoiceNumber = invoiceNumber;
        IssueDate = issueDate;
        InvoiceType = invoiceType;
        TotalTax = totalTax;
        TotalAmount = totalAmount;
        PreviousHash = previousHash;
        GeneratedAt = generatedAt;
        Hash = hash;
        Xml = xml;
        CreatedAt = DateTime.UtcNow;
    }
}
