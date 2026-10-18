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

**Backend.** Inject `IAppText` and use `Translate("errors.unexpected")` (current UI language) with keys in
`Localization/Messages/en.json`; placeholders are named: `Translate("preferences.unsupportedLanguage", ("language", x))`.
Messages that are not yet keys are returned in English; move them to keys as they are touched.
