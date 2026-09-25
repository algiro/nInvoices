using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.InvoiceEmails.Commands;
using nInvoices.Application.Services.Email;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Features.InvoiceEmails;

[TestFixture]
public sealed class CreateInvoiceEmailDraftCommandHandlerTests
{
    private const string UserId = "user-1";

    private Invoice _invoice = null!;
    private Mock<IInvoiceRepository> _invoices = null!;
    private InMemoryRepository<GmailConnection> _connections = null!;
    private InMemoryRepository<InvoiceEmail> _emails = null!;
    private Mock<IInvoiceEmailComposer> _composer = null!;
    private Mock<IGmailClient> _gmail = null!;
    private Mock<ISecretProtector> _protector = null!;
    private CreateInvoiceEmailDraftCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var customer = new Customer("ACME S.p.A.", "IT01234567890", new Address("Via Roma", "10", "Milano", "20121", "Italy")) { Id = 3 };
        _invoice = new Invoice(3, new InvoiceNumber("26-09-001"), InvoiceType.Monthly, new DateOnly(2026, 9, 30), new Money(1000m, "EUR"), "EUR") { Id = 42 };
        _invoice.FinalizeInvoice();

        _invoices = new Mock<IInvoiceRepository>();
        _invoices.Setup(r => r.GetByIdWithRelatedAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(() => _invoice);

        _connections = new InMemoryRepository<GmailConnection>(
            new GmailConnection(UserId, "me@gmail.com", "encrypted", GmailScopes.Compose));
        _emails = new InMemoryRepository<InvoiceEmail>();

        _composer = new Mock<IInvoiceEmailComposer>();
        _composer.Setup(c => c.BuildAttachmentsAsync(_invoice, customer, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new EmailAttachment("Invoice-26-09-001.pdf", "application/pdf", [1, 2, 3])]);

        _gmail = new Mock<IGmailClient>();
        _gmail.SetupGet(g => g.IsConfigured).Returns(true);
        _gmail.Setup(g => g.CreateDraftAsync("refresh", It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GmailDraft("draft-1", "msg-1"));

        _protector = new Mock<ISecretProtector>();
        _protector.Setup(p => p.Unprotect("encrypted")).Returns("refresh");

        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.UserId).Returns(UserId);

        _handler = new CreateInvoiceEmailDraftCommandHandler(
            _invoices.Object,
            new InMemoryRepository<Customer>(customer),
            _connections,
            _emails,
            new Mock<IUnitOfWork>().Object,
            _composer.Object,
            _gmail.Object,
            _protector.Object,
            userContext.Object,
            NullLogger<CreateInvoiceEmailDraftCommandHandler>.Instance);
    }

    private static CreateInvoiceEmailDraftDto Email(string to = "billing@acme.it", string? cc = null, string subject = "Invoice 26-09-001") =>
        new(to, cc, subject, "<p>Please find attached…</p>");

    private Task<InvoiceEmailDto> Send(CreateInvoiceEmailDraftDto? email = null) =>
        _handler.Handle(new CreateInvoiceEmailDraftCommand(42, email ?? Email()), TestContext.CurrentContext.CancellationToken);

    [Test]
    public async Task Handle_CreatesDraftFromConnectedAccountAndRecordsIt()
    {
        OutgoingEmail? sent = null;
        _gmail.Setup(g => g.CreateDraftAsync("refresh", It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()))
            .Callback<string, OutgoingEmail, CancellationToken>((_, e, _) => sent = e)
            .ReturnsAsync(new GmailDraft("draft-1", "msg-1"));

        var result = await Send(Email(to: "billing@acme.it; boss@acme.it", cc: "cfo@acme.it", subject: "Invoice\r\n 26-09-001"));

        sent.ShouldNotBeNull();
        sent.From.ShouldBe("me@gmail.com");
        sent.To.ShouldBe(["billing@acme.it", "boss@acme.it"]);
        sent.Cc.ShouldBe(["cfo@acme.it"]);
        sent.Subject.ShouldBe("Invoice 26-09-001");
        sent.Attachments.ShouldHaveSingleItem().FileName.ShouldBe("Invoice-26-09-001.pdf");

        var recorded = _emails.Items.ShouldHaveSingleItem();
        recorded.InvoiceId.ShouldBe(42);
        recorded.GmailDraftId.ShouldBe("draft-1");
        recorded.RfcMessageId.ShouldBe(sent.MessageId);
        result.GmailUrl.ShouldContain("compose=msg-1");
        result.GmailUrl.ShouldContain("authuser=me%40gmail.com");
        _connections.Items.Single().LastUsedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task Handle_DoesNotChangeTheInvoiceStatus()
    {
        await Send();

        _invoice.Status.ShouldBe(InvoiceStatus.Finalized);
    }

    [Test]
    public async Task Handle_DraftInvoice_IsRefused()
    {
        _invoice = new Invoice(3, new InvoiceNumber("26-09-002"), InvoiceType.OneTime, new DateOnly(2026, 9, 30), new Money(1m, "EUR"), "EUR") { Id = 42 };

        var ex = await Should.ThrowAsync<InvoiceEmailException>(() => Send());

        ex.Code.ShouldBe(InvoiceEmailException.InvoiceNotReady);
        _gmail.Verify(g => g.CreateDraftAsync(It.IsAny<string>(), It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("")]
    [TestCase("not-an-address")]
    [TestCase("billing@acme.it, Name <x@y.it>")]
    public async Task Handle_InvalidRecipients_AreRefused(string to)
    {
        var ex = await Should.ThrowAsync<InvoiceEmailException>(() => Send(Email(to: to)));

        ex.Code.ShouldBe(InvoiceEmailException.InvalidRecipients);
    }

    [Test]
    public async Task Handle_NoGmailConnection_AsksToConnect()
    {
        _connections.Items.Clear();

        var ex = await Should.ThrowAsync<InvoiceEmailException>(() => Send());

        ex.Code.ShouldBe(InvoiceEmailException.GmailNotConnected);
    }

    [Test]
    public async Task Handle_TokenRevokedAtGoogle_ForgetsConnectionAndAsksToReconnect()
    {
        _gmail.Setup(g => g.CreateDraftAsync("refresh", It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GmailAuthorizationException("invalid_grant"));

        var ex = await Should.ThrowAsync<InvoiceEmailException>(() => Send());

        ex.Code.ShouldBe(InvoiceEmailException.GmailReconnectRequired);
        _connections.Items.ShouldBeEmpty();
        _emails.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Handle_TokenNoLongerDecryptable_ForgetsConnectionAndAsksToReconnect()
    {
        _protector.Setup(p => p.Unprotect("encrypted")).Throws(new CryptographicException("key not found"));

        var ex = await Should.ThrowAsync<InvoiceEmailException>(() => Send());

        ex.Code.ShouldBe(InvoiceEmailException.GmailReconnectRequired);
        _connections.Items.ShouldBeEmpty();
    }
}
