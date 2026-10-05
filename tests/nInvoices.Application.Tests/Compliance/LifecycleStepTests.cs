using Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using nInvoices.Application.Compliance.Spain.Verifactu;
using nInvoices.Application.Features.Invoices;
using nInvoices.Application.Features.Invoices.Commands;
using nInvoices.Application.Models;
using nInvoices.Application.Services;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance;

[TestFixture]
public sealed class LifecycleStepTests
{
    private InMemoryRepository<Invoice> _invoices = null!;
    private InMemoryRepository<Customer> _customers = null!;
    private Mock<IInvoiceNumbering> _numbering = null!;
    private Mock<IDraftInvoiceSynchronizer> _drafts = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private Mock<IInvoiceLifecycleStep> _step = null!;
    private readonly List<string> _calls = [];

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public void SetUp()
    {
        _calls.Clear();
        _customers = new InMemoryRepository<Customer>(
            new Customer("Cliente", "A58818501", new Address("Calle", "1", "Madrid", "28001", "Spain")) { Id = 1 });
        _invoices = new InMemoryRepository<Invoice>();

        _numbering = new Mock<IInvoiceNumbering>();
        _numbering.Setup(n => n.TakeAsync(It.IsAny<Customer>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceNumber("26-10-001"));

        _drafts = new Mock<IDraftInvoiceSynchronizer>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => _calls.Add("save")).ReturnsAsync(1);
        _step = new Mock<IInvoiceLifecycleStep>();
    }

    private Invoice Draft(long id = 1)
    {
        var invoice = new Invoice(1, new InvoiceNumber("DRAFT"), InvoiceType.OneTime, new DateOnly(2026, 10, 3), new Money(100m, "EUR"), "EUR") { Id = id };
        _invoices.Items.Add(invoice);
        return invoice;
    }

    private FinalizeInvoiceCommandHandler Finalizer() =>
        new(_invoices, _customers, _numbering.Object, _drafts.Object, _unitOfWork.Object, Mock.Of<IPublisher>(), [_step.Object]);

    // --- Finalizing -----------------------------------------------------------------------------

    [Test]
    public async Task Finalize_RunsTheStep_BeforeTheInvoiceIsSaved()
    {
        Draft();
        _step.Setup(s => s.OnFinalizingAsync(It.IsAny<Invoice>(), It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("step")).ReturnsAsync(false);

        await Finalizer().Handle(new FinalizeInvoiceCommand(1), Token);

        _calls.ShouldBe(["step", "save"]);
        // The step sees the invoice with its final number
        _step.Verify(s => s.OnFinalizingAsync(
            It.Is<Invoice>(i => i.Number.ToString() == "26-10-001" && i.Status == InvoiceStatus.Finalized),
            It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Finalize_StepThatRefuses_StopsTheFinalization_WithNothingSaved()
    {
        Draft();
        _step.Setup(s => s.OnFinalizingAsync(It.IsAny<Invoice>(), It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VerifactuException([new nInvoices.Core.Compliance.ComplianceIssue(null, "no VAT line")]));

        await Should.ThrowAsync<VerifactuException>(() => Finalizer().Handle(new FinalizeInvoiceCommand(1), Token).AsTask());

        _calls.ShouldBeEmpty();
    }

    [Test]
    public async Task Finalize_StepChangingTheDocument_RerendersIt_EvenWithTheNumberUnchanged()
    {
        var invoice = Draft();
        invoice.Number = new InvoiceNumber("26-10-001"); // the draft already showed the number it takes
        _step.Setup(s => s.OnFinalizingAsync(It.IsAny<Invoice>(), It.IsAny<Customer>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Finalizer().Handle(new FinalizeInvoiceCommand(1), Token);

        _drafts.Verify(d => d.RerenderAsync(It.Is<IEnumerable<long>>(ids => ids.SequenceEqual(new[] { 1L })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Finalize_NoSteps_BehavesAsBefore()
    {
        var invoice = Draft();
        invoice.Number = new InvoiceNumber("26-10-001");

        await new FinalizeInvoiceCommandHandler(_invoices, _customers, _numbering.Object, _drafts.Object, _unitOfWork.Object, Mock.Of<IPublisher>())
            .Handle(new FinalizeInvoiceCommand(1), Token);

        _drafts.Verify(d => d.RerenderAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()), Times.Never);
        invoice.Status.ShouldBe(InvoiceStatus.Finalized);
    }

    // --- Cancelling and deleting ---------------------------------------------------------------------

    [Test]
    public async Task Cancel_RunsTheStep_BeforeTheSave()
    {
        var invoice = Draft();
        invoice.FinalizeInvoice();
        _step.Setup(s => s.OnCancellingAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>())).Callback(() => _calls.Add("step")).Returns(Task.CompletedTask);

        await new CancelInvoiceCommandHandler(_invoices, _unitOfWork.Object, [_step.Object]).Handle(new CancelInvoiceCommand(1), Token);

        _calls.ShouldBe(["step", "save"]);
        invoice.Status.ShouldBe(InvoiceStatus.Cancelled);
    }

    [Test]
    public async Task Delete_StepThatRefuses_KeepsTheInvoice()
    {
        var invoice = Draft();
        invoice.FinalizeInvoice();
        _step.Setup(s => s.OnDeletingAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("part of the chain"));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            new DeleteInvoiceCommandHandler(_invoices, _unitOfWork.Object, [_step.Object]).Handle(new DeleteInvoiceCommand(1, Force: true), Token).AsTask());

        _invoices.Items.ShouldContain(invoice);
        _calls.ShouldBeEmpty();
    }

    [Test]
    public async Task Delete_Draft_WithNoStepObjecting_IsDeleted()
    {
        Draft();

        await new DeleteInvoiceCommandHandler(_invoices, _unitOfWork.Object, [_step.Object]).Handle(new DeleteInvoiceCommand(1), Token);

        _invoices.Items.ShouldBeEmpty();
    }

    // --- The data a template can print ---------------------------------------------------------------

    [Test]
    public async Task Contributor_AddsTheQrCodeUnderComplianceVerifactu()
    {
        var verifactu = new Mock<IVerifactuService>();
        var record = new VerifactuRecord(
            3, VerifactuRecordKind.Issued, 1, "12345678Z", "26-10-001", "03-10-2026", "F1", "21.00", "121.00",
            "", "2026-10-03T11:30:00+02:00", new string('A', 64), "<x/>");
        verifactu.Setup(v => v.GetRecordsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([record]);
        verifactu.Setup(v => v.QrUrl(record)).Returns("https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR?nif=12345678Z");
        var invoice = new Invoice(1, new InvoiceNumber("26-10-001"), InvoiceType.OneTime, new DateOnly(2026, 10, 3), new Money(100m, "EUR"), "EUR") { Id = 1 };

        var model = await new VerifactuTemplateContributor(verifactu.Object).ContributeAsync(invoice, new InvoiceTemplateModel(), Token);

        var contribution = model.Compliance["verifactu"].ShouldBeOfType<VerifactuTemplateModel>();
        contribution.IsRecorded.ShouldBeTrue();
        contribution.Sequence.ShouldBe(3);
        contribution.QrSvg.ShouldStartWith("<svg");
        contribution.QrDataUri.ShouldStartWith("data:image/svg+xml;base64,");
        contribution.Heading.ShouldBe("QR tributario:");
        contribution.Legend.ShouldBe("Factura verificable en la sede electrónica de la AEAT");
        contribution.IsCancelled.ShouldBeFalse();
    }

    [Test]
    public async Task Contributor_InvoiceWithoutRecord_AddsNothing()
    {
        var verifactu = new Mock<IVerifactuService>();
        verifactu.Setup(v => v.GetRecordsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var invoice = new Invoice(1, new InvoiceNumber("26-10-001"), InvoiceType.OneTime, new DateOnly(2026, 10, 3), new Money(100m, "EUR"), "EUR") { Id = 1 };

        var model = await new VerifactuTemplateContributor(verifactu.Object).ContributeAsync(invoice, new InvoiceTemplateModel(), Token);

        // The entry stays, empty, so a template can print from it without checking
        var contribution = model.Compliance["verifactu"].ShouldBeOfType<VerifactuTemplateModel>();
        contribution.IsRecorded.ShouldBeFalse();
        contribution.QrSvg.ShouldBeEmpty();
    }

    [Test]
    public async Task Template_CanPrintTheQrAndTheLegend_WithTheRealRenderer()
    {
        var imageAssets = new Mock<IRepository<ImageAsset>>();
        imageAssets.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ImageAsset>());
        var renderer = new ScribanTemplateRenderer(Mock.Of<ILogger<ScribanTemplateRenderer>>(), Mock.Of<ILocalizationService>(), imageAssets.Object);
        var contribution = new VerifactuTemplateModel { IsRecorded = true, QrSvg = "<svg>QR</svg>", Sequence = 7 };
        var model = new InvoiceTemplateModel { Compliance = new Dictionary<string, object> { ["verifactu"] = contribution } };

        var html = await renderer.RenderAsync(
            "[[ if compliance.verifactu.isRecorded ]]<p>[[ compliance.verifactu.heading ]]</p>[[ compliance.verifactu.qrSvg ]]<p>[[ compliance.verifactu.legend ]]</p>[[ end ]]",
            model, Token);

        html.ShouldContain("QR tributario:");
        html.ShouldContain("<svg>QR</svg>");
        html.ShouldContain("Factura verificable en la sede electrónica de la AEAT");
    }

    private static ScribanTemplateRenderer Renderer()
    {
        var imageAssets = new Mock<IRepository<ImageAsset>>();
        imageAssets.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ImageAsset>());
        return new ScribanTemplateRenderer(Mock.Of<ILogger<ScribanTemplateRenderer>>(), Mock.Of<ILocalizationService>(), imageAssets.Object);
    }

    [Test]
    public async Task Template_PrintingTheQrWithoutAGuard_PrintsNothingWhenThereIsNoRecord()
    {
        // A draft, or a user without Verifactu: a template that prints the QR unconditionally must not fail
        var html = await Renderer().RenderAsync(
            "<p>[[ compliance.verifactu.qrSvg ]]</p><p>[[ compliance.verifactu.heading ]]</p>", new InvoiceTemplateModel(), Token);

        html.ShouldBe("<p></p><p>QR tributario:</p>");
    }

    [Test]
    public async Task Template_GuardedWithIsRecorded_ShowsTheBlockOnlyWhenThereIsARecord()
    {
        const string template = "[[ if compliance.verifactu.isRecorded ]]QR:[[ compliance.verifactu.qrSvg ]][[ end ]]";
        var recorded = new InvoiceTemplateModel
        {
            Compliance = new Dictionary<string, object> { ["verifactu"] = new VerifactuTemplateModel { IsRecorded = true, QrSvg = "<svg/>" } }
        };

        (await Renderer().RenderAsync(template, new InvoiceTemplateModel(), Token)).ShouldBeEmpty();
        (await Renderer().RenderAsync(template, recorded, Token)).ShouldBe("QR:<svg/>");
    }

    [Test]
    public async Task Preview_InTheTemplateEditor_ShowsASampleQr_WithoutFailing()
    {
        var preview = new TemplatePreviewService(
            Renderer(), new InMemoryRepository<Customer>(), new InMemoryRepository<Rate>(), new InMemoryRepository<Tax>());

        var result = await preview.PreviewInvoiceAsync("<div>[[ compliance.verifactu.qrSvg ]]</div>", null, Token);

        result.Errors.ShouldBeEmpty();
        result.Html.ShouldNotBeNull().ShouldContain("<svg");
    }
}
