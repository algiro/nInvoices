using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

[TestFixture]
public sealed class UnitOfWorkTests
{
    private const string Alice = "alice-sub";

    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private UnitOfWork _unitOfWork = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(Token);
        _context = NewContext();
        await _context.Database.EnsureCreatedAsync(Token);
        _unitOfWork = new UnitOfWork(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        _unitOfWork.Dispose();
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options, TestEncryption.Encryptor, new TestUserContext(Alice));

    private static Customer NewCustomer(string name) =>
        new(name, name.ToUpperInvariant(), new Address("Main", "1", "Town", "12345", "Italy"));

    [Test]
    public async Task ExecuteInTransactionAsync_OperationSucceeds_CommitsEverythingSaved()
    {
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            _context.Customers.Add(NewCustomer("First"));
            await _unitOfWork.SaveChangesAsync(ct);
            _context.Customers.Add(NewCustomer("Second"));
            await _unitOfWork.SaveChangesAsync(ct);
        }, Token);

        await using var other = NewContext();
        (await other.Customers.CountAsync(Token)).ShouldBe(2);
    }

    [Test]
    public async Task ExecuteInTransactionAsync_OperationThrows_RollsBackWhatWasSaved()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            _context.Customers.Add(NewCustomer("First"));
            await _unitOfWork.SaveChangesAsync(ct);
            throw new InvalidOperationException("boom");
        }, Token));

        await using var other = NewContext();
        (await other.Customers.CountAsync(Token)).ShouldBe(0);
    }

    [Test]
    public async Task ExecuteInTransactionAsync_CalledAgainAfterAFailure_Works()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _unitOfWork.ExecuteInTransactionAsync(_ => throw new InvalidOperationException("boom"), Token));

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            _context.Customers.Add(NewCustomer("After"));
            await _unitOfWork.SaveChangesAsync(ct);
        }, Token);

        await using var other = NewContext();
        (await other.Customers.CountAsync(Token)).ShouldBe(1);
    }
}
