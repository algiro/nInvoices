# Contributing to nInvoices

Thanks for wanting to help. nInvoices is source-available under the
[PolyForm Shield License 1.0.0](LICENSE.md): free to use and self-host, and contributions are
accepted under that same license.

## The most useful things right now

1. **Test the e-invoicing against the real services.** Spain's AEAT and FACe test environments, the
   Italian SDI, a German recipient's portal. Report what they answer, in an issue. See the *Status*
   notes in the [README](README.md#e-invoicing-by-country).
2. **Tell us a country's rules.** Open an issue naming the country, the regime and the official
   specification. "This is what a freelancer here must do, and by when" is enough.
3. **Review a country's implementation** if you know its rules, especially the points listed as unverified.
4. **Translate.** A language is two JSON files, see [`Docs/LOCALIZATION.md`](Docs/LOCALIZATION.md).
5. **Bug reports and ideas.** Say what you did, what you expected, and what happened.

## Before you write code

Please **open an issue first** so we can agree on the approach. This is most important for a new
country, a new dependency, or anything that changes the data model.

## Set up

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and
[Node.js 20+](https://nodejs.org/). Follow the [quick start](README.md#quick-start) (SQLite, no login).
Docker is only needed for the PostgreSQL tests and the full stack.

## Before you open a pull request

```bash
dotnet build          # warnings are errors
dotnet test           # all test projects
cd src/nInvoices.Web && npm run check   # types up to date, typecheck, lint, tests
```

- **Tests** are NUnit + Shouldly, named `Method_Scenario_ExpectedOutcome`. Add one for what you change.
- **A model change needs two EF migrations**, one for SQLite and one for PostgreSQL;
  `MigrationsUpToDateTests` fails otherwise.
- **A DTO change** needs the OpenAPI snapshot and the generated frontend types refreshed.
- **Texts are never inline**: UI strings go in `src/nInvoices.Web/src/locales/<lang>.json`, API messages in
  `Application/Localization/Messages/<lang>.json`.
- The architecture rules (layers, no repositories in controllers, no country names outside their project)
  are enforced by `ArchitectureTests`.

`CLAUDE.md` has the full commands and conventions, and `.github/instructions/` the code style.

## Adding a country

A country is one project, `src/nInvoices.Countries.<Name>`. The steps and the hooks to implement are in the
README's [help wanted](README.md#help-wanted-more-countries) section. Validate against the official schemas
and test vectors, and say plainly in the docs what has and hasn't been verified.

## Pull requests

Keep them focused: one change, with its tests. Describe what changed and why. By submitting a pull
request you agree it is licensed under the project's license.
