using Mediator;
using Moq;
using nInvoices.Application.Services;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Telemetry;

/// <summary>Issuing an invoice is counted, by type, once the caller's save has gone through.</summary>
[TestFixture]
public sealed class InvoiceFinalizerMetricsTests
{
    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static readonly Customer Customer =
        new("Cliente", "A58818501", new Address("Calle", "1", "Madrid", "28001", "Spain")) { Id = 1 };

    private static InvoiceFinalizer Finalizer()
    {
        var numbering = new Mock<IInvoiceNumbering>();
        numbering.Setup(n => n.TakeAsync(It.IsAny<Customer>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceNumber("26-10-001"));
        return new InvoiceFinalizer(numbering.Object, Mock.Of<IDraftInvoiceSynchronizer>(), Mock.Of<IPublisher>());
    }

    private static Invoice Draft(long id, InvoiceType type) =>
        new(1, new InvoiceNumber("DRAFT"), type, new DateOnly(2026, 10, 3), new Money(100m, "EUR"), "EUR") { Id = id };

    [Test]
    public async Task Complete_CountsEachInvoice_ByItsType()
    {
        var finalizer = Finalizer();
        var monthly = Draft(1, InvoiceType.Monthly);
        var oneTime = Draft(2, InvoiceType.OneTime);
        var another = Draft(3, InvoiceType.Monthly);
        using var capture = new MetricCapture("ninvoices.invoices.finalized");

        await finalizer.FinalizeAsync(monthly, Customer, Token);
        await finalizer.FinalizeAsync(oneTime, Customer, Token);
        await finalizer.FinalizeAsync(another, Customer, Token);
        await finalizer.CompleteAsync([1, 2, 3], [], Token);

        capture.Total(("type", "Monthly")).ShouldBe(2);
        capture.Total(("type", "OneTime")).ShouldBe(1);
    }

    [Test]
    public async Task WithoutComplete_NothingIsCounted_BecauseTheSaveDidNotHappen()
    {
        var finalizer = Finalizer();
        using var capture = new MetricCapture("ninvoices.invoices.finalized");

        await finalizer.FinalizeAsync(Draft(1, InvoiceType.Monthly), Customer, Token);
        // The caller's save failed: CompleteAsync is never called

        capture.All().ShouldBeEmpty();
    }

    [Test]
    public async Task Complete_OnlyCountsWhatWasFinalizedHere_AndOnlyOnce()
    {
        var finalizer = Finalizer();
        await finalizer.FinalizeAsync(Draft(1, InvoiceType.Monthly), Customer, Token);
        using var capture = new MetricCapture("ninvoices.invoices.finalized");

        // 7 was not finalized by this instance; 1 is reported twice
        await finalizer.CompleteAsync([1, 7], [], Token);
        await finalizer.CompleteAsync([1], [], Token);

        capture.Total(("type", "Monthly")).ShouldBe(1);
        capture.All().Count.ShouldBe(1);
    }
}
