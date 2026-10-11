using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Encryption;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>The tax prediction's records: stored per user, amounts and descriptions encrypted.</summary>
[TestFixture]
public sealed class TaxRecordsPersistenceTests
{
    private const string Alice = "alice";

    private SqliteConnection _connection = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(Token);
        await using var context = ContextFor(null);
        await context.Database.EnsureCreatedAsync(Token);
    }

    [TearDown]
    public async Task TearDown() => await _connection.DisposeAsync();

    private ApplicationDbContext ContextFor(string? userId) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options, TestEncryption.Encryptor, new TestUserContext(userId));

    private async Task<string?> RawAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(Token))?.ToString();
    }

    private async Task SeedAsync()
    {
        await using var db = ContextFor(Alice);
        db.AddRange(
            new TaxProfile("ES", "eds", new Dictionary<string, string> { ["territory"] = "canarias", ["difficultExpenses"] = "true" }),
            new BusinessExpense(new DateOnly(2026, 2, 10), "Gestor", new Money(60m, "EUR"), ExpenseCategory.ProfessionalServices, 4.20m,
                supplier: "Asesoría Pérez"),
            new BusinessExpense(new DateOnly(2026, 3, 1), "Cloud hosting", new Money(20m, "EUR"), ExpenseCategory.Software,
                reverseCharge: true, deductiblePercent: 80m),
            new RecurringExpense("Social Security", new Money(300.00m, "EUR"), ExpenseCategory.SocialSecurity,
                new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1)),
            new TaxPayment(2026, 1, "130", new Money(2625.00m, "EUR"), new DateOnly(2026, 4, 20)));
        await db.SaveChangesAsync(Token);
    }

    [Test]
    public async Task RoundTrip_KeepsEveryValue()
    {
        await SeedAsync();

        await using var db = ContextFor(Alice);
        var profile = await db.TaxProfiles.SingleAsync(Token);
        (profile.CountryCode, profile.RegimeId, profile.Values["difficultExpenses"]).ShouldBe(("ES", "eds", "true"));

        var expenses = await db.BusinessExpenses.OrderBy(e => e.Date).ToListAsync(Token);
        expenses.Select(e => (e.Description, e.Supplier, e.Amount, e.IndirectTax, e.ReverseCharge, e.Category, e.DeductiblePercent))
            .ShouldBe(
            [
                ("Gestor", "Asesoría Pérez", new Money(60m, "EUR"), 4.20m, false, ExpenseCategory.ProfessionalServices, 100m),
                ("Cloud hosting", null, new Money(20m, "EUR"), 0m, true, ExpenseCategory.Software, 80m)
            ]);

        var fee = await db.RecurringExpenses.SingleAsync(Token);
        (fee.Amount.Amount, fee.FromMonth, fee.ToMonth).ShouldBe((300.00m, new DateOnly(2026, 1, 1), (DateOnly?)new DateOnly(2026, 6, 1)));

        var payment = await db.TaxPayments.SingleAsync(Token);
        (payment.Year, payment.Period, payment.Form, payment.Amount.Amount, payment.PaidOn)
            .ShouldBe((2026, 1, "130", 2625.00m, new DateOnly(2026, 4, 20)));
    }

    [TestCase("""SELECT "Description" FROM "BusinessExpenses" """)]
    [TestCase("""SELECT "Supplier" FROM "BusinessExpenses" WHERE "Supplier" IS NOT NULL""")]
    [TestCase("""SELECT "Amount" FROM "BusinessExpenses" """)]
    [TestCase("""SELECT "IndirectTax" FROM "BusinessExpenses" """)]
    [TestCase("""SELECT "Description" FROM "RecurringExpenses" """)]
    [TestCase("""SELECT "Amount" FROM "RecurringExpenses" """)]
    [TestCase("""SELECT "Amount" FROM "TaxPayments" """)]
    public async Task SensitiveColumns_AreStoredEncrypted(string sql)
    {
        await SeedAsync();

        (await RawAsync(sql)).ShouldNotBeNull().ShouldStartWith(FieldEncryptor.TextPrefix);
    }

    [Test]
    public async Task Records_AreVisibleToTheirOwnerOnly()
    {
        await SeedAsync();

        await using var bob = ContextFor("bob");
        (await bob.TaxProfiles.CountAsync(Token)).ShouldBe(0);
        (await bob.BusinessExpenses.CountAsync(Token)).ShouldBe(0);
        (await bob.RecurringExpenses.CountAsync(Token)).ShouldBe(0);
        (await bob.TaxPayments.CountAsync(Token)).ShouldBe(0);
    }

    [Test]
    public async Task TaxProfile_IsOnePerUser()
    {
        await SeedAsync();

        await using var db = ContextFor(Alice);
        db.TaxProfiles.Add(new TaxProfile("ES", "eds", null));
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Token));
    }

    [Test]
    public async Task TaxPayment_IsOnePerFormAndPeriod()
    {
        await SeedAsync();

        await using var db = ContextFor(Alice);
        db.TaxPayments.Add(new TaxPayment(2026, 1, "130", new Money(1m, "EUR"), new DateOnly(2026, 4, 1)));
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Token));
    }
}
