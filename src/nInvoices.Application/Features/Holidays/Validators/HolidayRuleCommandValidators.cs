using FluentValidation;
using nInvoices.Application.Features.Holidays.Commands;

namespace nInvoices.Application.Features.Holidays.Validators;

public sealed class CreateHolidayRuleCommandValidator : AbstractValidator<CreateHolidayRuleCommand>
{
    public CreateHolidayRuleCommandValidator()
    {
        RuleFor(x => x.Rule).NotNull().SetValidator(new SaveHolidayRuleDtoValidator());
    }
}

public sealed class UpdateHolidayRuleCommandValidator : AbstractValidator<UpdateHolidayRuleCommand>
{
    public UpdateHolidayRuleCommandValidator()
    {
        RuleFor(x => x.Rule).NotNull().SetValidator(new SaveHolidayRuleDtoValidator());
    }
}
