using FluentValidation;
using nInvoices.Application.Features.InvoiceTemplates.Commands;
using nInvoices.Application.Services;

namespace nInvoices.Application.Features.InvoiceTemplates.Validators;

public sealed class CreateInvoiceTemplateCommandValidator : AbstractValidator<CreateInvoiceTemplateCommand>
{
    public CreateInvoiceTemplateCommandValidator(ITemplateRenderer renderer)
    {
        RuleFor(x => x.Template).NotNull().SetValidator(new CreateInvoiceTemplateDtoValidator());
        RuleFor(x => x.Template.Content).MustBeValidTemplate(renderer).When(x => x.Template is not null);
    }
}

public sealed class UpdateInvoiceTemplateCommandValidator : AbstractValidator<UpdateInvoiceTemplateCommand>
{
    public UpdateInvoiceTemplateCommandValidator(ITemplateRenderer renderer)
    {
        RuleFor(x => x.Template).NotNull().SetValidator(new UpdateInvoiceTemplateDtoValidator());
        RuleFor(x => x.Template.Content).MustBeValidTemplate(renderer).When(x => x.Template is not null);
    }
}
