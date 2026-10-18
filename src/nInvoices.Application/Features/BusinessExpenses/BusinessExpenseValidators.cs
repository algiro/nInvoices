using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Rates.Validators;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.BusinessExpenses;

public sealed class SaveBusinessExpenseDtoValidator : AbstractValidator<SaveBusinessExpenseDto>
{
    public SaveBusinessExpenseDtoValidator(IAppText text)
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithText(text, "validation.descriptionRequired")
            .MaximumLength(500).WithText(text, "validation.descriptionTooLong", ("max", 500));

        RuleFor(x => x.Supplier)
            .MaximumLength(200).WithText(text, "validation.expense.supplierTooLong", ("max", 200));

        RuleFor(x => x.Amount)
            .NotNull().WithText(text, "validation.amountRequired")
            .SetValidator(new MoneyDtoValidator(text));

        RuleFor(x => x.IndirectTax)
            .GreaterThanOrEqualTo(0).WithText(text, "validation.expense.indirectTaxNegative");

        RuleFor(x => x.IndirectTax)
            .Equal(0).When(x => x.ReverseCharge)
            .WithText(text, "validation.expense.reverseChargeTax");

        RuleFor(x => x.Category)
            .IsInEnum().WithText(text, "validation.expense.invalidCategory");

        RuleFor(x => x.DeductiblePercent)
            .InclusiveBetween(0, 100).WithText(text, "validation.expense.deductibleRange");
    }
}

public sealed class SaveRecurringExpenseDtoValidator : AbstractValidator<SaveRecurringExpenseDto>
{
    public SaveRecurringExpenseDtoValidator(IAppText text)
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithText(text, "validation.descriptionRequired")
            .MaximumLength(500).WithText(text, "validation.descriptionTooLong", ("max", 500));

        RuleFor(x => x.Amount)
            .NotNull().WithText(text, "validation.amountRequired")
            .SetValidator(new MoneyDtoValidator(text));

        RuleFor(x => x.Category)
            .IsInEnum().WithText(text, "validation.expense.invalidCategory");

        RuleFor(x => x.ToMonth)
            .Must((dto, to) => to is null || (to.Value.Year, to.Value.Month).CompareTo((dto.FromMonth.Year, dto.FromMonth.Month)) >= 0)
            .WithText(text, "validation.expense.endsBeforeStart");
    }
}

public sealed class CreateBusinessExpenseCommandValidator : AbstractValidator<CreateBusinessExpenseCommand>
{
    public CreateBusinessExpenseCommandValidator(IAppText text)
    {
        RuleFor(x => x.Expense).NotNull().SetValidator(new SaveBusinessExpenseDtoValidator(text));
    }
}

public sealed class UpdateBusinessExpenseCommandValidator : AbstractValidator<UpdateBusinessExpenseCommand>
{
    public UpdateBusinessExpenseCommandValidator(IAppText text)
    {
        RuleFor(x => x.Expense).NotNull().SetValidator(new SaveBusinessExpenseDtoValidator(text));
    }
}

public sealed class CreateRecurringExpenseCommandValidator : AbstractValidator<CreateRecurringExpenseCommand>
{
    public CreateRecurringExpenseCommandValidator(IAppText text)
    {
        RuleFor(x => x.Expense).NotNull().SetValidator(new SaveRecurringExpenseDtoValidator(text));
    }
}

public sealed class UpdateRecurringExpenseCommandValidator : AbstractValidator<UpdateRecurringExpenseCommand>
{
    public UpdateRecurringExpenseCommandValidator(IAppText text)
    {
        RuleFor(x => x.Expense).NotNull().SetValidator(new SaveRecurringExpenseDtoValidator(text));
    }
}

public sealed class GetBusinessExpensesQueryValidator : AbstractValidator<GetBusinessExpensesQuery>
{
    public GetBusinessExpensesQueryValidator(IAppText text)
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithText(text, "validation.yearRange");
    }
}
