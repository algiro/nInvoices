namespace nInvoices.Application.Services.Email;

/// <summary>
/// An invoice email could not be prepared or drafted for a reason the user can fix.
/// <see cref="Code"/> lets the UI react (e.g. offer "Reconnect Gmail").
/// </summary>
public sealed class InvoiceEmailException : Exception
{
    public const string GmailNotConfigured = "gmail_not_configured";
    public const string GmailNotConnected = "gmail_not_connected";
    public const string GmailReconnectRequired = "gmail_reconnect_required";
    public const string InvoiceNotReady = "invoice_not_ready";
    public const string InvalidRecipients = "invalid_recipients";
    public const string InvalidTemplate = "invalid_template";
    public const string AttachmentFailed = "attachment_failed";

    public string Code { get; }

    public InvoiceEmailException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }
}
