using Mediator;
using nInvoices.Application.Telemetry;

namespace nInvoices.Application.Behaviors;

/// <summary>
/// Times every request that goes through the pipeline and records it in
/// <see cref="AppMetrics.RequestDuration"/>, tagged with the request's name and how it ended. It is the
/// outermost behaviour, so what it measures includes validation, and a request refused by a validator is
/// counted too. A new feature needs nothing for this: its requests are measured by being requests.
/// </summary>
public sealed class MetricsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    // The type's name: the same for every instance, and small (one per use case)
    private static readonly string RequestName = typeof(TRequest).Name;

    private readonly TimeProvider _timeProvider;

    /// <param name="timeProvider">The clock; the system's when none is registered, so measuring never stops a request.</param>
    public MetricsBehavior(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        var started = _timeProvider.GetTimestamp();
        var outcome = AppMetrics.Outcome.Success;

        try
        {
            return await next(request, cancellationToken);
        }
        catch (Exception exception)
        {
            outcome = AppMetrics.OutcomeOf(exception);
            throw;
        }
        finally
        {
            AppMetrics.RequestDuration.Record(
                _timeProvider.GetElapsedTime(started).TotalSeconds,
                new KeyValuePair<string, object?>("request", RequestName),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }
}
