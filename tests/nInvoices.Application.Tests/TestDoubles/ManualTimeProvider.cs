namespace nInvoices.Application.Tests.TestDoubles;

/// <summary>A clock whose high-resolution timestamp only moves when the test advances it.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => 1000; // one tick = one millisecond

    public override long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan elapsed) => _timestamp += (long)elapsed.TotalMilliseconds;
}
