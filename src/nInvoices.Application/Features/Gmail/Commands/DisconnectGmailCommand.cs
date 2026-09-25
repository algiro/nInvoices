using MediatR;

namespace nInvoices.Application.Features.Gmail.Commands;

/// <summary>Revokes and forgets the current user's Gmail authorization. False when none was stored.</summary>
public sealed record DisconnectGmailCommand : IRequest<bool>;
