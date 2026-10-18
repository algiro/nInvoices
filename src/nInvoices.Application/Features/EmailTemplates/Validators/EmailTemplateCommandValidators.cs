using FluentValidation;
using nInvoices.Application.Features.EmailTemplates.Commands;
using nInvoices.Application.Localization;
using nInvoices.Application.Services;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.EmailTemplates.Validators;

// Subject and body are both rendered with Scriban when an email is composed: both must parse

public sealed class CreateEmailTemplateCommandValidator : AbstractValidator<CreateEmailTemplateCommand>
{
    public CreateEmailTemplateCommandValidator(ITemplateRenderer renderer, IAppText text)
    {
        RuleFor(x => x.Template).NotNull();
        When(x => x.Template is not null, () =>
        {
            RuleFor(x => x.Template.Subject).MustBeValidTemplate(renderer, text, "validation.template.subjectSyntaxError");
            RuleFor(x => x.Template.Body).MustBeValidTemplate(renderer, text, "validation.template.bodySyntaxError");
        });
    }
}

public sealed class UpdateEmailTemplateCommandValidator : AbstractValidator<UpdateEmailTemplateCommand>
{
    public UpdateEmailTemplateCommandValidator(ITemplateRenderer renderer, IAppText text)
    {
        RuleFor(x => x.Template).NotNull();
        When(x => x.Template is not null, () =>
        {
            RuleFor(x => x.Template.Subject).MustBeValidTemplate(renderer, text, "validation.template.subjectSyntaxError");
            RuleFor(x => x.Template.Body).MustBeValidTemplate(renderer, text, "validation.template.bodySyntaxError");
        });
    }
}
