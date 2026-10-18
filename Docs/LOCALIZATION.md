# Localization

nInvoices has two separate languages:

| | Chosen by | Applies to | Where |
|---|---|---|---|
| **App language** | each user, in Settings › General | screens, toasts, API error messages | `UserPreferences.UiLanguage` |
| **Document language** | each customer (`Customer.Locale`) | invoices, monthly reports, emails | `Application/Localization/<locale>.json` (month and day names) |

Changing the app language never changes how an invoice is rendered.

## How the app language works

- Every user starts in **English** (`UserPreferences.DefaultUiLanguage`). A user without a `UserPreferences` row
  has the defaults; the row is created the first time they pick a language.
- The choice is stored on the server (`GET`/`PUT /api/preferences`), so it follows the user to every device,
  and it is part of the settings backup (`UserSettingsExportDto.Preferences`; an import keeps a language the user
  already set).
- The web app (`src/i18n/index.ts`, vue-i18n) starts in the language this browser used last, then applies the
  user's preference once signed in (`MainLayout` → `stores/preferences.ts`). Switching in Settings changes every
  screen at once and saves; a failed save switches back.
- Every API request carries the app language as `Accept-Language`. `UiLanguageMiddleware` sets the request's
  `CurrentUICulture` (never `CurrentCulture`, so numbers and dates on the server stay as they were) to the best
  language the API has texts for. `IAppText` and FluentValidation's built-in messages follow it.
- Dates, numbers and money on screen are formatted in the app language (`utils/format.ts`, `currentLanguage()`).

## Adding a language

Add two files, named with the language tag (`de`, `fr`, `pt-BR`…):

1. `src/nInvoices.Web/src/locales/<lang>.json`: copy `en.json` and translate the values.
2. `src/nInvoices.Application/Localization/Messages/<lang>.json`: copy `en.json` and translate the values.

Nothing else to wire up: both sides discover their files (`import.meta.glob` in the web app, embedded resources in
the API), the language picker lists the new language under its own name, and the API accepts it. Then run the tests:

```bash
dotnet test --filter "FullyQualifiedName~AppText"      # the API's files; both sides list the same languages
cd src/nInvoices.Web && npm run test                   # the web files: syntax, keys, placeholders, plural forms
```

A partial translation is fine: a missing text shows the English one. The tests fail when a translation has a key
English lacks, loses a `{placeholder}`, or has a different number of plural forms.

## Writing texts

**Frontend.** Never put user-facing English in a component: add a key to `src/locales/en.json` (grouped by screen,
e.g. `invoices.details.title`) and use it.

```vue
<BaseButton>{{ t('common.save') }}</BaseButton>
<BasePanel :title="t('invoices.details.title', { number: invoice.invoiceNumber })" />
<i18n-t keypath="invoices.details.noEmails" tag="p"><template #create><button>…</button></template></i18n-t>
```

```ts
import { useI18n } from 'vue-i18n'
const { t } = useI18n()                      // in components
import { t } from '@/i18n'                   // in stores, composables, utils
```

- Placeholders are named: `"Invoice {number}"` → `t('…', { number })`.
- Plurals are separated by `|` and chosen by a count: `"No invoices | {n} invoice | {n} invoices"` → `t('invoices.count', count)`.
- `@`, `|`, `{` and `}` are syntax; write a literal one as `{'@'}`.
- A label kept in data (a menu, a route title) is stored as a key (type `MessageKey`) and translated where it is
  shown, so it follows a language change. So is anything computed: put `t()` inside a `computed`, not in a constant.
- Keys built at run time (``t(`invoices.bulk.${key}.title`)``) are not checked by the tests: keep them rare.
- A country's screens use keys under `countries.<code>.*`.

**Backend.** Every message the API shows the user is a key in `src/nInvoices.Application/Localization/Messages/en.json`
(Italian in `it.json`), translated into the request's UI language by `IAppText` (registered as a singleton; inject it).
Keys are grouped by kind: `validation.*` (FluentValidation rules), `errors.*` (a `DomainException` /
`InvoiceEmailException`: a rule refused), `notFound.*` (`NotFoundException`), `compliance.*` (the per-field
`ComplianceIssue`s of country rules and e-invoice formats: `compliance.<country or format>.<name>`, with what several
share under `compliance.common.*` and `compliance.einvoice.*`; the labels, help and options of their fields under
`compliance.<country>.fields` / `customerFields` / `taxFields`), `taxPrediction.*` (the tax regimes' settings, the
estimate's periods, forms, lines and warnings), `countries.<CODE>` (a country's name). Placeholders are named, values
passed as tuples:

```csharp
// a validator: its constructor takes IAppText, and passes it on to the validators it nests
RuleFor(x => x.Name).NotEmpty().WithText(text, "validation.customer.nameRequired")
    .MaximumLength(200).WithText(text, "validation.customer.nameTooLong", ("max", 200));
RuleFor(x => x.ContentType).Must(Allowed.Contains)
    .WithMessage(x => text.Translate("validation.image.typeNotAllowed", ("type", x.ContentType), ("allowed", list)));

// a handler or service
?? throw new NotFoundException(_text.Translate("notFound.invoice", ("id", request.InvoiceId)));
throw new DomainException(_text.Translate("errors.project.nameExists", ("name", name)));
```

- `WithText` (`Validation/LocalizedRuleExtensions`) translates when the rule fails, so the message follows the
  request's language even though validators are built once. Template syntax rules take the key of their
  message: `MustBeValidTemplate(renderer, text, "validation.template.subjectSyntaxError")`.
- Pure code takes the text as a parameter instead of injecting it (`InvoiceCalculator.EnsureBillable(dto, rates, text)`,
  `new DayRates(defaultRate, dayRates, text)`, the tax calculators, `VerifactuChainVerifier.Verify(text, chain)`).
- What a module describes is built on each read, so it follows the request's language: `Fields`, `Regimes` and
  `DisplayName` are `=>` properties that translate, never `{ get; } =` lists made once. Proper names stay as they are
  in every language (Codice fiscale, Kirchensteuer, Modelo 303, the FatturaPA's codes).
- Pass dates in an invariant form (`yyyy-MM-dd`) and ids, numbers, names as they are; a value is formatted with the
  UI culture.
- Tests construct classes with `TestText.Instance` (the shipped files) and run in the machine's UI language;
  English where it has no file. `AppTextTests` fails when the code uses a key `en.json` lacks, when a translation
  has a key English lacks, or when it loses a placeholder. `CountryTextsTests` reads every country's fields, regimes and
  a full estimate in each language and fails when a text comes out as its key (keys built at run time, such as a
  month's, escape `AppTextTests`).

- `Core` takes no services, so it can't translate: what it throws carries the key and values
  (`throw new ArgumentException("Street cannot be empty", nameof(street)).WithText("errors.input.street")`), its own
  message staying English for the logs, and the API translates when it answers (`IAppText.MessageOf(exception)`, used
  by `ApiExceptionFilter` and the import's per-item errors). A value that is a `UserText` is translated too (the
  lifecycle refusal names its action and reason that way); `InvoiceLifecycle.WhyNot` gives the reason's key
  (`errors.lifecycle.*`) with its English.

Still English: what comes from outside
(Scriban's syntax errors, the answers of FACe and the Tax Agency) and what runs without a request (the Verifactu
submitter's notes); Infrastructure's internal checks (tax handlers, PDF export guards that the request handlers already
check); and the signing-certificate checks of the Spanish signers.
