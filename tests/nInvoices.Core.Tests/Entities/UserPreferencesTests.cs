using nInvoices.Core.Entities;
using Shouldly;

namespace nInvoices.Core.Tests.Entities;

[TestFixture]
public sealed class UserPreferencesTests
{
    [TestCase("en", "en")]
    [TestCase(" IT ", "it")]
    [TestCase("pt_br", "pt-BR")]
    [TestCase("zh-hant-tw", "zh-Hant-TW")]
    [TestCase("es-419", "es-419")]
    public void NormalizeLanguage_IsTheCanonicalTag(string language, string expected)
    {
        UserPreferences.NormalizeLanguage(language).ShouldBe(expected);
    }

    [TestCase("")]
    [TestCase("e")]
    [TestCase("english")]
    [TestCase("en-")]
    [TestCase("en-US-x")]
    [TestCase("<script>")]
    public void NormalizeLanguage_NotALanguageTag_Throws(string language)
    {
        Should.Throw<ArgumentException>(() => UserPreferences.NormalizeLanguage(language));
    }

    [Test]
    public void ChangeUiLanguage_StoresTheCanonicalTag()
    {
        var preferences = new UserPreferences("en");

        preferences.ChangeUiLanguage("IT");

        preferences.UiLanguage.ShouldBe("it");
    }
}
