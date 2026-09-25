using nInvoices.Core.Entities;

namespace nInvoices.Application.Services.Holidays;

/// <summary>
/// National public holidays per country, used to create a country's <see cref="HolidayCalendar"/>
/// the first time it is needed (and to reset it). Regional and local holidays are not included,
/// nor are substitute days when a holiday falls on a weekend; users add those as rules.
/// Names are in the country's language, as they appear on its calendars.
/// </summary>
public static class BuiltInHolidays
{
    private static readonly Dictionary<string, Func<IEnumerable<HolidayRule>>> Countries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IT"] = () =>
        [
            HolidayRule.Fixed("Capodanno", 1, 1),
            HolidayRule.Fixed("Epifania", 1, 6),
            HolidayRule.FromEaster("Lunedì dell'Angelo", 1),
            HolidayRule.Fixed("Festa della Liberazione", 4, 25),
            HolidayRule.Fixed("Festa del Lavoro", 5, 1),
            HolidayRule.Fixed("Festa della Repubblica", 6, 2),
            HolidayRule.Fixed("Ferragosto", 8, 15),
            // National holiday again from 2026 (Law 151/2025)
            HolidayRule.Fixed("San Francesco d'Assisi", 10, 4, fromYear: 2026),
            HolidayRule.Fixed("Ognissanti", 11, 1),
            HolidayRule.Fixed("Immacolata Concezione", 12, 8),
            HolidayRule.Fixed("Natale", 12, 25),
            HolidayRule.Fixed("Santo Stefano", 12, 26)
        ],
        ["DE"] = () =>
        [
            HolidayRule.Fixed("Neujahr", 1, 1),
            HolidayRule.FromEaster("Karfreitag", -2),
            HolidayRule.FromEaster("Ostermontag", 1),
            HolidayRule.Fixed("Tag der Arbeit", 5, 1),
            HolidayRule.FromEaster("Christi Himmelfahrt", 39),
            HolidayRule.FromEaster("Pfingstmontag", 50),
            HolidayRule.Fixed("Tag der Deutschen Einheit", 10, 3),
            HolidayRule.Fixed("1. Weihnachtstag", 12, 25),
            HolidayRule.Fixed("2. Weihnachtstag", 12, 26)
        ],
        ["AT"] = () =>
        [
            HolidayRule.Fixed("Neujahr", 1, 1),
            HolidayRule.Fixed("Heilige Drei Könige", 1, 6),
            HolidayRule.FromEaster("Ostermontag", 1),
            HolidayRule.Fixed("Staatsfeiertag", 5, 1),
            HolidayRule.FromEaster("Christi Himmelfahrt", 39),
            HolidayRule.FromEaster("Pfingstmontag", 50),
            HolidayRule.FromEaster("Fronleichnam", 60),
            HolidayRule.Fixed("Mariä Himmelfahrt", 8, 15),
            HolidayRule.Fixed("Nationalfeiertag", 10, 26),
            HolidayRule.Fixed("Allerheiligen", 11, 1),
            HolidayRule.Fixed("Mariä Empfängnis", 12, 8),
            HolidayRule.Fixed("Christtag", 12, 25),
            HolidayRule.Fixed("Stefanitag", 12, 26)
        ],
        ["FR"] = () =>
        [
            HolidayRule.Fixed("Jour de l'an", 1, 1),
            HolidayRule.FromEaster("Lundi de Pâques", 1),
            HolidayRule.Fixed("Fête du Travail", 5, 1),
            HolidayRule.Fixed("Victoire 1945", 5, 8),
            HolidayRule.FromEaster("Ascension", 39),
            HolidayRule.FromEaster("Lundi de Pentecôte", 50),
            HolidayRule.Fixed("Fête nationale", 7, 14),
            HolidayRule.Fixed("Assomption", 8, 15),
            HolidayRule.Fixed("Toussaint", 11, 1),
            HolidayRule.Fixed("Armistice 1918", 11, 11),
            HolidayRule.Fixed("Noël", 12, 25)
        ],
        ["ES"] = () =>
        [
            HolidayRule.Fixed("Año Nuevo", 1, 1),
            HolidayRule.Fixed("Epifanía del Señor", 1, 6),
            HolidayRule.FromEaster("Viernes Santo", -2),
            HolidayRule.Fixed("Fiesta del Trabajo", 5, 1),
            HolidayRule.Fixed("Asunción de la Virgen", 8, 15),
            HolidayRule.Fixed("Fiesta Nacional de España", 10, 12),
            HolidayRule.Fixed("Todos los Santos", 11, 1),
            HolidayRule.Fixed("Día de la Constitución", 12, 6),
            HolidayRule.Fixed("Inmaculada Concepción", 12, 8),
            HolidayRule.Fixed("Navidad", 12, 25)
        ],
        // England and Wales bank holidays
        ["GB"] = () =>
        [
            HolidayRule.Fixed("New Year's Day", 1, 1),
            HolidayRule.FromEaster("Good Friday", -2),
            HolidayRule.FromEaster("Easter Monday", 1),
            HolidayRule.NthWeekdayOf("Early May bank holiday", 5, DayOfWeek.Monday, 1),
            HolidayRule.NthWeekdayOf("Spring bank holiday", 5, DayOfWeek.Monday, -1),
            HolidayRule.NthWeekdayOf("Summer bank holiday", 8, DayOfWeek.Monday, -1),
            HolidayRule.Fixed("Christmas Day", 12, 25),
            HolidayRule.Fixed("Boxing Day", 12, 26)
        ],
        // Federal holidays
        ["US"] = () =>
        [
            HolidayRule.Fixed("New Year's Day", 1, 1),
            HolidayRule.NthWeekdayOf("Martin Luther King Jr. Day", 1, DayOfWeek.Monday, 3),
            HolidayRule.NthWeekdayOf("Washington's Birthday", 2, DayOfWeek.Monday, 3),
            HolidayRule.NthWeekdayOf("Memorial Day", 5, DayOfWeek.Monday, -1),
            HolidayRule.Fixed("Juneteenth", 6, 19, fromYear: 2021),
            HolidayRule.Fixed("Independence Day", 7, 4),
            HolidayRule.NthWeekdayOf("Labor Day", 9, DayOfWeek.Monday, 1),
            HolidayRule.NthWeekdayOf("Columbus Day", 10, DayOfWeek.Monday, 2),
            HolidayRule.Fixed("Veterans Day", 11, 11),
            HolidayRule.NthWeekdayOf("Thanksgiving Day", 11, DayOfWeek.Thursday, 4),
            HolidayRule.Fixed("Christmas Day", 12, 25)
        ]
    };

    /// <summary>Countries with built-in rules, as upper-case ISO codes in alphabetical order.</summary>
    public static IReadOnlyList<string> CountryCodes { get; } = Countries.Keys.Order().ToList();

    public static bool Has(string countryCode) => Countries.ContainsKey(countryCode);

    /// <summary>New, unsaved rules for the country; empty when it has no built-in rules.</summary>
    public static IReadOnlyList<HolidayRule> RulesFor(string countryCode) =>
        Countries.TryGetValue(countryCode, out var rules) ? rules().ToList() : [];
}
