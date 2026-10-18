using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

public static class UserPreferencesMapper
{
    /// <summary>The user's preferences; <paramref name="preferences"/> is null for a user who never changed them.</summary>
    public static UserPreferencesDto ToDto(UserPreferences? preferences, IAppText text) => new(
        // A language whose files were removed since the user chose it reads as the closest one left
        text.Match(preferences?.UiLanguage) ?? UserPreferences.DefaultUiLanguage,
        text.Languages);
}
