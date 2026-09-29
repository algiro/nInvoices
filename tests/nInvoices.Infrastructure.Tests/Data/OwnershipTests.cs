using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
            new TestUserContext(userId));
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
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);

        await context.Database.MigrateAsync(Token);

        (await context.AssignUnownedDataAsync(null, Token)).ShouldBe((0, 0));
    }
}
