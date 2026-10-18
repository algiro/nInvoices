using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Features.Rates.Commands;

public sealed class UpdateRateCommandHandler : IRequestHandler<UpdateRateCommand, RateDto>
{
    private readonly IRepository<Rate> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppText _text;

    public UpdateRateCommandHandler(IRepository<Rate> repository, IUnitOfWork unitOfWork, IAppText text)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<RateDto> Handle(UpdateRateCommand request, CancellationToken cancellationToken)
    {
        var rate = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (rate == null)
            throw new NotFoundException(_text.Translate("notFound.rate", ("id", request.Id)));

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