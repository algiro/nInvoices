using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Rates.Validators;

public sealed class MoneyDtoValidator : AbstractValidator<MoneyDto>
{
    public MoneyDtoValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be a 3-letter ISO code (e.g., USD, EUR)")
            .Matches(@"^[A-Z]{3}$").WithMessage("Currency must be uppercase 3-letter code");
    }
}