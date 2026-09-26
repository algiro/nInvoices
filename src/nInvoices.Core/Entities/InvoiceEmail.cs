namespace nInvoices.Core.Entities;

/// <summary>
/// An email draft created in Gmail for an invoice. Kept as history: creating another draft
/// for the same invoice adds a row.
/// </summary>
public sealed class InvoiceEmail : OwnedEntityBase
{
    public long InvoiceId { get; set; }

    /// <summary>The Gmail address the draft was created in.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Comma-separated recipients.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>Comma-separated CC recipients, if any.</summary>
    public string? Cc { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>File names of the attached documents, comma-separated.</summary>
    public string Attachments { get; set; } = string.Empty;

    public string GmailDraftId { get; set; } = string.Empty;
    public string GmailMessageId { get; set; } = string.Empty;

    /// <summary>The RFC 5322 Message-ID header set on the message, for finding it later.</summary>
    public string RfcMessageId { get; set; } = string.Empty;

    // Navigation property
    public Invoice Invoice { get; set; } = null!;

    public InvoiceEmail()
    {
        CreatedAt = DateTime.UtcNow;
    }
}
