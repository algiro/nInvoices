using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using nInvoices.Core.Exceptions;

namespace nInvoices.Core.Entities;

/// <summary>
/// A cost of the user's business, deductible from their income for the tax prediction. Not to be
/// confused with <see cref="Expense"/>, a cost billed to a customer on an invoice.
/// </summary>
public sealed class BusinessExpense : OwnedEntityBase
{
    /// <summary>When it was incurred: the supplier invoice date.</summary>
    public DateOnly Date { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string? Supplier { get; private set; }

    /// <summary>The amount without indirect tax (IVA, IGIC...).</summary>
    public Money Amount { get; private set; } = null!;

    /// <summary>Indirect tax paid on it, in the currency of <see cref="Amount"/>.</summary>
    public decimal IndirectTax { get; private set; }

    /// <summary>
    /// A supplier abroad that charged no indirect tax (e.g. a US software company): the user charges it
    /// to themselves and deducts it.
    /// </summary>
    public bool ReverseCharge { get; private set; }

    public ExpenseCategory Category { get; private set; }

    /// <summary>How much of it is a business cost, from 0 to 100.</summary>
    public decimal DeductiblePercent { get; private set; } = 100m;

    private BusinessExpense() { }

    /// <exception cref="ArgumentException">See <see cref="Update"/>.</exception>
    public BusinessExpense(
        DateOnly date,
        string description,
        Money amount,
        ExpenseCategory category = ExpenseCategory.Other,
        decimal indirectTax = 0m,
        bool reverseCharge = false,
        decimal deductiblePercent = 100m,
        string? supplier = null)
    {
        Update(date, description, amount, category, indirectTax, reverseCharge, deductiblePercent, supplier);
    }

    /// <exception cref="ArgumentException">
    /// The description is empty, an amount is negative, the percentage is not between 0 and 100, the
    /// category is unknown, or a reverse-charge expense carries indirect tax.
    /// </exception>
    public void Update(
        DateOnly date,
        string description,
        Money amount,
        ExpenseCategory category,
        decimal indirectTax,
        bool reverseCharge,
        decimal deductiblePercent,
        string? supplier)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(amount);

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description)).WithText("errors.input.descriptionRequired");

        if (amount.Amount < 0)
            throw new ArgumentException("The amount cannot be negative", nameof(amount)).WithText("errors.input.amountNegative");
        if (indirectTax < 0)
            throw new ArgumentException("The indirect tax cannot be negative", nameof(indirectTax)).WithText("errors.input.indirectTaxNegative");
        if (reverseCharge && indirectTax != 0)
            throw new ArgumentException("A reverse-charge expense has no indirect tax: the supplier didn't charge any", nameof(indirectTax)).WithText("errors.input.reverseChargeTax");
        if (deductiblePercent is < 0 or > 100)
            throw new ArgumentException("The deductible percentage must be between 0 and 100", nameof(deductiblePercent)).WithText("errors.input.deductiblePercent");
        if (!Enum.IsDefined(category))
            throw new ArgumentException("Unknown expense category", nameof(category)).WithText("errors.input.expenseCategory");

        Date = date;
        Description = description.Trim();
        Amount = amount;
        Category = category;
        IndirectTax = indirectTax;
        ReverseCharge = reverseCharge;
        DeductiblePercent = deductiblePercent;
        Supplier = string.IsNullOrWhiteSpace(supplier) ? null : supplier.Trim();
    }
}
