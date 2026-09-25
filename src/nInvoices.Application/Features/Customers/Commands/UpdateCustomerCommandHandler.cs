using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Features.Customers.Commands;

public sealed class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, CustomerDto>
{
    private readonly IRepository<Customer> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerCommandHandler(IRepository<Customer> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CustomerDto> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID {request.Id} not found");

        var dto = request.Customer;
        
        var address = new Address(
            dto.Address.Street,
            dto.Address.HouseNumber,
            dto.Address.City,
            dto.Address.ZipCode,
            dto.Address.Country,
            dto.Address.State);

        customer.Update(dto.Name, dto.FiscalId, address, dto.Locale);
        CustomerContact.Apply(customer, dto.Email, dto.CcEmails);

        await _repository.UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CustomerMapper.ToDto(customer);
    }
}
