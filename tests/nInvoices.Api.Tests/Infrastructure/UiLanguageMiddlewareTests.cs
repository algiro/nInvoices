using Microsoft.AspNetCore.Http;
using nInvoices.Api.Infrastructure;
using nInvoices.Application.Localization;
using Shouldly;

namespace nInvoices.Api.Tests.Infrastructure;

/// <summary>The API answers in the language the web app sends (the user's choice), English when it has no texts for it.</summary>
[TestFixture]
public sealed class UiLanguageMiddlewareTests
{
    private static readonly AppText Text = new(new Dictionary<string, string> { ["en"] = "{}", ["it"] = "{}", ["pt-BR"] = "{}" });

    [TestCase(null, "en")]
    [TestCase("it", "it")]
    [TestCase("it-CH", "it")]
    [TestCase("fr-FR, it;q=0.8, en;q=0.5", "it")]
    [TestCase("en;q=0.4, it;q=0.9", "it")]
    [TestCase("it;q=0, pt-BR", "pt-BR")]
    [TestCase("fr, de", "en")]
    [TestCase("*", "en")]
    public void PickLanguage_IsTheBestLanguageTheApiHas(string? acceptLanguage, string expected)
    {
        var context = new DefaultHttpContext();
        if (acceptLanguage is not null)
            context.Request.Headers.AcceptLanguage = acceptLanguage;

        UiLanguageMiddleware.PickLanguage(context.Request.GetTypedHeaders(), Text).ShouldBe(expected);
    }
}
