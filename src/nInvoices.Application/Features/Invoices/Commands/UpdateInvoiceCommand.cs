using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Commands;

/// <summary>
/// Command to update a draft invoice.
/// Only draft invoices can be updated.
/// </summary>
public sealed record UpdateInvoiceCommand(long Id, UpdateInvoiceDto Invoice) : IRequest<InvoiceDto>;