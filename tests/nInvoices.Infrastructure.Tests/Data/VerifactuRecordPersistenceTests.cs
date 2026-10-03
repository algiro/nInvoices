using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Data.Repositories;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

[TestFixture]
public sealed class VerifactuRecordPersistenceTests
{
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
        // SQLite does not enforce foreign keys unless asked, and the chain relies on them
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        return context;
    }

    private async Task<long> AddInvoiceAsync(string user)
    {
        var context = ContextFor(user);
        var customer = new Customer("Cliente", "A58818501", new Address("Calle", "1", "Madrid", "28001", "Spain"));
        context.Customers.Add(customer);
        await context.SaveChangesAsync(Token);

        var invoice = new Invoice(customer.Id, new InvoiceNumber("26-10-001"), InvoiceType.OneTime, new DateOnly(2026, 10, 3), new Money(100m, "EUR"), "EUR");
        // As the application does after building an invoice: this gives Total its own Money
        invoice.AddTaxes(new Money(0m, "EUR"));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(Token);
        return invoice.Id;
    }

    private static VerifactuRecord Record(long sequence, long invoiceId, string previousHash = "") => new(
        sequence, VerifactuRecordKind.Issued, invoiceId, "12345678Z", $"26-10-{sequence:000}", "03-10-2026", "F1", "21.00", "121.00",
        previousHash, "2026-10-03T11:30:00+02:00", $"HASH{sequence}", "<x/>");

    [Test]
    public async Task RoundTrip_KeepsEverythingThatWasHashed()
    {
        var invoiceId = await AddInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.VerifactuRecords.Add(Record(1, invoiceId));
        await context.SaveChangesAsync(Token);

        var loaded = await ContextFor("alice").VerifactuRecords.SingleAsync(Token);

        loaded.Sequence.ShouldBe(1);
        loaded.Kind.ShouldBe(VerifactuRecordKind.Issued);
        loaded.IssuerTaxId.ShouldBe("12345678Z");
        loaded.TotalAmount.ShouldBe("121.00");
        loaded.GeneratedAt.ShouldBe("2026-10-03T11:30:00+02:00");
        loaded.Hash.ShouldBe("HASH1");
        loaded.OwnerId.ShouldBe("alice");
    }

    [Test]
    public async Task ASavedRecord_CannotBeChanged()
    {
        var invoiceId = await AddInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.VerifactuRecords.Add(Record(1, invoiceId));
        await context.SaveChangesAsync(Token);

        var edit = ContextFor("alice");
        var record = await edit.VerifactuRecords.SingleAsync(Token);
        edit.Entry(record).Property(r => r.TotalAmount).CurrentValue = "1.00";

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => edit.SaveChangesAsync(Token));
        ex.Message.ShouldContain("append-only");
        (await ContextFor("alice").VerifactuRecords.SingleAsync(Token)).TotalAmount.ShouldBe("121.00");
    }

    [Test]
    public async Task ASavedRecord_CannotBeDeleted()
    {
        var invoiceId = await AddInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.VerifactuRecords.Add(Record(1, invoiceId));
        await context.SaveChangesAsync(Token);

        var edit = ContextFor("alice");
        edit.VerifactuRecords.Remove(await edit.VerifactuRecords.SingleAsync(Token));

        await Should.ThrowAsync<InvalidOperationException>(() => edit.SaveChangesAsync(Token));
        (await ContextFor("alice").VerifactuRecords.CountAsync(Token)).ShouldBe(1);
    }

    [Test]
    public async Task ARecordedInvoice_CannotBeDeleted()
    {
        var invoiceId = await AddInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.VerifactuRecords.Add(Record(1, invoiceId));
        await context.SaveChangesAsync(Token);

        var edit = ContextFor("alice");
        edit.Invoices.Remove(await edit.Invoices.SingleAsync(Token));

        await Should.ThrowAsync<DbUpdateException>(() => edit.SaveChangesAsync(Token));
    }

    [Test]
    public async Task TwoRecordsCannotClaimTheSamePlaceInTheChain()
    {
        var invoiceId = await AddInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.VerifactuRecords.Add(Record(1, invoiceId));
        await context.SaveChangesAsync(Token);

        var second = ContextFor("alice");
        second.VerifactuRecords.Add(Record(1, invoiceId));

        await Should.ThrowAsync<DbUpdateException>(() => second.SaveChangesAsync(Token));
    }

    [Test]
    public async Task EachUserHasTheirOwnChain_StartingAtOne()
    {
        var alice = await AddInvoiceAsync("alice");
        var bob = await AddInvoiceAsync("bob");

        var a = ContextFor("alice");
        a.VerifactuRecords.Add(Record(1, alice));
        await a.SaveChangesAsync(Token);
        var b = ContextFor("bob");
        b.VerifactuRecords.Add(Record(1, bob));
        await b.SaveChangesAsync(Token);

        (await ContextFor("alice").VerifactuRecords.CountAsync(Token)).ShouldBe(1);
        (await ContextFor("bob").VerifactuRecords.CountAsync(Token)).ShouldBe(1);
    }

    [Test]
    public async Task Repository_FindsTheLastRecord_TheRecordsOfAnInvoice_AndTheChainInOrder()
    {
        var invoiceId = await AddInvoiceAsync("alice");
        var other = await AddInvoiceAsync("alice");
        var context = ContextFor("alice");
        context.VerifactuRecords.AddRange(Record(2, other, "HASH1"), Record(1, invoiceId), Record(3, invoiceId, "HASH2"));
        await context.SaveChangesAsync(Token);

        var repository = new VerifactuRecordRepository(ContextFor("alice"));

        (await repository.GetLastAsync(Token))!.Sequence.ShouldBe(3);
        (await repository.GetByInvoiceAsync(invoiceId, Token)).Select(r => r.Sequence).ShouldBe([1L, 3L]);
        (await repository.GetChainAsync(Token)).Select(r => r.Sequence).ShouldBe([1L, 2L, 3L]);
        (await new VerifactuRecordRepository(ContextFor("bob")).GetLastAsync(Token)).ShouldBeNull();
    }

    [Test]
    public async Task Repository_RefusesToUpdateOrDelete()
    {
        var repository = new VerifactuRecordRepository(ContextFor("alice"));

        await Should.ThrowAsync<InvalidOperationException>(() => repository.UpdateAsync(Record(1, 1), Token));
        await Should.ThrowAsync<InvalidOperationException>(() => repository.DeleteAsync(Record(1, 1), Token));
    }

    [Test]
    public async Task TaxComplianceValues_ArePersisted()
    {
        var context = ContextFor("alice");
        var customer = new Customer("Cliente", "A58818501", new Address("Calle", "1", "Madrid", "28001", "Spain"));
        context.Customers.Add(customer);
        await context.SaveChangesAsync(Token);
        var tax = new Tax(customer.Id, "VAT", "VAT 0%", "PERCENTAGE", 0m, TaxApplicationType.OnSubtotal, 0);
        tax.SetComplianceValues("ES", new Dictionary<string, string> { ["operation"] = "N2" });
        context.Taxes.Add(tax);
        await context.SaveChangesAsync(Token);

        var loaded = await ContextFor("alice").Taxes.SingleAsync(Token);

        loaded.GetComplianceValues("ES")["operation"].ShouldBe("N2");
    }
}
