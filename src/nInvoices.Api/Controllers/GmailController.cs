using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Gmail.Commands;
using nInvoices.Application.Features.Gmail.Queries;
using nInvoices.Application.Services.Email;
using nInvoices.Infrastructure.Gmail;

namespace nInvoices.Api.Controllers;

/// <summary>
/// Connects the user's Gmail account (OAuth2 authorization-code flow) so invoice emails can
/// be created as Gmail drafts.
/// </summary>
[ApiController]
[Route("api/gmail")]
[Authorize]
public sealed class GmailController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly GmailOptions _options;

    public GmailController(IMediator mediator, IOptions<GmailOptions> options)
    {
        _mediator = mediator;
        _options = options.Value;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(GmailStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GmailStatusDto>> GetStatus(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetGmailStatusQuery(), cancellationToken));

    /// <summary>
    /// Returns the Google consent URL. The web app navigates the browser there; Google then
    /// redirects to <see cref="Callback"/>.
    /// </summary>
    [HttpPost("connect")]
    [ProducesResponseType(typeof(GmailConnectUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GmailConnectUrlDto>> Connect(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _mediator.Send(new StartGmailConnectCommand(), cancellationToken));
        }
        catch (InvoiceEmailException ex)
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
    }

    /// <summary>
    /// Google's redirect after the consent screen. It is a plain browser navigation without the
    /// app's bearer token, hence anonymous; the one-time <paramref name="state"/> identifies the
    /// user. Always ends by redirecting the browser to the web app's Settings page.
    /// </summary>
    [HttpGet("oauth/callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var outcome = await _mediator.Send(new CompleteGmailConnectCommand(code, state, error), cancellationToken);

        // The target comes from configuration only, never from the request (no open redirect)
        var query = outcome.Succeeded ? "gmail=connected" : $"gmail=error&reason={Uri.EscapeDataString(outcome.Reason ?? "unknown")}";
        var separator = _options.PostConnectRedirect.Contains('?') ? '&' : '?';
        return Redirect($"{_options.PostConnectRedirect}{separator}{query}");
    }

    [HttpDelete("connection")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken) =>
        await _mediator.Send(new DisconnectGmailCommand(), cancellationToken) ? NoContent() : NotFound();
}
