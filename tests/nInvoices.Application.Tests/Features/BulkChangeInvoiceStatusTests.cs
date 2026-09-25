using Moq;
using nInvoices.Application.Features.Invoices.Commands;
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
    private BulkChangeInvoiceStatusCommandHandler _handler = null!;
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
        _handler = new BulkChangeInvoiceStatusCommandHandler(_repository.Object, _unitOfWork.Object);
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
        if (status != InvoiceStatus.Draft) invoice.FinalizeInvoice();
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
            TestContext.CurrentContext.CancellationToken));
    }
}
