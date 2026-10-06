-- nInvoices :: PostgreSQL migration
-- Mirrors EF migration 20261006075339_AddImageAssetAliasPerUser, and aligns the production schema with
-- the EF model once and for all.
--
-- Production started from a hand-written base schema that drifted from the model (see
-- tests/nInvoices.Infrastructure.Tests/PostgreSql: production-schema.sql is pg_dump of production, and
-- PostgreSqlMigrationScriptsTests checks that production + these scripts = the schema of the model):
--   * an image alias was unique across ALL users (IX_ImageAssets_Alias): a second user could not have a
--     "logo". Now unique per user (IX_ImageAssets_OwnerId_Alias), like the model
--   * no database-level "one active invoice template per customer and type" (partial unique index)
--   * Invoices.InvoiceNumber was varchar(50) instead of varchar(100)
--   * integer ids and foreign keys in 6 tables (the model uses bigint), serial instead of identity
--   * PostgreSQL's default constraint names instead of EF's, other indexes, leftover column defaults
--   * an obsolete, empty table: InvoiceLines
-- After it, production has exactly the schema EF builds, so EF migrations can take over (point 6.A).
--
-- Idempotent and a no-op on a schema built from the model: every step checks the current state first.

\set ON_ERROR_STOP on
\connect ninvoices_db

BEGIN;

-- ---- helpers (temporary: they live for this session only) -------------------------------------

CREATE FUNCTION pg_temp.rename_constraint(t text, old_name text, new_name text) RETURNS void AS $$
BEGIN
    IF EXISTS (SELECT FROM pg_constraint WHERE conrelid = to_regclass(format('public.%I', t)) AND conname = old_name) THEN
        EXECUTE format('ALTER TABLE public.%I RENAME CONSTRAINT %I TO %I', t, old_name, new_name);
    END IF;
END $$ LANGUAGE plpgsql;

CREATE FUNCTION pg_temp.set_type(t text, c text, data_type text, new_type text) RETURNS void AS $$
BEGIN
    -- data_type as information_schema names it ('integer', 'character varying'...)
    IF EXISTS (SELECT FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = t AND column_name = c AND columns.data_type = set_type.data_type) THEN
        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I TYPE %s', t, c, new_type);
    END IF;
END $$ LANGUAGE plpgsql;

-- ---- the obsolete table -----------------------------------------------------------------------

DROP TABLE IF EXISTS "InvoiceLines";

-- ---- EF's constraint names (renaming a primary key renames its index too) --------------------

SELECT pg_temp.rename_constraint('Customers', 'Customers_pkey', 'PK_Customers');
SELECT pg_temp.rename_constraint('EmailTemplates', 'EmailTemplates_pkey', 'PK_EmailTemplates');
SELECT pg_temp.rename_constraint('Expenses', 'Expenses_CustomerId_fkey', 'FK_Expenses_Customers_CustomerId');
SELECT pg_temp.rename_constraint('Expenses', 'Expenses_InvoiceId_fkey', 'FK_Expenses_Invoices_InvoiceId');
SELECT pg_temp.rename_constraint('Expenses', 'Expenses_pkey', 'PK_Expenses');
SELECT pg_temp.rename_constraint('GmailConnections', 'GmailConnections_pkey', 'PK_GmailConnections');
SELECT pg_temp.rename_constraint('HolidayCalendars', 'HolidayCalendars_pkey', 'PK_HolidayCalendars');
SELECT pg_temp.rename_constraint('HolidayRules', 'HolidayRules_pkey', 'PK_HolidayRules');
SELECT pg_temp.rename_constraint('ImageAssets', 'ImageAssets_pkey', 'PK_ImageAssets');
SELECT pg_temp.rename_constraint('InvoiceEmails', 'InvoiceEmails_pkey', 'PK_InvoiceEmails');
SELECT pg_temp.rename_constraint('InvoiceSequence', 'InvoiceSequence_pkey', 'PK_InvoiceSequence');
SELECT pg_temp.rename_constraint('InvoiceTaxLines', 'InvoiceTaxLines_InvoiceId_fkey', 'FK_InvoiceTaxLines_Invoices_InvoiceId');
SELECT pg_temp.rename_constraint('InvoiceTaxLines', 'InvoiceTaxLines_pkey', 'PK_InvoiceTaxLines');
SELECT pg_temp.rename_constraint('InvoiceTemplates', 'InvoiceTemplates_pkey', 'PK_InvoiceTemplates');
SELECT pg_temp.rename_constraint('Invoices', 'Invoices_pkey', 'PK_Invoices');
SELECT pg_temp.rename_constraint('MonthlyReportTemplates', 'MonthlyReportTemplates_pkey', 'PK_MonthlyReportTemplates');
SELECT pg_temp.rename_constraint('OAuthStates', 'OAuthStates_pkey', 'PK_OAuthStates');
SELECT pg_temp.rename_constraint('Projects', 'Projects_pkey', 'PK_Projects');
SELECT pg_temp.rename_constraint('Rates', 'Rates_CustomerId_fkey', 'FK_Rates_Customers_CustomerId');
SELECT pg_temp.rename_constraint('Rates', 'Rates_pkey', 'PK_Rates');
SELECT pg_temp.rename_constraint('Taxes', 'Taxes_AppliedToTaxId_fkey', 'FK_Taxes_Taxes_AppliedToTaxId');
SELECT pg_temp.rename_constraint('Taxes', 'Taxes_CustomerId_fkey', 'FK_Taxes_Customers_CustomerId');
SELECT pg_temp.rename_constraint('Taxes', 'Taxes_pkey', 'PK_Taxes');
SELECT pg_temp.rename_constraint('WorkDayProjects', 'WorkDayProjects_pkey', 'PK_WorkDayProjects');
SELECT pg_temp.rename_constraint('WorkDays', 'WorkDays_CustomerId_fkey', 'FK_WorkDays_Customers_CustomerId');
SELECT pg_temp.rename_constraint('WorkDays', 'WorkDays_pkey', 'PK_WorkDays');

-- ---- column types --------------------------------------------------------------------------------

-- bigint ids and foreign keys, as the model (long). The foreign keys pointing at them follow
SELECT pg_temp.set_type('Customers', 'Id', 'integer', 'bigint');
SELECT pg_temp.set_type('ImageAssets', 'Id', 'integer', 'bigint');
SELECT pg_temp.set_type('Invoices', 'Id', 'integer', 'bigint');
SELECT pg_temp.set_type('Invoices', 'CustomerId', 'integer', 'bigint');
SELECT pg_temp.set_type('Invoices', 'MonthlyReportTemplateId', 'integer', 'bigint');
SELECT pg_temp.set_type('InvoiceTemplates', 'Id', 'integer', 'bigint');
SELECT pg_temp.set_type('InvoiceTemplates', 'CustomerId', 'integer', 'bigint');
SELECT pg_temp.set_type('MonthlyReportTemplates', 'Id', 'integer', 'bigint');
SELECT pg_temp.set_type('MonthlyReportTemplates', 'CustomerId', 'integer', 'bigint');
SELECT pg_temp.set_type('WorkDays', 'Id', 'integer', 'bigint');
SELECT pg_temp.set_type('WorkDays', 'CustomerId', 'integer', 'bigint');

SELECT pg_temp.set_type('Invoices', 'InvoiceNumber', 'character varying', 'character varying(100)');
SELECT pg_temp.set_type('Invoices', 'Status', 'character varying', 'text');
SELECT pg_temp.set_type('Invoices', 'Type', 'character varying', 'text');
SELECT pg_temp.set_type('InvoiceTemplates', 'InvoiceType', 'character varying', 'text');

-- ---- identity ids instead of serial, continuing after the highest id ever handed out -------------

DO $$
DECLARE
    t text;
    old_sequence text;
    last bigint;
BEGIN
    FOR t IN
        SELECT table_name FROM information_schema.columns
        WHERE table_schema = 'public' AND column_name = 'Id' AND is_identity = 'NO' AND column_default LIKE 'nextval(%'
    LOOP
        old_sequence := pg_get_serial_sequence(format('public.%I', t), 'Id');
        EXECUTE format('SELECT GREATEST(COALESCE(max("Id"), 0), %s) FROM public.%I',
            CASE WHEN old_sequence IS NULL THEN '0' ELSE format('(SELECT last_value FROM %s)', old_sequence) END, t)
            INTO last;

        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN "Id" DROP DEFAULT', t);
        IF old_sequence IS NOT NULL THEN
            EXECUTE format('DROP SEQUENCE %s', old_sequence);
        END IF;
        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY', t);
        IF last > 0 THEN
            PERFORM setval(pg_get_serial_sequence(format('public.%I', t), 'Id'), last);
        END IF;
    END LOOP;
END $$;

-- ---- column defaults: only the model's (the app always writes the others) -----------------------

DO $$
DECLARE
    c record;
BEGIN
    -- OwnerId '' was only needed to add the column to tables that had rows
    FOR c IN
        SELECT table_name FROM information_schema.columns
        WHERE table_schema = 'public' AND column_name = 'OwnerId' AND column_default IS NOT NULL
    LOOP
        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN "OwnerId" DROP DEFAULT', c.table_name);
    END LOOP;
END $$;

ALTER TABLE "Customers" ALTER COLUMN "CreatedAt" DROP DEFAULT, ALTER COLUMN "Locale" DROP DEFAULT;
ALTER TABLE "ImageAssets" ALTER COLUMN "CreatedAt" DROP DEFAULT, ALTER COLUMN "UpdatedAt" DROP DEFAULT;
ALTER TABLE "InvoiceSequence" ALTER COLUMN "CreatedAt" DROP DEFAULT, ALTER COLUMN "CurrentValue" DROP DEFAULT;
ALTER TABLE "InvoiceTaxLines" ALTER COLUMN "Order" DROP DEFAULT;
ALTER TABLE "InvoiceTemplates" ALTER COLUMN "CreatedAt" DROP DEFAULT, ALTER COLUMN "IsActive" SET DEFAULT true;
ALTER TABLE "Invoices" ALTER COLUMN "CreatedAt" DROP DEFAULT, ALTER COLUMN "Status" SET DEFAULT 'Draft';
ALTER TABLE "MonthlyReportTemplates" ALTER COLUMN "CreatedAt" DROP DEFAULT, ALTER COLUMN "IsActive" DROP DEFAULT;
ALTER TABLE "WorkDays" ALTER COLUMN "CreatedAt" DROP DEFAULT, ALTER COLUMN "DayType" DROP DEFAULT;

-- ---- indexes: the model's ------------------------------------------------------------------------

DROP INDEX IF EXISTS "IX_ImageAssets_Alias";
DROP INDEX IF EXISTS "IX_InvoiceTemplates_CustomerId";
DROP INDEX IF EXISTS "IX_Invoices_CustomerId";
DROP INDEX IF EXISTS "IX_Invoices_InvoiceNumber";
DROP INDEX IF EXISTS "IX_MonthlyReportTemplates_CustomerId";
-- A unique index rather than a unique constraint, as EF creates it
ALTER TABLE "WorkDays" DROP CONSTRAINT IF EXISTS "WorkDays_CustomerId_Date_key";

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ImageAssets_OwnerId_Alias" ON "ImageAssets" ("OwnerId", "Alias");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_InvoiceTemplates_CustomerId_InvoiceType_IsActive"
    ON "InvoiceTemplates" ("CustomerId", "InvoiceType", "IsActive") WHERE "IsActive";
CREATE INDEX IF NOT EXISTS "IX_Invoices_CustomerId_Year_Month" ON "Invoices" ("CustomerId", "Year", "Month");
CREATE INDEX IF NOT EXISTS "IX_Invoices_IssueDate" ON "Invoices" ("IssueDate");
CREATE INDEX IF NOT EXISTS "IX_Invoices_Status" ON "Invoices" ("Status");
CREATE INDEX IF NOT EXISTS "IX_MonthlyReportTemplates_CustomerId_InvoiceType_IsActive"
    ON "MonthlyReportTemplates" ("CustomerId", "InvoiceType", "IsActive");
CREATE INDEX IF NOT EXISTS "IX_Taxes_AppliedToTaxId" ON "Taxes" ("AppliedToTaxId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkDays_CustomerId_Date" ON "WorkDays" ("CustomerId", "Date");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261006075339_AddImageAssetAliasPerUser', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
