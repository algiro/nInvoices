using System.Diagnostics.Metrics;
using nInvoices.Application.Telemetry;

namespace nInvoices.Application.Tests.TestDoubles;

/// <summary>Collects what is recorded in one of the application's instruments (a histogram or a counter) while it is alive.</summary>
public sealed class MetricCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<Recorded> _recorded = [];

    public MetricCapture(string instrumentName)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AppMetrics.MeterName && instrument.Name == instrumentName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<double>((_, value, tags, _) => Add(value, tags));
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) => Add(value, tags));
        _listener.Start();
    }

    /// <summary>One measurement: a duration in seconds for a histogram, an amount for a counter.</summary>
    public sealed record Recorded(double Value, IReadOnlyDictionary<string, object?> Tags)
    {
        public double Seconds => Value;

        public string? Tag(string name) => Tags.GetValueOrDefault(name) as string;
    }

    private void Add(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var recorded = new Recorded(value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value));
        lock (_recorded)
            _recorded.Add(recorded);
    }

    public IReadOnlyList<Recorded> Where(string tag, string value)
    {
        lock (_recorded)
            return _recorded.Where(r => r.Tag(tag) == value).ToList();
    }

    /// <summary>The sum of what was counted with all of these tag values.</summary>
    public double Total(params (string Tag, string Value)[] tags)
    {
        lock (_recorded)
            return _recorded.Where(r => tags.All(t => r.Tag(t.Tag) == t.Value)).Sum(r => r.Value);
    }

    public IReadOnlyList<Recorded> All()
    {
        lock (_recorded)
            return _recorded.ToList();
    }

    public void Dispose() => _listener.Dispose();
}
