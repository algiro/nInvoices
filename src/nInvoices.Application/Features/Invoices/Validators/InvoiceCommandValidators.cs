using FluentValidation;
using nInvoices.Application.Features.Invoices.Commands;
using nInvoices.Application.Localization;

namespace nInvoices.Application.Features.Invoices.Validators;

public sealed class GenerateInvoiceCommandValidator : AbstractValidator<GenerateInvoiceCommand>
{
    public GenerateInvoiceCommandValidator(IAppText text)
    {
        RuleFor(x => x.Invoice).NotNull().SetValidator(new GenerateInvoiceDtoValidator(text));
    }
}

public sealed class UpdateInvoiceCommandValidator : AbstractValidator<UpdateInvoiceCommand>
{
    public UpdateInvoiceCommandValidator(IAppText text)
    {
        RuleFor(x => x.Dto).NotNull().SetValidator(new UpdateInvoiceDtoValidator(text));
    }
}
