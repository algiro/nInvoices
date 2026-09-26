namespace nInvoices.Application.Tests.TestDoubles;

/// <summary>A clock that shows whatever time the test sets.</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    public FixedTimeProvider(DateTime utcNow)
    {
        Now = utcNow;
    }

    public DateTime Now { get; set; }

    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
}
