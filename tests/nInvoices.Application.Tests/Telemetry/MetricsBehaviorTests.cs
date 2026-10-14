using FluentValidation;
using FluentValidation.Results;
using Mediator;
using nInvoices.Application.Behaviors;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Telemetry;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Exceptions;
using Shouldly;

namespace nInvoices.Application.Tests.Telemetry;

/// <summary>What the application measures about its own requests: how long, and how each one ended.</summary>
[TestFixture]
public sealed class MetricsBehaviorTests
{
    private sealed record PingQuery : IRequest<string>;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static MetricCapture.Recorded Only(MetricCapture capture, string request) =>
        capture.Where("request", request).ShouldHaveSingleItem();

    private static async ValueTask<string> Run(
        MetricsBehavior<PingQuery, string> behavior,
        Func<ValueTask<string>> handler) =>
        await behavior.Handle(new PingQuery(), (_, _) => handler(), Token);

    [Test]
    public async Task Handle_Success_RecordsTheDurationWithTheRequestName()
    {
        var clock = new ManualTimeProvider();
        using var capture = new MetricCapture("ninvoices.request.duration");
        var behavior = new MetricsBehavior<PingQuery, string>(clock);

        var result = await Run(behavior, () =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(250));
            return ValueTask.FromResult("pong");
        });

        result.ShouldBe("pong");
        var recorded = Only(capture, nameof(PingQuery));
        recorded.Seconds.ShouldBe(0.25, tolerance: 0.0001);
        recorded.Tag("outcome").ShouldBe("success");
    }

    [TestCaseSource(nameof(Failures))]
    public async Task Handle_Failure_RecordsHowItEnded_AndRethrows(Exception failure, string expectedOutcome)
    {
        var clock = new ManualTimeProvider();
        using var capture = new MetricCapture("ninvoices.request.duration");
        var behavior = new MetricsBehavior<PingQuery, string>(clock);

        // Caught by hand: Should.ThrowAsync would swap a cancellation for its own exception
        Exception? thrown = null;
        try
        {
            await Run(behavior, () =>
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                throw failure;
            });
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        thrown.ShouldBeSameAs(failure); // not swallowed, not wrapped
        var recorded = Only(capture, nameof(PingQuery));
        recorded.Tag("outcome").ShouldBe(expectedOutcome);
        recorded.Seconds.ShouldBe(1.0, tolerance: 0.0001); // a failure takes time too
    }

    private static IEnumerable<TestCaseData> Failures()
    {
        yield return new TestCaseData(new ValidationException([new ValidationFailure("Year", "bad")]), "invalid").SetName("Validation_IsInvalid");
        yield return new TestCaseData(new NotFoundException("no such invoice"), "not_found").SetName("NotFound_IsNotFound");
        yield return new TestCaseData(new DomainException("not allowed"), "rejected").SetName("Domain_IsRejected");
        yield return new TestCaseData(new ArgumentException("negative amount"), "rejected").SetName("ArgumentFromAnEntity_IsRejected");
        yield return new TestCaseData(new OperationCanceledException(), "canceled").SetName("Cancelled_IsCanceled");
        yield return new TestCaseData(new InvalidOperationException("a bug"), "error").SetName("Anything_IsError");
    }

    [Test]
    public void Meter_NameStartsWithWhatServiceDefaultsSubscribesTo()
    {
        // ServiceDefaults registers AddMeter("nInvoices*"): a renamed meter would silently stop exporting
        AppMetrics.Meter.Name.ShouldStartWith("nInvoices");
        AppMetrics.RequestDuration.Meter.ShouldBeSameAs(AppMetrics.Meter);
        AppMetrics.PdfRenderDuration.Meter.ShouldBeSameAs(AppMetrics.Meter);
    }
}
