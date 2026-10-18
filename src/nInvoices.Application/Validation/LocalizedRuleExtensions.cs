using FluentValidation;
using nInvoices.Application.Localization;

namespace nInvoices.Application.Validation;

public static class LocalizedRuleExtensions
{
    /// <summary>
    /// The rule's message is the text of <paramref name="key"/> (<c>Localization/Messages</c>) in the user's
    /// language, translated when the rule fails, with its <c>{name}</c> placeholders filled from <paramref name="args"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, TProperty> WithText<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule,
        IAppText text,
        string key,
        params (string Name, object? Value)[] args) =>
        rule.WithMessage(_ => text.Translate(key, args));
}
