namespace nInvoices.Application.DTOs;

/// <summary>The user's preferences, wherever they sign in.</summary>
/// <param name="UiLanguage">Language of the app (e.g. <c>en</c>, <c>it</c>); <c>en</c> until the user picks another.</param>
/// <param name="UiLanguages">The languages the app is available in, <c>en</c> first.</param>
public sealed record UserPreferencesDto(string UiLanguage, IReadOnlyList<string> UiLanguages);

/// <param name="UiLanguage">One of <see cref="UserPreferencesDto.UiLanguages"/>.</param>
public sealed record UpdateUserPreferencesDto(string UiLanguage);
