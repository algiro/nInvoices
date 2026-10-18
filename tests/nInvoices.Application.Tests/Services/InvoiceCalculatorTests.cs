using nInvoices.Application.DTOs;
using nInvoices.Application.Services.InvoiceGeneration;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;
using nInvoices.Core.ValueObjects;
using Shouldly;
using nInvoices.Application.Tests.TestDoubles;

namespace nInvoices.Application.Tests.Services;

/// <summary>The invoice arithmetic on plain values: no repositories, no mocks.</summary>
[TestFixture]
public sealed class InvoiceCalculatorTests
{
    private static Rate Rate(long id, RateType type, decimal amount, string currency = "EUR") =>
        new(1, type, new Money(amount, currency)) { Id = id };

    private static WorkDayDto Day(int day, DayType type = DayType.Worked, decimal? hours = null, long? rateId = null, params (string Name, decimal Hours)[] projects) =>
        new(new DateOnly(2026, 9, day), type, hours,
            Projects: projects.Length == 0 ? null : projects.Select(p => new WorkDayProjectDto(p.Name, p.Hours)).ToList(),
            RateId: rateId);

    private static GenerateInvoiceDto Monthly(params WorkDayDto[] days) =>
        new() { CustomerId = 1, InvoiceType = InvoiceType.Monthly, IssueDate = new DateOnly(2026, 9, 30), Year = 2026, Month = 9, WorkDays = days };

    private static GenerateInvoiceDto OneTime(decimal? hours = null) =>
        new() { CustomerId = 1, InvoiceType = InvoiceType.OneTime, IssueDate = new DateOnly(2026, 9, 30), Hours = hours };

    [Test]
    public void Subtotal_DailyRate_BillsWorkedDays_PartialDaysAsHoursOverEight()
    {
        var rates = new DayRates(Rate(1, RateType.Daily, 400m), [], TestText.Instance);
        var dto = Monthly(Day(1), Day(2, hours: 4m), Day(3, DayType.PublicHoliday));

        InvoiceCalculator.Subtotal(dto, rates).ShouldBe(new Money(600m, "EUR"));
    }

    [Test]
    public void Subtotal_HourlyRate_BillsProjectHoursBeforeTheDaysHours()
    {
        var rates = new DayRates(Rate(1, RateType.Hourly, 50m), [], TestText.Instance);
        var dto = Monthly(Day(1, hours: 8m, projects: [("A", 3m), ("B", 2m)]), Day(2, hours: 6m));

        InvoiceCalculator.Subtotal(dto, rates).ShouldBe(new Money(550m, "EUR")); // (5 + 6) h × 50
    }

    [Test]
    public void Subtotal_DaysWithTheirOwnRate_AreBilledAtIt()
    {
        var hourly = Rate(2, RateType.Hourly, 100m);
        var rates = new DayRates(Rate(1, RateType.Daily, 400m), [hourly], TestText.Instance);
        var dto = Monthly(Day(1), Day(2, hours: 3m, rateId: 2));

        InvoiceCalculator.Subtotal(dto, rates).ShouldBe(new Money(700m, "EUR"));
    }

    [Test]
    public void Subtotal_FixedMonthlyRate_IsThePrice_WhateverTheDays()
    {
        var rates = new DayRates(Rate(1, RateType.Monthly, 9000m), [], TestText.Instance);

        InvoiceCalculator.Subtotal(Monthly(Day(1), Day(2)), rates).ShouldBe(new Money(9000m, "EUR"));
    }

    [Test]
    public void Subtotal_OneTime_HoursTimesHourlyRate_OrTheFixedPrice()
    {
        InvoiceCalculator.Subtotal(OneTime(hours: 2.5m), new DayRates(Rate(1, RateType.Hourly, 80m), [], TestText.Instance))
            .ShouldBe(new Money(200m, "EUR"));
        InvoiceCalculator.Subtotal(OneTime(), new DayRates(Rate(1, RateType.Daily, 500m), [], TestText.Instance))
            .ShouldBe(new Money(500m, "EUR"));
    }

    [Test]
    public void EnsureBillable_HourlyDayWithoutHours_IsRefused()
    {
        var rates = new DayRates(Rate(1, RateType.Hourly, 50m), [], TestText.Instance);

        Should.Throw<DomainException>(() => InvoiceCalculator.EnsureBillable(Monthly(Day(1, hours: 8m), Day(2)), rates, TestText.Instance))
            .Message.ShouldContain("Missing hours for 1 day(s)");
    }

    [Test]
    public void EnsureBillable_HourlyMonthlyWithoutDays_AndHourlyOneTimeWithoutHours_AreRefused()
    {
        var rates = new DayRates(Rate(1, RateType.Hourly, 50m), [], TestText.Instance);

        Should.Throw<DomainException>(() => InvoiceCalculator.EnsureBillable(Monthly(), rates, TestText.Instance));
        Should.Throw<DomainException>(() => InvoiceCalculator.EnsureBillable(OneTime(), rates, TestText.Instance));
        Should.NotThrow(() => InvoiceCalculator.EnsureBillable(OneTime(hours: 1m), rates, TestText.Instance));
    }

    [Test]
    public void ExpensesTotal_AddsUp_InTheExpensesCurrency()
    {
        var expenses = new List<ExpenseDto>
        {
            new() { Description = "Train", Amount = 40m, Currency = "CHF" },
            new() { Description = "Hotel", Amount = 110m, Currency = "CHF" }
        };

        InvoiceCalculator.ExpensesTotal(expenses, "EUR").ShouldBe(new Money(150m, "CHF"));
        InvoiceCalculator.ExpensesTotal(null, "EUR").ShouldBe(Money.Zero("EUR"));
    }

    [Test]
    public void MergeAllocations_JoinsTheSameProjectWithinADay_IgnoringCaseAndEmptyHours()
    {
        var merged = InvoiceCalculator.MergeAllocations([Day(1, projects: [("Alpha", 2m), (" alpha ", 3m), ("Beta", 0m)]), Day(2)]);

        merged[0].Projects.ShouldNotBeNull().ShouldHaveSingleItem().ShouldBe(new WorkDayProjectDto("Alpha", 5m));
        merged[1].Projects.ShouldBeNull();
    }
}
