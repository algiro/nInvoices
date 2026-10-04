using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Encryption;
using nInvoices.Infrastructure.Services;
using nInvoices.Infrastructure.Tests.Data;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Encryption;

/// <summary>
/// Checks, against a real (in-memory) SQLite database, that sensitive columns are stored encrypted
/// and read back in clear, and that data stored before encryption gets encrypted in place.
/// </summary>
[TestFixture]
public sealed class EncryptedColumnsTests
{
    private const string Alice = "alice-sub";

    private SqliteConnection _connection = null!;
    private ServiceProvider _provider = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private sealed class NoHttp : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(Token);

        var services = new ServiceCollection();
        services.AddScoped<OwnerOverride>();
        services.AddSingleton<IHttpContextAccessor, NoHttp>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton(TestEncryption.Encryptor);
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync(Token);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private ApplicationDbContext ContextFor(string userId) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options,
            TestEncryption.Encryptor, new TestUserContext(userId));

    private async Task<string?> RawAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(Token))?.ToString();
    }

    private async Task<(long CustomerId, long RateId)> AddCustomerWithRateAsync()
    {
        await using var context = ContextFor(Alice);
        var customer = new Customer("Acme S.p.A.", "IT01234567890", new Address("Via Roma", "10", "Milano", "20100", "Italy"))
        {
            Email = "billing@acme.example"
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync(Token);

        var rate = new Rate(customer.Id, RateType.Daily, new Money(450.50m, "EUR"));
        context.Rates.Add(rate);
        await context.SaveChangesAsync(Token);
        return (customer.Id, rate.Id);
    }

    [Test]
    public async Task SaveChangesAsync_SensitiveColumns_AreStoredEncrypted()
    {
        await AddCustomerWithRateAsync();

        var name = await RawAsync("""SELECT "Name" FROM "Customers" """);
        var city = await RawAsync("""SELECT "Address_City" FROM "Customers" """);
        var price = await RawAsync("""SELECT "PriceAmount" FROM "Rates" """);

        foreach (var stored in new[] { name, city, price })
        {
            stored.ShouldNotBeNull();
            stored.ShouldStartWith(FieldEncryptor.TextPrefix);
        }
        name.ShouldNotContain("Acme");
        // Not sensitive: stays readable
        (await RawAsync("""SELECT "PriceCurrency" FROM "Rates" """)).ShouldBe("EUR");
    }

    [Test]
    public async Task Query_EncryptedColumns_AreReadInClear()
    {
        var (customerId, rateId) = await AddCustomerWithRateAsync();

        await using var context = ContextFor(Alice);
        var customer = await context.Customers.SingleAsync(c => c.Id == customerId, Token);
        var rate = await context.Rates.SingleAsync(r => r.Id == rateId, Token);

        customer.Name.ShouldBe("Acme S.p.A.");
        customer.FiscalId.ShouldBe("IT01234567890");
        customer.Email.ShouldBe("billing@acme.example");
        customer.Address.ShouldBe(new Address("Via Roma", "10", "Milano", "20100", "Italy"));
        rate.Price.ShouldBe(new Money(450.50m, "EUR"));
    }

    [Test]
    public async Task Query_ComparingAnEncryptedColumn_FailsLoudly()
    {
        await AddCustomerWithRateAsync();
        await using var context = ContextFor(Alice);

        // Could never match (random nonce): refused instead of silently finding nothing
        await Should.ThrowAsync<InvalidOperationException>(() =>
            context.Customers.Where(c => c.FiscalId == "IT01234567890").ToListAsync(Token));
    }

    [Test]
    public async Task EncryptLegacyDataAsync_PlainValues_AreEncryptedInPlace()
    {
        var (customerId, rateId) = await AddCustomerWithRateAsync();
        // As an older version stored them
        await RawAsync("""UPDATE "Customers" SET "Name" = 'Legacy Ltd', "Address_City" = 'Torino', "Email" = NULL""");
        await RawAsync("""UPDATE "Rates" SET "PriceAmount" = '300.25'""");

        var encrypted = await _provider.EncryptLegacyDataAsync(Token);

        encrypted.ShouldBe(2);
        (await RawAsync("""SELECT "Name" FROM "Customers" """)).ShouldStartWith(FieldEncryptor.TextPrefix);
        (await RawAsync("""SELECT "Address_City" FROM "Customers" """)).ShouldStartWith(FieldEncryptor.TextPrefix);
        (await RawAsync("""SELECT "PriceAmount" FROM "Rates" """)).ShouldStartWith(FieldEncryptor.TextPrefix);

        await using var context = ContextFor(Alice);
        var customer = await context.Customers.SingleAsync(c => c.Id == customerId, Token);
        customer.Name.ShouldBe("Legacy Ltd");
        customer.Address.City.ShouldBe("Torino");
        customer.Email.ShouldBeNull();
        customer.UpdatedAt.ShouldBeNull();
        (await context.Rates.SingleAsync(r => r.Id == rateId, Token)).Price.Amount.ShouldBe(300.25m);
    }

    [Test]
    public async Task EncryptLegacyDataAsync_NothingLeftInClear_ChangesNothing()
    {
        await AddCustomerWithRateAsync();
        var before = await RawAsync("""SELECT "Name" FROM "Customers" """);

        var encrypted = await _provider.EncryptLegacyDataAsync(Token);

        encrypted.ShouldBe(0);
        (await RawAsync("""SELECT "Name" FROM "Customers" """)).ShouldBe(before);
    }
}
