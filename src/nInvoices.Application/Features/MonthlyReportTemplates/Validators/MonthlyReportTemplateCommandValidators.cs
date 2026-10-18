using FluentValidation;
using nInvoices.Application.Features.MonthlyReportTemplates.Commands;
using nInvoices.Application.Localization;
using nInvoices.Application.Services;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.MonthlyReportTemplates.Validators;

public sealed class CreateMonthlyReportTemplateCommandValidator : AbstractValidator<CreateMonthlyReportTemplateCommand>
{
    public CreateMonthlyReportTemplateCommandValidator(ITemplateRenderer renderer, IAppText text)
    {
        RuleFor(x => x.Template).NotNull();
        RuleFor(x => x.Template.Content).MustBeValidTemplate(renderer, text).When(x => x.Template is not null);
    }
}

public sealed class UpdateMonthlyReportTemplateCommandValidator : AbstractValidator<UpdateMonthlyReportTemplateCommand>
{
    public UpdateMonthlyReportTemplateCommandValidator(ITemplateRenderer renderer, IAppText text)
    {
        RuleFor(x => x.Template).NotNull();
        RuleFor(x => x.Template.Content).MustBeValidTemplate(renderer, text).When(x => x.Template is not null);
    }
}
