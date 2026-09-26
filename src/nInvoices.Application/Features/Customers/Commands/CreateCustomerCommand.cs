using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Customers.Commands;

/// <summary>
/// Command to create a new customer.
/// Follows CQRS pattern - separates write operations from reads.
/// </summary>
public sealed record CreateCustomerCommand(CreateCustomerDto Customer) : IRequest<CustomerDto>;
