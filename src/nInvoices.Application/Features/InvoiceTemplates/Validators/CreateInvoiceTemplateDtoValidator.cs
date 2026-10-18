using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.InvoiceTemplates.Validators;

public sealed class CreateInvoiceTemplateDtoValidator : AbstractValidator<CreateInvoiceTemplateDto>
{
    public CreateInvoiceTemplateDtoValidator(IAppText text)
    {
        // No customer = a template shared by all the user's customers
        RuleFor(x => x.CustomerId)
            .GreaterThan(0).WithText(text, "validation.customerIdPositive")
            .When(x => x.CustomerId.HasValue);

        RuleFor(x => x.InvoiceType)
            .IsInEnum().WithText(text, "validation.invalidInvoiceType");

        RuleFor(x => x.Content)
            .NotEmpty().WithText(text, "validation.template.contentRequired")
            .MinimumLength(10).WithText(text, "validation.template.contentTooShort", ("min", 10))
            .MaximumLength(100000).WithText(text, "validation.template.contentTooLong", ("max", 100_000));
    }
}