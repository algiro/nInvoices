using System.Linq.Expressions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
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

/// <summary>
/// An invoice only takes its number when it is finalized. Until then every draft shows the same
/// "next number", and finalizing one moves the others on. Runs the real numbering, finalizing and
/// draft-refresh services over in-memory data.
/// </summary>
[TestFixture]
public sealed class DraftInvoiceNumberingTests
{
    private const string Format = "N-{NUMBER:000}";

    private InMemoryRepository<InvoiceSequence> _sequences = null!;
    private InMemoryRepository<Customer> _customers = null!;
    private Mock<IInvoiceRepository> _invoiceRepository = null!;
    private Mock<IInvoiceGenerationService> _generation = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private List<Invoice> _invoices = null!;
    private List<long> _rendered = null!;
    private InvoiceNumbering _numbering = null!;
    private DraftInvoiceSynchronizer _drafts = null!;
    private FinalizeInvoiceCommandHandler _finalize = null!;
    private Mock<IPublisher> _publisher = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public void SetUp()
    {
        _invoices = [];
        _rendered = [];
        _sequences = new InMemoryRepository<InvoiceSequence>(new InvoiceSequence(5));
        _customers = new InMemoryRepository<Customer>(
            new Customer("Acme", "ACME1", new Address("Main", "1", "Town", "12345", "Italy")) { Id = 1 });

        _invoiceRepository = new Mock<IInvoiceRepository>();
        _invoiceRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Invoice, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Invoice, bool>> p, CancellationToken _) => _invoices.Where(p.Compile()).ToList());
        _invoiceRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => _invoices.FirstOrDefault(i => i.Id == id));

        _generation = new Mock<IInvoiceGenerationService>();
        _generation
            .Setup(g => g.RegenerateInvoiceHtmlAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Callback((long id, CancellationToken _) => _rendered.Add(id))
            .Returns(Task.CompletedTask);

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _numbering = new InvoiceNumbering(_sequences, Options.Create(new InvoiceSettings { NumberFormat = Format }));
        _drafts = new DraftInvoiceSynchronizer(
            _invoiceRepository.Object, _customers, _numbering, _generation.Object, _unitOfWork.Object,
            NullLogger<DraftInvoiceSynchronizer>.Instance);
        _publisher = new Mock<IPublisher>();
        _finalize = new FinalizeInvoiceCommandHandler(
            _invoiceRepository.Object, _customers, _numbering, _drafts, _unitOfWork.Object, _publisher.Object);
    }

    private int SequenceValue => _sequences.Items.Single().CurrentValue;

    /// <summary>A new draft, as generating an invoice makes it: it shows the next number and takes none.</summary>
    private async Task<Invoice> NewDraftAsync(long id, DateOnly? issueDate = null)
    {
        var date = issueDate ?? new DateOnly(2026, 3, 1);
        var invoice = new Invoice(
            1, await _numbering.PeekAsync(_customers.Items.Single(), date, Token), InvoiceType.OneTime, date, new Money(100m, "EUR"), "EUR")
        { Id = id };
        _invoices.Add(invoice);
        return invoice;
    }

    [Test]
    public async Task Drafts_AllShowTheNextNumber_AndTakeNone()
    {
        var first = await NewDraftAsync(1);
        var second = await NewDraftAsync(2);
        var third = await NewDraftAsync(3);

        new[] { first, second, third }.Select(i => i.Number.ToString()).ShouldAllBe(n => n == "N-005");
        SequenceValue.ShouldBe(5);
    }

    [Test]
    public async Task Finalize_TakesTheNextNumber_AndTheOtherDraftsMoveOn()
    {
        var first = await NewDraftAsync(1);
        var second = await NewDraftAsync(2);
        var third = await NewDraftAsync(3);

        await _finalize.Handle(new FinalizeInvoiceCommand(2), Token);

        second.Status.ShouldBe(InvoiceStatus.Finalized);
        second.Number.ToString().ShouldBe("N-005");
        SequenceValue.ShouldBe(6);
        // The other drafts now show the new next number, and their documents were re-rendered with it
        first.Number.ToString().ShouldBe("N-006");
        third.Number.ToString().ShouldBe("N-006");
        _rendered.Order().ShouldBe([1L, 3L]);
    }

    [Test]
    public async Task Finalize_AnnouncesTheFinalizedInvoice_SoComplianceStepsCanFollow()
    {
        await NewDraftAsync(1);
        await NewDraftAsync(2);

        await _finalize.Handle(new FinalizeInvoiceCommand(2), Token);

        _publisher.Verify(
            p => p.Publish(It.Is<nInvoices.Application.Features.Invoices.Notifications.InvoiceFinalizedNotification>(n => n.InvoiceId == 2), It.IsAny<CancellationToken>()),
            Times.Once);
        _publisher.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Finalize_OneAfterTheOther_NumbersRunOnWithoutGaps()
    {
        var first = await NewDraftAsync(1);
        var second = await NewDraftAsync(2);

        await _finalize.Handle(new FinalizeInvoiceCommand(2), Token);
        await _finalize.Handle(new FinalizeInvoiceCommand(1), Token);

        second.Number.ToString().ShouldBe("N-005");
        first.Number.ToString().ShouldBe("N-006");
        SequenceValue.ShouldBe(7);
    }

    [Test]
    public async Task DeletingADraft_LeavesNoGap()
    {
        var kept = await NewDraftAsync(1);
        var deleted = await NewDraftAsync(2);
        _invoices.Remove(deleted);

        await _finalize.Handle(new FinalizeInvoiceCommand(1), Token);

        kept.Number.ToString().ShouldBe("N-005");
        SequenceValue.ShouldBe(6);
    }

    [Test]
    public async Task Finalize_AlreadyFinalized_ThrowsAndUsesNoNumber()
    {
        var invoice = await NewDraftAsync(1);
        await _finalize.Handle(new FinalizeInvoiceCommand(1), Token);

        await Should.ThrowAsync<InvalidOperationException>(() => _finalize.Handle(new FinalizeInvoiceCommand(1), Token));

        invoice.Number.ToString().ShouldBe("N-005");
        SequenceValue.ShouldBe(6);
    }

    [Test]
    public async Task Finalize_UnknownInvoice_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => _finalize.Handle(new FinalizeInvoiceCommand(99), Token));
        SequenceValue.ShouldBe(5);
    }

    [Test]
    public async Task Finalize_ADraftWithAStaleNumber_IsRenderedAgainWithItsRealNumber()
    {
        // A draft created before numbers were taken on finalizing holds an old number
        var stale = await NewDraftAsync(1);
        stale.Number = new InvoiceNumber("N-002");

        await _finalize.Handle(new FinalizeInvoiceCommand(1), Token);

        stale.Number.ToString().ShouldBe("N-005");
        _rendered.ShouldContain(1L);
    }

    [Test]
    public async Task Finalize_NumberFollowsTheIssueDateOfEachInvoice()
    {
        _sequences.Items.Single().SetNumberFormat("{YEAR:yy}-{MONTH:00}-{NUMBER:000}");
        var february = await NewDraftAsync(1, new DateOnly(2026, 2, 10));
        var march = await NewDraftAsync(2, new DateOnly(2026, 3, 20));

        february.Number.ToString().ShouldBe("26-02-005");
        march.Number.ToString().ShouldBe("26-03-005");

        await _finalize.Handle(new FinalizeInvoiceCommand(2), Token);

        march.Number.ToString().ShouldBe("26-03-005");
        february.Number.ToString().ShouldBe("26-02-006");
    }

    [Test]
    public async Task RefreshDraftsAsync_AfterTheSequenceIsEdited_DraftsShowTheNewNumber()
    {
        var draft = await NewDraftAsync(1);

        _sequences.Items.Single().SetValue(40);
        await _drafts.RefreshDraftsAsync(Token);

        draft.Number.ToString().ShouldBe("N-040");
        _rendered.ShouldBe([1L]);
    }

    [Test]
    public async Task RefreshDraftsAsync_NothingChanged_SavesAndRendersNothing()
    {
        await NewDraftAsync(1);
        await NewDraftAsync(2);

        await _drafts.RefreshDraftsAsync(Token);

        _rendered.ShouldBeEmpty();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RefreshDraftsAsync_IgnoresInvoicesThatAreNotDrafts()
    {
        await NewDraftAsync(1);
        var sent = await NewDraftAsync(2);
        sent.FinalizeInvoice();
        sent.MarkAsSent();
        sent.Number = new InvoiceNumber("N-001");

        _sequences.Items.Single().SetValue(9);
        await _drafts.RefreshDraftsAsync(Token);

        sent.Number.ToString().ShouldBe("N-001");
    }

    [Test]
    public async Task RefreshDraftsAsync_ADocumentThatCannotBeRendered_DoesNotStopTheOthers()
    {
        var first = await NewDraftAsync(1);
        var second = await NewDraftAsync(2);
        _generation
            .Setup(g => g.RegenerateInvoiceHtmlAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No active template"));

        _sequences.Items.Single().SetValue(7);
        await _drafts.RefreshDraftsAsync(Token);

        // Both numbers are updated; only the one that could be rendered was
        first.Number.ToString().ShouldBe("N-007");
        second.Number.ToString().ShouldBe("N-007");
        _rendered.ShouldBe([2L]);
    }

    [Test]
    public async Task Take_UserWithoutASequenceYet_CreatesItAndStartsAtOne()
    {
        var empty = new InMemoryRepository<InvoiceSequence>();
        var numbering = new InvoiceNumbering(empty, Options.Create(new InvoiceSettings { NumberFormat = Format }));

        var number = await numbering.TakeAsync(_customers.Items.Single(), new DateOnly(2026, 3, 1), Token);

        number.ToString().ShouldBe("N-001");
        empty.Items.Single().CurrentValue.ShouldBe(2);
    }

    [Test]
    public async Task Peek_DoesNotCreateOrAdvanceAnything()
    {
        var empty = new InMemoryRepository<InvoiceSequence>();
        var numbering = new InvoiceNumbering(empty, Options.Create(new InvoiceSettings { NumberFormat = Format }));

        (await numbering.PeekAsync(_customers.Items.Single(), new DateOnly(2026, 3, 1), Token)).ToString().ShouldBe("N-001");

        empty.Items.ShouldBeEmpty();
    }
}
