using FluentValidation;
using nInvoices.Application.Features.Rates.Commands;

namespace nInvoices.Application.Features.Rates.Validators;

public sealed class CreateRateCommandValidator : AbstractValidator<CreateRateCommand>
{
    public CreateRateCommandValidator()
    {
        RuleFor(x => x.Rate).NotNull().SetValidator(new CreateRateDtoValidator());
    }
}

public sealed class UpdateRateCommandValidator : AbstractValidator<UpdateRateCommand>
{
    public UpdateRateCommandValidator()
    {
        RuleFor(x => x.Rate).NotNull().SetValidator(new UpdateRateDtoValidator());
    }
}
