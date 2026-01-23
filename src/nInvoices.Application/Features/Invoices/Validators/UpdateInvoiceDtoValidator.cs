using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Validators;

public sealed class UpdateInvoiceDtoValidator : AbstractValidator<UpdateInvoiceDto>
{
    public UpdateInvoiceDtoValidator()
    {
        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes != null)
            .WithMessage("Notes must not exceed 1000 characters");

        RuleFor(x => x.RenderedContent)
            .MaximumLength(50000)
            .When(x => x.RenderedContent != null)
            .WithMessage("Rendered content must not exceed 50000 characters");
    }
}
