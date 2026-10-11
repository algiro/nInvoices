using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Rates.Validators;

namespace nInvoices.Application.Features.BusinessExpenses;

public sealed class SaveBusinessExpenseDtoValidator : AbstractValidator<SaveBusinessExpenseDto>
{
    public SaveBusinessExpenseDtoValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

        RuleFor(x => x.Supplier)
            .MaximumLength(200).WithMessage("Supplier must not exceed 200 characters");

        RuleFor(x => x.Amount)
            .NotNull().WithMessage("Amount is required")
            .SetValidator(new MoneyDtoValidator());

        RuleFor(x => x.IndirectTax)
            .GreaterThanOrEqualTo(0).WithMessage("The indirect tax cannot be negative");

        RuleFor(x => x.IndirectTax)
            .Equal(0).When(x => x.ReverseCharge)
            .WithMessage("A reverse-charge expense has no indirect tax: the supplier didn't charge any");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Invalid expense category");

        RuleFor(x => x.DeductiblePercent)
            .InclusiveBetween(0, 100).WithMessage("The deductible percentage must be between 0 and 100");
    }
}

public sealed class SaveRecurringExpenseDtoValidator : AbstractValidator<SaveRecurringExpenseDto>
{
    public SaveRecurringExpenseDtoValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

        RuleFor(x => x.Amount)
            .NotNull().WithMessage("Amount is required")
            .SetValidator(new MoneyDtoValidator());

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Invalid expense category");

        RuleFor(x => x.ToMonth)
            .Must((dto, to) => to is null || (to.Value.Year, to.Value.Month).CompareTo((dto.FromMonth.Year, dto.FromMonth.Month)) >= 0)
            .WithMessage("It cannot end before it starts");
    }
}

public sealed class CreateBusinessExpenseCommandValidator : AbstractValidator<CreateBusinessExpenseCommand>
{
    public CreateBusinessExpenseCommandValidator()
    {
        RuleFor(x => x.Expense).NotNull().SetValidator(new SaveBusinessExpenseDtoValidator());
    }
}

public sealed class UpdateBusinessExpenseCommandValidator : AbstractValidator<UpdateBusinessExpenseCommand>
{
    public UpdateBusinessExpenseCommandValidator()
    {
        RuleFor(x => x.Expense).NotNull().SetValidator(new SaveBusinessExpenseDtoValidator());
    }
}

public sealed class CreateRecurringExpenseCommandValidator : AbstractValidator<CreateRecurringExpenseCommand>
{
    public CreateRecurringExpenseCommandValidator()
    {
        RuleFor(x => x.Expense).NotNull().SetValidator(new SaveRecurringExpenseDtoValidator());
    }
}

public sealed class UpdateRecurringExpenseCommandValidator : AbstractValidator<UpdateRecurringExpenseCommand>
{
    public UpdateRecurringExpenseCommandValidator()
    {
        RuleFor(x => x.Expense).NotNull().SetValidator(new SaveRecurringExpenseDtoValidator());
    }
}

public sealed class GetBusinessExpensesQueryValidator : AbstractValidator<GetBusinessExpensesQuery>
{
    public GetBusinessExpensesQueryValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100");
    }
}
