using Mediator;
using Microsoft.Extensions.Options;
using Moq;
using nInvoices.Application.Features.Invoices.Commands;
using nInvoices.Application.Services;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Features;

[TestFixture]
public sealed class BulkChangeInvoiceStatusTests
{
    private Mock<IInvoiceRepository> _repository = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private Mock<IDraftInvoiceSynchronizer> _drafts = null!;
    private InMemoryRepository<InvoiceSequence> _sequences = null!;
    private BulkChangeInvoiceStatusCommandHandler _handler = null!;
    private Mock<IPublisher> _publisher = null!;
    private List<Invoice> _invoices = null!;

    [SetUp]
    public void SetUp()
    {
        _invoices = [];
        _repository = new Mock<IInvoiceRepository>();
        _repository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<long> ids, CancellationToken _) => _invoices.Where(i => ids.Contains(i.Id)).ToList());
        _unitOfWork = new Mock<IUnitOfWork>();
        _drafts = new Mock<IDraftInvoiceSynchronizer>();

        var customer = new Customer("Acme", "ACME1", new Address("Main", "1", "Town", "12345", "Italy")) { Id = 1 };
        _sequences = new InMemoryRepository<InvoiceSequence>(new InvoiceSequence(10));
        var numbering = new InvoiceNumbering(_sequences, Options.Create(new InvoiceSettings { NumberFormat = "N-{NUMBER:000}" }));

        _publisher = new Mock<IPublisher>();
        _handler = new BulkChangeInvoiceStatusCommandHandler(
            _repository.Object, new InMemoryRepository<Customer>(customer), numbering, _drafts.Object, _unitOfWork.Object, _publisher.Object);
    }

    private Invoice Given(long id, InvoiceStatus status)
    {
        var invoice = new Invoice(
            1,
            InvoiceNumber.Generate("INV-{NUMBER:000}", (int)id, new DateTime(2026, 1, 31), null),
            InvoiceType.Monthly,
            new DateOnly(2026, 1, 31),
            new Money(100m, "EUR"),
            "EUR") { Id = id };
        if (status != InvoiceStatus.Draft) invoice.Finalize(invoice.Number);
        if (status is InvoiceStatus.Sent or InvoiceStatus.Paid) invoice.MarkAsSent();
        if (status == InvoiceStatus.Paid) invoice.MarkAsPaid();
        if (status == InvoiceStatus.Cancelled) invoice.Cancel();
        _invoices.Add(invoice);
        return invoice;
    }

    [Test]
    public async Task Handle_MarkAsPaid_ChangesFinalizedAndSentAndSkipsTheRest()
    {
        var finalized = Given(1, InvoiceStatus.Finalized);
        var sent = Given(2, InvoiceStatus.Sent);
        var draft = Given(3, InvoiceStatus.Draft);
        Given(4, InvoiceStatus.Paid);

        var result = await _handler.Handle(
            new BulkChangeInvoiceStatusCommand(BulkInvoiceStatusAction.MarkAsPaid, [1, 2, 3, 4, 99]),
            TestContext.CurrentContext.CancellationToken);

        result.Succeeded.ShouldBe([1L, 2L]);
        finalized.Status.ShouldBe(InvoiceStatus.Paid);
        sent.Status.ShouldBe(InvoiceStatus.Paid);
        draft.Status.ShouldBe(InvoiceStatus.Draft);
        result.Skipped.Select(s => (s.Id, s.Reason)).ShouldBe([(3L, "Not finalized yet"), (4L, "Already paid"), (99L, "Not found")]);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_Finalize_GivesNumbersByIssueDateThenCreationAndMovesTheSequenceOn()
    {
        // Created in the order 1, 2, 3, but issued in the order 3, 1, 2 (1 and 2 on the same day)
        var one = Given(1, InvoiceStatus.Draft);
        var two = Given(2, InvoiceStatus.Draft);
        var three = Given(3, InvoiceStatus.Draft);
        one.IssueDate = new DateOnly(2026, 2, 10);
        two.IssueDate = new DateOnly(2026, 2, 10);
        three.IssueDate = new DateOnly(2026, 2, 1);

        var result = await _handler.Handle(
            new BulkChangeInvoiceStatusCommand(BulkInvoiceStatusAction.Finalize, [1, 2, 3]),
            TestContext.CurrentContext.CancellationToken);

        result.Succeeded.ShouldBe([1L, 2L, 3L]);
        three.Number.ToString().ShouldBe("N-010");
        one.Number.ToString().ShouldBe("N-011");
        two.Number.ToString().ShouldBe("N-012");
        _sequences.Items.Single().CurrentValue.ShouldBe(13);
        one.Status.ShouldBe(InvoiceStatus.Finalized);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _drafts.Verify(d => d.RefreshDraftsAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Each finalized invoice is announced, so compliance steps follow it
        foreach (var id in new[] { 1L, 2L, 3L })
        {
            _publisher.Verify(
                p => p.Publish(It.Is<nInvoices.Application.Features.Invoices.Notifications.InvoiceFinalizedNotification>(n => n.InvoiceId == id), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Test]
    public async Task Handle_Finalize_SkippedInvoicesDoNotUseUpNumbers()
    {
        var draft = Given(1, InvoiceStatus.Draft);
        Given(2, InvoiceStatus.Sent);

        var result = await _handler.Handle(
            new BulkChangeInvoiceStatusCommand(BulkInvoiceStatusAction.Finalize, [1, 2]),
            TestContext.CurrentContext.CancellationToken);

        result.Succeeded.ShouldBe([1L]);
        result.Skipped.Select(s => s.Id).ShouldBe([2L]);
        draft.Number.ToString().ShouldBe("N-010");
        _sequences.Items.Single().CurrentValue.ShouldBe(11);
    }

    [Test]
    public async Task Handle_MarkAsSent_DoesNotTouchTheSequence()
    {
        Given(1, InvoiceStatus.Finalized);

        await _handler.Handle(
            new BulkChangeInvoiceStatusCommand(BulkInvoiceStatusAction.MarkAsSent, [1]),
            TestContext.CurrentContext.CancellationToken);

        _sequences.Items.Single().CurrentValue.ShouldBe(10);
        _drafts.Verify(d => d.RefreshDraftsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_NothingApplies_SavesNothing()
    {
        Given(1, InvoiceStatus.Paid);

        var result = await _handler.Handle(
            new BulkChangeInvoiceStatusCommand(BulkInvoiceStatusAction.Finalize, [1]),
            TestContext.CurrentContext.CancellationToken);

        result.Succeeded.ShouldBeEmpty();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(BulkInvoiceStatusAction.Finalize, InvoiceStatus.Draft, true)]
    [TestCase(BulkInvoiceStatusAction.Finalize, InvoiceStatus.Sent, false)]
    [TestCase(BulkInvoiceStatusAction.MarkAsSent, InvoiceStatus.Finalized, true)]
    [TestCase(BulkInvoiceStatusAction.MarkAsSent, InvoiceStatus.Draft, false)]
    [TestCase(BulkInvoiceStatusAction.MarkAsSent, InvoiceStatus.Paid, false)]
    [TestCase(BulkInvoiceStatusAction.MarkAsPaid, InvoiceStatus.Sent, true)]
    [TestCase(BulkInvoiceStatusAction.MarkAsPaid, InvoiceStatus.Cancelled, false)]
    public void WhyNot_FollowsTheInvoiceLifecycle(BulkInvoiceStatusAction action, InvoiceStatus status, bool applies)
    {
        (BulkChangeInvoiceStatusCommandHandler.WhyNot(action, status) is null).ShouldBe(applies);
    }

    [Test]
    public async Task Handle_TooManyInvoices_Throws()
    {
        var ids = Enumerable.Range(1, BulkChangeInvoiceStatusCommandHandler.MaxInvoices + 1).Select(i => (long)i).ToList();

        await Should.ThrowAsync<ArgumentException>(() => _handler.Handle(
            new BulkChangeInvoiceStatusCommand(BulkInvoiceStatusAction.MarkAsPaid, ids),
            TestContext.CurrentContext.CancellationToken).AsTask());
    }
}
