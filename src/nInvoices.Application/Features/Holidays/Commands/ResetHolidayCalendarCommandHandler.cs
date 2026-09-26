using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Holidays.Commands;

public sealed class ResetHolidayCalendarCommandHandler : IRequestHandler<ResetHolidayCalendarCommand, HolidayCalendarDto?>
{
    private readonly IHolidayCalendarService _holidayCalendarService;
    private readonly IRepository<HolidayRule> _ruleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResetHolidayCalendarCommandHandler(
        IHolidayCalendarService holidayCalendarService,
        IRepository<HolidayRule> ruleRepository,
        IUnitOfWork unitOfWork)
    {
        _holidayCalendarService = holidayCalendarService;
        _ruleRepository = ruleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<HolidayCalendarDto?> Handle(ResetHolidayCalendarCommand request, CancellationToken cancellationToken)
    {
        var code = HolidayCalendar.NormalizeCountryCode(request.CountryCode);
        if (!BuiltInHolidays.Has(code))
            return null;

        // Exists: a country with built-in rules always gets a calendar
        var calendar = (await _holidayCalendarService.GetCalendarAsync(code, cancellationToken))!;

        foreach (var rule in calendar.Rules.ToList())
            await _ruleRepository.DeleteAsync(rule, cancellationToken);

        var builtIn = BuiltInHolidays.RulesFor(code);
        foreach (var rule in builtIn)
        {
            rule.HolidayCalendar = calendar;
            await _ruleRepository.AddAsync(rule, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        calendar.Rules = builtIn.ToList();
        return HolidayMapper.ToDto(calendar, request.Year);
    }
}
