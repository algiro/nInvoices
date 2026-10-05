using FluentValidation;
using nInvoices.Application.Features.Invoices.Commands;

namespace nInvoices.Application.Features.Invoices.Validators;

public sealed class GenerateInvoiceCommandValidator : AbstractValidator<GenerateInvoiceCommand>
{
    public GenerateInvoiceCommandValidator()
    {
        RuleFor(x => x.Invoice).NotNull().SetValidator(new GenerateInvoiceDtoValidator());
    }
}

public sealed class UpdateInvoiceCommandValidator : AbstractValidator<UpdateInvoiceCommand>
{
    public UpdateInvoiceCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull().SetValidator(new UpdateInvoiceDtoValidator());
    }
}
