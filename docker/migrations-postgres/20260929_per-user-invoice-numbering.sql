-- nInvoices :: PostgreSQL migration
-- Mirrors EF migration 20260929111921_PerUserInvoiceNumbering
-- Invoice numbering belongs to the user (tenant): one counter and one optional number pattern
-- per user, shared by all of their customers. This undoes 20260929_per-customer-invoice-numbering
-- (already applied to production): each user's per-customer counters go back to one, taking the
-- highest so no number already handed out can be issued again, and a pattern set on the
-- customers moves to the user when they all use the same one.
-- Idempotent: safe to run repeatedly.

\set ON_ERROR_STOP on
\connect ninvoices_db

BEGIN;

ALTER TABLE "InvoiceSequence" ADD COLUMN IF NOT EXISTS "NumberFormat" varchar(100) NULL;

-- The per-customer structure exists only where the previous migration ran
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'InvoiceSequence' AND column_name = 'CustomerId') THEN

        ALTER TABLE "InvoiceSequence" DROP CONSTRAINT IF EXISTS "FK_InvoiceSequence_Customers_CustomerId";
        DROP INDEX IF EXISTS "IX_InvoiceSequence_OwnerId_CustomerId";
        DROP INDEX IF EXISTS "IX_InvoiceSequence_CustomerId";

        -- One row per user, holding the highest counter
        UPDATE "InvoiceSequence" s
        SET "CurrentValue" = m."MaxValue"
        FROM (SELECT "OwnerId", MAX("CurrentValue") AS "MaxValue" FROM "InvoiceSequence" GROUP BY "OwnerId") m
        WHERE s."OwnerId" = m."OwnerId";
        DELETE FROM "InvoiceSequence"
        WHERE "Id" NOT IN (SELECT MIN("Id") FROM "InvoiceSequence" GROUP BY "OwnerId");

        -- A pattern the customers all share becomes the user's
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = 'Customers' AND column_name = 'NumberFormat') THEN
            UPDATE "InvoiceSequence" s
            SET "NumberFormat" = f."Format"
            FROM (SELECT "OwnerId", MIN("NumberFormat") AS "Format"
                  FROM "Customers"
                  WHERE "NumberFormat" IS NOT NULL
                  GROUP BY "OwnerId"
                  HAVING COUNT(DISTINCT "NumberFormat") = 1) f
            WHERE s."OwnerId" = f."OwnerId";
        END IF;

        ALTER TABLE "InvoiceSequence" DROP COLUMN "CustomerId";
    END IF;
END $$;

ALTER TABLE "Customers" DROP COLUMN IF EXISTS "NumberFormat";

-- One sequence per user
DROP INDEX IF EXISTS "IX_InvoiceSequence_OwnerId";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_InvoiceSequence_OwnerId"
    ON "InvoiceSequence" ("OwnerId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260929111921_PerUserInvoiceNumbering', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
