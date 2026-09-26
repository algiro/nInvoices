using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Commands;

/// <summary>
/// Command to generate a new invoice from worked days and expenses.
/// Orchestrates: template selection, rate calculation, tax application, rendering.
/// </summary>
public sealed record GenerateInvoiceCommand(GenerateInvoiceDto Invoice) : IRequest<InvoiceDto>;