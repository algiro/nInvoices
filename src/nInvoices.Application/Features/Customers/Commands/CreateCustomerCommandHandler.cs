using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Features.Customers.Commands;

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    private readonly IRepository<Customer> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerCommandHandler(IRepository<Customer> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Customer;
        
        var address = new Address(
            dto.Address.Street,
            dto.Address.HouseNumber,
            dto.Address.City,
            dto.Address.ZipCode,
            dto.Address.Country,
            dto.Address.State);

        var customer = new Customer(dto.Name, dto.FiscalId, address);

        await _repository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
