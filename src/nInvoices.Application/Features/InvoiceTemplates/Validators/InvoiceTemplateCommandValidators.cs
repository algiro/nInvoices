using FluentValidation;
using nInvoices.Application.Features.InvoiceTemplates.Commands;
using nInvoices.Application.Localization;
using nInvoices.Application.Services;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.InvoiceTemplates.Validators;

public sealed class CreateInvoiceTemplateCommandValidator : AbstractValidator<CreateInvoiceTemplateCommand>
{
    public CreateInvoiceTemplateCommandValidator(ITemplateRenderer renderer, IAppText text)
    {
        RuleFor(x => x.Template).NotNull().SetValidator(new CreateInvoiceTemplateDtoValidator(text));
        RuleFor(x => x.Template.Content).MustBeValidTemplate(renderer, text).When(x => x.Template is not null);
    }
}

public sealed class UpdateInvoiceTemplateCommandValidator : AbstractValidator<UpdateInvoiceTemplateCommand>
{
    public UpdateInvoiceTemplateCommandValidator(ITemplateRenderer renderer, IAppText text)
    {
        RuleFor(x => x.Template).NotNull().SetValidator(new UpdateInvoiceTemplateDtoValidator(text));
        RuleFor(x => x.Template.Content).MustBeValidTemplate(renderer, text).When(x => x.Template is not null);
    }
}
