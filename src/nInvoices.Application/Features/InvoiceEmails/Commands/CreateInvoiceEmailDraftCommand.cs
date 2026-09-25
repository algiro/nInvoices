using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceEmails.Commands;

/// <summary>
/// Creates a draft in the current user's Gmail with the reviewed subject/body and the invoice
/// documents attached, and records it on the invoice. The user sends it from Gmail.
/// </summary>
public sealed record CreateInvoiceEmailDraftCommand(long InvoiceId, CreateInvoiceEmailDraftDto Email) : IRequest<InvoiceEmailDto>;
