using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

[TestFixture]
public sealed class EInvoiceSubmissionPersistenceTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc);

    private SqliteConnection _connection = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(Token);
        await ContextFor(null).Database.EnsureCreatedAsync(Token);
    }

    [TearDown]
    public async Task TearDown() => await _connection.DisposeAsync();

    private ApplicationDbContext ContextFor(string? userId)
    {
        var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options, new TestUserContext(userId));
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        return context;
    }

    private async Task<long> AddEInvoiceAsync(string user)
    {
        var context = ContextFor(user);
        var customer = new Customer("Ayuntamiento", "Q2826000H", new Address("Plaza", "1", "Madrid", "28001", "Spain"));
        context.Customers.Add(customer);
        await context.SaveChangesAsync(Token);

        var invoice = new Invoice(customer.Id, new InvoiceNumber("26-10-001"), InvoiceType.OneTime, new DateOnly(2026, 10, 3), new Money(100m, "EUR"), "EUR");
        invoice.AddTaxes(new Money(0m, "EUR"));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(Token);

        var file = new InvoiceEInvoice(invoice.Id, "ES", "facturae-3.2.2", [1, 2, 3], "application/xml", "xsig");
        context.InvoiceEInvoices.Add(file);
        await context.SaveChangesAsync(Token);
        return file.Id;
    }

    private static EInvoiceSubmission Submission(long fileId, string channel = "face")
    {
        var submission = new EInvoiceSubmission(fileId, channel, "Test", "REGAGE26e000001", Now);
        submission.Update("1200", "Registrada", "No solicitada anulación", Now, Now);
        return submission;
    }

    [Test]
    public async Task RoundTrip_KeepsTheDeliveryAndItsStatus()
    {
        var fileId = await AddEInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.EInvoiceSubmissions.Add(Submission(fileId));
        await context.SaveChangesAsync(Token);

        var loaded = await ContextFor("alice").EInvoiceSubmissions.SingleAsync(Token);

        loaded.ChannelId.ShouldBe("face");
        loaded.Environment.ShouldBe("Test");
        loaded.Reference.ShouldBe("REGAGE26e000001");
        loaded.StatusCode.ShouldBe("1200");
        loaded.StatusName.ShouldBe("Registrada");
        loaded.CancellationStatus.ShouldBe("No solicitada anulación");
        loaded.SubmittedAt.ShouldBe(Now);
        loaded.OwnerId.ShouldBe("alice");
    }

    [Test]
    public async Task TheStatusCanBeUpdated()
    {
        var fileId = await AddEInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.EInvoiceSubmissions.Add(Submission(fileId));
        await context.SaveChangesAsync(Token);

        var edit = ContextFor("alice");
        (await edit.EInvoiceSubmissions.SingleAsync(Token)).Update("2400", "Contabilizada", null, null, Now.AddDays(2));
        await edit.SaveChangesAsync(Token);

        var loaded = await ContextFor("alice").EInvoiceSubmissions.SingleAsync(Token);
        loaded.StatusCode.ShouldBe("2400");
        loaded.RegisteredAt.ShouldBe(Now); // kept
        loaded.CheckedAt.ShouldBe(Now.AddDays(2));
    }

    [Test]
    public async Task OneDeliveryPerFileAndChannel()
    {
        var fileId = await AddEInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.EInvoiceSubmissions.Add(Submission(fileId));
        context.EInvoiceSubmissions.Add(Submission(fileId));

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
    }

    [Test]
    public async Task ADeliveredFile_CannotBeDeleted()
    {
        var fileId = await AddEInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.EInvoiceSubmissions.Add(Submission(fileId));
        await context.SaveChangesAsync(Token);

        var edit = ContextFor("alice");
        edit.InvoiceEInvoices.Remove(await edit.InvoiceEInvoices.SingleAsync(Token));

        await Should.ThrowAsync<DbUpdateException>(() => edit.SaveChangesAsync(Token));
    }

    [Test]
    public async Task OtherUsersDoNotSeeIt()
    {
        var fileId = await AddEInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.EInvoiceSubmissions.Add(Submission(fileId));
        await context.SaveChangesAsync(Token);

        (await ContextFor("bob").EInvoiceSubmissions.CountAsync(Token)).ShouldBe(0);
    }
}
