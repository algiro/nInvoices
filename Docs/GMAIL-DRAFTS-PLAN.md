# Implementation plan — Send invoices as Gmail drafts

Status: implemented 2026-09-25 (uncommitted). See "Implementation notes" at the end for where it differs from the plan below.

## Goal

From an invoice, create a **draft in the user's own Gmail account**. The draft contains a
subject and body rendered from a configurable email template, plus the invoice PDF (and, for
monthly invoices, optionally the monthly report / calendar PDF). The user opens the draft in
Gmail, reviews it and clicks Send. Because the mail really leaves from Gmail, it appears in
**Sent**, replies arrive in the Gmail inbox, and SPF/DKIM/DMARC pass.

Out of scope for now: sending directly from the app, Proton Mail, detecting replies in the app.

## Key decisions

| Topic | Decision | Why |
|---|---|---|
| Gmail access | Gmail API, OAuth2 authorization-code flow, scope `https://www.googleapis.com/auth/gmail.compose` | Needed to create drafts. No Gmail password is stored. |
| OAuth client type | **Web application**. The API does the code exchange and holds the refresh token. | The token never reaches the browser. |
| Consent screen | External. Testing while developing, then **In production, unverified** (personal use). | In Testing, refresh tokens expire after 7 days. |
| Libraries | `Google.Apis.Gmail.v1`, `Google.Apis.Auth` (Infrastructure); `MimeKit` (Infrastructure) | The official client handles token refresh; MimeKit builds the MIME message. |
| Token storage | New table, refresh token encrypted with ASP.NET **Data Protection** | Never store the token in plain text. |
| Data Protection keys | Persisted to a **Docker volume** (`./volumes/dataprotection`) | Without this, keys are regenerated on every container restart and the stored token becomes unreadable. Keeping the keys out of the DB means a DB backup alone cannot decrypt the token. |
| Template syntax | Scriban, same renderer (`ITemplateRenderer`) and camelCase variables as invoice templates | One syntax for the user, reuses validation and preview. |
| Status flow | Creating a draft does **not** change the invoice status. The user clicks "Mark as sent" after sending from Gmail. | With only `gmail.compose` the app cannot confirm that the mail was sent. |

## The OAuth callback problem

The SPA authenticates to the API with a Keycloak **bearer token in a header**. Google's redirect
to the callback is a plain browser navigation, so it carries **no** bearer token. Therefore:

1. `POST /api/gmail/connect` (authenticated) creates a random `state` (32 bytes, URL-safe) and
   stores it server-side together with the Keycloak `UserId` and an expiry (10 minutes). It
   returns the Google authorization URL. The SPA does `window.location.href = url`.
2. `GET /api/gmail/oauth/callback?code&state` is **`[AllowAnonymous]`**. It looks up the
   `state` (single use: delete it on read), rejects the request if it is missing or expired,
   exchanges the `code`, stores the token for the user bound to that state, then redirects the
   browser (302) to `/settings?gmail=connected` (or `?gmail=error&reason=…`).

`state` is the only thing protecting the callback, so it must be unguessable, single-use and
short-lived. Store it in the DB table `OAuthStates` (an in-memory cache would also work for a
single API instance, but it would not survive a restart during the consent screen).

## Phases

### Phase 0 — Google Cloud setup (manual, once)

1. Create project `nInvoices` → enable **Gmail API**.
2. Google Auth Platform → Branding (name, support email) → Audience: **External**, add your own
   address as test user → Data Access: add scope `gmail.compose`.
3. Clients → Create client → **Web application**. Authorized redirect URIs:
   - `http://localhost:5297/api/gmail/oauth/callback` (dev, `dotnet run`)
   - `https://<prod-domain>/api/gmail/oauth/callback` (prod, via nginx `/api/`)
4. Put the client ID and secret in configuration, never in the repo:
   - dev: `dotnet user-secrets set "Gmail:ClientId" …` / `"Gmail:ClientSecret"` in `src/nInvoices.Api`
   - prod: `.env` → `Gmail__ClientId`, `Gmail__ClientSecret`, `Gmail__RedirectUri` passed to the
     `api` service in `docker/docker-compose.prod.yml`
5. After Phase 3 works end-to-end: Audience → **Publish app** (do not submit for verification).

### Phase 1 — Domain & persistence (Core + Infrastructure)

**Customer** (`Core/Entities/Customer.cs`): add
- `string? Email`: default To address
- `string? CcEmails`: comma-separated, optional

Add both to `CreateCustomerDto`/`UpdateCustomerDto`/`CustomerDto`. Validate with FluentValidation
(`EmailAddress()`, each CC entry). Add them to `CustomerForm.vue` and `DataExportDto`
(import/export).

**New entities** (`Core/Entities/`, each deriving from `EntityBase`, with an EF config in
`Infrastructure/Data/Configurations/`):

- `EmailTemplate`
  - `long? CustomerId` (null = global default), `string Name`, `string Subject`,
    `string Body` (HTML), `bool IsDefault`
  - Resolution order when creating a draft: the customer's default → the global default →
    a built-in fallback (a constant in Application).
- `GmailConnection` (one row per Keycloak user)
  - `string UserId` (unique), `string EmailAddress`, `string EncryptedRefreshToken`,
    `string Scopes`, `DateTime ConnectedAt`, `DateTime? LastUsedAt`
- `OAuthState`
  - `string State` (PK/unique), `string UserId`, `DateTime ExpiresAt`
- `InvoiceEmail` (log of drafts per invoice, so recreating a draft keeps history)
  - `long InvoiceId`, `string To`, `string? Cc`, `string Subject`, `string GmailDraftId`,
    `string GmailMessageId`, `string RfcMessageId` (the `Message-ID` header we set),
    `DateTime CreatedAt`
  - Navigation: `Invoice.Emails`

Add the `DbSet`s and **one migration**. It must work on SQLite and PostgreSQL (see CLAUDE.md), so
check the generated SQL for both. Remember `docker/migrations-postgres` if the prod DB is
migrated by script.

### Phase 2 — Application layer

**Interfaces** (`Application/Services/`, or `Core/Interfaces/` next to `IPdfExportService`):

```csharp
public interface IGmailAuthService
{
    string BuildAuthorizationUrl(string state);
    Task<GmailConnectionResult> ExchangeCodeAsync(string code, CancellationToken ct); // tokens + email address
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}

public interface IGmailDraftService
{
    Task<GmailDraftResult> CreateDraftAsync(string refreshToken, MimeMessageSpec message, CancellationToken ct);
}

public interface ISecretProtector   // wraps IDataProtector, purpose "nInvoices.Gmail.RefreshToken"
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
```

`MimeMessageSpec` is a plain record (From, To, Cc, Subject, HtmlBody, attachments as
`(fileName, contentType, byte[])`), so Application does not depend on MimeKit.

**Features** (`Features/Gmail/`, `Features/EmailTemplates/`, `Features/Invoices/`):

- `StartGmailConnectCommand` → creates an `OAuthState`, returns the auth URL.
- `CompleteGmailConnectCommand(code, state)` → validates and consumes the state, exchanges the
  code, upserts `GmailConnection` (encrypted token + the Gmail address from the token's
  `id_token`/`users.getProfile`).
- `DisconnectGmailCommand` → revokes the token at Google (best effort) and deletes the row.
- `GetGmailStatusQuery` → `{ connected, emailAddress, connectedAt }`.
- EmailTemplates CRUD + `SetDefault`, following the InvoiceTemplates pattern. Validation reuses
  `ITemplateRenderer.ValidateAsync` on both Subject and Body.
- `PreviewInvoiceEmailQuery(invoiceId, templateId?)` → rendered `{ to, cc, subject, body,
  attachments[] }` for the dialog.
- `CreateInvoiceEmailDraftCommand(invoiceId, to, cc, subject, body, includeMonthlyReport)`:
  1. Load the invoice + customer and the user's `GmailConnection` (400 "Gmail not connected" if missing).
  2. Allow only `Finalized`/`Sent` invoices (a draft invoice can still change).
  3. Build the attachments with the existing services:
     - invoice PDF: `IPdfExportService.GenerateInvoicePdf`, the same code path as `GET /api/invoices/{id}/pdf`
     - monthly report: `IMonthlyReportGenerationService` + `IHtmlToPdfConverter` (move the
       inline code from `InvoicesController.ExportMonthlyReportPdf` into a service method so
       both use it)
  4. From = `GmailConnection.EmailAddress`; set our own `Message-ID`.
  5. Call `IGmailDraftService.CreateDraftAsync`, then save an `InvoiceEmail` row and return
     `{ draftId, messageId, gmailUrl }`, with
     `gmailUrl = https://mail.google.com/mail/u/0/#drafts?compose={messageId}`.
  6. If Google returns `invalid_grant` (token revoked or expired): delete the connection and return
     a specific error so the UI shows "Reconnect Gmail".

**Email template model**: `BuildTemplateModel` in `InvoiceGenerationService` needs the
generation-time `GenerateInvoiceDto`, so add a small `EmailTemplateModel` built from the
persisted `Invoice` + `Customer`:
`invoiceNumber, issueDate, dueDate, currency, total, subtotal, totalTax, monthNumber,
monthDescription, year, customer { name, email, fiscalId }, sender { emailAddress }`.
The same helper functions (`FormatCurrency`, `FormatDate`, `LocalizeMonth`, …) are available
because rendering goes through `ScribanTemplateRenderer`.

Built-in fallback template (example):

```
Subject: Invoice {{ invoiceNumber }} — {{ customer.name }}
Body:    <p>Dear {{ customer.name }},</p>
         <p>please find attached invoice {{ invoiceNumber }} dated {{ FormatDate issueDate "dd/MM/yyyy" }}
         for {{ FormatCurrency total currency }}.</p>
         <p>Kind regards</p>
```

### Phase 3 — Infrastructure

`Infrastructure/Gmail/`:
- `GmailOptions` (`ClientId`, `ClientSecret`, `RedirectUri`), bound from section `Gmail`, and
  validated on start only when `Gmail:Enabled` is true. If it is not configured, the feature is
  hidden instead of crashing.
- `GmailAuthService`: `GoogleAuthorizationCodeFlow` with `Scopes = [gmail.compose]`. Build the
  URL with `access_type=offline` and `prompt=consent` (guarantees a refresh token on reconnect)
  and `login_hint` = the user's email.
- `GmailDraftService`: `UserCredential` from the decrypted refresh token → `GmailService` →
  `Users.Drafts.Create("me", new Draft { Message = new Message { Raw = base64url(mime) } })`.
  Build the MIME with MimeKit (`multipart/mixed`: html body + PDFs).
- `DataProtectionSecretProtector`.
- `AddGmail(IConfiguration)` extension, called from `Program.cs` like `AddPdfExport()`.

`Program.cs`:
```csharp
builder.Services.AddDataProtection()
    .SetApplicationName("nInvoices")
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"] ?? "keys"));
builder.Services.AddGmail(builder.Configuration);
```

Docker (`docker-compose.prod.yml` / `registry.yml`, `api` service):
- volume `./volumes/dataprotection:/app/keys`, env `DataProtection__KeysPath=/app/keys`
- env `Gmail__ClientId`, `Gmail__ClientSecret`, `Gmail__RedirectUri`, `Gmail__Enabled=true`
- add the variables to `.env.example` and a short section in `docker/PRODUCTION-DEPLOYMENT.md`
- check that nginx forwards `/api/gmail/oauth/callback` unchanged, including the query string
  (the `location /api/` block does)

### Phase 4 — API

`GmailController` (`api/gmail`, `[Authorize]`):
- `GET status`
- `POST connect` → `{ authorizationUrl }`
- `GET oauth/callback` → `[AllowAnonymous]`, 302 to the SPA. The SPA base URL comes from config
  (`Gmail:PostConnectRedirect`, e.g. `/settings`), never from a query parameter (open redirect).
- `DELETE connection`

`EmailTemplatesController` (`api/emailtemplates`): CRUD, `POST {id}/default`, `POST validate`.

`InvoicesController`:
- `GET {id}/email/preview?templateId=`
- `POST {id}/email/draft` → `{ draftId, gmailUrl }`
- `GET {id}/emails` → the draft history

### Phase 5 — Frontend (`src/nInvoices.Web`)

- `src/api/gmail.ts`, `src/api/emailTemplates.ts`, plus invoice email calls in `invoices.ts`;
  types in `types/index.ts`.
- **Settings.vue**: new `BasePanel` "Gmail". It shows the status ("Connected as
  alex…@gmail.com since …") with a Connect/Disconnect button. On return, it reads
  `?gmail=connected|error`, shows a toast and cleans the URL.
- **Email templates**: either a tab in the existing TemplateEditor view (reusing
  `TemplateCodeEditor`, `TemplatePreviewPane`, `TemplateVariablesPanel` with an email variables
  list) or a simpler dedicated page. Recommendation: reuse the editor, with a Subject input above
  the code editor.
- **CustomerForm.vue**: Email and CC fields; optionally a per-customer default email template.
- **InvoiceDetails.vue / useInvoiceActions.ts**: a new action `emailDraft` ("Create Gmail
  draft", icon `send`), available when the status is Finalized or Sent. It is the primary action
  for Finalized, and "Mark as sent" moves to the menu.
  - Dialog (`BaseDialog`): To, CC, template picker, editable Subject, a preview of the rendered
    body (iframe `srcdoc`, sandboxed), attachment checkboxes, and a "Create draft" button.
  - On success: a toast with an **Open in Gmail** link (`target="_blank"`), and the same prompt
    offers "Mark as sent".
  - An "Emails" section on the invoice page lists the `InvoiceEmail` history with Gmail links.
  - If Gmail is not connected: the button shows "Connect Gmail first" → link to Settings.

### Phase 6 — Tests

- **Core.Tests**: Customer email fields and the EmailTemplate invariants.
- **Application.Tests** (NUnit + Shouldly + Moq):
  - `CreateInvoiceEmailDraftCommandHandler`: not connected → error; draft invoice → error;
    happy path saves an `InvoiceEmail`; `invalid_grant` → the connection is removed.
  - `CompleteGmailConnectCommandHandler`: unknown, expired or reused state → rejected.
  - Template resolution order (customer → global → built-in).
  - Rendering the built-in template with a sample model.
- **Infrastructure.Tests**: the MIME builder (attachments, headers, UTF-8 subject), and a
  Data Protection round-trip with an ephemeral provider.
- **Manual end-to-end**: connect → create a draft → it appears in Gmail Drafts with the PDF →
  send → it is in Sent → reply → it arrives in the inbox → Mark as sent in the app.

## Delivery order

1. Phase 0 (Google setup) + Phase 1 (customer email fields only) → ship on their own.
2. Phases 1–4 for the Gmail connection only (connect/status/disconnect + Settings panel) → verify
   the refresh token survives an API restart and a container redeploy.
3. Email templates (entity, CRUD, editor).
4. Draft creation + invoice dialog + history.
5. Publish the consent screen (Phase 0 step 5), then deploy to prod with the volume and env vars.

## Risks & notes

- **Refresh token lost** (the user revoked access, changed their password, or the app stayed in Testing
  for more than 7 days): handled by `invalid_grant` → "Reconnect Gmail".
- **Data Protection keys lost** (volume deleted): the token cannot be decrypted → treat it like
  `invalid_grant`, delete the connection, reconnect. No data loss beyond that.
- **Deep link format** `#drafts?compose=<messageId>` is Gmail UI behaviour, not a documented
  API. If it ever breaks, fall back to `https://mail.google.com/mail/u/0/#drafts`.
- **Multi-account Gmail**: `/u/0/` opens the first signed-in Google account. If the user is
  signed in to several, add `?authuser=<email>` to the link.
- **Single user**: the app has no tenant separation (entities carry no owner). The Gmail
  connection is keyed by Keycloak `UserId` anyway, so it stays correct if more users are added.
- **Future**: with the extra scope `gmail.readonly`, the app could detect "sent" automatically
  by searching `rfc822msgid:<RfcMessageId> in:sent` and could surface replies in the same
  thread. That needs a new consent, so it is a separate step.

## Implementation notes (2026-09-25)

Differences from the plan above:

- **Email templates are per customer only** (no global template). The customer's active
  template is used; without one, the built-in `DefaultEmailTemplate` applies. They are edited
  in the existing template editor (customer › *Email templates* tab), which gained a Subject field.
- Email templates CRUD lives in `EmailTemplatesController` with repositories, like
  `MonthlyReportTemplatesController`; Gmail and invoice-email logic are MediatR features
  (`Features/Gmail`, `Features/InvoiceEmails`) plus `IInvoiceEmailComposer`.
- One abstraction, `IGmailClient` (Application), implemented by `GoogleGmailClient` (Infrastructure).
- Invoice status never changes automatically; the draft dialog offers "Mark as sent".
- Allowed for Finalized, Sent and Paid invoices (not Draft/Cancelled).
- Dev config: `Gmail:ClientId`/`ClientSecret` in user secrets; `RedirectUri` and
  `PostConnectRedirect` in `appsettings.Development.json`. Data Protection keys default to
  `src/nInvoices.Api/keys/` (gitignored).
- Migrations are not applied at startup: run `dotnet ef database update` locally; in
  production `deploy.sh --migrate` runs `docker/migrations-postgres/20260925_add-invoice-emails.sql`.
