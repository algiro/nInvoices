using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using Shouldly;

namespace nInvoices.Core.Tests.Entities;

[TestFixture]
public sealed class HolidayRuleTests
{
    [TestCase(2019, 4, 21)]
    [TestCase(2024, 3, 31)]
    [TestCase(2025, 4, 20)]
    [TestCase(2026, 4, 5)]
    [TestCase(2027, 3, 28)]
    [TestCase(2038, 4, 25)]
    public void EasterSunday_KnownYears_ReturnsTheRightDate(int year, int month, int day)
    {
        HolidayRule.EasterSunday(year).ShouldBe(new DateOnly(year, month, day));
    }

    [Test]
    public void DateIn_EasterOffset_CountsFromEasterSunday()
    {
        HolidayRule.FromEaster("Easter Monday", 1).DateIn(2026).ShouldBe(new DateOnly(2026, 4, 6));
        HolidayRule.FromEaster("Good Friday", -2).DateIn(2026).ShouldBe(new DateOnly(2026, 4, 3));
    }

    [Test]
    public void DateIn_Fixed_IsTheSameDayEveryYear()
    {
        HolidayRule.Fixed("Christmas", 12, 25).DateIn(2031).ShouldBe(new DateOnly(2031, 12, 25));
    }

    [Test]
    public void DateIn_29FebruaryInACommonYear_IsNull()
    {
        var rule = HolidayRule.Fixed("Leap day", 2, 29);

        rule.DateIn(2028).ShouldBe(new DateOnly(2028, 2, 29));
        rule.DateIn(2027).ShouldBeNull();
    }

    [TestCase(1, DayOfWeek.Monday, 3, 2026, 1, 19)] // Martin Luther King Jr. Day
    [TestCase(11, DayOfWeek.Thursday, 4, 2026, 11, 26)] // Thanksgiving
    [TestCase(5, DayOfWeek.Monday, 1, 2026, 5, 4)] // Early May bank holiday
    [TestCase(5, DayOfWeek.Monday, -1, 2026, 5, 25)] // Spring bank holiday
    [TestCase(8, DayOfWeek.Monday, -1, 2026, 8, 31)] // Summer bank holiday
    [TestCase(6, DayOfWeek.Monday, 5, 2026, 6, 29)] // a fifth Monday that exists
    public void DateIn_NthWeekday_FindsTheWeekday(int month, DayOfWeek weekday, int occurrence, int year, int expectedMonth, int expectedDay)
    {
        HolidayRule.NthWeekdayOf("Holiday", month, weekday, occurrence).DateIn(year)
            .ShouldBe(new DateOnly(year, expectedMonth, expectedDay));
    }

    [Test]
    public void DateIn_FifthWeekdayTheMonthDoesNotHave_IsNull()
    {
        HolidayRule.NthWeekdayOf("Holiday", 2, DayOfWeek.Monday, 5).DateIn(2026).ShouldBeNull();
    }

    [Test]
    public void DateIn_OutsideTheYearRange_IsNull()
    {
        var rule = HolidayRule.Fixed("San Francesco", 10, 4, fromYear: 2026, toYear: 2030);

        rule.DateIn(2025).ShouldBeNull();
        rule.DateIn(2026).ShouldBe(new DateOnly(2026, 10, 4));
        rule.DateIn(2031).ShouldBeNull();
    }

    [Test]
    public void Define_ChangingKind_ClearsTheFieldsOfTheOldKind()
    {
        var rule = HolidayRule.Fixed("Holiday", 5, 1);

        rule.Define("Holiday", HolidayRuleKind.EasterOffset, 5, 1, 39, null, null, null, null);

        rule.Month.ShouldBeNull();
        rule.Day.ShouldBeNull();
        rule.EasterOffset.ShouldBe(39);
    }

    [TestCase(HolidayRuleKind.Fixed, 13, 1, null, null, null)]
    [TestCase(HolidayRuleKind.Fixed, 4, 31, null, null, null)]
    [TestCase(HolidayRuleKind.EasterOffset, null, null, null, null, null)]
    [TestCase(HolidayRuleKind.NthWeekday, 5, null, null, DayOfWeek.Monday, 0)]
    [TestCase(HolidayRuleKind.NthWeekday, 5, null, null, null, 1)]
    public void Create_FieldsThatDoNotFitTheKind_Throws(
        HolidayRuleKind kind, int? month, int? day, int? easterOffset, DayOfWeek? weekday, int? occurrence)
    {
        Should.Throw<ArgumentException>(() =>
            HolidayRule.Create("Holiday", kind, month, day, easterOffset, weekday, occurrence, null, null));
    }

    [Test]
    public void Create_FromYearAfterToYear_Throws()
    {
        Should.Throw<ArgumentException>(() => HolidayRule.Fixed("Holiday", 1, 1, fromYear: 2030, toYear: 2020));
    }

    [Test]
    public void HolidaysIn_SkipsInactiveRulesAndSortsByDate()
    {
        var inactive = HolidayRule.Fixed("Off", 3, 1);
        inactive.Deactivate();
        var calendar = new HolidayCalendar("it")
        {
            Rules = [HolidayRule.Fixed("Christmas", 12, 25), inactive, HolidayRule.Fixed("New Year", 1, 1)]
        };

        calendar.CountryCode.ShouldBe("IT");
        calendar.HolidaysIn(2026).Select(h => h.Name).ShouldBe(["New Year", "Christmas"]);
    }

    [TestCase("ITA")]
    [TestCase("1T")]
    [TestCase("")]
    public void HolidayCalendar_InvalidCountryCode_Throws(string code)
    {
        Should.Throw<ArgumentException>(() => new HolidayCalendar(code));
    }
}
