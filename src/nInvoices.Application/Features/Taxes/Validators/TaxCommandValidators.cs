using FluentValidation;
using nInvoices.Application.Features.Taxes.Commands;

namespace nInvoices.Application.Features.Taxes.Validators;

public sealed class CreateTaxCommandValidator : AbstractValidator<CreateTaxCommand>
{
    public CreateTaxCommandValidator()
    {
        RuleFor(x => x.Tax).NotNull().SetValidator(new CreateTaxDtoValidator());
    }
}

public sealed class UpdateTaxCommandValidator : AbstractValidator<UpdateTaxCommand>
{
    public UpdateTaxCommandValidator()
    {
        RuleFor(x => x.Tax).NotNull().SetValidator(new UpdateTaxDtoValidator());
    }
}
