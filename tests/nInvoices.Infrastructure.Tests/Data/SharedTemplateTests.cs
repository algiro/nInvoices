using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>
/// Templates with no customer are shared by all of a user's customers. Checked against a real
/// (in-memory) SQLite database, including the rule of one active shared invoice template per type.
/// </summary>
[TestFixture]
public sealed class SharedTemplateTests
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

    private async Task<long> AddCustomerAsync(string userId, string name)
    {
        var context = ContextFor(userId);
        var customer = new Customer(name, name.ToUpperInvariant(), new Address("Main", "1", "Town", "12345", "Italy"));
        context.Customers.Add(customer);
        await context.SaveChangesAsync(Token);
        return customer.Id;
    }

    private static InvoiceTemplate Active(long? customerId, InvoiceType type = InvoiceType.Monthly)
    {
        var template = new InvoiceTemplate(customerId, type, "Template", "<p>content</p>");
        template.Activate();
        return template;
    }

    [Test]
    public async Task SharedTemplates_OfEveryKind_AreSavedWithoutACustomer()
    {
        var context = ContextFor(Alice);
        context.InvoiceTemplates.Add(Active(null));
        context.MonthlyReportTemplates.Add(new MonthlyReportTemplate(null, "Report", "<p>x</p>"));
        context.EmailTemplates.Add(new EmailTemplate(null, "Email", "Subject", "<p>x</p>"));

        await context.SaveChangesAsync(Token);

        var read = ContextFor(Alice);
        (await read.InvoiceTemplates.SingleAsync(Token)).CustomerId.ShouldBeNull();
        (await read.MonthlyReportTemplates.SingleAsync(Token)).CustomerId.ShouldBeNull();
        (await read.EmailTemplates.SingleAsync(Token)).CustomerId.ShouldBeNull();
    }

    [Test]
    public async Task SharedTemplate_BelongsToItsUser_OtherUsersCannotSeeIt()
    {
        var alice = ContextFor(Alice);
        alice.InvoiceTemplates.Add(Active(null));
        await alice.SaveChangesAsync(Token);

        (await ContextFor(Bob).InvoiceTemplates.CountAsync(Token)).ShouldBe(0);
        (await ContextFor(Alice).InvoiceTemplates.CountAsync(Token)).ShouldBe(1);
    }

    [Test]
    public async Task SaveChangesAsync_TwoActiveSharedTemplatesOfTheSameType_Throws()
    {
        var context = ContextFor(Alice);
        context.InvoiceTemplates.Add(Active(null));
        context.InvoiceTemplates.Add(Active(null));

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
    }

    [Test]
    public async Task SaveChangesAsync_ActiveSharedTemplatesOfDifferentTypesOrUsers_AreAllowed()
    {
        foreach (var user in new[] { Alice, Bob })
        {
            var context = ContextFor(user);
            context.InvoiceTemplates.Add(Active(null, InvoiceType.Monthly));
            context.InvoiceTemplates.Add(Active(null, InvoiceType.OneTime));
            // Inactive ones are not limited
            context.InvoiceTemplates.Add(new InvoiceTemplate(null, InvoiceType.Monthly, "Draft", "<p>x</p>"));
            await context.SaveChangesAsync(Token);
        }

        (await ContextFor(Alice).InvoiceTemplates.CountAsync(Token)).ShouldBe(3);
        (await ContextFor(Bob).InvoiceTemplates.CountAsync(Token)).ShouldBe(3);
    }

    [Test]
    public async Task SaveChangesAsync_ActiveSharedAndActiveCustomerTemplateOfTheSameType_AreAllowed()
    {
        var customerId = await AddCustomerAsync(Alice, "Acme");
        var context = ContextFor(Alice);
        context.InvoiceTemplates.Add(Active(null));
        context.InvoiceTemplates.Add(Active(customerId));

        await context.SaveChangesAsync(Token);

        (await ContextFor(Alice).InvoiceTemplates.CountAsync(Token)).ShouldBe(2);
    }

    [Test]
    public async Task SaveChangesAsync_TwoActiveCustomerTemplatesOfTheSameType_StillThrows()
    {
        var customerId = await AddCustomerAsync(Alice, "Acme");
        var context = ContextFor(Alice);
        context.InvoiceTemplates.Add(Active(customerId));
        context.InvoiceTemplates.Add(Active(customerId));

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
    }

    [Test]
    public async Task DeletingACustomer_RemovesItsTemplatesButKeepsTheSharedOnes()
    {
        var customerId = await AddCustomerAsync(Alice, "Acme");
        var context = ContextFor(Alice);
        context.InvoiceTemplates.Add(Active(null));
        context.InvoiceTemplates.Add(Active(customerId));
        context.EmailTemplates.Add(new EmailTemplate(null, "Shared", "s", "<p>x</p>"));
        context.EmailTemplates.Add(new EmailTemplate(customerId, "Own", "s", "<p>x</p>"));
        await context.SaveChangesAsync(Token);

        var deleting = ContextFor(Alice);
        deleting.Customers.Remove(await deleting.Customers.SingleAsync(Token));
        await deleting.SaveChangesAsync(Token);

        var read = ContextFor(Alice);
        (await read.InvoiceTemplates.SingleAsync(Token)).CustomerId.ShouldBeNull();
        (await read.EmailTemplates.SingleAsync(Token)).CustomerId.ShouldBeNull();
    }
}
