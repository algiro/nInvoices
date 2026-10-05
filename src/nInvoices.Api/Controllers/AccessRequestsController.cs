using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using nInvoices.Api.Infrastructure;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Api.Controllers;

/// <summary>
/// Called by the "waiting for approval" page: tells the administrator, once per account, that someone
/// signed in and needs the "user" role. Open to any signed-in account, approved or not.
/// </summary>
[ApiController]
[Route("api/access-requests")]
[Authorize(Policy = SignedInPolicy)]
public sealed class AccessRequestsController : ControllerBase
{
    public const string SignedInPolicy = "SignedIn";

    private readonly ApplicationDbContext _context;
    private readonly IUserContext _userContext;
    private readonly IAdminNotifier _notifier;
    private readonly NotificationOptions _options;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _time;
    private readonly ILogger<AccessRequestsController> _logger;

    public AccessRequestsController(
        ApplicationDbContext context,
        IUserContext userContext,
        IAdminNotifier notifier,
        IOptions<NotificationOptions> options,
        IConfiguration configuration,
        TimeProvider time,
        ILogger<AccessRequestsController> logger)
    {
        _context = context;
        _userContext = userContext;
        _notifier = notifier;
        _options = options.Value;
        _configuration = configuration;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    /// Records the request and notifies the administrator if they haven't been told yet. Approved
    /// users have nothing to request.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccessRequestResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessRequestResultDto>> Request(CancellationToken cancellationToken)
    {
        var userId = _userContext.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();
        if (_userContext.IsInRole(KeycloakRoleClaims.AppUserRole))
            return Ok(new AccessRequestResultDto(Approved: true, AdministratorNotified: false));

        var now = _time.GetUtcNow().UtcDateTime;
        var name = User.FindFirst("name")?.Value ?? _userContext.Username;
        var request = await _context.AccessRequests.FirstOrDefaultAsync(cancellationToken);
        if (request is null)
        {
            request = new AccessRequest(_userContext.Email, name, now);
            _context.AccessRequests.Add(request);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The page asked twice at once: the other request recorded it (and notifies)
                return Ok(new AccessRequestResultDto(Approved: false, AdministratorNotified: _notifier.IsEnabled));
            }
            _logger.LogInformation("Account {UserId} is waiting for approval", userId);
        }

        if (request.NotifiedAt is null && await _notifier.TrySendAsync(Message(userId, request), cancellationToken))
        {
            request.MarkNotified(now);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new AccessRequestResultDto(Approved: false, AdministratorNotified: request.NotifiedAt is not null));
    }

    private string Message(string userId, AccessRequest request)
    {
        var who = (request.Name, request.Email) switch
        {
            ({ } n, { } e) when !string.Equals(n, e, StringComparison.OrdinalIgnoreCase) => $"{n} <{e}>",
            (_, { } e) => e,
            ({ } n, _) => n,
            _ => userId
        };
        var lines = new List<string> { "nInvoices: a new account is waiting for approval", who };
        if (KeycloakUserUrl(userId) is { } url)
            lines.Add($"Approve it (Role mapping > Assign role > user): {url}");
        return string.Join('\n', lines);
    }

    /// <summary>The user's page in the Keycloak admin console, from the realm URL the API trusts.</summary>
    private string? KeycloakUserUrl(string userId)
    {
        if (!string.IsNullOrWhiteSpace(_options.KeycloakUserUrl))
            return _options.KeycloakUserUrl.Replace("{userId}", Uri.EscapeDataString(userId), StringComparison.Ordinal);

        var authority = _configuration["Keycloak:ExternalAuthority"] ?? _configuration["Keycloak:Authority"];
        if (string.IsNullOrWhiteSpace(authority))
            return null;
        var marker = authority.LastIndexOf("/realms/", StringComparison.Ordinal);
        if (marker < 0)
            return null;
        var realm = authority[(marker + "/realms/".Length)..].Trim('/');
        return $"{authority[..marker]}/admin/master/console/#/{realm}/users/{Uri.EscapeDataString(userId)}/role-mapping";
    }
}

public sealed record AccessRequestResultDto(bool Approved, bool AdministratorNotified);
