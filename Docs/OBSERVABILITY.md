# Observability: traces, metrics and logs over OpenTelemetry

The API can send **traces**, **metrics** and **logs** to any OpenTelemetry (OTLP) backend. It is **off by
default**: nothing leaves the server until `OTEL_EXPORTER_OTLP_ENDPOINT` is set. This guide uses Grafana
Cloud (free tier), but a collector, Aspire's dashboard or Seq work the same way.

## What is sent

| Signal | What | From |
|---|---|---|
| Traces | One span per request (route, status, duration) and per outgoing call (Gmail, FACe, AEAT) | ASP.NET Core and `HttpClient` instrumentation |
| Metrics | Request and HTTP client durations and counts, .NET runtime (memory, GC, thread pool) | the same, plus runtime instrumentation |
| Logs | What Serilog writes (the console and `logs/` files continue as before), with the trace id of the request that wrote them | `Serilog.Sinks.OpenTelemetry` |

Every signal carries the same resource: `service.name` (`ninvoices-api`), `service.namespace`
(`ninvoices`) and `deployment.environment`, so they line up in the backend.

**Not sent, on purpose**
- **Query strings.** They are removed from spans before export (`QueryStringStripper`): the Gmail
  connection's authorization `code` and the text searched in the invoice list (customer names) would
  otherwise travel with the trace. The path stays.
- **Health checks** (`/health`, `/alive`) are not traced.
- **Data in the database.** Nothing is read from the database to be exported. The EF Core and PostgreSQL
  queries are not instrumented.

**Worth knowing:** log lines name entities by id, but two lines contain an email address, the Gmail
account being connected or removed. User ids (the Keycloak `sub`) appear in some. If you don't want logs
in a third party at all, set `OTEL_LOGS_EXPORTER=none`: traces and metrics are still sent.

## Application metrics

Besides what the instrumentation gives for free (`http_server_request_duration_seconds` per route, .NET
runtime metrics), the application records its own measurements, from the meter `nInvoices`
(`Application/Telemetry/AppMetrics.cs`). In Grafana's Prometheus they are named with underscores and a
unit suffix.

| Metric | Prometheus name | Tags | Answers |
|---|---|---|---|
| `ninvoices.request.duration` | `ninvoices_request_duration_seconds_{bucket,count,sum}` | `request`, `outcome` | How long each use case takes (open an invoice, create one, download a PDF...) and how it ended |
| `ninvoices.pdf.render.duration` | `ninvoices_pdf_render_duration_seconds_{bucket,count,sum}` | `engine`, `document`, `outcome`, `browser` | How long drawing a PDF takes |
| `ninvoices.invoices.finalized` | `ninvoices_invoices_finalized_total` (counter) | `type` | How many invoices are issued, by type (Monthly, OneTime...); counted once the save went through |
| `ninvoices.einvoice.submissions` | `ninvoices_einvoice_submissions_total` (counter) | `system`, `result` | What the Tax Agency (`verifactu`) and FACe (`face`) answered: `accepted`, `accepted_with_errors`, `rejected`, `failed` |
| `ninvoices.einvoice.submit.duration` | `ninvoices_einvoice_submit_duration_seconds_{bucket,count,sum}` | `system`, `outcome` | How long the authority takes to answer (the call only) |
| `ninvoices.email.drafts` | `ninvoices_email_drafts_total` (counter) | `outcome` | Gmail drafts: `created`, or `reconnect_required` when a user's Gmail link broke |

- **`request`** is the Mediator request's name, so every use case is measured without code of its own
  (`MetricsBehavior`, the outermost behaviour of the pipeline, validation included). "Opening an invoice" is
  `GetInvoiceByIdQuery`; "creating an invoice" `GenerateInvoiceCommand`; "invoice PDF" `GetInvoicePdfQuery`.
- **`outcome`**: `success`; `invalid` (refused by a validator); `not_found`; `rejected` (a business rule or an
  entity); `canceled`; `error` (anything else). A query that answers "nothing found" by returning null, such as
  opening an invoice that doesn't exist, is a `success`: the HTTP 404 is in the ASP.NET metrics.
- **PDF `engine`**: `chrome` renders the HTML templates (invoice, timesheet, email attachments; `document` is
  `html`), `questpdf` the built-in layouts (`document` is `invoice` or `calendar`). For Chrome, `browser=cold`
  marks the render that had to start the headless browser; read warm and cold apart.
- **Submission `result`**: `accepted` (the authority registered it, including a record it already had),
  `accepted_with_errors` (registered with remarks to correct), `rejected` (refused: needs attention), `failed`
  (not registered and tried again: unreachable, a refusal of the whole batch, or no answer for the record).
  For FACe, `accepted` means the portal took the invoice; where it goes next (registered, accounted, paid) is
  its status, which the user refreshes, and is not counted again. Nothing is counted when the send is refused
  before reaching the authority (no certificate, Spain not turned on...).
- **Buckets** are set with the instruments (requests 5 ms to 30 s, PDFs 50 ms to 60 s, submissions 100 ms to 60 s).

Example queries (p95 over the range):

```promql
histogram_quantile(0.95, sum by (le) (rate(ninvoices_request_duration_seconds_bucket{request="GetInvoiceByIdQuery"}[$__rate_interval])))
histogram_quantile(0.95, sum by (le, engine, browser) (rate(ninvoices_pdf_render_duration_seconds_bucket[$__rate_interval])))
```

A ready dashboard is in [`grafana/ninvoices-dashboard.json`](grafana/ninvoices-dashboard.json): in Grafana,
*Dashboards → New → Import*, upload it and pick your Prometheus data source. With one user's traffic a line
has gaps; widen the time range or use `increase()` over an hour.

### Adding a measurement

1. Create the instrument in `AppMetrics` (a `Histogram<double>` with `unit: "s"` and bucket `advice`, or a
   `Counter<long>`), named `ninvoices.<area>.<what>`.
2. Record it where the work happens. A use case needs nothing: `MetricsBehavior` already times it.
3. **Tags are small fixed sets.** Never a customer, an invoice number, an email or a user id: each distinct
   combination is a time series in a backend that is not ours (and the free tier has a series limit).
4. Test it with a `MeterListener` (see `MetricsBehaviorTests`).

## Grafana Cloud

1. In Grafana Cloud, open **Connections → Add new connection → OpenTelemetry (OTLP)** (or *Details* of
   your stack → *OpenTelemetry*). It shows your **OTLP endpoint**, your **instance ID** and lets you create
   a **token**. Give the token at least the scopes `metrics:write`, `logs:write` and `traces:write`.
2. Make the header value: the base64 of `<instance ID>:<token>`.

   ```bash
   printf '%s' '<instance id>:<token>' | base64 -w0
   ```

3. On the server, add to the `.env` next to `docker-compose.yml` (never in the repository):

   ```env
   OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp-gateway-prod-<region>.grafana.net/otlp
   OTEL_EXPORTER_OTLP_HEADERS=Authorization=Basic%20<the base64 from step 2>
   ```

   The protocol is `http/protobuf` by default in the compose files (Grafana Cloud's gateway doesn't take
   gRPC), the service is `ninvoices-api`. Write the space of `Basic ` as `%20`.
4. Restart the API: `docker compose up -d api` (a deploy does it too).
5. In Grafana: **Explore**, choose the *Tempo* (traces), *Prometheus* (metrics) or *Loki* (logs) data
   source of your stack and filter by `service.name = ninvoices-api`.

Loki keeps the log's resource as labels (`service_name`, `deployment_environment`), so
`{service_name="ninvoices-api"}` shows the logs, and a trace links to its logs by trace id.

## Settings

All standard OpenTelemetry variables; the compose files pass these:

| Variable | Default | |
|---|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | empty (off) | base URL of the OTLP/HTTP endpoint (`/v1/traces`, `/v1/metrics` and `/v1/logs` are appended) |
| `OTEL_EXPORTER_OTLP_HEADERS` | empty | `key=value,key2=value2`, values percent-encoded (`%20` for a space) |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` | `grpc` for a collector |
| `OTEL_SERVICE_NAME` | `ninvoices-api` | |
| `OTEL_RESOURCE_ATTRIBUTES` | `service.namespace=ninvoices` | `key=value,...` more attributes |
| `OTEL_LOGS_EXPORTER` | empty | `none` keeps logs local |
| `OTEL_METRIC_EXPORT_INTERVAL` | 60000 | milliseconds between metric exports |

## Free tier

Grafana Cloud's free tier has monthly limits on logs, traces and metric series. One API serving one
freelancer is far below them; if you ever approach one, `OTEL_LOGS_EXPORTER=none` is the biggest saving.

## Checking that it works

- Locally, run the API with the variables above pointing at any OTLP receiver, and make a few requests.
- The API's `Program.cs` says nothing when export fails (an exporter never blocks requests): a wrong
  token shows as no data in Grafana. Check the endpoint's region, that the header value is
  `Basic%20<base64>` and that the token has the three write scopes.
