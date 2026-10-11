using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>A cost of the user's business (not one billed to a customer: see <see cref="ExpenseDto"/>).</summary>
/// <param name="Amount">Without indirect tax.</param>
/// <param name="IndirectTax">Indirect tax paid on it, in the same currency.</param>
/// <param name="ReverseCharge">A supplier abroad that charged no indirect tax.</param>
/// <param name="DeductiblePercent">How much of it is a business cost, from 0 to 100.</param>
public sealed record BusinessExpenseDto(
    long Id,
    DateOnly Date,
    string Description,
    string? Supplier,
    MoneyDto Amount,
    decimal IndirectTax,
    bool ReverseCharge,
    ExpenseCategory Category,
    decimal DeductiblePercent);

/// <summary>Creates or replaces a business expense.</summary>
public sealed record SaveBusinessExpenseDto(
    DateOnly Date,
    string Description,
    string? Supplier,
    MoneyDto Amount,
    decimal IndirectTax = 0m,
    bool ReverseCharge = false,
    ExpenseCategory Category = ExpenseCategory.Other,
    decimal DeductiblePercent = 100m);

/// <summary>A business expense paid every month, such as the social security fee.</summary>
/// <param name="Amount">Of each month, without indirect tax.</param>
/// <param name="FromMonth">The first month, as its 1st day.</param>
/// <param name="ToMonth">The last month, as its 1st day; null while it goes on.</param>
public sealed record RecurringExpenseDto(
    long Id,
    string Description,
    MoneyDto Amount,
    ExpenseCategory Category,
    DateOnly FromMonth,
    DateOnly? ToMonth);

/// <summary>Creates or replaces a recurring expense. Any day of a month stands for the month.</summary>
public sealed record SaveRecurringExpenseDto(
    string Description,
    MoneyDto Amount,
    DateOnly FromMonth,
    DateOnly? ToMonth = null,
    ExpenseCategory Category = ExpenseCategory.Other);
