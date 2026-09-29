-- nInvoices :: PostgreSQL migration
-- Mirrors EF migration 20260929093753_PerCustomerInvoiceNumbering
-- Invoice numbering is per customer: every customer gets its own sequence (and optionally its
-- own number pattern) instead of one counter shared by all of a user's customers.
-- Each existing customer starts at the user's current counter, so no number already handed out
-- can be issued again. Idempotent: safe to run repeatedly.
--
-- SUPERSEDED by 20260929_per-user-invoice-numbering.sql: numbering ended up per user, not per
-- customer. This file stays because it was already applied to production, but it does nothing
-- once "InvoiceSequence"."NumberFormat" exists. Deploys re-run every file, and running this one
-- again would split the user's counter across customers once more.

\set ON_ERROR_STOP on
\connect ninvoices_db

SELECT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = current_schema()
      AND table_name = 'InvoiceSequence' AND column_name = 'NumberFormat') AS superseded \gset
\if :superseded
    \echo skipped: superseded by 20260929_per-user-invoice-numbering.sql
\else

BEGIN;

ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "NumberFormat" varchar(100) NULL;

ALTER TABLE "InvoiceSequence" ADD COLUMN IF NOT EXISTS "CustomerId" bigint NOT NULL DEFAULT 0;

-- A user now has several rows, so the unique index on the owner has to go before the copy
DROP INDEX IF EXISTS "IX_InvoiceSequence_OwnerId";
CREATE INDEX IF NOT EXISTS "IX_InvoiceSequence_OwnerId" ON "InvoiceSequence" ("OwnerId");

-- Per-user rows (CustomerId = 0) become one row per customer of that user.
-- The ids are given explicitly: the table's id counter can be behind its rows (the original
-- row was inserted with Id = 1 by hand), so letting it assign them collides with existing ids.
INSERT INTO "InvoiceSequence" ("Id", "OwnerId", "CustomerId", "CurrentValue", "CreatedAt")
SELECT (SELECT COALESCE(MAX("Id"), 0) FROM "InvoiceSequence") + ROW_NUMBER() OVER (ORDER BY c."Id"),
       c."OwnerId", c."Id", s."CurrentValue", s."CreatedAt"
FROM "Customers" c
JOIN "InvoiceSequence" s ON s."OwnerId" = c."OwnerId" AND s."CustomerId" = 0
WHERE NOT EXISTS (
    SELECT 1 FROM "InvoiceSequence" x
    WHERE x."OwnerId" = c."OwnerId" AND x."CustomerId" = c."Id");
DELETE FROM "InvoiceSequence" WHERE "CustomerId" = 0;

-- Bring the id counter past the rows, so the app can add sequences for new customers
SELECT setval(
    pg_get_serial_sequence('"InvoiceSequence"', 'Id'),
    COALESCE((SELECT MAX("Id") FROM "InvoiceSequence"), 0) + 1,
    false);

CREATE INDEX IF NOT EXISTS "IX_InvoiceSequence_CustomerId" ON "InvoiceSequence" ("CustomerId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_InvoiceSequence_OwnerId_CustomerId"
    ON "InvoiceSequence" ("OwnerId", "CustomerId");

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_InvoiceSequence_Customers_CustomerId') THEN
        ALTER TABLE "InvoiceSequence"
            ADD CONSTRAINT "FK_InvoiceSequence_Customers_CustomerId"
            FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260929093753_PerCustomerInvoiceNumbering', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

\endif
