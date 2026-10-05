using Mediator;

namespace nInvoices.Application.Features.Rates.Commands;

/// <summary>
/// Command to delete a rate.
/// </summary>
public sealed record DeleteRateCommand(long Id) : IRequest<bool>;
