using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Core.Tests.Entities;

/// <summary>The tax prediction's records: profile, business expenses, recurring expenses, payments.</summary>
[TestFixture]
public sealed class TaxRecordsTests
{
    private static Money Eur(decimal amount) => new(amount, "EUR");

    [Test]
    public void TaxProfile_NormalizesTheCountry_AndDropsEmptyValues()
    {
        var profile = new TaxProfile(" es ", " eds ", new Dictionary<string, string>
        {
            ["territory"] = " canarias ",
            ["deductWithholdings"] = "false",
            ["empty"] = " "
        });

        (profile.CountryCode, profile.RegimeId).ShouldBe(("ES", "eds"));
        profile.Values.ShouldBe(new Dictionary<string, string> { ["territory"] = "canarias", ["deductWithholdings"] = "false" });
    }

    [TestCase("ESP", "eds")]
    [TestCase("ES", " ")]
    public void TaxProfile_InvalidCountryOrRegime_Throws(string country, string regime)
    {
        Should.Throw<ArgumentException>(() => new TaxProfile(country, regime, null));
    }

    [Test]
    public void BusinessExpense_KeepsWhatItIsGiven_Trimmed()
    {
        var expense = new BusinessExpense(new DateOnly(2026, 3, 2), " Laptop ", Eur(1000m), ExpenseCategory.Equipment, 70m,
            deductiblePercent: 50m, supplier: "  ");

        expense.Description.ShouldBe("Laptop");
        expense.Supplier.ShouldBeNull();
        (expense.IndirectTax, expense.DeductiblePercent, expense.ReverseCharge).ShouldBe((70m, 50m, false));
    }

    [TestCase(-1, 0, false, 100)]    // negative amount
    [TestCase(10, -1, false, 100)]   // negative tax
    [TestCase(10, 1, true, 100)]     // reverse charge with tax
    [TestCase(10, 0, false, 101)]    // over 100%
    [TestCase(10, 0, false, -1)]
    public void BusinessExpense_InvalidValues_Throw(decimal amount, decimal tax, bool reverseCharge, decimal percent)
    {
        Should.Throw<ArgumentException>(() =>
            new BusinessExpense(new DateOnly(2026, 1, 1), "x", Eur(amount), ExpenseCategory.Other, tax, reverseCharge, percent));
    }

    [Test]
    public void BusinessExpense_UnknownCategory_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new BusinessExpense(new DateOnly(2026, 1, 1), "x", Eur(1m), (ExpenseCategory)99));
    }

    [Test]
    public void RecurringExpense_StoresWholeMonths()
    {
        var fee = new RecurringExpense("Fee", Eur(300.00m), ExpenseCategory.SocialSecurity, new DateOnly(2026, 1, 15), new DateOnly(2026, 6, 30));

        (fee.FromMonth, fee.ToMonth).ShouldBe((new DateOnly(2026, 1, 1), (DateOnly?)new DateOnly(2026, 6, 1)));
    }

    [Test]
    public void RecurringExpense_MonthsIn_AreTheMonthsOfTheYearItRuns()
    {
        var fee = new RecurringExpense("Fee", Eur(500.00m), ExpenseCategory.SocialSecurity, new DateOnly(2025, 11, 1));
        var ended = new RecurringExpense("Old", Eur(10m), ExpenseCategory.Software, new DateOnly(2026, 2, 1), new DateOnly(2026, 4, 1));

        fee.MonthsIn(2025).ShouldBe([new DateOnly(2025, 11, 1), new DateOnly(2025, 12, 1)]);
        fee.MonthsIn(2026).Count().ShouldBe(12);
        ended.MonthsIn(2026).Select(d => d.Month).ShouldBe([2, 3, 4]);
        ended.MonthsIn(2027).ShouldBeEmpty();
    }

    [Test]
    public void RecurringExpense_MonthsBetween_CountsTheMonthsStartingInTheRange()
    {
        var fee = new RecurringExpense("Fee", Eur(500.00m), ExpenseCategory.SocialSecurity, new DateOnly(2026, 1, 1));

        // A United Kingdom tax year: 6 April to 5 April, so May to April
        var months = fee.MonthsBetween(new DateOnly(2026, 4, 6), new DateOnly(2027, 4, 5)).ToList();

        months.Count.ShouldBe(12);
        months[0].ShouldBe(new DateOnly(2026, 5, 1));
        months[^1].ShouldBe(new DateOnly(2027, 4, 1));
    }

    [Test]
    public void TaxYear_Calendar_IsJanuaryToDecember()
    {
        var year = nInvoices.Core.TaxPrediction.TaxYear.Calendar(2026);

        (year.Start, year.End, year.Label).ShouldBe((new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "2026"));
        year.Contains(new DateOnly(2026, 12, 31)).ShouldBeTrue();
        year.Contains(new DateOnly(2027, 1, 1)).ShouldBeFalse();
    }

    [Test]
    public void RecurringExpense_EndingBeforeItStarts_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new RecurringExpense("Fee", Eur(1m), ExpenseCategory.Other, new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 30)));
    }

    [Test]
    public void TaxPayment_UpdatesAmountAndDate()
    {
        var payment = new TaxPayment(2026, 1, " 130 ", Eur(2625.00m), new DateOnly(2026, 4, 20));

        payment.Update(Eur(2625.61m), new DateOnly(2026, 4, 19));

        (payment.Form, payment.Amount.Amount, payment.PaidOn).ShouldBe(("130", 2625.61m, new DateOnly(2026, 4, 19)));
    }

    [TestCase(1999, 1, "130", 1)]
    [TestCase(2026, 0, "130", 1)]
    [TestCase(2026, 25, "130", 1)]
    [TestCase(2026, 1, "", 1)]
    [TestCase(2026, 1, "130", -1)]
    public void TaxPayment_InvalidValues_Throw(int year, int period, string form, decimal amount)
    {
        Should.Throw<ArgumentException>(() => new TaxPayment(year, period, form, Eur(amount), new DateOnly(2026, 4, 20)));
    }
}
