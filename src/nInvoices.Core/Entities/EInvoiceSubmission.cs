namespace nInvoices.Core.Entities;

/// <summary>
/// That the e-invoice of an invoice was delivered through a channel (Spain's FACe, say), and where it
/// stands there. It exists only once the channel accepted the file; a refused or failed delivery leaves
/// nothing behind, so it can simply be tried again. Once it exists, the e-invoice it refers to is final:
/// it cannot be regenerated, as what was delivered must not change.
/// </summary>
public sealed class EInvoiceSubmission : OwnedEntityBase
{
    public long InvoiceEInvoiceId { get; private set; }

    /// <summary>The channel, e.g. "face".</summary>
    public string ChannelId { get; private set; } = string.Empty;

    /// <summary>"Test" or "Production": where it was delivered to.</summary>
    public string Environment { get; private set; } = string.Empty;

    /// <summary>The channel's own identifier of the delivery (for FACe, the registry code).</summary>
    public string Reference { get; private set; } = string.Empty;

    public DateTime SubmittedAt { get; private set; }

    /// <summary>When the channel registered it, as it says.</summary>
    public DateTime? RegisteredAt { get; private set; }

    public string? StatusCode { get; private set; }

    public string? StatusName { get; private set; }

    public string? CancellationStatus { get; private set; }

    /// <summary>When the status was last read from the channel.</summary>
    public DateTime? CheckedAt { get; private set; }

    /// <summary>Why the last attempt to read the status failed, if it did.</summary>
    public string? LastError { get; private set; }

    public InvoiceEInvoice EInvoice { get; set; } = null!;

    private EInvoiceSubmission() { }

    public EInvoiceSubmission(long eInvoiceId, string channelId, string environment, string reference, DateTime now)
    {
        InvoiceEInvoiceId = eInvoiceId;
        ChannelId = channelId;
        Environment = environment;
        Reference = reference;
        SubmittedAt = now;
        CreatedAt = now;
    }

    /// <summary>Takes in what the channel reports.</summary>
    public void Update(string? statusCode, string? statusName, string? cancellationStatus, DateTime? registeredAt, DateTime now)
    {
        StatusCode = statusCode;
        StatusName = statusName;
        CancellationStatus = cancellationStatus;
        RegisteredAt = registeredAt ?? RegisteredAt;
        CheckedAt = now;
        LastError = null;
        UpdatedAt = now;
    }

    public void CheckFailed(string message, DateTime now)
    {
        LastError = message;
        UpdatedAt = now;
    }
}
