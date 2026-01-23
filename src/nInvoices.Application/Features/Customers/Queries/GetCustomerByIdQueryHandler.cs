using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Customers.Queries;

public sealed class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, CustomerDto?>
{
    private readonly IRepository<Customer> _repository;

    public GetCustomerByIdQueryHandler(IRepository<Customer> repository)
    {
        _repository = repository;
    }

    public async Task<CustomerDto?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await _repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (customer == null)
            return null;

        return new CustomerDto(
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
            customer.UpdatedAt ?? customer.CreatedAt);
    }
}
