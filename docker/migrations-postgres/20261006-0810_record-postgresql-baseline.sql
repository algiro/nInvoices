-- nInvoices :: PostgreSQL migration — THE LAST ONE
-- Records the EF migration 20261006081011_PostgreSqlBaseline as applied.
--
-- From here on PostgreSQL has real EF Core migrations (src/nInvoices.Infrastructure.Migrations.PostgreSql),
-- applied by the API at startup. Their baseline creates the schema the scripts in this folder lead to
-- (20261006-0753_align-with-ef-model.sql made production exactly the EF model; PostgreSqlMigrationScriptsTests
-- proves both). A database that already has that schema must not run the baseline again, so it is recorded
-- here. This folder is now frozen: it only brings a database restored from a backup older than 2026-10-06
-- up to the baseline (deploy.sh --migrate, then start the API). New schema changes are EF migrations.
--
-- Idempotent: safe to run repeatedly.

\set ON_ERROR_STOP on
\connect ninvoices_db

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261006081011_PostgreSqlBaseline', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;
