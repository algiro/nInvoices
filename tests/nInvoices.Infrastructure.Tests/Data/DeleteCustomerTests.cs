using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Application.Features.Customers.Commands;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Countries;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Data.Repositories;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>
/// Deleting a customer, against a real (in-memory) SQLite database with foreign keys enforced: a customer
/// whose days are split across its projects goes with them, and one with invoices is refused.
/// </summary>
[TestFixture]
public sealed class DeleteCustomerTests
{
    private readonly string _user = $"user-{Guid.NewGuid():N}";

    private SqliteConnection _connection = null!;
    private ApplicationDbContext _db = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(Token);
        _db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).UseCountryModels(TestCountries.All).Options,
            TestEncryption.Encryptor, new TestUserContext(_user));
        await _db.Database.EnsureCreatedAsync(Token);
        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", Token);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private DeleteCustomerCommandHandler Handler() => new(
        new Repository<Customer>(_db), new WorkDayRepository(_db), new InvoiceRepository(_db),
        new UnitOfWork(_db), new AppText());

    private async Task<Customer> SeedCustomerWithSplitDaysAsync()
    {
        var customer = new Customer("Acme", $"VAT-{_user}", new Address("Main", "1", "Town", "12345", "Italy"));
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(Token);

        var project = new Project(customer.Id, "Build");
        _db.Projects.Add(project);
        await _db.SaveChangesAsync(Token);

        var day = new WorkDay(customer.Id, new DateOnly(2026, 9, 1), DayType.Worked, 8m);
        day.Projects.Add(new WorkDayProject(project, 8m));
        _db.WorkDays.Add(day);
        await _db.SaveChangesAsync(Token);
        return customer;
    }

    [Test]
    public async Task Handle_CustomerWithDaysSplitAcrossProjects_DeletesItWithTheirDays()
    {
        var customer = await SeedCustomerWithSplitDaysAsync();

        var deleted = await Handler().Handle(new DeleteCustomerCommand(customer.Id), Token);

        deleted.ShouldBeTrue();
        (await _db.Customers.CountAsync(Token)).ShouldBe(0);
        (await _db.WorkDays.CountAsync(Token)).ShouldBe(0);
        (await _db.Projects.CountAsync(Token)).ShouldBe(0);
    }

    [Test]
    public async Task Handle_CustomerWithAnInvoice_IsRefusedAndNothingIsDeleted()
    {
        var customer = await SeedCustomerWithSplitDaysAsync();
        var invoice = new Invoice(customer.Id, new InvoiceNumber("INV-1"), InvoiceType.Monthly,
            new DateOnly(2026, 9, 30), new Money(640m, "EUR"), "EUR");
        invoice.AddTaxes(new Money(140.8m, "EUR"));
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(Token);

        await Should.ThrowAsync<DomainException>(
            Handler().Handle(new DeleteCustomerCommand(customer.Id), Token).AsTask());

        (await _db.Customers.CountAsync(Token)).ShouldBe(1);
        (await _db.WorkDays.CountAsync(Token)).ShouldBe(1);
    }

    [Test]
    public async Task Handle_UnknownCustomer_ReturnsFalse()
    {
        var deleted = await Handler().Handle(new DeleteCustomerCommand(999), Token);

        deleted.ShouldBeFalse();
    }
}
