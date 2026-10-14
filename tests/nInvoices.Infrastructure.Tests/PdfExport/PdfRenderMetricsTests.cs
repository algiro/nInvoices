using System.Diagnostics.Metrics;
using Moq;
using nInvoices.Application.Services;
using nInvoices.Application.Telemetry;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.PdfExport;
using QuestPDF.Infrastructure;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.PdfExport;

/// <summary>How long rendering a PDF takes, by engine, document and outcome (<c>ninvoices.pdf.render.duration</c>).</summary>
[TestFixture]
public sealed class PdfRenderMetricsTests
{
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => 1000; // one tick = one millisecond

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan elapsed) => _timestamp += (long)elapsed.TotalMilliseconds;
    }

    private sealed record Recorded(double Seconds, IReadOnlyDictionary<string, string?> Tags);

    private sealed class Capture : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<Recorded> _recorded = [];

        public Capture()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AppMetrics.MeterName && instrument.Name == "ninvoices.pdf.render.duration")
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            {
                lock (_recorded)
                    _recorded.Add(new Recorded(value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value as string)));
            });
            _listener.Start();
        }

        public IReadOnlyList<Recorded> Of(string document)
        {
            lock (_recorded)
                return _recorded.Where(r => r.Tags["document"] == document).ToList();
        }

        public void Dispose() => _listener.Dispose();
    }

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [OneTimeSetUp]
    public void QuestPdfLicense() => QuestPDF.Settings.License = LicenseType.Community; // as AddPdfExport does

    private static Invoice Invoice(InvoiceType type) =>
        new(1, new InvoiceNumber("26-10-001"), type, new DateOnly(2026, 10, 6), new Money(500m, "EUR"), "EUR");

    [Test]
    public async Task BuiltInInvoiceLayout_IsTimedAsQuestPdf()
    {
        using var capture = new Capture();
        var service = new PdfExportService(Mock.Of<IHtmlToPdfConverter>(), TimeProvider.System);

        await service.GenerateInvoicePdfAsync(Invoice(InvoiceType.OneTime), Token);

        var recorded = capture.Of("invoice").ShouldHaveSingleItem();
        recorded.Tags["engine"].ShouldBe("questpdf");
        recorded.Tags["outcome"].ShouldBe("success");
        recorded.Tags.ContainsKey("browser").ShouldBeFalse();
        recorded.Seconds.ShouldBeGreaterThan(0);
    }

    [Test]
    public void Calendar_IsTimedAsQuestPdf_AndARefusalIsAnOutcome()
    {
        using var capture = new Capture();
        var service = new PdfExportService(Mock.Of<IHtmlToPdfConverter>(), TimeProvider.System);
        var monthly = Invoice(InvoiceType.Monthly);
        monthly.SetMonthlyInvoiceDetails(2026, 9, 2);

        service.GenerateWorkedDaysCalendarPdf(monthly);

        capture.Of("calendar").ShouldHaveSingleItem().Tags["outcome"].ShouldBe("success");
        // Refused before drawing: nothing was rendered, so nothing is measured
        Should.Throw<DomainException>(() => service.GenerateWorkedDaysCalendarPdf(Invoice(InvoiceType.OneTime)));
        capture.Of("calendar").Count.ShouldBe(1);
    }

    [Test]
    public async Task RecordAsync_TagsTheBrowserState_AndMeasuresTheWait()
    {
        var clock = new ManualClock();
        using var capture = new Capture();

        var bytes = await PdfMetrics.RecordAsync(clock, "chrome", "html-test-cold", "cold", async () =>
        {
            clock.Advance(TimeSpan.FromSeconds(3.5));
            await Task.Yield();
            return new byte[] { 1 };
        });

        bytes.ShouldBe(new byte[] { 1 });
        var recorded = capture.Of("html-test-cold").ShouldHaveSingleItem();
        recorded.Seconds.ShouldBe(3.5, tolerance: 0.001);
        recorded.Tags["engine"].ShouldBe("chrome");
        recorded.Tags["browser"].ShouldBe("cold");
        recorded.Tags["outcome"].ShouldBe("success");
    }

    [Test]
    public async Task RecordAsync_AFailedRender_IsRecordedAsAnErrorAndRethrown()
    {
        var clock = new ManualClock();
        using var capture = new Capture();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await PdfMetrics.RecordAsync<byte[]>(clock, "chrome", "html-test-error", "warm", () =>
            {
                clock.Advance(TimeSpan.FromSeconds(2));
                throw new InvalidOperationException("browser crashed");
            }));

        var recorded = capture.Of("html-test-error").ShouldHaveSingleItem();
        recorded.Tags["outcome"].ShouldBe("error");
        recorded.Seconds.ShouldBe(2.0, tolerance: 0.001);
    }
}
