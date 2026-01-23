using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Customers.Queries;

public sealed record GetAllCustomersQuery : IRequest<IEnumerable<CustomerDto>>;
