using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.Holidays.Validators;

/// <summary>Field checks; whether the fields fit the rule's kind is checked by the entity.</summary>
public sealed class SaveHolidayRuleDtoValidator : AbstractValidator<SaveHolidayRuleDto>
{
    public SaveHolidayRuleDtoValidator(IAppText text)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithText(text, "validation.holiday.nameRequired")
            .MaximumLength(200).WithText(text, "validation.holiday.nameTooLong", ("max", 200));

        RuleFor(x => x.Kind).IsInEnum();

        RuleFor(x => x.FromYear).InclusiveBetween(1900, 2200).When(x => x.FromYear.HasValue);
        RuleFor(x => x.ToYear).InclusiveBetween(1900, 2200).When(x => x.ToYear.HasValue);
    }
}
