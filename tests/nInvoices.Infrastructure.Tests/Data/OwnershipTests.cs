using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>
/// Checks, against a real (in-memory) SQLite database, that each user only sees and changes
/// their own data.
/// </summary>
[TestFixture]
public sealed class OwnershipTests
{
    private const string Alice = "alice-sub";
    private const string Bob = "bob-sub";

    private SqliteConnection _connection = null!;
    private readonly List<ApplicationDbContext> _contexts = [];

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(Token);
        await ContextFor(null).Database.EnsureCreatedAsync(Token);
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (var context in _contexts)
            await context.DisposeAsync();
        _contexts.Clear();
        await _connection.DisposeAsync();
    }

    private ApplicationDbContext ContextFor(string? userId)
    {
        var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options,
            TestEncryption.Encryptor, new TestUserContext(userId));
        _contexts.Add(context);
        return context;
    }

    private static Customer NewCustomer(string name) =>
        new(name, name.ToUpperInvariant(), new Address("Main", "1", "Town", "12345", "Italy"));

    private async Task<long> AddCustomerAsync(string userId, string name)
    {
        var context = ContextFor(userId);
        var customer = NewCustomer(name);
        context.Customers.Add(customer);
        await context.SaveChangesAsync(Token);
        return customer.Id;
    }

    [Test]
    public async Task SaveChangesAsync_NewEntity_StampsCurrentUser()
    {
        var context = ContextFor(Alice);
        var customer = NewCustomer("Acme");
        // The project references a customer inserted in the same save
        customer.Projects.Add(new Project { Name = "Build", IsActive = true });
        context.Customers.Add(customer);

        await context.SaveChangesAsync(Token);

        customer.OwnerId.ShouldBe(Alice);
        customer.Projects.Single().OwnerId.ShouldBe(Alice);
    }

    [Test]
    public async Task Query_OtherUsersRows_AreNotReturned()
    {
        var aliceCustomer = await AddCustomerAsync(Alice, "Acme");
        await AddCustomerAsync(Bob, "Northwind");

        var names = await ContextFor(Alice).Customers.Select(c => c.Name).ToListAsync(Token);
        var found = await ContextFor(Bob).Customers.FindAsync([aliceCustomer], Token);

        names.ShouldBe(["Acme"]);
        found.ShouldBeNull();
    }

    [Test]
    public async Task Query_WithoutUser_ReturnsNothing()
    {
        await AddCustomerAsync(Alice, "Acme");

        (await ContextFor(null).Customers.CountAsync(Token)).ShouldBe(0);
    }

    [Test]
    public async Task SaveChangesAsync_WithoutUser_Throws()
    {
        var context = ContextFor(null);
        context.Customers.Add(NewCustomer("Acme"));

        await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(Token));
    }

    [Test]
    public async Task SaveChangesAsync_ReferencingOtherUsersCustomer_Throws()
    {
        var aliceCustomer = await AddCustomerAsync(Alice, "Acme");
        var bob = ContextFor(Bob);
        bob.Rates.Add(new Rate(aliceCustomer, RateType.Daily, new Money(1m, "EUR")));

        await Should.ThrowAsync<UnauthorizedAccessException>(() => bob.SaveChangesAsync(Token));
    }

    [Test]
    public async Task SaveChangesAsync_UpdatingOtherUsersRow_Throws()
    {
        var aliceCustomer = await AddCustomerAsync(Alice, "Acme");
        var stolen = await ContextFor(null).Customers.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(c => c.Id == aliceCustomer, Token);
        var bob = ContextFor(Bob);
        stolen.Update("Hacked", stolen.FiscalId, stolen.Address, stolen.Locale);
        bob.Customers.Update(stolen);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => bob.SaveChangesAsync(Token));
    }

    [Test]
    public async Task SaveChangesAsync_ChangingOwner_Throws()
    {
        await AddCustomerAsync(Alice, "Acme");
        var alice = ContextFor(Alice);
        var customer = await alice.Customers.SingleAsync(Token);
        customer.OwnerId = Bob;

        await Should.ThrowAsync<UnauthorizedAccessException>(() => alice.SaveChangesAsync(Token));
    }

    [Test]
    public async Task SaveChangesAsync_SameHolidayCountryForTwoUsers_IsAllowed()
    {
        foreach (var user in new[] { Alice, Bob })
        {
            var context = ContextFor(user);
            context.HolidayCalendars.Add(new HolidayCalendar("IT"));
            await context.SaveChangesAsync(Token);
        }

        (await ContextFor(null).HolidayCalendars.IgnoreQueryFilters().CountAsync(Token)).ShouldBe(2);
    }

    [Test]
    public async Task InvoiceSequence_IsKeptPerUser()
    {
        foreach (var (user, value) in new[] { (Alice, 10), (Bob, 3) })
        {
            var context = ContextFor(user);
            context.InvoiceSequences.Add(new InvoiceSequence(value));
            await context.SaveChangesAsync(Token);
        }

        (await ContextFor(Alice).InvoiceSequences.SingleAsync(Token)).CurrentValue.ShouldBe(10);
        (await ContextFor(Bob).InvoiceSequences.SingleAsync(Token)).CurrentValue.ShouldBe(3);
    }

    [Test]
    public async Task AssignUnownedDataAsync_WithOwner_GivesLegacyRowsToThatUser()
    {
        await AddCustomerAsync(Alice, "Acme");
        var customerId = await AddCustomerAsync(Bob, "Legacy");
        var invoice = new Invoice(
            customerId,
            InvoiceNumber.Generate("26-01-001", 1, new DateTime(2026, 1, 31), null),
            InvoiceType.Monthly,
            new DateOnly(2026, 1, 31),
            new Money(100m, "EUR"),
            "EUR");
        invoice.AddTaxes(Money.Zero("EUR"));
        var bob = ContextFor(Bob);
        bob.Invoices.Add(invoice);
        await bob.SaveChangesAsync(Token);
        // Simulate rows written before ownership existed
        var raw = ContextFor(null);
        await raw.Customers.IgnoreQueryFilters().Where(c => c.OwnerId == Bob)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.OwnerId, string.Empty), Token);
        await raw.Invoices.IgnoreQueryFilters()
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.OwnerId, string.Empty), Token);

        var before = await raw.AssignUnownedDataAsync(null, Token);
        var after = await raw.AssignUnownedDataAsync(Alice, Token);

        before.ShouldBe((0, 2));
        after.ShouldBe((2, 0));
        (await ContextFor(Alice).Customers.CountAsync(Token)).ShouldBe(2);
        (await ContextFor(Alice).Invoices.CountAsync(Token)).ShouldBe(1);
    }

    /// <summary>Saves the rows as Bob, then clears their owner as if written before ownership existed.</summary>
    private async Task AddLegacyAsync(params object[] entities)
    {
        var bob = ContextFor(Bob);
        bob.AddRange(entities);
        await bob.SaveChangesAsync(Token);
        var raw = ContextFor(null);
        await raw.HolidayCalendars.IgnoreQueryFilters().Where(c => c.OwnerId == Bob)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.OwnerId, string.Empty), Token);
        await raw.HolidayRules.IgnoreQueryFilters().Where(r => r.OwnerId == Bob)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.OwnerId, string.Empty), Token);
        await raw.InvoiceSequences.IgnoreQueryFilters().Where(s => s.OwnerId == Bob)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.OwnerId, string.Empty), Token);
    }

    [Test]
    public async Task AssignUnownedDataAsync_OwnerAlreadyHasCalendarForCountry_KeepsLegacyCalendar()
    {
        await AddLegacyAsync(new HolidayCalendar("IT") { Rules = [HolidayRule.Fixed("Sant'Ambrogio", 12, 7)] });
        // Created for Alice when she signed in before the legacy data was hers
        var alice = ContextFor(Alice);
        alice.HolidayCalendars.Add(new HolidayCalendar("IT") { Rules = [HolidayRule.Fixed("Natale", 12, 25)] });
        alice.HolidayCalendars.Add(new HolidayCalendar("ES"));
        await alice.SaveChangesAsync(Token);

        await ContextFor(null).AssignUnownedDataAsync(Alice, Token);

        var calendars = await ContextFor(Alice).HolidayCalendars.OrderBy(c => c.CountryCode).ToListAsync(Token);
        calendars.Select(c => c.CountryCode).ShouldBe(["ES", "IT"]);
        var rules = await ContextFor(Alice).HolidayRules.ToListAsync(Token);
        rules.Select(r => r.Name).ShouldBe(["Sant'Ambrogio"]);
        rules.Single().HolidayCalendarId.ShouldBe(calendars[1].Id);
    }

    [TestCase(12, 5, 12)]
    [TestCase(3, 7, 7)]
    public async Task AssignUnownedDataAsync_OwnerAlreadyHasSequence_KeepsHigherValue(int legacy, int own, int expected)
    {
        await AddLegacyAsync(new InvoiceSequence(legacy));
        var alice = ContextFor(Alice);
        alice.InvoiceSequences.Add(new InvoiceSequence(own));
        await alice.SaveChangesAsync(Token);

        var (assigned, _) = await ContextFor(null).AssignUnownedDataAsync(Alice, Token);

        assigned.ShouldBe(1);
        (await ContextFor(Alice).InvoiceSequences.SingleAsync(Token)).CurrentValue.ShouldBe(expected);
    }

    [Test]
    public async Task Migrations_OnFreshDatabase_LeaveNoUnownedRows()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(Token);
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options, TestEncryption.Encryptor);

        await context.Database.MigrateAsync(Token);

        (await context.AssignUnownedDataAsync(null, Token)).ShouldBe((0, 0));
    }

    [Test]
    public async Task Migrations_PerCustomerSequences_BecomeOneSequencePerUser()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(Token);
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options, TestEncryption.Encryptor);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20260929101858_AddRateAndHoursToInvoice", Token);

        // As left by the per-customer numbering: alice has three counters and a pattern shared by
        // her customers, bob two different patterns
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Customers" ("Id","OwnerId","Name","FiscalId","Locale","CreatedAt","NumberFormat","Address_Street","Address_HouseNumber","Address_City","Address_ZipCode","Address_Country")
            VALUES (1,'alice','A1','A1','en-US','2026-01-01','A-{{NUMBER:000}}','S','1','C','Z','IT'),
                   (2,'alice','A2','A2','en-US','2026-01-01','A-{{NUMBER:000}}','S','1','C','Z','IT'),
                   (3,'alice','A3','A3','en-US','2026-01-01',NULL,'S','1','C','Z','IT'),
                   (4,'bob','B1','B1','en-US','2026-01-01','X-{{NUMBER}}','S','1','C','Z','IT'),
                   (5,'bob','B2','B2','en-US','2026-01-01','Y-{{NUMBER}}','S','1','C','Z','IT');
            DELETE FROM "InvoiceSequence";
            INSERT INTO "InvoiceSequence" ("OwnerId","CustomerId","CurrentValue","CreatedAt")
            VALUES ('alice',1,12,'2026-01-01'), ('alice',2,14,'2026-01-01'), ('alice',3,12,'2026-01-01'),
                   ('bob',4,3,'2026-01-01'), ('bob',5,3,'2026-01-01');
            """, Token);

        await migrator.MigrateAsync(null, Token);

        var rows = await context.InvoiceSequences.IgnoreQueryFilters()
            .OrderBy(x => x.OwnerId)
            .Select(x => new { x.OwnerId, x.CurrentValue, x.NumberFormat })
            .ToListAsync(Token);
        rows.Select(r => (r.OwnerId, r.CurrentValue, r.NumberFormat))
            .ShouldBe([("alice", 14, "A-{NUMBER:000}"), ("bob", 3, (string?)null)]);
    }
}
