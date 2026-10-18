using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.Invoices.Validators;

public sealed class UpdateInvoiceDtoValidator : AbstractValidator<UpdateInvoiceDto>
{
    public const int MaxRenderedContentLength = 10_000_000;

    public UpdateInvoiceDtoValidator(IAppText text)
    {
        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes != null)
            .WithText(text, "validation.invoice.notesTooLong", ("max", 1000));

        // Rendered invoices embed their images (logos up to 1 MB each, the Verifactu QR) as base64
        // data URIs, so the limit only guards against runaway input
        RuleFor(x => x.RenderedContent)
            .MaximumLength(MaxRenderedContentLength)
            .When(x => x.RenderedContent != null)
            .WithText(text, "validation.invoice.renderedContentTooLong", ("max", MaxRenderedContentLength));
    }
}
