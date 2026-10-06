# PostgreSQL migrations (hand-written) — FROZEN

> **Since 2026-10-06 PostgreSQL has real EF Core migrations** in
> `src/nInvoices.Infrastructure.Migrations.PostgreSql`, applied by the API at startup. Their baseline
> (`20261006081011_PostgreSqlBaseline`) is the schema these scripts lead to, and
> `20261006-0810_record-postgresql-baseline.sql` records it as applied. **Don't add scripts here**: a schema
> change is an EF migration for each provider (see CLAUDE.md). These files stay only to bring a database
> restored from a backup older than 2026-10-06 up to the baseline: `deploy.sh --migrate`, then start the API.

**Why this exists:** the EF Core migrations under
`src/nInvoices.Infrastructure/Data/Migrations` were scaffolded for **SQLite**
(column type literals `"TEXT"`/`"INTEGER"`, `Sqlite:Autoincrement`, and an
`InsertData` of a `DateTime` into a `TEXT` column in
`20260124211626_AddInvoiceSequence`). Because of that, `dotnet ef database update`
and `dotnet ef migrations script` **fail against Npgsql**
(`Unable to cast object of type 'System.DateTime' to type 'System.String'`).

Until the migration history is re-baselined provider-agnostically, the production
PostgreSQL schema is evolved with the idempotent `*.sql` files in this directory.

## Applying

`deploy.sh --migrate` runs every `*.sql` here, in filename order, against the
production database:

```bash
./deploy.sh --migrate            # as part of a deploy
# or standalone:
for f in migrations-postgres/*.sql; do
  ssh your-server "docker exec -i ninvoices-postgres-prod \
    psql -v ON_ERROR_STOP=1 -U ninvoices_user -d ninvoices_db" < "$f"
done
```

`00000000-0000_ef-migrations-history.sql` runs first and creates `__EFMigrationsHistory` when it's
missing: a database built from the EF model (fresh Docker install, Aspire) doesn't have it, and without it
every other script stopped at its history insert.

Every file must be **idempotent** (`CREATE TABLE IF NOT EXISTS`,
`CREATE INDEX IF NOT EXISTS`, `... ON CONFLICT DO NOTHING`) so re-running a deploy
is safe. Each file also inserts its EF `MigrationId` into `__EFMigrationsHistory`
so the app's migration bookkeeping stays consistent.

## Naming

`<yyyyMMdd>-<HHmm>_<slug>.sql`, with the date and time taken from the EF migration it
mirrors (`20261003094352_AddEInvoicing` → `20261003-0943_add-einvoicing.sql`).

Files run in **filename order**, so the time matters: with the date alone, files from the same
day sort by slug, and a file can run before the one that creates the tables it needs
(`add-einvoice-submissions` before `add-einvoicing`). Older files with the date alone
(`<yyyyMMdd>_<slug>.sql`) are already applied everywhere and keep their names.

## Tested

`tests/nInvoices.Infrastructure.Tests/PostgreSql` runs every script with `psql` on a real PostgreSQL
(Testcontainers, so Docker is needed; the tests are skipped without it) on a schema built from the EF
model: they must all succeed, change nothing, and stay idempotent when run twice. A script that adds
something the model doesn't have, or contradicts it, fails the build.

The same tests restore `production-schema.sql` (a `pg_dump --schema-only` of production, no data), run the
scripts on it as `deploy.sh --migrate` does, and require **exactly** the schema EF builds from the model.
`20261006-0753_align-with-ef-model.sql` is what makes that true: production's original base schema had
drifted (global image-alias index, missing partial unique index, `int` ids, other names). After a deploy that
changes the schema, refresh the fixture with a new dump.

```bash
dotnet test tests/nInvoices.Infrastructure.Tests --filter Category=PostgreSql
```
