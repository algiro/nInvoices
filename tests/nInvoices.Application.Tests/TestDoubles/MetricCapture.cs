using System.Diagnostics.Metrics;
using nInvoices.Application.Telemetry;

namespace nInvoices.Application.Tests.TestDoubles;

/// <summary>Collects what is recorded in one of the application's histograms while it is alive.</summary>
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
        _listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            lock (_recorded)
                _recorded.Add(new Recorded(value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
        });
        _listener.Start();
    }

    public sealed record Recorded(double Seconds, IReadOnlyDictionary<string, object?> Tags)
    {
        public string? Tag(string name) => Tags.GetValueOrDefault(name) as string;
    }

    public IReadOnlyList<Recorded> Where(string tag, string value)
    {
        lock (_recorded)
            return _recorded.Where(r => r.Tag(tag) == value).ToList();
    }

    public void Dispose() => _listener.Dispose();
}
