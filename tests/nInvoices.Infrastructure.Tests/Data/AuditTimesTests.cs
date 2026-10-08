using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>
/// CreatedAt and UpdatedAt are stamped by the database context when an entity is saved, from the
/// injected clock: entities never read the clock for them, so a test can set the time.
/// </summary>
[TestFixture]
public sealed class AuditTimesTests
{
    private const string User = "audit-sub";

    private SqliteConnection _connection = null!;
    private ManualClock _clock = null!;
    private readonly List<ApplicationDbContext> _contexts = [];

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private sealed class ManualClock(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
    }

    [SetUp]
    public async Task SetUp()
    {
        _clock = new ManualClock(new DateTime(2026, 12, 31, 23, 30, 0, DateTimeKind.Utc));
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(Token);
        await Context().Database.EnsureCreatedAsync(Token);
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (var context in _contexts)
            await context.DisposeAsync();
        _contexts.Clear();
        await _connection.DisposeAsync();
    }

    private ApplicationDbContext Context()
    {
        var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options,
            TestEncryption.Encryptor, new TestUserContext(User), _clock);
        _contexts.Add(context);
        return context;
    }

    private static Customer NewCustomer() => new("Acme", "ACME1", new Address("Main", "1", "Town", "12345", "Italy"));

    [Test]
    public async Task Added_IsStampedCreated_FromTheClock_AndNotUpdated()
    {
        var customer = NewCustomer();
        var db = Context();
        db.Customers.Add(customer);

        await db.SaveChangesAsync(Token);

        customer.CreatedAt.ShouldBe(_clock.UtcNow);
        customer.UpdatedAt.ShouldBeNull();
    }

    [Test]
    public async Task Modified_IsStampedUpdated_AndKeepsItsCreationTime()
    {
        var customer = NewCustomer();
        var db = Context();
        db.Customers.Add(customer);
        await db.SaveChangesAsync(Token);
        var created = customer.CreatedAt;

        // Across the year boundary: the stamp is the clock's, not the machine's
        _clock.UtcNow = new DateTime(2027, 1, 1, 0, 15, 0, DateTimeKind.Utc);
        customer.SetContact("billing@acme.it", null);
        await db.SaveChangesAsync(Token);

        customer.CreatedAt.ShouldBe(created);
        customer.UpdatedAt.ShouldBe(_clock.UtcNow);
    }

    [Test]
    public async Task Added_WithACreationTimeAlready_KeepsIt()
    {
        // e.g. a record restored with the time it was first created
        var original = new DateTime(2025, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var customer = NewCustomer();
        customer.CreatedAt = original;
        var db = Context();
        db.Customers.Add(customer);

        await db.SaveChangesAsync(Token);

        customer.CreatedAt.ShouldBe(original);
    }

    [Test]
    public async Task Unchanged_IsNotStamped()
    {
        var customer = NewCustomer();
        var db = Context();
        db.Customers.Add(customer);
        await db.SaveChangesAsync(Token);

        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await db.SaveChangesAsync(Token);

        customer.UpdatedAt.ShouldBeNull();
    }
}
