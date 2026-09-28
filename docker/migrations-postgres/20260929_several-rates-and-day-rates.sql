-- nInvoices :: PostgreSQL migration
-- Mirrors EF migration 20260929135118_SeveralRatesAndDayRates
-- A customer can have several rates, including several of the same type (say two hourly rates),
-- and a rate can have a name. A worked day can be billed at a rate of its own ("RateId"), so one
-- invoice can mix hourly and daily rates. Before this, a unique index allowed one rate per type
-- per customer, so a second hourly rate failed with a server error.
-- Idempotent: safe to run repeatedly.

\set ON_ERROR_STOP on
\connect ninvoices_db

BEGIN;

ALTER TABLE "Rates" ADD COLUMN IF NOT EXISTS "Name" varchar(100) NULL;
ALTER TABLE "WorkDays" ADD COLUMN IF NOT EXISTS "RateId" bigint NULL;

-- Drop the unique index on (CustomerId, Type), whatever it is called
DO $$
DECLARE
    idx record;
BEGIN
    FOR idx IN
        SELECT indexname FROM pg_indexes
        WHERE schemaname = current_schema() AND tablename = 'Rates'
          AND indexdef LIKE 'CREATE UNIQUE INDEX%("CustomerId", "Type")'
    LOOP
        EXECUTE format('DROP INDEX %I', idx.indexname);
    END LOOP;
END $$;

CREATE INDEX IF NOT EXISTS "IX_Rates_CustomerId" ON "Rates" ("CustomerId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260929135118_SeveralRatesAndDayRates', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
