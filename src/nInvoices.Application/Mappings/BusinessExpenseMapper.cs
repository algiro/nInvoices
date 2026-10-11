using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Mappings;

/// <summary>Maps <see cref="BusinessExpense"/> and <see cref="RecurringExpense"/> to DTOs and back.</summary>
public static class BusinessExpenseMapper
{
    public static BusinessExpenseDto ToDto(BusinessExpense expense) => new(
        expense.Id,
        expense.Date,
        expense.Description,
        expense.Supplier,
        new MoneyDto(expense.Amount.Amount, expense.Amount.Currency),
        expense.IndirectTax,
        expense.ReverseCharge,
        expense.Category,
        expense.DeductiblePercent);

    public static RecurringExpenseDto ToDto(RecurringExpense expense) => new(
        expense.Id,
        expense.Description,
        new MoneyDto(expense.Amount.Amount, expense.Amount.Currency),
        expense.Category,
        expense.FromMonth,
        expense.ToMonth);

    /// <exception cref="ArgumentException">The values break the entity's rules.</exception>
    public static BusinessExpense ToEntity(SaveBusinessExpenseDto dto) => new(
        dto.Date, dto.Description, ToMoney(dto.Amount), dto.Category, dto.IndirectTax, dto.ReverseCharge, dto.DeductiblePercent, dto.Supplier);

    /// <exception cref="ArgumentException">The values break the entity's rules.</exception>
    public static void Apply(SaveBusinessExpenseDto dto, BusinessExpense expense) =>
        expense.Update(dto.Date, dto.Description, ToMoney(dto.Amount), dto.Category, dto.IndirectTax, dto.ReverseCharge, dto.DeductiblePercent, dto.Supplier);

    /// <exception cref="ArgumentException">The values break the entity's rules.</exception>
    public static RecurringExpense ToEntity(SaveRecurringExpenseDto dto) =>
        new(dto.Description, ToMoney(dto.Amount), dto.Category, dto.FromMonth, dto.ToMonth);

    /// <exception cref="ArgumentException">The values break the entity's rules.</exception>
    public static void Apply(SaveRecurringExpenseDto dto, RecurringExpense expense) =>
        expense.Update(dto.Description, ToMoney(dto.Amount), dto.Category, dto.FromMonth, dto.ToMonth);

    private static Money ToMoney(MoneyDto dto) => new(dto.Amount, dto.Currency);
}
