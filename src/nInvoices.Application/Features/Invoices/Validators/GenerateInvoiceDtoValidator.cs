using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Core.Enums;

namespace nInvoices.Application.Features.Invoices.Validators;

public sealed class GenerateInvoiceDtoValidator : AbstractValidator<GenerateInvoiceDto>
{
    public GenerateInvoiceDtoValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("Customer ID must be positive");

        RuleFor(x => x.InvoiceType)
            .IsInEnum()
            .WithMessage("Invalid invoice type");

        RuleFor(x => x.IssueDate)
            .NotEmpty()
            .WithMessage("Issue date is required");

        When(x => x.InvoiceType == InvoiceType.Monthly, () =>
        {
            RuleFor(x => x.Year)
                .NotNull()
                .InclusiveBetween(2000, 2100)
                .WithMessage("Year must be between 2000 and 2100");

            RuleFor(x => x.Month)
                .NotNull()
                .InclusiveBetween(1, 12)
                .WithMessage("Month must be between 1 and 12");

            RuleFor(x => x.WorkDays)
                .NotNull()
                .NotEmpty()
                .WithMessage("Work days are required for monthly invoices");
        });

        RuleForEach(x => x.Expenses)
            .SetValidator(new ExpenseDtoValidator())
            .When(x => x.Expenses != null);

        RuleForEach(x => x.WorkDays)
            .SetValidator(new WorkDayDtoValidator())
            .When(x => x.WorkDays != null);
    }
}

public sealed class ExpenseDtoValidator : AbstractValidator<ExpenseDto>
{
    public ExpenseDtoValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Description is required and must not exceed 500 characters");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be positive");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("Currency must be a 3-letter ISO code");
    }
}

public sealed class WorkDayDtoValidator : AbstractValidator<WorkDayDto>
{
    public WorkDayDtoValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage("Work day date is required");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes != null)
            .WithMessage("Notes must not exceed 500 characters");
    }
}
