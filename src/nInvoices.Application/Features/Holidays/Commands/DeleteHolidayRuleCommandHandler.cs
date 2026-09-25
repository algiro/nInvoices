using MediatR;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Holidays.Commands;

public sealed class DeleteHolidayRuleCommandHandler : IRequestHandler<DeleteHolidayRuleCommand, bool>
{
    private readonly IRepository<HolidayRule> _ruleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteHolidayRuleCommandHandler(IRepository<HolidayRule> ruleRepository, IUnitOfWork unitOfWork)
    {
        _ruleRepository = ruleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteHolidayRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _ruleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (rule is null)
            return false;

        await _ruleRepository.DeleteAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
