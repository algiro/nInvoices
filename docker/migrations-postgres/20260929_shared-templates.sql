-- nInvoices :: PostgreSQL migration
-- Mirrors EF migration 20260929123431_SharedTemplates
-- Templates (invoice, monthly report and email) can be shared by all of a user's customers: a
-- template with no customer (NULL "CustomerId") applies to every customer that has no active
-- template of its own. A user keeps at most one active shared invoice template per invoice type.
-- Idempotent: safe to run repeatedly.

\set ON_ERROR_STOP on
\connect ninvoices_db

BEGIN;

ALTER TABLE "InvoiceTemplates" ALTER COLUMN "CustomerId" DROP NOT NULL;
ALTER TABLE "MonthlyReportTemplates" ALTER COLUMN "CustomerId" DROP NOT NULL;
ALTER TABLE "EmailTemplates" ALTER COLUMN "CustomerId" DROP NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_InvoiceTemplates_Shared_OwnerId_InvoiceType"
    ON "InvoiceTemplates" ("OwnerId", "InvoiceType")
    WHERE "IsActive" AND "CustomerId" IS NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260929123431_SharedTemplates', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
