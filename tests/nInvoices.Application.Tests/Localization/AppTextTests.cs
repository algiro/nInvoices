using System.Globalization;
using System.Text.Json;
using nInvoices.Application.Localization;
using Shouldly;

namespace nInvoices.Application.Tests.Localization;

/// <summary>The API's texts in the user's language, falling back to English for what a language lacks.</summary>
[TestFixture]
public sealed class AppTextTests
{
    private static readonly AppText Text = new(new Dictionary<string, string>
    {
        ["en"] = """{ "errors": { "unexpected": "Something went wrong", "notFound": "{what} not found" }, "onlyInEnglish": "English" }""",
        ["it"] = """{ "errors": { "unexpected": "Qualcosa è andato storto", "notFound": "{what} non trovato" } }""",
        ["pt-BR"] = """{ "errors": { "unexpected": "Algo deu errado" } }"""
    });

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it");

    [Test]
    public void Translate_KeyInTheLanguage_IsItsText()
    {
        Text.TranslateIn(Italian, "errors.unexpected").ShouldBe("Qualcosa è andato storto");
    }

    [Test]
    public void Translate_KeyMissingFromTheLanguage_IsTheEnglishText()
    {
        Text.TranslateIn(Italian, "onlyInEnglish").ShouldBe("English");
    }

    [Test]
    public void Translate_KeyNoLanguageHas_IsTheKey()
    {
        Text.TranslateIn(Italian, "errors.nobodyWroteThis").ShouldBe("errors.nobodyWroteThis");
    }

    [Test]
    public void Translate_RegionalCulture_UsesItsLanguage()
    {
        Text.TranslateIn(CultureInfo.GetCultureInfo("it-CH"), "errors.unexpected").ShouldBe("Qualcosa è andato storto");
    }

    [Test]
    public void Translate_LanguageWithoutFile_IsEnglish()
    {
        Text.TranslateIn(CultureInfo.GetCultureInfo("fr-FR"), "errors.unexpected").ShouldBe("Something went wrong");
    }

    [Test]
    public void Translate_FillsThePlaceholders()
    {
        Text.TranslateIn(Italian, "errors.notFound", ("what", "Fattura 42")).ShouldBe("Fattura 42 non trovato");
    }

    [Test]
    public void Translate_WithoutCulture_FollowsTheCurrentUiCulture()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = Italian;
        try
        {
            Text.Translate("errors.unexpected").ShouldBe("Qualcosa è andato storto");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [TestCase("it", "it")]
    [TestCase("IT", "it")]
    [TestCase("it-CH", "it")]
    [TestCase("pt_br", "pt-BR")]
    [TestCase("pt", null)]
    [TestCase("fr", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void Match_IsTheLanguageOrItsParent(string? language, string? expected)
    {
        Text.Match(language).ShouldBe(expected);
    }

    [Test]
    public void Languages_EnglishFirst()
    {
        Text.Languages.ShouldBe(["en", "it", "pt-BR"]);
    }

    [Test]
    public void New_WithoutEnglish_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new AppText(new Dictionary<string, string> { ["it"] = "{}" }))
            .Message.ShouldContain("en.json");
    }

    [Test]
    public void New_TextThatIsNotAString_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new AppText(new Dictionary<string, string> { ["en"] = """{ "count": 3 }""" }))
            .Message.ShouldContain("count");
    }

    // The files shipped with the API

    private static readonly AppText Shipped = new();

    [Test]
    public void ShippedFiles_IncludeEnglishAndItalian()
    {
        Shipped.Languages.ShouldBe(["en", "it"]);
        Shipped.TranslateIn(Italian, "errors.unexpected").ShouldNotBe(Shipped.TranslateIn(CultureInfo.InvariantCulture, "errors.unexpected"));
    }

    [Test]
    public void ShippedFiles_HaveNoKeyEnglishLacks()
    {
        // A key only another language has is a typo or a leftover: English is the reference
        var english = Shipped.KeysOf("en").ToHashSet();
        foreach (var language in Shipped.Languages.Skip(1))
            Shipped.KeysOf(language).Where(k => !english.Contains(k)).ShouldBeEmpty($"{language}.json has keys en.json lacks");
    }

    [Test]
    public void ShippedFiles_KeepThePlaceholdersOfEnglish()
    {
        foreach (var language in Shipped.Languages.Skip(1))
        {
            var culture = CultureInfo.GetCultureInfo(language);
            foreach (var key in Shipped.KeysOf(language))
            {
                Placeholders(Shipped.TranslateIn(culture, key)).ShouldBe(
                    Placeholders(Shipped.TranslateIn(CultureInfo.InvariantCulture, key)), ignoreOrder: true,
                    customMessage: $"{language}.json: {key}");
            }
        }
    }

    [Test]
    public void ShippedLanguages_AreTheWebAppsLanguages()
    {
        // One list of languages: the web app sends the one the user picked, the API must know it
        var locales = Path.Combine(RepositoryRoot(), "src", "nInvoices.Web", "src", "locales");
        var web = Directory.GetFiles(locales, "*.json").Select(f => Path.GetFileNameWithoutExtension(f)).Order(StringComparer.Ordinal).ToList();

        web.ShouldBe(Shipped.Languages.Order(StringComparer.Ordinal).ToList(),
            "Each language needs a file on both sides: src/nInvoices.Web/src/locales/<language>.json and " +
            "src/nInvoices.Application/Localization/Messages/<language>.json (Docs/LOCALIZATION.md)");
        foreach (var file in Directory.GetFiles(locales, "*.json"))
            Should.NotThrow(() => JsonDocument.Parse(File.ReadAllText(file)).Dispose(), $"{file} is not valid JSON");
    }

    private static List<string> Placeholders(string text) =>
        System.Text.RegularExpressions.Regex.Matches(text, @"\{(\w+)\}").Select(m => m.Groups[1].Value).ToList();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nInvoices.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("nInvoices.slnx not found above the test directory");
    }
}
