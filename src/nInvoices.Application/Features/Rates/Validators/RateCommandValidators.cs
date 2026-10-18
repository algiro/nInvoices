using FluentValidation;
using nInvoices.Application.Features.Rates.Commands;
using nInvoices.Application.Localization;

namespace nInvoices.Application.Features.Rates.Validators;

public sealed class CreateRateCommandValidator : AbstractValidator<CreateRateCommand>
{
    public CreateRateCommandValidator(IAppText text)
    {
        RuleFor(x => x.Rate).NotNull().SetValidator(new CreateRateDtoValidator(text));
    }
}

public sealed class UpdateRateCommandValidator : AbstractValidator<UpdateRateCommand>
{
    public UpdateRateCommandValidator(IAppText text)
    {
        RuleFor(x => x.Rate).NotNull().SetValidator(new UpdateRateDtoValidator(text));
    }
}
