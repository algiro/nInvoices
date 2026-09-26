using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nInvoices.Application.Services.Email;

namespace nInvoices.Infrastructure.Gmail;

/// <summary>
/// <see cref="IGmailClient"/> on the official Google API client. Tokens are not cached here:
/// every draft starts from the stored refresh token, which is fine for the volume of an
/// invoicing app and keeps the API stateless.
/// </summary>
public sealed class GoogleGmailClient : IGmailClient
{
    private const string ApplicationName = "nInvoices";

    // The flow is keyed by a user id only for its token store, which is not used
    private const string FlowUser = "ninvoices";

    private readonly GmailOptions _options;
    private readonly ILogger<GoogleGmailClient> _logger;

    public GoogleGmailClient(IOptions<GmailOptions> options, ILogger<GoogleGmailClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public string BuildAuthorizationUrl(string state, string? loginHint)
    {
        EnsureConfigured();

        var request = new GoogleAuthorizationCodeRequestUrl(new Uri(GoogleAuthConsts.OidcAuthorizationUrl))
        {
            ClientId = _options.ClientId,
            RedirectUri = _options.RedirectUri,
            Scope = GmailScopes.Compose,
            State = state,
            // A refresh token is only returned for offline access, and on a re-consent only with prompt=consent
            AccessType = "offline",
            Prompt = "consent",
            LoginHint = loginHint
        };

        return request.Build().AbsoluteUri;
    }

    public async Task<GmailAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var flow = CreateFlow();

        TokenResponse token;
        try
        {
            token = await flow.ExchangeCodeForTokenAsync(FlowUser, code, _options.RedirectUri, cancellationToken);
        }
        catch (TokenResponseException ex)
        {
            throw new GmailAuthorizationException($"Google rejected the authorization code: {ex.Error?.Error}", ex);
        }

        if (string.IsNullOrEmpty(token.RefreshToken))
            throw new GmailAuthorizationException("Google returned no refresh token.");

        var credential = new UserCredential(flow, FlowUser, token);
        using var gmail = CreateService(credential);
        var profile = await gmail.Users.GetProfile("me").ExecuteAsync(cancellationToken);

        return new GmailAuthorization(profile.EmailAddress, token.RefreshToken, token.Scope ?? string.Empty);
    }

    public async Task<GmailDraft> CreateDraftAsync(string refreshToken, OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var flow = CreateFlow();
        var credential = new UserCredential(flow, FlowUser, new TokenResponse { RefreshToken = refreshToken });

        try
        {
            if (!await credential.RefreshTokenAsync(cancellationToken))
                throw new GmailAuthorizationException("Google did not issue an access token.");
        }
        catch (TokenResponseException ex)
        {
            // invalid_grant: revoked, expired (7 days while the consent screen is in Testing) or password changed
            throw new GmailAuthorizationException($"Google refused the stored authorization: {ex.Error?.Error}", ex);
        }

        using var gmail = CreateService(credential);
        var raw = MimeMessageFactory.ToRaw(MimeMessageFactory.Create(email));

        try
        {
            var draft = await gmail.Users.Drafts
                .Create(new Draft { Message = new Message { Raw = raw } }, "me")
                .ExecuteAsync(cancellationToken);
            return new GmailDraft(draft.Id, draft.Message.Id);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new GmailAuthorizationException($"Gmail refused to create the draft: {ex.Message}", ex);
        }
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return;

        try
        {
            using var flow = CreateFlow();
            await flow.RevokeTokenAsync(FlowUser, refreshToken, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Revoking the Gmail token failed; it stays valid until the user removes access in their Google account");
        }
    }

    private GoogleAuthorizationCodeFlow CreateFlow() => new(new GoogleAuthorizationCodeFlow.Initializer
    {
        ClientSecrets = new ClientSecrets { ClientId = _options.ClientId, ClientSecret = _options.ClientSecret },
        Scopes = [GmailScopes.Compose]
    });

    private static GmailService CreateService(UserCredential credential) => new(new BaseClientService.Initializer
    {
        HttpClientInitializer = credential,
        ApplicationName = ApplicationName
    });

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Gmail is not configured (Gmail:ClientId, Gmail:ClientSecret, Gmail:RedirectUri).");
    }
}
