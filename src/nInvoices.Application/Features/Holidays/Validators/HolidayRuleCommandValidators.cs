using FluentValidation;
using nInvoices.Application.Features.Holidays.Commands;
using nInvoices.Application.Localization;

namespace nInvoices.Application.Features.Holidays.Validators;

public sealed class CreateHolidayRuleCommandValidator : AbstractValidator<CreateHolidayRuleCommand>
{
    public CreateHolidayRuleCommandValidator(IAppText text)
    {
        RuleFor(x => x.Rule).NotNull().SetValidator(new SaveHolidayRuleDtoValidator(text));
    }
}

public sealed class UpdateHolidayRuleCommandValidator : AbstractValidator<UpdateHolidayRuleCommand>
{
    public UpdateHolidayRuleCommandValidator(IAppText text)
    {
        RuleFor(x => x.Rule).NotNull().SetValidator(new SaveHolidayRuleDtoValidator(text));
    }
}
