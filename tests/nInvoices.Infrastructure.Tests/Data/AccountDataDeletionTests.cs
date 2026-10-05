using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Encryption;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>
/// Deleting an account, against a real (in-memory) SQLite database with foreign keys enforced:
/// every row of the user goes, the other users' rows stay, and the user's data key is destroyed.
/// </summary>
[TestFixture]
public sealed class AccountDataDeletionTests
{
    // Unique ids: the shared test encryptor keeps keys across tests
    private readonly string _alice = $"alice-{Guid.NewGuid():N}";
    private readonly string _bob = $"bob-{Guid.NewGuid():N}";

    private SqliteConnection _connection = null!;
    private readonly List<ApplicationDbContext> _contexts = [];

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static readonly MethodInfo CountMethod = typeof(AccountDataDeletionTests)
        .GetMethod(nameof(CountAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

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
        // The deletion order must work with foreign keys checked, as PostgreSQL always does
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        _contexts.Add(context);
        return context;
    }

    /// <summary>One row in every table of user data, so the test notices a table the deletion misses.</summary>
    private async Task SeedEverythingAsync(string userId)
    {
        var db = ContextFor(userId);
        var customer = new Customer("Acme", $"VAT-{userId}", new Address("Main", "1", "Town", "12345", "Italy"));
        db.Customers.Add(customer);
        await db.SaveChangesAsync(Token);

        var rate = new Rate(customer.Id, RateType.Hourly, new Money(80m, "EUR"));
        var tax = new Tax(customer.Id, "VAT", "VAT 22%", "PERCENTAGE", 22m, TaxApplicationType.OnSubtotal);
        var project = new Project(customer.Id, "Build");
        db.AddRange(rate, tax, project,
            new InvoiceTemplate(customer.Id, InvoiceType.Monthly, "Invoice", "<p>{{ invoice.total }}</p>"),
            new MonthlyReportTemplate(customer.Id, "Report", "<p>report</p>", InvoiceType.Monthly),
            new EmailTemplate(customer.Id, "Email", "Invoice", "Hello"),
            new Expense(customer.Id, new DateOnly(2026, 9, 3), "Train", new Money(40m, "EUR")),
            new InvoiceSequence(5),
            new ImageAsset("logo", "logo.png", "image/png", "iVBORw0KGgo=", 8),
            new AccessRequest("me@example.com", "Me", DateTime.UtcNow));
        await db.SaveChangesAsync(Token);
        // A compound tax points at another tax of the same table
        var compound = new Tax(customer.Id, "SUR", "Surcharge", "COMPOUND", 1m, TaxApplicationType.OnSubtotal, 1);
        compound.SetCompoundTax(tax.Id);
        db.Taxes.Add(compound);

        var day = new WorkDay(customer.Id, new DateOnly(2026, 9, 1), DayType.Worked, 8m, "notes") { RateId = rate.Id };
        day.Projects.Add(new WorkDayProject(project, 8m));
        db.WorkDays.Add(day);

        var calendar = new HolidayCalendar("IT");
        calendar.Rules.Add(HolidayRule.Fixed("Patron saint", 6, 24));
        db.HolidayCalendars.Add(calendar);
        var compliance = new ComplianceSettings("ES");
        compliance.Update(true, "Name", "12345678Z", null, null);
        db.ComplianceSettings.Add(compliance);

        var invoice = new Invoice(customer.Id, new InvoiceNumber($"INV-{userId}"), InvoiceType.Monthly, new DateOnly(2026, 9, 30),
            new Money(640m, "EUR"), "EUR") { RateId = rate.Id, Status = InvoiceStatus.Finalized, Total = new Money(780.8m, "EUR") };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(Token);

        db.InvoiceTaxLines.Add(new InvoiceTaxLine
        {
            InvoiceId = invoice.Id, TaxId = "VAT", Description = "VAT 22%", Rate = 22m,
            BaseAmount = new Money(640m, "EUR"), TaxAmount = new Money(140.8m, "EUR")
        });
        db.InvoiceEmails.Add(new InvoiceEmail
        {
            InvoiceId = invoice.Id, From = "me@example.com", To = "them@example.com", Subject = "Invoice",
            Attachments = "invoice.pdf", GmailDraftId = "d", GmailMessageId = "m", RfcMessageId = "r"
        });
        var eInvoice = new InvoiceEInvoice(invoice.Id, "ES", "facturae", [1, 2, 3], "application/xml", "xsig");
        db.InvoiceEInvoices.Add(eInvoice);
        var record = new VerifactuRecord(1, VerifactuRecordKind.Issued, invoice.Id, "12345678Z", invoice.Number.Value, "30-09-2026",
            "F1", "140.80", "780.80", "", "2026-09-30T10:00:00+02:00", "HASH1", "<x/>");
        db.VerifactuRecords.Add(record);
        await db.SaveChangesAsync(Token);

        db.AddRange(
            new EInvoiceSubmission(eInvoice.Id, "face", "Test", "REF-1", DateTime.UtcNow),
            new VerifactuSubmission(record.Id, record.Sequence, DateTime.UtcNow),
            new GmailConnection(userId, "me@example.com", "protected-token", "gmail.compose"),
            new OAuthState($"state-{userId}", userId, DateTime.UtcNow));
        await db.SaveChangesAsync(Token);
    }

    private static async Task<int> CountAsync<TEntity>(ApplicationDbContext context, string ownerId, CancellationToken cancellationToken)
        where TEntity : class, IOwnedEntity =>
        await context.Set<TEntity>().IgnoreQueryFilters().CountAsync(e => e.OwnerId == ownerId, cancellationToken);

    /// <summary>Rows per user-data table (by name), plus the Gmail connection and OAuth state.</summary>
    private async Task<Dictionary<string, int>> RowsOfAsync(string userId)
    {
        var context = ContextFor(null);
        var counts = new Dictionary<string, int>();
        foreach (var type in context.Model.GetEntityTypes()
                     .Where(t => t.BaseType is null && !t.IsOwned() && typeof(IOwnedEntity).IsAssignableFrom(t.ClrType)))
        {
            counts[type.ClrType.Name] = await (Task<int>)CountMethod.MakeGenericMethod(type.ClrType).Invoke(null, [context, userId, Token])!;
        }
        counts[nameof(GmailConnection)] = await context.GmailConnections.CountAsync(c => c.UserId == userId, Token);
        counts[nameof(OAuthState)] = await context.OAuthStates.CountAsync(s => s.UserId == userId, Token);
        return counts;
    }

    [Test]
    public async Task Seed_CoversEveryUserDataTable()
    {
        await SeedEverythingAsync(_alice);

        // A new table of user data must be added to the seed, so the deletion tests cover it
        (await RowsOfAsync(_alice)).Where(c => c.Value == 0).Select(c => c.Key).ShouldBeEmpty();
    }

    [Test]
    public async Task DeleteAccountDataAsync_RemovesEveryRowOfTheUser()
    {
        await SeedEverythingAsync(_alice);

        var deleted = await ContextFor(_alice).DeleteAccountDataAsync(_alice, Token);

        deleted.ShouldBeGreaterThan(20);
        (await RowsOfAsync(_alice)).Where(c => c.Value > 0).Select(c => c.Key).ShouldBeEmpty();
    }

    [Test]
    public async Task DeleteAccountDataAsync_LeavesOtherUsersUntouched()
    {
        await SeedEverythingAsync(_alice);
        await SeedEverythingAsync(_bob);
        var before = await RowsOfAsync(_bob);

        await ContextFor(_alice).DeleteAccountDataAsync(_alice, Token);

        (await RowsOfAsync(_bob)).ShouldBe(before);
        // Still readable: Bob's key is intact
        (await ContextFor(_bob).Customers.SingleAsync(Token)).Name.ShouldBe("Acme");
    }

    [Test]
    public async Task DeleteAccountDataAsync_DestroysTheUsersKey()
    {
        await SeedEverythingAsync(_alice);
        await using var command = _connection.CreateCommand();
        command.CommandText = """SELECT "Name" FROM "Customers" """;
        var storedName = (string)(await command.ExecuteScalarAsync(Token))!;

        await ContextFor(_alice).DeleteAccountDataAsync(_alice, Token);

        // A copy of the row from an older backup can no longer be decrypted
        Should.Throw<System.Security.Cryptography.CryptographicException>(() =>
            TestEncryption.Encryptor.DecryptText("Customer.Name", storedName));
    }

    [Test]
    public async Task DeleteAccountDataAsync_RunTwice_SecondRunFindsNothing()
    {
        await SeedEverythingAsync(_alice);
        await ContextFor(_alice).DeleteAccountDataAsync(_alice, Token);

        (await ContextFor(_alice).DeleteAccountDataAsync(_alice, Token)).ShouldBe(0);
    }
}
