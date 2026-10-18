using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.Rates.Validators;

public sealed class MoneyDtoValidator : AbstractValidator<MoneyDto>
{
    public MoneyDtoValidator(IAppText text)
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithText(text, "validation.money.amountPositive");

        RuleFor(x => x.Currency)
            .NotEmpty().WithText(text, "validation.money.currencyRequired")
            .Length(3).WithText(text, "validation.money.currencyIsoCode")
            .Matches(@"^[A-Z]{3}$").WithText(text, "validation.money.currencyUppercase");
    }
}