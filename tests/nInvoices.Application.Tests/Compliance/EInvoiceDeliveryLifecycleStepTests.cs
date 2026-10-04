using nInvoices.Application.Compliance.EInvoice;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance;

[TestFixture]
public sealed class EInvoiceDeliveryLifecycleStepTests
{
    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static Invoice NewInvoice() => new(
        1, new InvoiceNumber("26-10-001"), InvoiceType.OneTime, new DateOnly(2026, 10, 3), new Money(100m, "EUR"), "EUR") { Id = 7 };

    [Test]
    public async Task InvoiceWithoutAFile_CanBeDeleted()
    {
        var step = new EInvoiceDeliveryLifecycleStep(new InMemoryRepository<InvoiceEInvoice>(), new InMemoryRepository<EInvoiceSubmission>());

        await Should.NotThrowAsync(() => step.OnDeletingAsync(NewInvoice(), Token));
    }

    [Test]
    public async Task InvoiceWithAFileThatWasNotSent_CanBeDeleted()
    {
        var files = new InMemoryRepository<InvoiceEInvoice>(new InvoiceEInvoice(7, "ES", "facturae-3.2.2", [1], "application/xml", "xsig"));
        var step = new EInvoiceDeliveryLifecycleStep(files, new InMemoryRepository<EInvoiceSubmission>());

        await Should.NotThrowAsync(() => step.OnDeletingAsync(NewInvoice(), Token));
    }

    [Test]
    public async Task SentInvoice_CannotBeDeleted()
    {
        var files = new InMemoryRepository<InvoiceEInvoice>(new InvoiceEInvoice(7, "ES", "facturae-3.2.2", [1], "application/xml", "xsig"));
        var submissions = new InMemoryRepository<EInvoiceSubmission>(
            new EInvoiceSubmission(files.Items[0].Id, "face", "Test", "REG1", new DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc)));
        var step = new EInvoiceDeliveryLifecycleStep(files, submissions);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => step.OnDeletingAsync(NewInvoice(), Token));

        ex.Message.ShouldContain("FACE");
        ex.Message.ShouldContain("REG1");
    }
}
