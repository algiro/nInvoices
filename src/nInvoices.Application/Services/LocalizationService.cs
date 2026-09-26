using System.Text.Json;

namespace nInvoices.Application.Services;

/// <summary>
/// Provides localization for template rendering.
/// Loads locale data from JSON files and provides translation functions.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Gets the localized day of week name.
    /// </summary>
    string GetDayOfWeek(DayOfWeek dayOfWeek, string locale, bool shortFormat = false);
    
    /// <summary>
    /// Gets the localized month name.
    /// </summary>
    string GetMonthName(int monthNumber, string locale, bool shortFormat = false);
    
    /// <summary>
    /// Checks if a locale is supported.
    /// </summary>
    bool IsLocaleSupported(string locale);
    
    /// <summary>
    /// Gets all supported locales.
    /// </summary>
    IEnumerable<string> GetSupportedLocales();
}

public sealed class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, LocaleData> _localeData = new();
    private readonly string _localizationPath;

    public LocalizationService()
    {
        // Localization files are in Application/Localization folder
        _localizationPath = Path.Combine(
            AppContext.BaseDirectory,
            "Localization"
        );
        
        LoadLocales();
    }

    private void LoadLocales()
    {
        if (!Directory.Exists(_localizationPath))
        {
            throw new DirectoryNotFoundException(
                $"Localization directory not found: {_localizationPath}"
            );
        }

        var localeFiles = Directory.GetFiles(_localizationPath, "*.json");
        
        foreach (var filePath in localeFiles)
        {
            var locale = Path.GetFileNameWithoutExtension(filePath);
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<LocaleData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data != null)
            {
                _localeData[locale] = data;
            }
        }

        if (_localeData.Count == 0)
        {
            throw new InvalidOperationException("No locale files found in Localization directory");
        }
    }

    public string GetDayOfWeek(DayOfWeek dayOfWeek, string locale, bool shortFormat = false)
    {
        if (!_localeData.TryGetValue(locale, out var data))
        {
            // Fallback to en-US if locale not found
            locale = "en-US";
            if (!_localeData.TryGetValue(locale, out data))
            {
                return dayOfWeek.ToString();
            }
        }

        var dayIndex = (int)dayOfWeek;
        var names = shortFormat ? data.DaysOfWeek.Short : data.DaysOfWeek.Full;
        
        return dayIndex >= 0 && dayIndex < names.Length 
            ? names[dayIndex] 
            : dayOfWeek.ToString();
    }

    public string GetMonthName(int monthNumber, string locale, bool shortFormat = false)
    {
        if (!_localeData.TryGetValue(locale, out var data))
        {
            // Fallback to en-US if locale not found
            locale = "en-US";
            if (!_localeData.TryGetValue(locale, out data))
            {
                return monthNumber.ToString();
            }
        }

        var monthIndex = monthNumber - 1; // Months are 1-based, array is 0-based
        var names = shortFormat ? data.Months.Short : data.Months.Full;
        
        return monthIndex >= 0 && monthIndex < names.Length 
            ? names[monthIndex] 
            : monthNumber.ToString();
    }

    public bool IsLocaleSupported(string locale) => _localeData.ContainsKey(locale);

    public IEnumerable<string> GetSupportedLocales() => _localeData.Keys;
}

/// <summary>
/// Represents localization data for a specific locale.
/// </summary>
public sealed class LocaleData
{
    public DaysOfWeekData DaysOfWeek { get; set; } = new();
    public MonthsData Months { get; set; } = new();
}

public sealed class DaysOfWeekData
{
    public string[] Full { get; set; } = Array.Empty<string>();
    public string[] Short { get; set; } = Array.Empty<string>();
}

public sealed class MonthsData
{
    public string[] Full { get; set; } = Array.Empty<string>();
    public string[] Short { get; set; } = Array.Empty<string>();
}
