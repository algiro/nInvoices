using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Rates.Validators;

public sealed class CreateRateDtoValidator : AbstractValidator<CreateRateDto>
{
    public CreateRateDtoValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0).WithMessage("Customer ID must be a positive number");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid rate type");

        RuleFor(x => x.Price)
            .NotNull().WithMessage("Price is required")
            .SetValidator(new MoneyDtoValidator());
    }
}