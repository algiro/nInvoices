using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Taxes.Commands;

/// <summary>
/// Command to create a new tax configuration for a customer.
/// Supports percentage, fixed amount, and compound taxes.
/// </summary>
public sealed record CreateTaxCommand(CreateTaxDto Tax) : IRequest<TaxDto>;