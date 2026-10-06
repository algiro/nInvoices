using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using nInvoices.Api.Infrastructure;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Account;
using nInvoices.Core.Configuration;
using nInvoices.Core.Interfaces;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Data.Repositories;
using nInvoices.Infrastructure.Encryption;
using Shouldly;

namespace nInvoices.Api.Tests.Features;

/// <summary>The "waiting for approval" request, against the real repository on SQLite.</summary>
[TestFixture]
public sealed class RequestAccessCommandHandlerTests
{
    private const string NewUser = "6f1c2d3e-0000-4000-8000-000000000001";
    private const string ConsoleUrl = "https://example.com/admin/master/console/#/ninvoices/users/{userId}/role-mapping";

    private static readonly FieldEncryptor Encryptor = FieldEncryptor.CreateEphemeral();

    private SqliteConnection _connection = null!;
    private readonly List<ApplicationDbContext> _contexts = [];
    private readonly FakeNotifier _notifier = new();

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private sealed class FakeNotifier : IAdminNotifier
    {
        public bool IsEnabled { get; set; } = true;
        public bool Delivers { get; set; } = true;
        public List<string> Sent { get; } = [];

        public Task<bool> TrySendAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
                return Task.FromResult(false);
            if (Delivers)
                Sent.Add(text);
            return Task.FromResult(Delivers);
        }
    }

    private sealed class TestUser(string id, string? email, params string[] roles) : IUserContext
    {
        public string? UserId => id;
        public string? Username => email;
        public string? Email => email;
        public IEnumerable<string> Roles => roles;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => roles.Contains(role);
    }

    [SetUp]
    public async Task SetUp()
    {
        _notifier.Sent.Clear();
        _notifier.IsEnabled = true;
        _notifier.Delivers = true;
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(Token);
        await Context(new TestUser(NewUser, null)).Database.EnsureCreatedAsync(Token);
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (var context in _contexts)
            await context.DisposeAsync();
        _contexts.Clear();
        await _connection.DisposeAsync();
    }

    private ApplicationDbContext Context(IUserContext user)
    {
        var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options, Encryptor, user);
        _contexts.Add(context);
        return context;
    }

    /// <summary>One request of the page: a fresh context, like a new HTTP request.</summary>
    private Task<AccessRequestResultDto> Request(IUserContext? user = null, string? name = "Maria Lopez", string? consoleUrl = ConsoleUrl)
    {
        user ??= new TestUser(NewUser, "maria@example.com");
        var context = Context(user);
        var handler = new RequestAccessCommandHandler(
            new AccessRequestRepository(context), new UnitOfWork(context), user, _notifier,
            Options.Create(new NotificationOptions { KeycloakUserUrl = consoleUrl }),
            TimeProvider.System, NullLogger<RequestAccessCommandHandler>.Instance);
        return handler.Handle(new RequestAccessCommand(name), Token).AsTask();
    }

    [Test]
    public async Task NewAccount_NotifiesTheAdministratorWithALinkToApprove()
    {
        var result = await Request();

        result.ShouldBe(new AccessRequestResultDto(Approved: false, AdministratorNotified: true));
        var message = _notifier.Sent.ShouldHaveSingleItem();
        message.ShouldContain("Maria Lopez <maria@example.com>");
        message.ShouldContain($"https://example.com/admin/master/console/#/ninvoices/users/{NewUser}/role-mapping");
    }

    [Test]
    public async Task Again_DoesNotNotifyTwice()
    {
        await Request();

        var result = await Request();

        result.AdministratorNotified.ShouldBeTrue();
        _notifier.Sent.Count.ShouldBe(1);
        (await Context(new TestUser(NewUser, null)).AccessRequests.CountAsync(Token)).ShouldBe(1);
    }

    [Test]
    public async Task DeliveryFailed_IsRetriedOnTheNextVisit()
    {
        _notifier.Delivers = false;
        (await Request()).AdministratorNotified.ShouldBeFalse();

        _notifier.Delivers = true;
        (await Request()).AdministratorNotified.ShouldBeTrue();

        _notifier.Sent.Count.ShouldBe(1);
    }

    [Test]
    public async Task ApprovedUser_RecordsNothing()
    {
        var approved = new TestUser(NewUser, "maria@example.com", AppRoles.User);

        var result = await Request(approved);

        result.Approved.ShouldBeTrue();
        _notifier.Sent.ShouldBeEmpty();
        (await Context(approved).AccessRequests.CountAsync(Token)).ShouldBe(0);
    }

    [Test]
    public async Task NotificationsOff_RecordsTheRequestOnly()
    {
        _notifier.IsEnabled = false;

        var result = await Request();

        result.AdministratorNotified.ShouldBeFalse();
        var stored = await Context(new TestUser(NewUser, null)).AccessRequests.SingleAsync(Token);
        (stored.Email, stored.Name, stored.NotifiedAt).ShouldBe(("maria@example.com", "Maria Lopez", (DateTime?)null));
    }

    [Test]
    public async Task ConfiguredConsoleUrl_IsUsedForTheLink()
    {
        await Request(consoleUrl: "https://sso.example.com/console/users/{userId}");

        _notifier.Sent.Single().ShouldContain($"https://sso.example.com/console/users/{NewUser}");
    }

    [Test]
    public async Task NoConsoleUrl_SendsTheMessageWithoutALink()
    {
        await Request(consoleUrl: null);

        _notifier.Sent.Single().ShouldNotContain("Approve it");
    }

    [Test]
    public async Task OnlyAnEmail_ShowsTheEmail()
    {
        await Request(name: null);

        _notifier.Sent.Single().Split('\n')[1].ShouldBe("maria@example.com");
    }

    [Test]
    public async Task SavedConcurrently_TheLoserDoesNotNotify()
    {
        // Another request of the same user is saved between this one's check and its insert
        var user = new TestUser(NewUser, "maria@example.com");
        var winner = Context(user);
        winner.AccessRequests.Add(new Core.Entities.AccessRequest("maria@example.com", "Maria Lopez", DateTime.UtcNow));
        var loser = new AccessRequestRepository(Context(user));
        await winner.SaveChangesAsync(Token);

        var added = await loser.TryAddAsync(new Core.Entities.AccessRequest("maria@example.com", "Maria Lopez", DateTime.UtcNow), Token);

        added.ShouldBeFalse();
        (await Context(user).AccessRequests.CountAsync(Token)).ShouldBe(1);
    }

    [TestCase("https://example.com/realms/ninvoices", null, "https://example.com/admin/master/console/#/ninvoices/users/{userId}/role-mapping")]
    [TestCase(null, "http://keycloak:8080/realms/ninvoices/", "http://keycloak:8080/admin/master/console/#/ninvoices/users/{userId}/role-mapping")]
    [TestCase(null, null, null)]
    [TestCase("https://example.com/not-a-realm", null, null)]
    public void ConsoleUrlTemplate_IsDerivedFromTheTrustedRealm(string? external, string? authority, string? expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:ExternalAuthority"] = external,
                ["Keycloak:Authority"] = authority
            })
            .Build();

        AdminNotificationExtensions.KeycloakUserUrlTemplate(configuration).ShouldBe(expected);
    }
}
