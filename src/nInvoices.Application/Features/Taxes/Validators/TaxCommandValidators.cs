using FluentValidation;
using nInvoices.Application.Features.Taxes.Commands;
using nInvoices.Application.Localization;

namespace nInvoices.Application.Features.Taxes.Validators;

public sealed class CreateTaxCommandValidator : AbstractValidator<CreateTaxCommand>
{
    public CreateTaxCommandValidator(IAppText text)
    {
        RuleFor(x => x.Tax).NotNull().SetValidator(new CreateTaxDtoValidator(text));
    }
}

public sealed class UpdateTaxCommandValidator : AbstractValidator<UpdateTaxCommand>
{
    public UpdateTaxCommandValidator(IAppText text)
    {
        RuleFor(x => x.Tax).NotNull().SetValidator(new UpdateTaxDtoValidator(text));
    }
}
