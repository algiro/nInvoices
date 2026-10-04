using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using nInvoices.Application.Compliance.Spain.Verifactu;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Services;
using nInvoices.Infrastructure.Verifactu;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Verifactu;

[TestFixture]
public sealed class VerifactuSubmissionWorkerTests
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _provider = null!;
    private readonly List<string?> _ranAs = [];
    private readonly Dictionary<string, int> _visible = [];
    private DateTime _now;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private sealed class NoHttp : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class FixedClock(Func<DateTime> now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now(), DateTimeKind.Utc));
    }

    /// <summary>Stands in for the real submitter and notes whose data the scope sees.</summary>
    private sealed class RecordingSubmitter(IUserContext user, List<string?> ranAs, Dictionary<string, int> visibleTo, ApplicationDbContext db) : IVerifactuSubmitter
    {
        public async Task<SubmissionRun> SubmitPendingAsync(CancellationToken cancellationToken = default)
        {
            ranAs.Add(user.UserId);
            // The usual per-user filter applies: only this user's submissions are visible
            var visible = await db.VerifactuSubmissions.CountAsync(cancellationToken);
            visibleTo[user.UserId!] = visible;
            return new SubmissionRun(visible, 0, 0, 0, null);
        }

        public Task<SubmissionSummary> GetSummaryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SubmissionSummary(0, 0, 0, 0, null));
    }

    [SetUp]
    public async Task SetUp()
    {
        _now = new DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc);
        _ranAs.Clear();
        _visible.Clear();
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(Token);

        var services = new ServiceCollection();
        services.AddSingleton(_connection);
        services.AddScoped<OwnerOverride>();
        services.AddSingleton<IHttpContextAccessor, NoHttp>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton(nInvoices.Infrastructure.Tests.Data.TestEncryption.Encryptor);
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IVerifactuSubmitter>(sp => new RecordingSubmitter(
            sp.GetRequiredService<IUserContext>(), _ranAs, _visible, sp.GetRequiredService<ApplicationDbContext>()));
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

    private VerifactuSubmissionWorker Worker(VerifactuOptions? options = null) => new(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(options ?? new VerifactuOptions { Environment = "Test", ProducerName = "P", ProducerTaxId = "B12345674" }),
        new FixedClock(() => _now),
        NullLogger<VerifactuSubmissionWorker>.Instance);

    /// <summary>A user with one record, and its submission in the state given.</summary>
    private async Task SeedAsync(string owner, VerifactuSubmissionStatus status = VerifactuSubmissionStatus.Pending, int nextInMinutes = 0)
    {
        await using var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<OwnerOverride>().OwnerId = owner;
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = new Customer("C", "A58818501", new Address("Calle", "1", "Madrid", "28001", "Spain"));
        db.Customers.Add(customer);
        await db.SaveChangesAsync(Token);
        var invoice = new Invoice(customer.Id, new InvoiceNumber("26-10-001"), InvoiceType.OneTime, new DateOnly(2026, 10, 3), new Money(100m, "EUR"), "EUR");
        invoice.AddTaxes(new Money(0m, "EUR"));
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(Token);

        var record = new VerifactuRecord(1, VerifactuRecordKind.Issued, invoice.Id, "12345678Z", "26-10-001", "03-10-2026", "F1",
            "0.00", "100.00", "", "2026-10-03T11:30:00+02:00", "H", "<x/>");
        db.VerifactuRecords.Add(record);
        await db.SaveChangesAsync(Token);

        var submission = new VerifactuSubmission(record.Id, 1, _now.AddMinutes(nextInMinutes));
        if (status != VerifactuSubmissionStatus.Pending)
            submission.Answered(status, "A", null, null, _now);
        db.VerifactuSubmissions.Add(submission);
        await db.SaveChangesAsync(Token);
    }

    [Test]
    public async Task RunOnce_SendsForEveryUserWithRecordsDue_AsThatUser()
    {
        await SeedAsync("alice");
        await SeedAsync("bob");

        var attended = await Worker().RunOnceAsync(Token);

        attended.ShouldBe(2);
        _ranAs.Order().ShouldBe(["alice", "bob"]);
    }

    [Test]
    public async Task RunOnce_EachUserSeesOnlyTheirOwnData()
    {
        await SeedAsync("alice");
        await SeedAsync("bob");

        await Worker().RunOnceAsync(Token);

        // Two submissions exist in all; each user's scope sees exactly one, its own
        _visible.ShouldBe(new Dictionary<string, int> { ["alice"] = 1, ["bob"] = 1 });
    }

    [Test]
    public async Task RunOnce_SkipsWhatIsNotDueYet_AndWhatIsDone()
    {
        await SeedAsync("waiting", nextInMinutes: 5);
        await SeedAsync("done", VerifactuSubmissionStatus.Accepted);
        await SeedAsync("rejected", VerifactuSubmissionStatus.Rejected);
        await SeedAsync("due");

        var attended = await Worker().RunOnceAsync(Token);

        attended.ShouldBe(1);
        _ranAs.ShouldBe(["due"]);
    }

    [Test]
    public async Task RunOnce_PicksThemUpOnceTheirTimeComes()
    {
        await SeedAsync("alice", nextInMinutes: 5);
        var worker = Worker();

        (await worker.RunOnceAsync(Token)).ShouldBe(0);
        _now = _now.AddMinutes(6);
        (await worker.RunOnceAsync(Token)).ShouldBe(1);
    }

    [Test]
    public async Task RunOnce_WithNothingPending_DoesNothing() =>
        (await Worker().RunOnceAsync(Token)).ShouldBe(0);

    [Test]
    public async Task OwnerOverride_MakesTheScopeActAsThatUser_AndNoOneElse()
    {
        await using var scope = _provider.CreateAsyncScope();
        var user = scope.ServiceProvider.GetRequiredService<IUserContext>();
        user.UserId.ShouldBeNull();
        user.IsAuthenticated.ShouldBeFalse();

        scope.ServiceProvider.GetRequiredService<OwnerOverride>().OwnerId = "alice";

        user.UserId.ShouldBe("alice");
        user.IsAuthenticated.ShouldBeTrue();
    }

    [Test]
    public async Task Worker_WithVerifactuNotSetUp_StaysIdle()
    {
        await SeedAsync("alice");
        var worker = Worker(new VerifactuOptions());

        await worker.StartAsync(Token);
        await Task.Delay(100, Token);
        await worker.StopAsync(Token);

        _ranAs.ShouldBeEmpty();
    }
}
