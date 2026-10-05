using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using nInvoices.Api.Controllers;
using nInvoices.Core.Configuration;
using nInvoices.Core.Interfaces;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Encryption;
using Shouldly;

namespace nInvoices.Api.Tests.Controllers;

[TestFixture]
public sealed class AccessRequestsControllerTests
{
    private const string NewUser = "6f1c2d3e-0000-4000-8000-000000000001";

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

    private AccessRequestsController Controller(
        IUserContext? user = null,
        string? name = "Maria Lopez",
        NotificationOptions? options = null)
    {
        user ??= new TestUser(NewUser, "maria@example.com");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = "http://keycloak:8080/realms/ninvoices",
                ["Keycloak:ExternalAuthority"] = "https://example.com/realms/ninvoices"
            })
            .Build();
        var claims = name is null ? [] : new[] { new Claim("name", name) };
        return new AccessRequestsController(
            Context(user), user, _notifier, Options.Create(options ?? new NotificationOptions()), configuration,
            TimeProvider.System, NullLogger<AccessRequestsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) }
            }
        };
    }

    private static AccessRequestResultDto Ok(ActionResult<AccessRequestResultDto> result) =>
        result.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<AccessRequestResultDto>();

    [Test]
    public async Task Request_NewAccount_NotifiesTheAdministratorWithALinkToApprove()
    {
        var result = Ok(await Controller().Request(Token));

        result.ShouldBe(new AccessRequestResultDto(Approved: false, AdministratorNotified: true));
        var message = _notifier.Sent.ShouldHaveSingleItem();
        message.ShouldContain("Maria Lopez <maria@example.com>");
        message.ShouldContain($"https://example.com/admin/master/console/#/ninvoices/users/{NewUser}/role-mapping");
    }

    [Test]
    public async Task Request_Again_DoesNotNotifyTwice()
    {
        await Controller().Request(Token);

        var result = Ok(await Controller().Request(Token));

        result.AdministratorNotified.ShouldBeTrue();
        _notifier.Sent.Count.ShouldBe(1);
        (await Context(new TestUser(NewUser, null)).AccessRequests.CountAsync(Token)).ShouldBe(1);
    }

    [Test]
    public async Task Request_DeliveryFailed_IsRetriedOnTheNextVisit()
    {
        _notifier.Delivers = false;
        Ok(await Controller().Request(Token)).AdministratorNotified.ShouldBeFalse();

        _notifier.Delivers = true;
        Ok(await Controller().Request(Token)).AdministratorNotified.ShouldBeTrue();

        _notifier.Sent.Count.ShouldBe(1);
    }

    [Test]
    public async Task Request_ApprovedUser_RecordsNothing()
    {
        var approved = new TestUser(NewUser, "maria@example.com", "user");

        var result = Ok(await Controller(approved).Request(Token));

        result.Approved.ShouldBeTrue();
        _notifier.Sent.ShouldBeEmpty();
        (await Context(approved).AccessRequests.CountAsync(Token)).ShouldBe(0);
    }

    [Test]
    public async Task Request_NotificationsOff_RecordsTheRequestOnly()
    {
        _notifier.IsEnabled = false;

        var result = Ok(await Controller().Request(Token));

        result.AdministratorNotified.ShouldBeFalse();
        var stored = await Context(new TestUser(NewUser, null)).AccessRequests.SingleAsync(Token);
        (stored.Email, stored.Name, stored.NotifiedAt).ShouldBe(("maria@example.com", "Maria Lopez", (DateTime?)null));
    }

    [Test]
    public async Task Request_ConfiguredConsoleUrl_IsUsedForTheLink()
    {
        var options = new NotificationOptions { KeycloakUserUrl = "https://sso.example.com/console/users/{userId}" };

        await Controller(options: options).Request(Token);

        _notifier.Sent.Single().ShouldContain($"https://sso.example.com/console/users/{NewUser}");
    }

    [Test]
    public async Task Request_OnlyAnEmail_ShowsTheEmail()
    {
        await Controller(name: null).Request(Token);

        _notifier.Sent.Single().Split('\n')[1].ShouldBe("maria@example.com");
    }
}
