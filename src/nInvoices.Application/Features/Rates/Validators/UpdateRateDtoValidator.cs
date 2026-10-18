using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.Rates.Validators;

public sealed class UpdateRateDtoValidator : AbstractValidator<UpdateRateDto>
{
    public UpdateRateDtoValidator(IAppText text)
    {
        RuleFor(x => x.Type)
            .IsInEnum().WithText(text, "validation.rate.invalidType");

        RuleFor(x => x.Name)
            .MaximumLength(Core.Entities.Rate.MaxNameLength)
            .WithText(text, "validation.rate.nameTooLong", ("max", Core.Entities.Rate.MaxNameLength));

        RuleFor(x => x.Price)
            .NotNull().WithText(text, "validation.rate.priceRequired")
            .SetValidator(new MoneyDtoValidator(text));
    }
}