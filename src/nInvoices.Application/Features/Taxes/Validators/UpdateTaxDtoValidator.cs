using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Taxes.Validators;

public sealed class UpdateTaxDtoValidator : AbstractValidator<UpdateTaxDto>
{
    public UpdateTaxDtoValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(200).WithMessage("Description must not exceed 200 characters");

        RuleFor(x => x.HandlerId)
            .NotEmpty().WithMessage("Handler ID is required")
            .MaximumLength(100).WithMessage("Handler ID must not exceed 100 characters");

        RuleFor(x => x.ApplicationType)
            .IsInEnum().WithMessage("Invalid application type");

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0).WithMessage("Order must be non-negative");
    }
}
