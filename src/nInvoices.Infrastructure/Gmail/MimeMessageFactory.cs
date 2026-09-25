using MimeKit;
using nInvoices.Application.Services.Email;

namespace nInvoices.Infrastructure.Gmail;

/// <summary>
/// Builds the RFC 5322 message Gmail stores as the draft: HTML body plus attachments.
/// </summary>
public static class MimeMessageFactory
{
    public static MimeMessage Create(OutgoingEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(email.From));
        message.To.AddRange(email.To.Select(MailboxAddress.Parse));
        message.Cc.AddRange(email.Cc.Select(MailboxAddress.Parse));
        message.Subject = email.Subject;
        message.MessageId = email.MessageId.Trim('<', '>');
        message.Date = DateTimeOffset.Now;

        var body = new BodyBuilder { HtmlBody = email.HtmlBody };
        foreach (var attachment in email.Attachments)
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        message.Body = body.ToMessageBody();

        return message;
    }

    /// <summary>The message as the base64url string the Gmail API expects in <c>message.raw</c>.</summary>
    public static string ToRaw(MimeMessage message)
    {
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return System.Buffers.Text.Base64Url.EncodeToString(stream.ToArray());
    }
}
