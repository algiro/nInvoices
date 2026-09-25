using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Holidays;

namespace nInvoices.Application.Features.Holidays.Queries;

public sealed class GetHolidayCalendarQueryHandler : IRequestHandler<GetHolidayCalendarQuery, HolidayCalendarDto?>
{
    private readonly IHolidayCalendarService _holidayCalendarService;

    public GetHolidayCalendarQueryHandler(IHolidayCalendarService holidayCalendarService)
    {
        _holidayCalendarService = holidayCalendarService;
    }

    public async Task<HolidayCalendarDto?> Handle(GetHolidayCalendarQuery request, CancellationToken cancellationToken)
    {
        var calendar = await _holidayCalendarService.GetCalendarAsync(request.CountryCode, cancellationToken);
        return calendar is null ? null : HolidayMapper.ToDto(calendar, request.Year);
    }
}
