using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceTemplates.Validators;

public sealed class UpdateInvoiceTemplateDtoValidator : AbstractValidator<UpdateInvoiceTemplateDto>
{
    public UpdateInvoiceTemplateDtoValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Template content is required")
            .MinimumLength(10).WithMessage("Template content must be at least 10 characters")
            .MaximumLength(100000).WithMessage("Template content must not exceed 100,000 characters");
    }
}