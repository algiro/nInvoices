using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Holidays.Commands;

public sealed class CreateHolidayRuleCommandHandler : IRequestHandler<CreateHolidayRuleCommand, HolidayRuleDto>
{
    private readonly IHolidayCalendarService _holidayCalendarService;
    private readonly IRepository<HolidayCalendar> _calendarRepository;
    private readonly IRepository<HolidayRule> _ruleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateHolidayRuleCommandHandler(
        IHolidayCalendarService holidayCalendarService,
        IRepository<HolidayCalendar> calendarRepository,
        IRepository<HolidayRule> ruleRepository,
        IUnitOfWork unitOfWork)
    {
        _holidayCalendarService = holidayCalendarService;
        _calendarRepository = calendarRepository;
        _ruleRepository = ruleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<HolidayRuleDto> Handle(CreateHolidayRuleCommand request, CancellationToken cancellationToken)
    {
        // Validate the rule before anything is stored
        var rule = new HolidayRule();
        HolidayMapper.Apply(rule, request.Rule);

        var calendar = await _holidayCalendarService.GetCalendarAsync(request.CountryCode, cancellationToken);
        if (calendar is null)
        {
            calendar = new HolidayCalendar(request.CountryCode);
            await _calendarRepository.AddAsync(calendar, cancellationToken);
        }

        rule.HolidayCalendar = calendar;
        await _ruleRepository.AddAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return HolidayMapper.ToDto(rule);
    }
}
