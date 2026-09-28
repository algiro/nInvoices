using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Rates.Validators;

public sealed class UpdateRateDtoValidator : AbstractValidator<UpdateRateDto>
{
    public UpdateRateDtoValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid rate type");

        RuleFor(x => x.Name)
            .MaximumLength(Core.Entities.Rate.MaxNameLength)
            .WithMessage("The rate name must not exceed 100 characters");

        RuleFor(x => x.Price)
            .NotNull().WithMessage("Price is required")
            .SetValidator(new MoneyDtoValidator());
    }
}