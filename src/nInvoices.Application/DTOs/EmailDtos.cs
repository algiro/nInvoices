namespace nInvoices.Application.DTOs;

public sealed record EmailTemplateDto(
    long Id,
    long CustomerId,
    string Name,
    string Subject,
    string Body,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateEmailTemplateDto(long CustomerId, string Name, string Subject, string Body);

public sealed record UpdateEmailTemplateDto(string Name, string Subject, string Body);

/// <summary>Request to render an email template being edited, for a customer.</summary>
public sealed record PreviewEmailTemplateDto(string Subject, string Body, long CustomerId);

/// <summary>Rendered subject and body, or the errors that prevented rendering.</summary>
public sealed record EmailTemplatePreviewDto(string? Subject, string? Html, IReadOnlyList<string> Errors);

/// <summary>Whether Gmail can be used, and which account is connected.</summary>
/// <param name="Configured">False when the server has no Google OAuth client configured.</param>
public sealed record GmailStatusDto(
    bool Configured,
    bool Connected,
    string? EmailAddress,
    DateTime? ConnectedAt,
    DateTime? LastUsedAt);

public sealed record GmailConnectUrlDto(string AuthorizationUrl);

/// <summary>A document that can be attached to an invoice email.</summary>
public sealed record EmailAttachmentOptionDto(string Key, string FileName, bool IncludedByDefault);

public sealed record EmailTemplateOptionDto(long? Id, string Name, bool IsActive);

/// <summary>An invoice email prepared from the customer's template, for review before drafting.</summary>
public sealed record InvoiceEmailComposeDto(
    long InvoiceId,
    long? TemplateId,
    IReadOnlyList<EmailTemplateOptionDto> Templates,
    string? From,
    string To,
    string? Cc,
    string Subject,
    string Body,
    IReadOnlyList<EmailAttachmentOptionDto> Attachments,
    IReadOnlyList<string> Errors);

/// <summary>The reviewed email to put into a Gmail draft.</summary>
public sealed record CreateInvoiceEmailDraftDto(
    string To,
    string? Cc,
    string Subject,
    string Body,
    bool IncludeMonthlyReport = true);

public sealed record InvoiceEmailDto(
    long Id,
    long InvoiceId,
    string From,
    string To,
    string? Cc,
    string Subject,
    IReadOnlyList<string> Attachments,
    string GmailDraftId,
    string GmailUrl,
    DateTime CreatedAt);
