using System.Linq.Expressions;
using Moq;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Services;

[TestFixture]
public sealed class HolidayCalendarServiceTests
{
    private List<HolidayCalendar> _calendars = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private HolidayCalendarService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _calendars = [];

        var calendarRepository = new Mock<IRepository<HolidayCalendar>>();
        calendarRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<HolidayCalendar, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<HolidayCalendar, bool>> p, CancellationToken _) => _calendars.Where(p.Compile()).ToList());
        calendarRepository
            .Setup(r => r.AddAsync(It.IsAny<HolidayCalendar>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HolidayCalendar c, CancellationToken _) =>
            {
                c.Id = _calendars.Count + 1;
                _calendars.Add(c);
                return c;
            });

        var ruleRepository = new Mock<IRepository<HolidayRule>>();
        ruleRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<HolidayRule, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<HolidayRule, bool>> p, CancellationToken _) =>
                _calendars.SelectMany(c => c.Rules.Select(r => { r.HolidayCalendarId = c.Id; return r; })).Where(p.Compile()).ToList());

        _unitOfWork = new Mock<IUnitOfWork>();
        _service = new HolidayCalendarService(calendarRepository.Object, ruleRepository.Object, _unitOfWork.Object);
    }

    [Test]
    public async Task GetHolidaysAsync_Italy2026_ReturnsTheNationalHolidays()
    {
        var holidays = await _service.GetHolidaysAsync("IT", 2026, null, TestContext.CurrentContext.CancellationToken);

        holidays.Select(h => h.Date).ShouldBe(
        [
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), new DateOnly(2026, 4, 6),
            new DateOnly(2026, 4, 25), new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 2),
            new DateOnly(2026, 8, 15), new DateOnly(2026, 10, 4), new DateOnly(2026, 11, 1),
            new DateOnly(2026, 12, 8), new DateOnly(2026, 12, 25), new DateOnly(2026, 12, 26)
        ]);
    }

    [Test]
    public async Task GetHolidaysAsync_WithMonth_ReturnsOnlyThatMonth()
    {
        var holidays = await _service.GetHolidaysAsync("it", 2026, 12, TestContext.CurrentContext.CancellationToken);

        holidays.Select(h => h.Name).ShouldBe(["Immacolata Concezione", "Natale", "Santo Stefano"]);
    }

    [Test]
    public async Task GetCalendarAsync_FirstUse_StoresTheBuiltInRulesOnce()
    {
        var ct = TestContext.CurrentContext.CancellationToken;

        await _service.GetCalendarAsync("DE", ct);
        await _service.GetCalendarAsync("DE", ct);

        _calendars.ShouldHaveSingleItem().Rules.Count.ShouldBe(BuiltInHolidays.RulesFor("DE").Count);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetCalendarAsync_EditedCalendar_UsesTheStoredRules()
    {
        var calendar = new HolidayCalendar("IT") { Id = 1, Rules = [HolidayRule.Fixed("Sant'Ambrogio", 12, 7)] };
        _calendars.Add(calendar);

        var holidays = await _service.GetHolidaysAsync("IT", 2026, 12, TestContext.CurrentContext.CancellationToken);

        holidays.ShouldHaveSingleItem().Name.ShouldBe("Sant'Ambrogio");
    }

    [Test]
    public async Task GetHolidaysAsync_CountryWithoutRules_IsEmptyAndStoresNothing()
    {
        var holidays = await _service.GetHolidaysAsync("JP", 2026, null, TestContext.CurrentContext.CancellationToken);

        holidays.ShouldBeEmpty();
        _calendars.ShouldBeEmpty();
    }

    [TestCase(null, "Italy", "IT")]
    [TestCase(null, "Italia", "IT")]
    [TestCase(null, " germany ", "DE")]
    [TestCase(null, "UK", "GB")]
    [TestCase(null, "Atlantis", null)]
    [TestCase("CH", "Italy", "CH")]
    public void ResolveCountry_UsesTheChosenCountryOrTheAddress(string? chosen, string addressCountry, string? expected)
    {
        var customer = new Customer("Acme", "ACME1", new Address("Via Roma", "1", "Milano", "20121", addressCountry));
        customer.SetHolidayCountry(chosen);

        _service.ResolveCountry(customer).ShouldBe(expected);
    }

    [Test]
    public void BuiltInHolidays_EveryRuleGivesADateIn2026()
    {
        foreach (var code in BuiltInHolidays.CountryCodes)
            BuiltInHolidays.RulesFor(code).ShouldAllBe(r => r.DateIn(2026) != null, $"country {code}");
    }
}
