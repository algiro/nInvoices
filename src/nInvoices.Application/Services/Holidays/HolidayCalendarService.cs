using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Services.Holidays;

/// <summary>A public holiday on a date.</summary>
public sealed record PublicHoliday(DateOnly Date, string Name);

/// <summary>
/// Public holiday calendars per country: created from <see cref="BuiltInHolidays"/> on first use,
/// editable afterwards.
/// </summary>
public interface IHolidayCalendarService
{
    /// <summary>
    /// The country's calendar with its rules. When the country has none yet and built-in rules
    /// exist, the calendar is created from them and saved. Null for a country with neither.
    /// </summary>
    Task<HolidayCalendar?> GetCalendarAsync(string countryCode, CancellationToken cancellationToken = default);

    /// <summary>The country's public holidays in a year, or in one month of it; empty for an unknown country.</summary>
    Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(string countryCode, int year, int? month, CancellationToken cancellationToken = default);

    /// <summary>The customer's holiday country: the one chosen on the customer, otherwise the address country.</summary>
    string? ResolveCountry(Customer customer);
}

public sealed class HolidayCalendarService : IHolidayCalendarService
{
    private readonly IRepository<HolidayCalendar> _calendarRepository;
    private readonly IRepository<HolidayRule> _ruleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public HolidayCalendarService(
        IRepository<HolidayCalendar> calendarRepository,
        IRepository<HolidayRule> ruleRepository,
        IUnitOfWork unitOfWork)
    {
        _calendarRepository = calendarRepository;
        _ruleRepository = ruleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<HolidayCalendar?> GetCalendarAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        var code = HolidayCalendar.NormalizeCountryCode(countryCode);

        var calendar = (await _calendarRepository.FindAsync(c => c.CountryCode == code, cancellationToken)).FirstOrDefault();
        if (calendar is not null)
        {
            calendar.Rules = (await _ruleRepository.FindAsync(r => r.HolidayCalendarId == calendar.Id, cancellationToken)).ToList();
            return calendar;
        }

        if (!BuiltInHolidays.Has(code))
            return null;

        calendar = new HolidayCalendar(code) { Rules = BuiltInHolidays.RulesFor(code).ToList() };
        await _calendarRepository.AddAsync(calendar, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return calendar;
    }

    public async Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(
        string countryCode,
        int year,
        int? month,
        CancellationToken cancellationToken = default)
    {
        var calendar = await GetCalendarAsync(countryCode, cancellationToken);
        if (calendar is null)
            return [];

        return calendar.HolidaysIn(year)
            .Where(h => month is null || h.Date.Month == month)
            .Select(h => new PublicHoliday(h.Date, h.Name))
            .ToList();
    }

    public string? ResolveCountry(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return customer.HolidayCountry ?? CountryCodes.FromName(customer.Address?.Country);
    }
}
