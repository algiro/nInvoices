using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Taxes.Validators;

public sealed class CreateTaxDtoValidator : AbstractValidator<CreateTaxDto>
{
    public CreateTaxDtoValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0).WithMessage("Customer ID must be a positive number");

        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("Tax ID is required")
            .MaximumLength(50).WithMessage("Tax ID must not exceed 50 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(200).WithMessage("Description must not exceed 200 characters");

        RuleFor(x => x.HandlerId)
            .NotEmpty().WithMessage("Handler ID is required")
            .MaximumLength(100).WithMessage("Handler ID must not exceed 100 characters");

        RuleFor(x => x.ApplicationType)
            .IsInEnum().WithMessage("Invalid application type");

        RuleFor(x => x.Rate)
            .GreaterThanOrEqualTo(0).WithMessage("Rate must be non-negative");

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0).WithMessage("Order must be non-negative");
    }
}
