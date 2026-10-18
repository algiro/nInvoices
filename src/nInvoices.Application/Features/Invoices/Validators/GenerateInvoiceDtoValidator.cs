using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;
using nInvoices.Core.Enums;

namespace nInvoices.Application.Features.Invoices.Validators;

public sealed class GenerateInvoiceDtoValidator : AbstractValidator<GenerateInvoiceDto>
{
    public GenerateInvoiceDtoValidator(IAppText text)
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithText(text, "validation.customerIdPositive");

        RuleFor(x => x.InvoiceType)
            .IsInEnum()
            .WithText(text, "validation.invalidInvoiceType");

        RuleFor(x => x.IssueDate)
            .NotEmpty()
            .WithText(text, "validation.invoice.issueDateRequired");

        When(x => x.InvoiceType == InvoiceType.Monthly, () =>
        {
            RuleFor(x => x.Year)
                .NotNull()
                .InclusiveBetween(2000, 2100)
                .WithText(text, "validation.yearRange");

            RuleFor(x => x.Month)
                .NotNull()
                .InclusiveBetween(1, 12)
                .WithText(text, "validation.monthRange");

            // The month's days must be sent, but may be none: a fixed monthly rate bills its price
            // without them. Rates that need worked days are checked when the invoice is built.
            RuleFor(x => x.WorkDays)
                .NotNull()
                .WithText(text, "validation.invoice.workDaysRequired");
        });

        RuleFor(x => x.RateId)
            .GreaterThan(0)
            .When(x => x.RateId.HasValue)
            .WithText(text, "validation.rateIdPositive");

        RuleFor(x => x.Hours)
            .GreaterThan(0)
            .LessThanOrEqualTo(100_000)
            .When(x => x.Hours.HasValue)
            .WithText(text, "validation.invoice.hoursPositive");

        RuleForEach(x => x.Expenses)
            .SetValidator(new ExpenseDtoValidator(text))
            .When(x => x.Expenses != null);

        RuleForEach(x => x.WorkDays)
            .SetValidator(new WorkDayDtoValidator(text))
            .When(x => x.WorkDays != null);
    }
}

public sealed class ExpenseDtoValidator : AbstractValidator<ExpenseDto>
{
    public ExpenseDtoValidator(IAppText text)
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500)
            .WithText(text, "validation.invoice.expenseDescription", ("max", 500));

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithText(text, "validation.amountPositive");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithText(text, "validation.currencyIsoCode");
    }
}

public sealed class WorkDayDtoValidator : AbstractValidator<WorkDayDto>
{
    public WorkDayDtoValidator(IAppText text)
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .WithText(text, "validation.invoice.workDayDateRequired");

        RuleFor(x => x.RateId)
            .GreaterThan(0)
            .When(x => x.RateId.HasValue)
            .WithText(text, "validation.rateIdPositive");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes != null)
            .WithText(text, "validation.invoice.notesTooLong", ("max", 500));

        RuleForEach(x => x.Projects)
            .SetValidator(new WorkDayProjectDtoValidator(text))
            .When(x => x.Projects != null);
    }
}

public sealed class WorkDayProjectDtoValidator : AbstractValidator<WorkDayProjectDto>
{
    public WorkDayProjectDtoValidator(IAppText text)
    {
        RuleFor(x => x.Hours)
            .GreaterThan(0)
            .WithText(text, "validation.invoice.projectHoursPositive");

        RuleFor(x => x.ProjectName)
            .NotEmpty()
            .WithText(text, "validation.project.nameRequired")
            .MaximumLength(200)
            .WithText(text, "validation.project.nameTooLong", ("max", 200))
            .When(x => x.ProjectId is null or <= 0);
    }
}
