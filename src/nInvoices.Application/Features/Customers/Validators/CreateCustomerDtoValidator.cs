using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Services.Email;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.Customers.Validators;

public sealed class CreateCustomerDtoValidator : AbstractValidator<CreateCustomerDto>
{
    public CreateCustomerDtoValidator(IAppText text)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithText(text, "validation.customer.nameRequired")
            .MaximumLength(200).WithText(text, "validation.customer.nameTooLong", ("max", 200));

        RuleFor(x => x.FiscalId)
            .NotEmpty().WithText(text, "validation.customer.fiscalIdRequired")
            .MaximumLength(50).WithText(text, "validation.customer.fiscalIdTooLong", ("max", 50));

        RuleFor(x => x.Address)
            .NotNull().WithText(text, "validation.customer.addressRequired")
            .SetValidator(new AddressDtoValidator(text));

        RuleFor(x => x.Email)
            .Must(EmailAddresses.IsValid!).WithText(text, "validation.customer.emailInvalid")
            .MaximumLength(320)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.CcEmails)
            .Must(EmailAddresses.AreAllValid).WithText(text, "validation.customer.ccInvalid")
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.CcEmails));

        RuleFor(x => x.HolidayCountry)
            .Matches("^[A-Za-z]{2}$").WithText(text, "validation.customer.holidayCountryInvalid")
            .When(x => !string.IsNullOrWhiteSpace(x.HolidayCountry));
    }
}
