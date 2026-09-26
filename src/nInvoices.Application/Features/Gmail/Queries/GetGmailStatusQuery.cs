using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Gmail.Queries;

public sealed record GetGmailStatusQuery : IRequest<GmailStatusDto>;
