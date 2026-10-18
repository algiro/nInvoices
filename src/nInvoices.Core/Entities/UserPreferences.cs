using System.Globalization;
using System.Text.RegularExpressions;
using nInvoices.Core.Exceptions;

namespace nInvoices.Core.Entities;

/// <summary>
/// How the app looks for one user, wherever they sign in. One per user; a user without a row has the
/// defaults. The language of the invoices is not here: it is each customer's <see cref="Customer.Locale"/>.
/// </summary>
public sealed partial class UserPreferences : OwnedEntityBase
{
    /// <summary>The language of the app until the user picks another.</summary>
    public const string DefaultUiLanguage = "en";

    /// <summary>Language of the app's screens and messages: a language tag such as <c>en</c> or <c>pt-BR</c>.</summary>
    public string UiLanguage { get; private set; } = DefaultUiLanguage;

    private UserPreferences() { }

    /// <exception cref="ArgumentException">The language is not a language tag.</exception>
    public UserPreferences(string uiLanguage)
    {
        ChangeUiLanguage(uiLanguage);
    }

    /// <exception cref="ArgumentException">The language is not a language tag.</exception>
    public void ChangeUiLanguage(string uiLanguage)
    {
        UiLanguage = NormalizeLanguage(uiLanguage);
    }

    /// <summary>
    /// " IT " → "it", "pt_br" → "pt-BR", "zh-hant" → "zh-Hant": the canonical case of a BCP 47 language tag
    /// (language, optional script and region), so one language is stored one way.
    /// </summary>
    /// <exception cref="ArgumentException">The value is not a language tag.</exception>
    public static string NormalizeLanguage(string language)
    {
        var tag = language?.Trim().Replace('_', '-') ?? string.Empty;
        if (!LanguageTag().IsMatch(tag))
            throw new ArgumentException($"'{language}' is not a language tag such as 'en' or 'pt-BR'", nameof(language)).WithText("errors.input.language", ("language", language));

        var parts = tag.Split('-');
        parts[0] = parts[0].ToLowerInvariant();
        for (var i = 1; i < parts.Length; i++)
        {
            parts[i] = parts[i].Length switch
            {
                2 => parts[i].ToUpperInvariant(),
                4 => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(parts[i].ToLowerInvariant()),
                _ => parts[i].ToLowerInvariant()
            };
        }
        return string.Join('-', parts);
    }

    // language (2-3 letters), then optional script (4 letters) and region (2 letters or 3 digits)
    [GeneratedRegex(@"^[A-Za-z]{2,3}(-[A-Za-z]{4})?(-([A-Za-z]{2}|[0-9]{3}))?$")]
    private static partial Regex LanguageTag();
}
