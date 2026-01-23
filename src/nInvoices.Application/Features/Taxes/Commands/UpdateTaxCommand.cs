using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Taxes.Commands;

/// <summary>
/// Command to update an existing tax configuration.
/// </summary>
public sealed record UpdateTaxCommand(long Id, UpdateTaxDto Tax) : IRequest<TaxDto>;