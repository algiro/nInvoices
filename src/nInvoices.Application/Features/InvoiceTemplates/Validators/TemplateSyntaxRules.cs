using FluentValidation;
using nInvoices.Application.Services;

namespace nInvoices.Application.Features.InvoiceTemplates.Validators;

public static class TemplateSyntaxRules
{
    /// <summary>
    /// The content must parse with the engine that renders it (Scriban, <c>[[ ]]</c> delimiters), so
    /// a template that saves is one that renders. Each syntax error becomes a failure with its line
    /// and column. Blank content is left to the required rule.
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, string> MustBeValidTemplate<T>(
        this IRuleBuilder<T, string> rule,
        ITemplateRenderer renderer) =>
        rule.CustomAsync(async (content, context, cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            var result = await renderer.ValidateAsync(content, cancellationToken);
            foreach (var error in result.Errors)
                context.AddFailure($"Template syntax error: {error}");
        });
}
