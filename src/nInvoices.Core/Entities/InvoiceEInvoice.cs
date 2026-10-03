namespace nInvoices.Core.Entities;

/// <summary>
/// The structured e-invoice generated for an invoice (a signed Facturae file, for instance).
/// It is kept as generated, never rebuilt: re-signing would change the document that was issued.
/// One per invoice and format.
/// </summary>
public sealed class InvoiceEInvoice : OwnedEntityBase
{
    public long InvoiceId { get; private set; }

    /// <summary>ISO alpha-2 code of the regime it was generated for.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>The format, e.g. "facturae-3.2.2".</summary>
    public string FormatId { get; private set; } = string.Empty;

    public byte[] Content { get; private set; } = [];

    public string ContentType { get; private set; } = string.Empty;

    public string FileExtension { get; private set; } = string.Empty;

    /// <summary>SHA-256 of <see cref="Content"/>, lower-case hex.</summary>
    public string Sha256 { get; private set; } = string.Empty;

    public DateTime GeneratedAt { get; private set; }

    public Invoice Invoice { get; set; } = null!;

    private InvoiceEInvoice() { }

    public InvoiceEInvoice(long invoiceId, string countryCode, string formatId, byte[] content, string contentType, string fileExtension)
    {
        InvoiceId = invoiceId;
        CountryCode = countryCode;
        FormatId = formatId;
        CreatedAt = DateTime.UtcNow;
        Replace(content, contentType, fileExtension);
    }

    /// <summary>Stores a newly generated file in place of the previous one.</summary>
    public void Replace(byte[] content, string contentType, string fileExtension)
    {
        Content = content;
        ContentType = contentType;
        FileExtension = fileExtension;
        Sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));
        GeneratedAt = DateTime.UtcNow;
        UpdatedAt = GeneratedAt;
    }
}
