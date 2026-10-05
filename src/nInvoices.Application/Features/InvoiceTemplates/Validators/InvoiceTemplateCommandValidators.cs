using FluentValidation;
using nInvoices.Application.Features.InvoiceTemplates.Commands;

namespace nInvoices.Application.Features.InvoiceTemplates.Validators;

public sealed class CreateInvoiceTemplateCommandValidator : AbstractValidator<CreateInvoiceTemplateCommand>
{
    public CreateInvoiceTemplateCommandValidator()
    {
        RuleFor(x => x.Template).NotNull().SetValidator(new CreateInvoiceTemplateDtoValidator());
    }
}

public sealed class UpdateInvoiceTemplateCommandValidator : AbstractValidator<UpdateInvoiceTemplateCommand>
{
    public UpdateInvoiceTemplateCommandValidator()
    {
        RuleFor(x => x.Template).NotNull().SetValidator(new UpdateInvoiceTemplateDtoValidator());
    }
}
