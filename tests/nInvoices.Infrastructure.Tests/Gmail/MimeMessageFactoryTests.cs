using System.Buffers.Text;
using MimeKit;
using nInvoices.Application.Services.Email;
using nInvoices.Infrastructure.Gmail;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Gmail;

[TestFixture]
public sealed class MimeMessageFactoryTests
{
    private static OutgoingEmail Email() => new(
        "me@gmail.com",
        ["billing@acme.it", "boss@acme.it"],
        ["cfo@acme.it"],
        "Fattura 26-09-001 – settembre",
        "<p>Gentile cliente, in allegato la fattura.</p>",
        "<abc123.invoice-42@ninvoices>",
        [
            new EmailAttachment("Invoice-26-09-001.pdf", "application/pdf", [0x25, 0x50, 0x44, 0x46]),
            new EmailAttachment("MonthlyReport-2026-09-ACME.pdf", "application/pdf", [0x25, 0x50])
        ]);

    [Test]
    public void Create_SetsHeadersBodyAndAttachments()
    {
        var message = MimeMessageFactory.Create(Email());

        message.From.Mailboxes.Single().Address.ShouldBe("me@gmail.com");
        message.To.Mailboxes.Select(m => m.Address).ShouldBe(["billing@acme.it", "boss@acme.it"]);
        message.Cc.Mailboxes.Single().Address.ShouldBe("cfo@acme.it");
        message.Subject.ShouldBe("Fattura 26-09-001 – settembre");
        message.MessageId.ShouldBe("abc123.invoice-42@ninvoices");
        message.HtmlBody.ShouldContain("in allegato la fattura");
        message.Attachments.OfType<MimePart>().Select(a => a.FileName)
            .ShouldBe(["Invoice-26-09-001.pdf", "MonthlyReport-2026-09-ACME.pdf"]);
    }

    [Test]
    public void ToRaw_IsBase64UrlOfAParsableMessage()
    {
        var raw = MimeMessageFactory.ToRaw(MimeMessageFactory.Create(Email()));

        raw.ShouldNotContain('+');
        raw.ShouldNotContain('/');
        using var stream = new MemoryStream(Base64Url.DecodeFromChars(raw));
        var parsed = MimeMessage.Load(stream);
        parsed.Subject.ShouldBe("Fattura 26-09-001 – settembre");
        parsed.Attachments.Count().ShouldBe(2);
    }
}
