using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Customers.Queries;

public sealed record GetCustomerByIdQuery(long Id) : IRequest<CustomerDto?>;
