using System.Globalization;
using System.Text.Json;

namespace nInvoices.Application.Localization;

/// <summary>
/// The API's own texts (error messages and the like) in the user's language: the one of the request's
/// <see cref="CultureInfo.CurrentUICulture"/>, set from its <c>Accept-Language</c> header.
/// </summary>
/// <remarks>
/// Not the language of invoices and emails: those follow the customer's locale (<c>ILocalizationService</c>).
/// </remarks>
public interface IAppText
{
    /// <summary>The languages the API has texts for (<c>en</c> first), e.g. <c>["en", "it"]</c>.</summary>
    IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// The text of <paramref name="key"/> (e.g. <c>errors.unexpected</c>) in the current UI language, with
    /// <c>{name}</c> placeholders filled from <paramref name="args"/>. A text the language lacks is the English
    /// one; a key no language has is returned as is.
    /// </summary>
    string Translate(string key, params (string Name, object? Value)[] args);

    /// <summary>The same, in <paramref name="culture"/> instead of the current UI language.</summary>
    string TranslateIn(CultureInfo culture, string key, params (string Name, object? Value)[] args);

    /// <summary>
    /// The language this API has texts for that serves <paramref name="language"/> best: itself ("it"), or its
    /// parent ("it-CH" → "it"); null when there is none.
    /// </summary>
    string? Match(string? language);
}

/// <summary>
/// Reads <c>Localization/Messages/&lt;language&gt;.json</c>, embedded in this assembly: one file per language,
/// nested objects whose leaves are the texts. Adding a language is adding its file; <c>en.json</c> is the
/// reference every other language falls back to.
/// </summary>
public sealed class AppText : IAppText
{
    public const string FallbackLanguage = "en";

    private const string ResourcePrefix = "nInvoices.Messages.";

    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _texts;

    /// <summary>The texts embedded in this assembly.</summary>
    public AppText() : this(EmbeddedFiles())
    {
    }

    /// <summary>The texts of the given files: language → JSON content.</summary>
    /// <exception cref="InvalidOperationException">There is no English file, or a file is not valid.</exception>
    public AppText(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        _texts = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (language, json) in files)
        {
            try
            {
                _texts[language] = Flatten(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Localization/Messages/{language}.json is not valid: {ex.Message}", ex);
            }
        }

        if (!_texts.ContainsKey(FallbackLanguage))
            throw new InvalidOperationException($"Localization/Messages/{FallbackLanguage}.json is missing: every text falls back to it");

        Languages = [FallbackLanguage, .. _texts.Keys.Where(l => !l.Equals(FallbackLanguage, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal)];
    }

    public IReadOnlyList<string> Languages { get; }

    /// <summary>Every key of a language's file (for checking the files against each other).</summary>
    public IReadOnlyCollection<string> KeysOf(string language) =>
        _texts.TryGetValue(language, out var texts) ? texts.Keys.ToList() : [];

    public string Translate(string key, params (string Name, object? Value)[] args) =>
        TranslateIn(CultureInfo.CurrentUICulture, key, args);

    public string TranslateIn(CultureInfo culture, string key, params (string Name, object? Value)[] args)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var text = Find(culture, key) ?? key;
        foreach (var (name, value) in args)
            text = text.Replace("{" + name + "}", Convert.ToString(value, culture), StringComparison.Ordinal);
        return text;
    }

    public string? Match(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        // "pt-BR" → "pt-BR", then "pt"
        var tag = language.Trim().Replace('_', '-');
        while (tag.Length > 0)
        {
            if (_texts.ContainsKey(tag))
                return Languages.First(l => l.Equals(tag, StringComparison.OrdinalIgnoreCase));
            var dash = tag.LastIndexOf('-');
            tag = dash < 0 ? string.Empty : tag[..dash];
        }
        return null;
    }

    // it-CH, then it, then en
    private string? Find(CultureInfo culture, string key)
    {
        for (var c = culture; !Equals(c, CultureInfo.InvariantCulture); c = c.Parent)
        {
            if (_texts.TryGetValue(c.Name, out var texts) && texts.TryGetValue(key, out var text))
                return text;
        }
        return _texts[FallbackLanguage].TryGetValue(key, out var english) ? english : null;
    }

    private static Dictionary<string, string> Flatten(string json)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        Add(document.RootElement, string.Empty);
        return texts;

        void Add(JsonElement element, string prefix)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                        Add(property.Value, prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}");
                    break;
                case JsonValueKind.String:
                    texts[prefix] = element.GetString()!;
                    break;
                default:
                    throw new JsonException($"'{prefix}' must be a text or an object of texts");
            }
        }
    }

    private static Dictionary<string, string> EmbeddedFiles()
    {
        var assembly = typeof(AppText).Assembly;
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            files[Path.GetFileNameWithoutExtension(name[ResourcePrefix.Length..])] = reader.ReadToEnd();
        }
        return files;
    }
}
