using System.Globalization;
using Microsoft.AspNetCore.Http.Headers;
using nInvoices.Application.Localization;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// Answers each request in the user's language: the web app sends the language the user chose in Settings
/// as <c>Accept-Language</c>, and this sets the request's <see cref="CultureInfo.CurrentUICulture"/> to the best
/// language the API has texts for (<see cref="IAppText"/>), English when there is none. Validation messages
/// (FluentValidation's own included) and error messages follow it.
/// </summary>
/// <remarks>
/// Only the UI culture: <see cref="CultureInfo.CurrentCulture"/>, which formats numbers and dates, is left as
/// it is, so invoices, exports and logs read the same whatever the user's language.
/// </remarks>
public static class UiLanguageMiddleware
{
    public static IApplicationBuilder UseUiLanguage(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var language = PickLanguage(context.Request.GetTypedHeaders(), context.RequestServices.GetRequiredService<IAppText>());
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            try
            {
                await next(context);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        });

    /// <summary>"it-CH, en;q=0.5" → "it" when the API has Italian; the best served language, by quality, else English.</summary>
    public static string PickLanguage(RequestHeaders headers, IAppText text)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(text);

        return headers.AcceptLanguage
            .Where(l => l.Quality is null or > 0)
            .OrderByDescending(l => l.Quality ?? 1) // stable: equal qualities keep the client's order
            .Select(l => text.Match(l.Value.Value))
            .FirstOrDefault(l => l is not null)
            ?? AppText.FallbackLanguage;
    }
}
