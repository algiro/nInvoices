-- nInvoices :: PostgreSQL migration
-- Mirrors EF migration 20261004181558_AddFieldEncryption
-- Encryption of the sensitive columns (Docs/ENCRYPTION.md):
--   * "UserKeys" holds each user's data key, wrapped by the server's master key file
--   * encrypted columns become text: a stored value is "enc1:<base64>", longer than the plain
--     value, and amounts are stored the same way (numeric -> text keeps the digits, e.g. 1234.50)
--   * the index on the customers' fiscal id goes: encrypted values can't be looked up
-- The values themselves are encrypted by the API at its next start, not by this script.
-- Idempotent: safe to run repeatedly.

\set ON_ERROR_STOP on
\connect ninvoices_db

BEGIN;

CREATE TABLE IF NOT EXISTS "UserKeys" (
    "Id"          uuid         NOT NULL,
    "OwnerId"     varchar(255) NOT NULL,
    "MasterKeyId" varchar(16)  NOT NULL,
    "WrappedKey"  bytea        NOT NULL,
    "CreatedAt"   timestamp with time zone NOT NULL,
    CONSTRAINT "PK_UserKeys" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserKeys_OwnerId"
    ON "UserKeys" ("OwnerId");

DROP INDEX IF EXISTS "IX_Customers_FiscalId";

ALTER TABLE "Customers"
    ALTER COLUMN "Name"                TYPE text,
    ALTER COLUMN "FiscalId"            TYPE text,
    ALTER COLUMN "Email"               TYPE text,
    ALTER COLUMN "CcEmails"            TYPE text,
    ALTER COLUMN "Address_Street"      TYPE text,
    ALTER COLUMN "Address_HouseNumber" TYPE text,
    ALTER COLUMN "Address_City"        TYPE text,
    ALTER COLUMN "Address_ZipCode"     TYPE text,
    ALTER COLUMN "Address_State"       TYPE text,
    ALTER COLUMN "Address_Country"     TYPE text;

ALTER TABLE "ComplianceSettings"
    ALTER COLUMN "LegalName"           TYPE text,
    ALTER COLUMN "TaxId"               TYPE text,
    ALTER COLUMN "Address_Street"      TYPE text,
    ALTER COLUMN "Address_HouseNumber" TYPE text,
    ALTER COLUMN "Address_City"        TYPE text,
    ALTER COLUMN "Address_ZipCode"     TYPE text,
    ALTER COLUMN "Address_State"       TYPE text,
    ALTER COLUMN "Address_Country"     TYPE text;

ALTER TABLE "Invoices"
    ALTER COLUMN "SubtotalAmount"      TYPE text USING "SubtotalAmount"::text,
    ALTER COLUMN "TotalExpensesAmount" TYPE text USING "TotalExpensesAmount"::text,
    ALTER COLUMN "TotalTaxesAmount"    TYPE text USING "TotalTaxesAmount"::text,
    ALTER COLUMN "TotalAmount"         TYPE text USING "TotalAmount"::text,
    ALTER COLUMN "RenderedContent"     TYPE text,
    ALTER COLUMN "Notes"               TYPE text;

ALTER TABLE "InvoiceTaxLines"
    ALTER COLUMN "BaseAmount" TYPE text USING "BaseAmount"::text,
    ALTER COLUMN "TaxAmount"  TYPE text USING "TaxAmount"::text;

ALTER TABLE "Expenses"
    ALTER COLUMN "Description" TYPE text,
    ALTER COLUMN "Amount"      TYPE text USING "Amount"::text;

ALTER TABLE "Rates"
    ALTER COLUMN "PriceAmount" TYPE text USING "PriceAmount"::text;

ALTER TABLE "WorkDays"
    ALTER COLUMN "Notes" TYPE text;

ALTER TABLE "InvoiceEmails"
    ALTER COLUMN "From"        TYPE text,
    ALTER COLUMN "To"          TYPE text,
    ALTER COLUMN "Cc"          TYPE text,
    ALTER COLUMN "Subject"     TYPE text,
    ALTER COLUMN "Attachments" TYPE text;

ALTER TABLE "VerifactuRecords"
    ALTER COLUMN "IssuerTaxId" TYPE text,
    ALTER COLUMN "TotalTax"    TYPE text,
    ALTER COLUMN "TotalAmount" TYPE text,
    ALTER COLUMN "Xml"         TYPE text;

ALTER TABLE "InvoiceTemplates"
    ALTER COLUMN "Content" TYPE text;

ALTER TABLE "MonthlyReportTemplates"
    ALTER COLUMN "Content" TYPE text;

ALTER TABLE "EmailTemplates"
    ALTER COLUMN "Subject" TYPE text,
    ALTER COLUMN "Body"    TYPE text;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261004181558_AddFieldEncryption', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
