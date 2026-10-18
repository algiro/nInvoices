using FluentValidation;
using nInvoices.Application.Localization;
using nInvoices.Application.Services;

namespace nInvoices.Application.Validation;

public static class TemplateSyntaxRules
{
    /// <summary>
    /// The content must parse with the engine that renders it (Scriban, <c>[[ ]]</c> delimiters), so
    /// a template that saves is one that renders. Each syntax error becomes a failure with its line
    /// and column, in the text of <paramref name="errorKey"/> ("Template syntax error: Line 2, …", or
    /// <c>validation.template.subjectSyntaxError</c> for one of several parts) in the user's language.
    /// Blank content is left to the required rule.
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, string> MustBeValidTemplate<T>(
        this IRuleBuilder<T, string> rule,
        ITemplateRenderer renderer,
        IAppText text,
        string errorKey = "validation.template.syntaxError") =>
        rule.CustomAsync(async (content, context, cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            var result = await renderer.ValidateAsync(content, cancellationToken);
            foreach (var error in result.Errors)
                context.AddFailure(text.Translate(errorKey, ("error", error)));
        });
}
