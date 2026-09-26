using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Holidays.Queries;

public sealed class GetHolidayCountriesQueryHandler : IRequestHandler<GetHolidayCountriesQuery, IReadOnlyList<HolidayCountryDto>>
{
    private readonly IRepository<HolidayCalendar> _calendarRepository;

    public GetHolidayCountriesQueryHandler(IRepository<HolidayCalendar> calendarRepository)
    {
        _calendarRepository = calendarRepository;
    }

    public async Task<IReadOnlyList<HolidayCountryDto>> Handle(GetHolidayCountriesQuery request, CancellationToken cancellationToken)
    {
        var stored = (await _calendarRepository.GetAllAsync(cancellationToken)).Select(c => c.CountryCode).ToHashSet();

        return BuiltInHolidays.CountryCodes
            .Union(stored)
            .Select(code => new HolidayCountryDto(code, CountryCodes.DisplayName(code), BuiltInHolidays.Has(code), stored.Contains(code)))
            .OrderBy(c => c.CountryName)
            .ToList();
    }
}
