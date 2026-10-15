using nInvoices.Application.Telemetry;

namespace nInvoices.Infrastructure.PdfExport;

/// <summary>Times PDF rendering into <see cref="AppMetrics.PdfRenderDuration"/>, whichever engine does it.</summary>
internal static class PdfMetrics
{
    public const string Chrome = "chrome";
    public const string QuestPdf = "questpdf";

    /// <summary>Times a render drawn in memory (QuestPDF).</summary>
    public static T Record<T>(TimeProvider clock, string engine, string document, Func<T> render)
    {
        ArgumentNullException.ThrowIfNull(render);

        var started = clock.GetTimestamp();
        var outcome = AppMetrics.Outcome.Success;
        try
        {
            return render();
        }
        catch (Exception exception)
        {
            outcome = AppMetrics.OutcomeOf(exception);
            throw;
        }
        finally
        {
            Write(clock, started, engine, document, outcome, browser: null);
        }
    }

    /// <summary>Times a render that awaits something (the headless browser).</summary>
    /// <param name="browser">"cold" when the render has to start the browser, "warm" when it is running.</param>
    public static async Task<T> RecordAsync<T>(TimeProvider clock, string engine, string document, string? browser, Func<Task<T>> render)
    {
        ArgumentNullException.ThrowIfNull(render);

        var started = clock.GetTimestamp();
        var outcome = AppMetrics.Outcome.Success;
        try
        {
            return await render();
        }
        catch (Exception exception)
        {
            outcome = AppMetrics.OutcomeOf(exception);
            throw;
        }
        finally
        {
            Write(clock, started, engine, document, outcome, browser);
        }
    }

    private static void Write(TimeProvider clock, long started, string engine, string document, string outcome, string? browser)
    {
        var seconds = clock.GetElapsedTime(started).TotalSeconds;
        var engineTag = new KeyValuePair<string, object?>("engine", engine);
        var documentTag = new KeyValuePair<string, object?>("document", document);
        var outcomeTag = new KeyValuePair<string, object?>("outcome", outcome);

        if (browser is null)
            AppMetrics.PdfRenderDuration.Record(seconds, engineTag, documentTag, outcomeTag);
        else
            AppMetrics.PdfRenderDuration.Record(seconds, engineTag, documentTag, outcomeTag, new KeyValuePair<string, object?>("browser", browser));
    }
}
