using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Features.Rates.Commands;

public sealed class CreateRateCommandHandler : IRequestHandler<CreateRateCommand, RateDto>
{
    private readonly IRepository<Rate> _rateRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRateCommandHandler(
        IRepository<Rate> rateRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork)
    {
        _rateRepository = rateRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RateDto> Handle(CreateRateCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Rate;

        var customer = await _customerRepository.GetByIdAsync(dto.CustomerId, cancellationToken);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID {dto.CustomerId} not found");

        var price = new Money(dto.Price.Amount, dto.Price.Currency);
        var rate = new Rate(dto.CustomerId, dto.Type, price);

        await _rateRepository.AddAsync(rate, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RateDto(
            rate.Id,
            rate.CustomerId,
            rate.Type,
            new MoneyDto(rate.Price.Amount, rate.Price.Currency),
            rate.CreatedAt,
            rate.UpdatedAt ?? rate.CreatedAt);
    }
}