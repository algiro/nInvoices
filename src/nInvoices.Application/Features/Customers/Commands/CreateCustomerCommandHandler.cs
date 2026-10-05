using Mediator;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Features.Customers.Commands;

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    private readonly IRepository<Customer> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICustomerCompliance _compliance;

    public CreateCustomerCommandHandler(IRepository<Customer> repository, IUnitOfWork unitOfWork, ICustomerCompliance compliance)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _compliance = compliance;
    }

    public async ValueTask<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Customer;
        
        var address = new Address(
            dto.Address.Street,
            dto.Address.HouseNumber,
            dto.Address.City,
            dto.Address.ZipCode,
            dto.Address.Country,
            dto.Address.State);

        var customer = new Customer(dto.Name, dto.FiscalId, address, dto.Locale);
        CustomerContact.Apply(customer, dto.Email, dto.CcEmails);
        customer.SetHolidayCountry(dto.HolidayCountry);
        await _compliance.ApplyAsync(customer, dto.ComplianceValues, cancellationToken);

        await _repository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CustomerMapper.ToDto(customer);
    }
}
