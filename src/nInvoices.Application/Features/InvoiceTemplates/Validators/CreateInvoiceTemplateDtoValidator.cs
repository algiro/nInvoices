using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceTemplates.Validators;

public sealed class CreateInvoiceTemplateDtoValidator : AbstractValidator<CreateInvoiceTemplateDto>
{
    public CreateInvoiceTemplateDtoValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0).WithMessage("Customer ID must be a positive number");

        RuleFor(x => x.InvoiceType)
            .IsInEnum().WithMessage("Invalid invoice type");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Template content is required")
            .MinimumLength(10).WithMessage("Template content must be at least 10 characters")
            .MaximumLength(100000).WithMessage("Template content must not exceed 100,000 characters");
    }
}