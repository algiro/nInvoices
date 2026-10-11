using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Entities;

/// <summary>
/// What the user actually paid for one tax form and period. The prediction shows it next to the
/// estimate, and cumulative forms (Spain's Modelo 130) subtract it in the later periods.
/// </summary>
public sealed class TaxPayment : OwnedEntityBase
{
    public int Year { get; private set; }

    /// <summary>1-based period of the year: the quarter, for a quarterly form.</summary>
    public int Period { get; private set; }

    /// <summary>The form's code, as the country module names it (e.g. "130", "420").</summary>
    public string Form { get; private set; } = string.Empty;

    public Money Amount { get; private set; } = null!;

    public DateOnly PaidOn { get; private set; }

    private TaxPayment() { }

    /// <exception cref="ArgumentException">The year or period is out of range, the form is empty, or the amount is negative.</exception>
    public TaxPayment(int year, int period, string form, Money amount, DateOnly paidOn)
    {
        if (string.IsNullOrWhiteSpace(form))
            throw new ArgumentException("The tax form is required", nameof(form));
        if (year is < 2000 or > 2100)
            throw new ArgumentException("Year must be between 2000 and 2100", nameof(year));
        if (period is < 1 or > 12)
            throw new ArgumentException("Period must be between 1 and 12", nameof(period));

        Year = year;
        Period = period;
        Form = form.Trim();
        Update(amount, paidOn);
    }

    /// <exception cref="ArgumentException">The amount is negative.</exception>
    public void Update(Money amount, DateOnly paidOn)
    {
        ArgumentNullException.ThrowIfNull(amount);
        if (amount.Amount < 0)
            throw new ArgumentException("The amount cannot be negative", nameof(amount));

        Amount = amount;
        PaidOn = paidOn;
    }
}
