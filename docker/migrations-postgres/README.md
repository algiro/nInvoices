# PostgreSQL migrations (hand-written)

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
  ssh he-it-tudes "docker exec -i ninvoices-postgres-prod \
    psql -v ON_ERROR_STOP=1 -U ninvoices_user -d ninvoices_db" < "$f"
done
```

Every file must be **idempotent** (`CREATE TABLE IF NOT EXISTS`,
`CREATE INDEX IF NOT EXISTS`, `... ON CONFLICT DO NOTHING`) so re-running a deploy
is safe. Each file also inserts its EF `MigrationId` into `__EFMigrationsHistory`
so the app's migration bookkeeping stays consistent.

## Naming

`<yyyyMMdd>_<slug>.sql`, matching the EF migration it mirrors where there is one.
