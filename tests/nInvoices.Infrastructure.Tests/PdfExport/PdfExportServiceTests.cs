using System.Text;
using Moq;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.PdfExport;
using QuestPDF.Infrastructure;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.PdfExport;

[TestFixture]
public sealed class PdfExportServiceTests
{
    private Mock<IHtmlToPdfConverter> _converter = null!;
    private PdfExportService _service = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [OneTimeSetUp]
    public void QuestPdfLicense() => QuestPDF.Settings.License = LicenseType.Community; // as AddPdfExport does

    [SetUp]
    public void SetUp()
    {
        _converter = new Mock<IHtmlToPdfConverter>(MockBehavior.Strict);
        _service = new PdfExportService(_converter.Object);
    }

    [Test]
    public async Task InvoicePdf_WithRenderedContent_IsTheConvertedTemplate()
    {
        var invoice = Invoice(InvoiceType.OneTime);
        invoice.SetRenderedContent("<h1>Invoice</h1>");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _converter.Setup(c => c.ConvertAsync("<h1>Invoice</h1>", cancellation.Token)).ReturnsAsync("PDF"u8.ToArray());

        var pdf = await _service.GenerateInvoicePdfAsync(invoice, cancellation.Token);

        Encoding.ASCII.GetString(pdf).ShouldBe("PDF");
        _converter.VerifyAll(); // the caller's token reached the converter
    }

    [Test]
    public async Task InvoicePdf_WaitsForTheConverterWithoutBlocking()
    {
        // The headless browser renders asynchronously: the call must hand back an unfinished task,
        // not hold the thread until the PDF is ready (it used to, with .GetAwaiter().GetResult())
        var invoice = Invoice(InvoiceType.OneTime);
        invoice.SetRenderedContent("<p>x</p>");
        var rendering = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _converter.Setup(c => c.ConvertAsync("<p>x</p>", It.IsAny<CancellationToken>())).Returns(rendering.Task);
        // A blocking implementation would wait here forever: this ends the render after 2 s so it fails instead
        await using var safety = new Timer(_ => rendering.TrySetResult("late"u8.ToArray()), null, 2000, Timeout.Infinite);

        var pdf = _service.GenerateInvoicePdfAsync(invoice, Token);

        pdf.IsCompleted.ShouldBeFalse();
        rendering.TrySetResult("done"u8.ToArray());
        Encoding.ASCII.GetString(await pdf).ShouldBe("done");
    }

    [Test]
    public async Task InvoicePdf_WithoutRenderedContent_IsTheBuiltInLayout()
    {
        var pdf = await _service.GenerateInvoicePdfAsync(Invoice(InvoiceType.OneTime), Token);

        Encoding.ASCII.GetString(pdf, 0, 4).ShouldBe("%PDF");
        _converter.VerifyNoOtherCalls();
    }

    [Test]
    public void CalendarPdf_OfAMonthlyInvoice_IsAPdf()
    {
        var invoice = Invoice(InvoiceType.Monthly);
        invoice.SetMonthlyInvoiceDetails(2026, 9, 2);

        Encoding.ASCII.GetString(_service.GenerateWorkedDaysCalendarPdf(invoice), 0, 4).ShouldBe("%PDF");
    }

    [Test]
    public void CalendarPdf_OfAOneTimeInvoice_IsRefused()
    {
        Should.Throw<DomainException>(() => _service.GenerateWorkedDaysCalendarPdf(Invoice(InvoiceType.OneTime)));
    }

    private static Invoice Invoice(InvoiceType type) =>
        new(1, new InvoiceNumber("26-10-001"), type, new DateOnly(2026, 10, 6), new Money(500m, "EUR"), "EUR");
}
