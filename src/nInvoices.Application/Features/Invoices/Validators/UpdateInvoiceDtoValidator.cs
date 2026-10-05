using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Validators;

public sealed class UpdateInvoiceDtoValidator : AbstractValidator<UpdateInvoiceDto>
{
    public const int MaxRenderedContentLength = 10_000_000;

    public UpdateInvoiceDtoValidator()
    {
        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes != null)
            .WithMessage("Notes must not exceed 1000 characters");

        // Rendered invoices embed their images (logos up to 1 MB each, the Verifactu QR) as base64
        // data URIs, so the limit only guards against runaway input
        RuleFor(x => x.RenderedContent)
            .MaximumLength(MaxRenderedContentLength)
            .When(x => x.RenderedContent != null)
            .WithMessage($"Rendered content must not exceed {MaxRenderedContentLength:N0} characters");
    }
}
