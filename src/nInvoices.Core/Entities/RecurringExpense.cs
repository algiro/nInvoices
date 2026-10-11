using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Entities;

/// <summary>
/// A business expense paid every month for a span of months, such as the social security fee. It is
/// counted month by month, without storing a row per month; a change of amount is a new one starting
/// the month after the old one ends.
/// </summary>
public sealed class RecurringExpense : OwnedEntityBase
{
    public string Description { get; private set; } = string.Empty;

    /// <summary>The amount of each month, without indirect tax.</summary>
    public Money Amount { get; private set; } = null!;

    public ExpenseCategory Category { get; private set; }

    /// <summary>The first month it is paid (always the 1st of the month).</summary>
    public DateOnly FromMonth { get; private set; }

    /// <summary>The last month it is paid (the 1st of the month), or null while it goes on.</summary>
    public DateOnly? ToMonth { get; private set; }

    private RecurringExpense() { }

    /// <exception cref="ArgumentException">See <see cref="Update"/>.</exception>
    public RecurringExpense(string description, Money amount, ExpenseCategory category, DateOnly fromMonth, DateOnly? toMonth = null)
    {
        Update(description, amount, category, fromMonth, toMonth);
    }

    /// <param name="fromMonth">Any day of the first month.</param>
    /// <param name="toMonth">Any day of the last month, or null.</param>
    /// <exception cref="ArgumentException">
    /// The description is empty, the amount is negative, the category is unknown, or it ends before it starts.
    /// </exception>
    public void Update(string description, Money amount, ExpenseCategory category, DateOnly fromMonth, DateOnly? toMonth)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(amount);

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        if (amount.Amount < 0)
            throw new ArgumentException("The amount cannot be negative", nameof(amount));
        if (!Enum.IsDefined(category))
            throw new ArgumentException("Unknown expense category", nameof(category));

        var from = FirstOfMonth(fromMonth);
        var to = toMonth is { } t ? FirstOfMonth(t) : (DateOnly?)null;
        if (to < from)
            throw new ArgumentException("It cannot end before it starts", nameof(toMonth));

        Description = description.Trim();
        Amount = amount;
        Category = category;
        FromMonth = from;
        ToMonth = to;
    }

    /// <summary>The months of <paramref name="year"/> it is paid in, as the 1st of each month.</summary>
    public IEnumerable<DateOnly> MonthsIn(int year)
    {
        for (var month = 1; month <= 12; month++)
        {
            var day = new DateOnly(year, month, 1);
            if (day >= FromMonth && (ToMonth is null || day <= ToMonth))
                yield return day;
        }
    }

    private static DateOnly FirstOfMonth(DateOnly day) => new(day.Year, day.Month, 1);
}
