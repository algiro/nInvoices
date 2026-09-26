using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Data.Repositories;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>
/// Runs the invoice list queries against a real (in-memory) SQLite database, so filters and sort
/// orders are checked as SQL, not as LINQ to objects.
/// </summary>
[TestFixture]
public sealed class InvoiceRepositorySearchTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private InvoiceRepository _repository = null!;
    private long _acme;
    private long _northwind;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
        await _context.Database.EnsureCreatedAsync();

        var acme = new Customer("Acme Corp", "ACME1", new Address("Main", "1", "Town", "12345", "Italy"));
        var northwind = new Customer("Northwind Labs", "NORTH1", new Address("Via Roma", "2", "Milano", "20121", "Italy"));
        _context.Customers.AddRange(acme, northwind);
        await _context.SaveChangesAsync();
        (_acme, _northwind) = (acme.Id, northwind.Id);

        Add(_acme, "25-11-001", new DateOnly(2025, 11, 30), 1000m, InvoiceStatus.Paid);
        Add(_northwind, "25-12-002", new DateOnly(2025, 12, 31), 250.5m, InvoiceStatus.Sent);
        Add(_northwind, "26-01-003", new DateOnly(2026, 1, 31), 99.99m, InvoiceStatus.Finalized);
        Add(_acme, "26-02-004", new DateOnly(2026, 2, 28), 5000m, InvoiceStatus.Draft);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _repository = new InvoiceRepository(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private void Add(long customerId, string number, DateOnly issued, decimal amount, InvoiceStatus status)
    {
        var invoice = new Invoice(
            customerId,
            InvoiceNumber.Generate(number, 1, issued.ToDateTime(TimeOnly.MinValue), null),
            InvoiceType.Monthly,
            issued,
            new Money(amount, "EUR"),
            "EUR");
        invoice.SetMonthlyInvoiceDetails(issued.Year, issued.Month, 20);
        // As generation does; it also gives Total its own Money instance, which EF needs to save it
        invoice.AddTaxes(Money.Zero("EUR"));
        if (status != InvoiceStatus.Draft) invoice.FinalizeInvoice();
        if (status is InvoiceStatus.Sent or InvoiceStatus.Paid) invoice.MarkAsSent();
        if (status == InvoiceStatus.Paid) invoice.MarkAsPaid();
        _context.Invoices.Add(invoice);
    }

    private async Task<IReadOnlyList<string>> NumbersAsync(InvoiceSearchCriteria criteria) =>
        (await _repository.SearchAsync(criteria, TestContext.CurrentContext.CancellationToken)).Items
            .Select(i => i.Number.Value)
            .ToList();

    [Test]
    public async Task SearchAsync_Default_NewestIssuedFirst()
    {
        (await NumbersAsync(new InvoiceSearchCriteria())).ShouldBe(["26-02-004", "26-01-003", "25-12-002", "25-11-001"]);
    }

    [Test]
    public async Task SearchAsync_SortByTotalAscending_OrdersByAmount()
    {
        (await NumbersAsync(new InvoiceSearchCriteria(Sort: InvoiceSortField.Total, Descending: false)))
            .ShouldBe(["26-01-003", "25-12-002", "25-11-001", "26-02-004"]);
    }

    [Test]
    public async Task SearchAsync_SortByStatus_FollowsTheLifecycle()
    {
        (await NumbersAsync(new InvoiceSearchCriteria(Sort: InvoiceSortField.Status, Descending: false)))
            .ShouldBe(["26-02-004", "26-01-003", "25-12-002", "25-11-001"]);
    }

    [Test]
    public async Task SearchAsync_SortByCustomer_ThenNewestFirst()
    {
        (await NumbersAsync(new InvoiceSearchCriteria(Sort: InvoiceSortField.Customer, Descending: false)))
            .ShouldBe(["26-02-004", "25-11-001", "26-01-003", "25-12-002"]);
    }

    [TestCase("north", new[] { "26-01-003", "25-12-002" })]
    [TestCase("ACME", new[] { "26-02-004", "25-11-001" })]
    [TestCase("12-00", new[] { "25-12-002" })]
    public async Task SearchAsync_Search_MatchesNumberOrCustomerIgnoringCase(string term, string[] expected)
    {
        (await NumbersAsync(new InvoiceSearchCriteria(Search: term))).ShouldBe(expected);
    }

    [Test]
    public async Task SearchAsync_YearAndCustomer_Combine()
    {
        (await NumbersAsync(new InvoiceSearchCriteria(CustomerId: _northwind, Year: 2026))).ShouldBe(["26-01-003"]);
    }

    [Test]
    public async Task SearchAsync_SecondPage_SkipsTheFirstAndCountsAll()
    {
        var result = await _repository.SearchAsync(new InvoiceSearchCriteria(Page: 2, PageSize: 3), TestContext.CurrentContext.CancellationToken);

        result.TotalCount.ShouldBe(4);
        result.Items.ShouldHaveSingleItem().Number.Value.ShouldBe("25-11-001");
    }

    [Test]
    public async Task CountByStatusAsync_IgnoresTheStatusFilterButKeepsTheOthers()
    {
        var counts = await _repository.CountByStatusAsync(
            new InvoiceSearchCriteria(Status: InvoiceStatus.Paid, CustomerId: _acme), TestContext.CurrentContext.CancellationToken);

        counts.ShouldBe(new Dictionary<InvoiceStatus, int> { [InvoiceStatus.Paid] = 1, [InvoiceStatus.Draft] = 1 }, ignoreOrder: true);
    }

    [Test]
    public async Task GetTotalsAsync_ReturnsEveryInvoice()
    {
        var rows = await _repository.GetTotalsAsync(TestContext.CurrentContext.CancellationToken);

        rows.Count.ShouldBe(4);
        rows.Sum(r => r.Total).ShouldBe(6350.49m);
    }
}
