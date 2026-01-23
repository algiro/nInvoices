using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Customers.Commands;

/// <summary>
/// Command to update an existing customer.
/// </summary>
public sealed record UpdateCustomerCommand(long Id, UpdateCustomerDto Customer) : IRequest<CustomerDto>;
