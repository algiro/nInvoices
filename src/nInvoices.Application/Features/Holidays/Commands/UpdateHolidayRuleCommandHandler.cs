using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Holidays.Commands;

public sealed class UpdateHolidayRuleCommandHandler : IRequestHandler<UpdateHolidayRuleCommand, HolidayRuleDto?>
{
    private readonly IRepository<HolidayRule> _ruleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateHolidayRuleCommandHandler(IRepository<HolidayRule> ruleRepository, IUnitOfWork unitOfWork)
    {
        _ruleRepository = ruleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<HolidayRuleDto?> Handle(UpdateHolidayRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _ruleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (rule is null)
            return null;

        HolidayMapper.Apply(rule, request.Rule);
        await _ruleRepository.UpdateAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return HolidayMapper.ToDto(rule);
    }
}
