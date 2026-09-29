-- nInvoices :: PostgreSQL migration
-- Mirrors EF migration 20260929101858_AddRateAndHoursToInvoice
-- An invoice remembers the rate it was billed with (so regenerating it uses the same one) and,
-- for a one-time invoice with an hourly rate, the hours billed.
-- Idempotent: safe to run repeatedly.

\set ON_ERROR_STOP on
\connect ninvoices_db

BEGIN;

ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "RateId" bigint NULL;
ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "Hours" numeric(9,2) NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260929101858_AddRateAndHoursToInvoice', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
