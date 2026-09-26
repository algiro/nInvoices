using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using nInvoices.Application.Features.Gmail.Commands;
using nInvoices.Application.Services.Email;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Features.Gmail;

[TestFixture]
public sealed class CompleteGmailConnectCommandHandlerTests
{
    private const string UserId = "user-1";
    private const string State = "state-abc";
    private static readonly DateTime Now = new(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);

    private Mock<IGmailClient> _gmail = null!;
    private Mock<ISecretProtector> _protector = null!;
    private InMemoryRepository<OAuthState> _states = null!;
    private InMemoryRepository<GmailConnection> _connections = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private FixedTimeProvider _clock = null!;
    private CompleteGmailConnectCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _gmail = new Mock<IGmailClient>();
        _protector = new Mock<ISecretProtector>();
        _protector.Setup(p => p.Protect(It.IsAny<string>())).Returns<string>(s => $"enc({s})");
        _states = new InMemoryRepository<OAuthState>(new OAuthState(State, UserId, Now));
        _connections = new InMemoryRepository<GmailConnection>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _clock = new FixedTimeProvider(Now.AddMinutes(1));
        _handler = new CompleteGmailConnectCommandHandler(
            _gmail.Object, _protector.Object, _states, _connections, _unitOfWork.Object, _clock,
            NullLogger<CompleteGmailConnectCommandHandler>.Instance);
    }

    private void GoogleGrants(string scopes) =>
        _gmail.Setup(g => g.ExchangeCodeAsync("code-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GmailAuthorization("me@gmail.com", "refresh-1", scopes));

    private Task<GmailConnectOutcome> Complete(string? state = State, string? code = "code-1", string? error = null) =>
        _handler.Handle(new CompleteGmailConnectCommand(code, state, error), TestContext.CurrentContext.CancellationToken);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("forged-state")]
    public async Task Handle_UnknownState_IsRejectedWithoutCallingGoogle(string? state)
    {
        var outcome = await Complete(state);

        outcome.Succeeded.ShouldBeFalse();
        outcome.Reason.ShouldBe(GmailConnectOutcome.InvalidState);
        _gmail.Verify(g => g.ExchangeCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _connections.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Handle_ExpiredState_IsRejectedAndConsumed()
    {
        _clock.Now = Now.Add(OAuthState.Lifetime);

        var outcome = await Complete();

        outcome.Reason.ShouldBe(GmailConnectOutcome.ExpiredState);
        _states.Items.ShouldBeEmpty();
        _gmail.Verify(g => g.ExchangeCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UserDeclined_ReturnsAccessDenied()
    {
        var outcome = await Complete(code: null, error: "access_denied");

        outcome.Reason.ShouldBe(GmailConnectOutcome.AccessDenied);
        _states.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Handle_StateCanOnlyBeUsedOnce()
    {
        GoogleGrants(GmailScopes.Compose);

        (await Complete()).Succeeded.ShouldBeTrue();
        var replay = await Complete();

        replay.Reason.ShouldBe(GmailConnectOutcome.InvalidState);
    }

    [Test]
    public async Task Handle_ComposeScopeUnticked_RevokesAndFails()
    {
        GoogleGrants("openid");

        var outcome = await Complete();

        outcome.Reason.ShouldBe(GmailConnectOutcome.MissingScope);
        _gmail.Verify(g => g.RevokeAsync("refresh-1", It.IsAny<CancellationToken>()), Times.Once);
        _connections.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Handle_CodeRejectedByGoogle_ReturnsExchangeFailed()
    {
        _gmail.Setup(g => g.ExchangeCodeAsync("code-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GmailAuthorizationException("invalid_grant"));

        var outcome = await Complete();

        outcome.Reason.ShouldBe(GmailConnectOutcome.ExchangeFailed);
    }

    [Test]
    public async Task Handle_Success_StoresEncryptedTokenForTheUserWhoStarted()
    {
        GoogleGrants($"{GmailScopes.Compose} openid");

        var outcome = await Complete();

        outcome.Succeeded.ShouldBeTrue();
        outcome.EmailAddress.ShouldBe("me@gmail.com");
        var connection = _connections.Items.ShouldHaveSingleItem();
        connection.UserId.ShouldBe(UserId);
        connection.EmailAddress.ShouldBe("me@gmail.com");
        connection.EncryptedRefreshToken.ShouldBe("enc(refresh-1)");
    }

    [Test]
    public async Task Handle_Reconnect_ReplacesTheExistingConnection()
    {
        _connections.Items.Add(new GmailConnection(UserId, "old@gmail.com", "enc(old)", GmailScopes.Compose) { Id = 7 });
        GoogleGrants(GmailScopes.Compose);

        await Complete();

        var connection = _connections.Items.ShouldHaveSingleItem();
        connection.Id.ShouldBe(7);
        connection.EmailAddress.ShouldBe("me@gmail.com");
        connection.EncryptedRefreshToken.ShouldBe("enc(refresh-1)");
    }
}
