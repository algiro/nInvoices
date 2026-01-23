using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Customers.Queries;

public sealed class GetAllCustomersQueryHandler : IRequestHandler<GetAllCustomersQuery, IEnumerable<CustomerDto>>
{
    private readonly IRepository<Customer> _repository;

    public GetAllCustomersQueryHandler(IRepository<Customer> repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CustomerDto>> Handle(GetAllCustomersQuery request, CancellationToken cancellationToken)
    {
        var customers = await _repository.GetAllAsync(cancellationToken);

        return customers.Select(customer => new CustomerDto(
            customer.Id,
            customer.Name,
            customer.FiscalId,
            new AddressDto(
                customer.Address.Street,
                customer.Address.HouseNumber,
                customer.Address.City,
                customer.Address.ZipCode,
                customer.Address.Country,
                customer.Address.State),
            customer.CreatedAt,
            customer.UpdatedAt ?? customer.CreatedAt));
    }
}
