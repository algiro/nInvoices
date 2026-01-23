using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Customers.Validators;

public sealed class UpdateCustomerDtoValidator : AbstractValidator<UpdateCustomerDto>
{
    public UpdateCustomerDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Customer name is required")
            .MaximumLength(200).WithMessage("Customer name must not exceed 200 characters");

        RuleFor(x => x.FiscalId)
            .NotEmpty().WithMessage("Fiscal ID is required")
            .MaximumLength(50).WithMessage("Fiscal ID must not exceed 50 characters");

        RuleFor(x => x.Address)
            .NotNull().WithMessage("Address is required")
            .SetValidator(new AddressDtoValidator());
    }
}
