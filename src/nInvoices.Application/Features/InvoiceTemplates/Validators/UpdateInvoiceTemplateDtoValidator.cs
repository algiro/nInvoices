using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.InvoiceTemplates.Validators;

public sealed class UpdateInvoiceTemplateDtoValidator : AbstractValidator<UpdateInvoiceTemplateDto>
{
    public UpdateInvoiceTemplateDtoValidator(IAppText text)
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithText(text, "validation.template.contentRequired")
            .MinimumLength(10).WithText(text, "validation.template.contentTooShort", ("min", 10))
            .MaximumLength(100000).WithText(text, "validation.template.contentTooLong", ("max", 100_000));
    }
}