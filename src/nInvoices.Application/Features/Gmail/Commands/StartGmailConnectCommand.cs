using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Gmail.Commands;

/// <summary>
/// Starts connecting the current user's Gmail account: records a one-time state and returns
/// the Google consent URL to send the browser to.
/// </summary>
public sealed record StartGmailConnectCommand : IRequest<GmailConnectUrlDto>;
