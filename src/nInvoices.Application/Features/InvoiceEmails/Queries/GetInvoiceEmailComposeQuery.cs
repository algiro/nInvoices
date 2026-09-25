using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceEmails.Queries;

/// <summary>
/// Prepares the email for an invoice from the chosen (or the customer's active) template,
/// for the user to review. Null when the invoice does not exist.
/// </summary>
public sealed record GetInvoiceEmailComposeQuery(long InvoiceId, long? TemplateId = null) : IRequest<InvoiceEmailComposeDto?>;
