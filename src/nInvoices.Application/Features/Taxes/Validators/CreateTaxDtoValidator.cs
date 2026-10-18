using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.Taxes.Validators;

public sealed class CreateTaxDtoValidator : AbstractValidator<CreateTaxDto>
{
    public CreateTaxDtoValidator(IAppText text)
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0).WithText(text, "validation.customerIdPositive");

        // Optional: when blank, the handler derives it from the description
        RuleFor(x => x.TaxId)
            .MaximumLength(50).WithText(text, "validation.tax.taxIdTooLong", ("max", 50));

        RuleFor(x => x.Description)
            .NotEmpty().WithText(text, "validation.descriptionRequired")
            .MaximumLength(200).WithText(text, "validation.descriptionTooLong", ("max", 200));

        RuleFor(x => x.HandlerId)
            .NotEmpty().WithText(text, "validation.tax.handlerRequired")
            .MaximumLength(100).WithText(text, "validation.tax.handlerTooLong", ("max", 100));

        RuleFor(x => x.ApplicationType)
            .IsInEnum().WithText(text, "validation.tax.invalidApplicationType");

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0).WithText(text, "validation.tax.orderNegative");
    }
}
