-- nInvoices :: PostgreSQL migration
-- Mirrors EF migration 20260926164022_AddOwnerId
-- Multi-user support: every user-data table gets an "OwnerId" (the user's "sub" claim) and
-- the app only shows each user their own rows. Existing rows get an empty owner; set
-- MultiUser:LegacyOwnerId (env MultiUser__LegacyOwnerId) so the API assigns them at startup.
-- Idempotent: safe to run repeatedly.

\set ON_ERROR_STOP on
\connect ninvoices_db

BEGIN;

ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "EmailTemplates" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "Expenses" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "HolidayCalendars" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "HolidayRules" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "ImageAssets" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "InvoiceEmails" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "InvoiceSequence" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "InvoiceTaxLines" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "InvoiceTemplates" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "MonthlyReportTemplates" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "Rates" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "Taxes" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "WorkDays" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';
ALTER TABLE "WorkDayProjects" ADD COLUMN IF NOT EXISTS "OwnerId" varchar(255) NOT NULL DEFAULT '';

CREATE INDEX IF NOT EXISTS "IX_Customers_OwnerId" ON "Customers" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_EmailTemplates_OwnerId" ON "EmailTemplates" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_Expenses_OwnerId" ON "Expenses" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_HolidayCalendars_OwnerId" ON "HolidayCalendars" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_HolidayRules_OwnerId" ON "HolidayRules" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_ImageAssets_OwnerId" ON "ImageAssets" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_Invoices_OwnerId" ON "Invoices" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_InvoiceEmails_OwnerId" ON "InvoiceEmails" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_InvoiceTaxLines_OwnerId" ON "InvoiceTaxLines" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_InvoiceTemplates_OwnerId" ON "InvoiceTemplates" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_MonthlyReportTemplates_OwnerId" ON "MonthlyReportTemplates" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_Projects_OwnerId" ON "Projects" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_Rates_OwnerId" ON "Rates" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_Taxes_OwnerId" ON "Taxes" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_WorkDays_OwnerId" ON "WorkDays" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_WorkDayProjects_OwnerId" ON "WorkDayProjects" ("OwnerId");

-- One holiday calendar per country, per user (was: per country)
DROP INDEX IF EXISTS "IX_HolidayCalendars_CountryCode";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_HolidayCalendars_OwnerId_CountryCode"
    ON "HolidayCalendars" ("OwnerId", "CountryCode");

-- One invoice sequence per user. The seeded row is kept if it was ever used (it holds the
-- existing numbering and goes to the legacy owner); an untouched one is dropped.
DELETE FROM "InvoiceSequence"
    WHERE "Id" = 1 AND "OwnerId" = '' AND "CurrentValue" = 1 AND "UpdatedAt" IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_InvoiceSequence_OwnerId"
    ON "InvoiceSequence" ("OwnerId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260926164022_AddOwnerId', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
