using Mediator;

namespace nInvoices.Application.Features.Customers.Commands;

/// <summary>
/// Command to delete a customer.
/// </summary>
public sealed record DeleteCustomerCommand(long Id) : IRequest<bool>;
