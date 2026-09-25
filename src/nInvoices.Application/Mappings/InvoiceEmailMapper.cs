using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

public static class InvoiceEmailMapper
{
    public static InvoiceEmailDto ToDto(InvoiceEmail email) => new(
        email.Id,
        email.InvoiceId,
        email.From,
        email.To,
        email.Cc,
        email.Subject,
        email.Attachments.Split(", ", StringSplitOptions.RemoveEmptyEntries),
        email.GmailDraftId,
        GmailDraftUrl(email.From, email.GmailMessageId),
        email.CreatedAt);

    /// <summary>
    /// Opens the draft in Gmail's web UI. <c>authuser</c> selects the right account when the
    /// browser is signed in to several Google accounts.
    /// </summary>
    public static string GmailDraftUrl(string account, string messageId) =>
        $"https://mail.google.com/mail/?authuser={Uri.EscapeDataString(account)}#drafts?compose={Uri.EscapeDataString(messageId)}";
}
