using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Holidays.Queries;

public sealed class GetCustomerHolidaysQueryHandler : IRequestHandler<GetCustomerHolidaysQuery, CustomerHolidaysDto?>
{
    private readonly IRepository<Customer> _customerRepository;
    private readonly IHolidayCalendarService _holidayCalendarService;

    public GetCustomerHolidaysQueryHandler(
        IRepository<Customer> customerRepository,
        IHolidayCalendarService holidayCalendarService)
    {
        _customerRepository = customerRepository;
        _holidayCalendarService = holidayCalendarService;
    }

    public async Task<CustomerHolidaysDto?> Handle(GetCustomerHolidaysQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
            return null;

        var country = _holidayCalendarService.ResolveCountry(customer);
        if (country is null)
            return new CustomerHolidaysDto(null, null, []);

        var holidays = await _holidayCalendarService.GetHolidaysAsync(country, request.Year, request.Month, cancellationToken);
        return new CustomerHolidaysDto(
            country,
            CountryCodes.DisplayName(country),
            holidays.Select(h => new PublicHolidayDto(h.Date, h.Name)).ToList());
    }
}
