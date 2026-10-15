using System.Diagnostics.Metrics;
using FluentValidation;
using nInvoices.Application.Exceptions;
using nInvoices.Core.Exceptions;

namespace nInvoices.Application.Telemetry;

/// <summary>
/// The application's own measurements (as opposed to ASP.NET's and the runtime's, which come with the
/// instrumentation). They leave the process through the OpenTelemetry exporter when
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set; <c>ServiceDefaults</c> subscribes to the meter by name.
/// </summary>
/// <remarks>
/// Tags are small fixed sets (a request name, an engine, an outcome), never a customer, an invoice number
/// or a user id: each distinct combination is a time series in the backend, and the backend is not ours.
/// </remarks>
public static class AppMetrics
{
    /// <summary>ServiceDefaults registers the meters whose name starts with this.</summary>
    public const string MeterName = "nInvoices";

    public static readonly Meter Meter = new(MeterName, "1.0");

    /// <summary>
    /// How long a use case (a Mediator request) takes, from the pipeline's entry to its end, validation
    /// included. Tags: <c>request</c> (e.g. <c>GetInvoiceByIdQuery</c>) and <c>outcome</c> (<see cref="Outcome"/>).
    /// </summary>
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram(
        "ninvoices.request.duration",
        unit: "s",
        description: "Duration of an application request (a query or a command).",
        tags: null,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30]
        });

    /// <summary>
    /// How long drawing a PDF takes. Tags: <c>engine</c> (<c>chrome</c> for HTML templates, <c>questpdf</c>
    /// for the built-in layouts), <c>document</c>, <c>outcome</c> and, for Chrome, <c>browser</c>
    /// (<c>cold</c> when this render had to start the headless browser, <c>warm</c> otherwise).
    /// </summary>
    public static readonly Histogram<double> PdfRenderDuration = Meter.CreateHistogram(
        "ninvoices.pdf.render.duration",
        unit: "s",
        description: "Duration of rendering a PDF document.",
        tags: null,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.05, 0.1, 0.25, 0.5, 1, 2, 4, 8, 15, 30, 60]
        });

    /// <summary>
    /// Invoices issued (finalized), counted once the save went through. Tag: <c>type</c> (the invoice type:
    /// Monthly, OneTime...).
    /// </summary>
    public static readonly Counter<long> InvoicesFinalized = Meter.CreateCounter<long>(
        "ninvoices.invoices.finalized",
        unit: "{invoice}",
        description: "Invoices finalized.");

    /// <summary>
    /// What came of handing an e-invoice record to a tax authority or a portal. Tags: <c>system</c>
    /// (<see cref="Systems"/>) and <c>result</c> (<see cref="SubmissionResult"/>).
    /// </summary>
    public static readonly Counter<long> EInvoiceSubmissions = Meter.CreateCounter<long>(
        "ninvoices.einvoice.submissions",
        unit: "{submission}",
        description: "E-invoice submissions to a tax authority or portal, by result.");

    /// <summary>
    /// How long the remote system takes to answer a submission (the call only, not our own work). Tags:
    /// <c>system</c> and <c>outcome</c> (<c>success</c> or <c>error</c>).
    /// </summary>
    public static readonly Histogram<double> EInvoiceSubmitDuration = Meter.CreateHistogram(
        "ninvoices.einvoice.submit.duration",
        unit: "s",
        description: "Duration of the call that submits e-invoices to a tax authority or portal.",
        tags: null,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.1, 0.25, 0.5, 1, 2, 5, 10, 20, 30, 60]
        });

    /// <summary>
    /// Gmail drafts of invoice emails. Tag: <c>outcome</c> (<see cref="DraftOutcome"/>). A reconnect that is
    /// needed is worth seeing: the user's Gmail link broke.
    /// </summary>
    public static readonly Counter<long> EmailDrafts = Meter.CreateCounter<long>(
        "ninvoices.email.drafts",
        unit: "{draft}",
        description: "Gmail drafts created for invoices, by outcome.");

    /// <summary>The values of the <c>system</c> tag.</summary>
    public static class Systems
    {
        /// <summary>The Spanish Tax Agency (AEAT), through Verifactu.</summary>
        public const string Verifactu = "verifactu";

        /// <summary>FACe, the Spanish portal for invoices to public administrations.</summary>
        public const string Face = "face";
    }

    /// <summary>The values of the <c>result</c> tag of <see cref="EInvoiceSubmissions"/>.</summary>
    public static class SubmissionResult
    {
        /// <summary>The authority registered it.</summary>
        public const string Accepted = "accepted";

        /// <summary>Registered, with errors the user may want to correct (Verifactu).</summary>
        public const string AcceptedWithErrors = "accepted_with_errors";

        /// <summary>The authority refused it.</summary>
        public const string Rejected = "rejected";

        /// <summary>
        /// It was not registered and is tried again: the authority could not be reached, refused the whole batch,
        /// or gave no answer for the record.
        /// </summary>
        public const string Failed = "failed";
    }

    /// <summary>The values of the <c>outcome</c> tag of <see cref="EmailDrafts"/>.</summary>
    public static class DraftOutcome
    {
        public const string Created = "created";

        /// <summary>The Gmail authorization is gone or unreadable: the user must connect Gmail again.</summary>
        public const string ReconnectRequired = "reconnect_required";
    }

    /// <summary>Counts <paramref name="count"/> submissions with the same result (nothing is recorded for 0).</summary>
    public static void RecordSubmissions(string system, string result, long count)
    {
        if (count <= 0)
            return;

        EInvoiceSubmissions.Add(
            count,
            new KeyValuePair<string, object?>("system", system),
            new KeyValuePair<string, object?>("result", result));
    }

    /// <summary>Records the time a remote system took to answer.</summary>
    public static void RecordSubmitDuration(string system, double seconds, bool succeeded) =>
        EInvoiceSubmitDuration.Record(
            seconds,
            new KeyValuePair<string, object?>("system", system),
            new KeyValuePair<string, object?>("outcome", succeeded ? Outcome.Success : Outcome.Error));

    /// <summary>The values of the <c>outcome</c> tag.</summary>
    public static class Outcome
    {
        public const string Success = "success";

        /// <summary>The request did not pass its validators (a 400 with the fields).</summary>
        public const string Invalid = "invalid";

        public const string NotFound = "not_found";

        /// <summary>A business rule or an entity refused it (a 400 with the reason).</summary>
        public const string Rejected = "rejected";

        public const string Canceled = "canceled";

        /// <summary>Anything else: a bug, or an infrastructure failure.</summary>
        public const string Error = "error";
    }

    /// <summary>The <c>outcome</c> tag for a request that ended with <paramref name="exception"/>.</summary>
    public static string OutcomeOf(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OperationCanceledException => Outcome.Canceled,
            ValidationException => Outcome.Invalid,
            NotFoundException => Outcome.NotFound,
            DomainException or ArgumentException => Outcome.Rejected,
            _ => Outcome.Error
        };
    }
}
