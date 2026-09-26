using MediatR;

namespace nInvoices.Application.Features.Taxes.Commands;

/// <summary>
/// Command to delete a tax configuration.
/// </summary>
public sealed record DeleteTaxCommand(long Id) : IRequest<bool>;