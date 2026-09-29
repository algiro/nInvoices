using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>
/// A customer can have several rates, including several of the same type (say two hourly rates),
/// and a worked day can carry the rate it is billed at. Before, a unique index allowed one rate
/// per type and a second hourly rate failed with a server error.
/// </summary>
[TestFixture]
public sealed class SeveralRatesTests
{
    private const string Alice = "alice-sub";

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

    private async Task<long> AddCustomerAsync()
    {
        var context = ContextFor(Alice);
        var customer = new Customer("Acme", "ACME1", new Address("Main", "1", "Town", "12345", "Italy"));
        context.Customers.Add(customer);
        await context.SaveChangesAsync(Token);
        return customer.Id;
    }

    private static Rate NewRate(long customerId, RateType type, decimal amount, string? name = null)
    {
        var rate = new Rate(customerId, type, new Money(amount, "EUR"));
        rate.SetName(name);
        return rate;
    }

    [Test]
    public async Task Rates_SeveralOfTheSameType_AreAllowed()
    {
        var customerId = await AddCustomerAsync();
        var context = ContextFor(Alice);
        context.Rates.Add(NewRate(customerId, RateType.Hourly, 50m, "Junior"));
        context.Rates.Add(NewRate(customerId, RateType.Hourly, 70m, "Senior"));
        context.Rates.Add(NewRate(customerId, RateType.Daily, 400m));

        await context.SaveChangesAsync(Token);

        var rates = await ContextFor(Alice).Rates.OrderBy(r => r.Id).ToListAsync(Token);
        rates.Select(r => (r.Type, r.Price.Amount, r.Name)).ShouldBe([
            (RateType.Hourly, 50m, "Junior"),
            (RateType.Hourly, 70m, "Senior"),
            (RateType.Daily, 400m, null)]);
    }

    [Test]
    public async Task WorkDay_RemembersItsRate()
    {
        var customerId = await AddCustomerAsync();
        var rate = NewRate(customerId, RateType.Hourly, 70m);
        var context = ContextFor(Alice);
        context.Rates.Add(rate);
        await context.SaveChangesAsync(Token);

        context.WorkDays.Add(new WorkDay(customerId, new DateOnly(2026, 1, 5), hoursWorked: 4m) { RateId = rate.Id });
        context.WorkDays.Add(new WorkDay(customerId, new DateOnly(2026, 1, 6), hoursWorked: 4m));
        await context.SaveChangesAsync(Token);

        var days = await ContextFor(Alice).WorkDays.OrderBy(d => d.Date).ToListAsync(Token);
        days.Select(d => d.RateId).ShouldBe([rate.Id, null]);
    }

    [Test]
    public async Task Migrations_AllowSeveralRatesOfTheSameType()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(Token);
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await context.Database.MigrateAsync(Token);

        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Customers" ("Id","OwnerId","Name","FiscalId","Locale","CreatedAt","Address_Street","Address_HouseNumber","Address_City","Address_ZipCode","Address_Country")
            VALUES (1,'alice','A','A1','en-US','2026-01-01','S','1','C','Z','IT');
            INSERT INTO "Rates" ("OwnerId","CustomerId","Type","PriceAmount","PriceCurrency","IsActive","CreatedAt","Name")
            VALUES ('alice',1,'Hourly',50,'EUR',1,'2026-01-01','Junior'),
                   ('alice',1,'Hourly',70,'EUR',1,'2026-01-01','Senior');
            """, Token);

        (await context.Rates.IgnoreQueryFilters().CountAsync(Token)).ShouldBe(2);
    }
}
