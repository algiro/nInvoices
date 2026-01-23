using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Customers.Validators;

public sealed class AddressDtoValidator : AbstractValidator<AddressDto>
{
    public AddressDtoValidator()
    {
        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("Street is required")
            .MaximumLength(200).WithMessage("Street must not exceed 200 characters");

        RuleFor(x => x.HouseNumber)
            .NotEmpty().WithMessage("House number is required")
            .MaximumLength(20).WithMessage("House number must not exceed 20 characters");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters");

        RuleFor(x => x.ZipCode)
            .NotEmpty().WithMessage("Zip code is required")
            .MaximumLength(20).WithMessage("Zip code must not exceed 20 characters");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters");

        RuleFor(x => x.State)
            .MaximumLength(100).WithMessage("State must not exceed 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.State));
    }
}
