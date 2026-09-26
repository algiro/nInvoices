using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Holidays.Validators;

/// <summary>Field checks; whether the fields fit the rule's kind is checked by the entity.</summary>
public sealed class SaveHolidayRuleDtoValidator : AbstractValidator<SaveHolidayRuleDto>
{
    public SaveHolidayRuleDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Holiday name is required")
            .MaximumLength(200).WithMessage("Holiday name must not exceed 200 characters");

        RuleFor(x => x.Kind).IsInEnum();

        RuleFor(x => x.FromYear).InclusiveBetween(1900, 2200).When(x => x.FromYear.HasValue);
        RuleFor(x => x.ToYear).InclusiveBetween(1900, 2200).When(x => x.ToYear.HasValue);
    }
}
