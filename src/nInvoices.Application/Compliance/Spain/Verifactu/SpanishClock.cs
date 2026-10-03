using System.Globalization;

namespace nInvoices.Application.Compliance.Spain.Verifactu;

/// <summary>The time of Spain (peninsula), which records are stamped with (FechaHoraHusoGenRegistro).</summary>
public static class SpanishClock
{
    private static readonly Lazy<TimeZoneInfo> Zone = new(Find);

    /// <summary>ISO 8601 with the offset in force then, e.g. "2026-10-03T11:30:00+02:00".</summary>
    public static string Stamp(DateTimeOffset now) =>
        TimeZoneInfo.ConvertTime(now, Zone.Value).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    private static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Europe/Madrid", "Romance Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        // The server knows neither name: CET/CEST by the rule that applies in Spain since 1996
        return TimeZoneInfo.CreateCustomTimeZone(
            "Spain", TimeSpan.FromHours(1), "Spain", "Spain",
            "Spain DST", [CentralEuropeanRule()]);
    }

    private static TimeZoneInfo.AdjustmentRule CentralEuropeanRule() =>
        TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday));
}
