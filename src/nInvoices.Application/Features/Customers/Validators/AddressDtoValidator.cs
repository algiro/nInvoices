using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.Customers.Validators;

public sealed class AddressDtoValidator : AbstractValidator<AddressDto>
{
    public AddressDtoValidator(IAppText text)
    {
        RuleFor(x => x.Street)
            .NotEmpty().WithText(text, "validation.address.streetRequired")
            .MaximumLength(200).WithText(text, "validation.address.streetTooLong", ("max", 200));

        RuleFor(x => x.HouseNumber)
            .NotEmpty().WithText(text, "validation.address.houseNumberRequired")
            .MaximumLength(20).WithText(text, "validation.address.houseNumberTooLong", ("max", 20));

        RuleFor(x => x.City)
            .NotEmpty().WithText(text, "validation.address.cityRequired")
            .MaximumLength(100).WithText(text, "validation.address.cityTooLong", ("max", 100));

        RuleFor(x => x.ZipCode)
            .NotEmpty().WithText(text, "validation.address.zipCodeRequired")
            .MaximumLength(20).WithText(text, "validation.address.zipCodeTooLong", ("max", 20));

        RuleFor(x => x.Country)
            .NotEmpty().WithText(text, "validation.address.countryRequired")
            .MaximumLength(100).WithText(text, "validation.address.countryTooLong", ("max", 100));

        RuleFor(x => x.State)
            .MaximumLength(100).WithText(text, "validation.address.stateTooLong", ("max", 100))
            .When(x => !string.IsNullOrWhiteSpace(x.State));
    }
}
