using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Email;

namespace nInvoices.Application.Features.Customers.Validators;

public sealed class CreateCustomerDtoValidator : AbstractValidator<CreateCustomerDto>
{
    public CreateCustomerDtoValidator()
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

        RuleFor(x => x.Email)
            .Must(EmailAddresses.IsValid!).WithMessage("Email must be a valid address, e.g. accounts@example.com")
            .MaximumLength(320)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.CcEmails)
            .Must(EmailAddresses.AreAllValid).WithMessage("CC must be valid addresses separated by commas")
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.CcEmails));
    }
}
