using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Features.Rates.Commands;

public sealed class UpdateRateCommandHandler : IRequestHandler<UpdateRateCommand, RateDto>
{
    private readonly IRepository<Rate> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRateCommandHandler(IRepository<Rate> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RateDto> Handle(UpdateRateCommand request, CancellationToken cancellationToken)
    {
        var rate = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (rate == null)
            throw new KeyNotFoundException($"Rate with ID {request.Id} not found");

        var dto = request.Rate;
        var price = new Money(dto.Price.Amount, dto.Price.Currency);

        rate.Type = dto.Type;
        rate.Price = price;
        rate.SetName(dto.Name);

        await _repository.UpdateAsync(rate, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return RateMapper.ToDto(rate);
    }
}