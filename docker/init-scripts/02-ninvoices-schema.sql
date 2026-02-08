-- nInvoices PostgreSQL Schema
-- This script creates the database schema for PostgreSQL

\c ninvoices_db

-- Migration history table
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" VARCHAR(150) NOT NULL PRIMARY KEY,
    "ProductVersion" VARCHAR(32) NOT NULL
);

-- Customers table
CREATE TABLE IF NOT EXISTS "Customers" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(500) NOT NULL,
    "FiscalId" VARCHAR(100) NOT NULL,
    "Address_Street" VARCHAR(500) NOT NULL,
    "Address_HouseNumber" VARCHAR(50) NOT NULL,
    "Address_City" VARCHAR(200) NOT NULL,
    "Address_ZipCode" VARCHAR(20) NOT NULL,
    "Address_State" VARCHAR(200) NULL,
    "Address_Country" VARCHAR(100) NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP NULL
);

-- Invoices table
CREATE TABLE IF NOT EXISTS "Invoices" (
    "Id" SERIAL PRIMARY KEY,
    "CustomerId" INTEGER NOT NULL,
    "InvoiceNumber" VARCHAR(50) NOT NULL,
    "Type" VARCHAR(50) NOT NULL,
    "Status" VARCHAR(50) NOT NULL DEFAULT 'Draft',
    "IssueDate" TIMESTAMP NOT NULL,
    "DueDate" TIMESTAMP NULL,
    "WorkedDays" INTEGER NULL,
    "Year" INTEGER NULL,
    "Month" INTEGER NULL,
    "SubtotalAmount" DECIMAL(18,2) NOT NULL,
    "SubtotalCurrency" VARCHAR(3) NOT NULL,
    "TotalExpensesAmount" DECIMAL(18,2) NOT NULL,
    "TotalExpensesCurrency" VARCHAR(3) NOT NULL,
    "TotalTaxesAmount" DECIMAL(18,2) NOT NULL,
    "TotalTaxesCurrency" VARCHAR(3) NOT NULL,
    "TotalAmount" DECIMAL(18,2) NOT NULL,
    "TotalCurrency" VARCHAR(3) NOT NULL,
    "RenderedContent" TEXT NULL,
    "Notes" TEXT NULL,
    "MonthlyReportTemplateId" INTEGER NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP NULL,
    CONSTRAINT "FK_Invoices_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT
);

-- Invoice Templates table
CREATE TABLE IF NOT EXISTS "InvoiceTemplates" (
    "Id" SERIAL PRIMARY KEY,
    "CustomerId" INTEGER NOT NULL,
    "InvoiceType" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(200) NOT NULL,
    "HtmlTemplate" TEXT NOT NULL,
    "CssTemplate" TEXT NULL,
    "IsDefault" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP NULL,
    CONSTRAINT "FK_InvoiceTemplates_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

-- Monthly Report Templates table
CREATE TABLE IF NOT EXISTS "MonthlyReportTemplates" (
    "Id" SERIAL PRIMARY KEY,
    "CustomerId" INTEGER NOT NULL,
    "Name" VARCHAR(200) NOT NULL,
    "HtmlTemplate" TEXT NOT NULL,
    "CssTemplate" TEXT NULL,
    "IsDefault" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP NULL,
    CONSTRAINT "FK_MonthlyReportTemplates_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

-- Invoice Lines table
CREATE TABLE IF NOT EXISTS "InvoiceLines" (
    "Id" SERIAL PRIMARY KEY,
    "InvoiceId" INTEGER NOT NULL,
    "Description" VARCHAR(1000) NOT NULL,
    "Quantity" DECIMAL(18,3) NOT NULL,
    "UnitPriceAmount" DECIMAL(18,2) NOT NULL,
    "UnitPriceCurrency" VARCHAR(3) NOT NULL,
    "TaxRate" DECIMAL(5,2) NOT NULL,
    "SubtotalAmount" DECIMAL(18,2) NOT NULL,
    "SubtotalCurrency" VARCHAR(3) NOT NULL,
    "TotalAmount" DECIMAL(18,2) NOT NULL,
    "TotalCurrency" VARCHAR(3) NOT NULL,
    "SortOrder" INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT "FK_InvoiceLines_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("Id") ON DELETE CASCADE
);

-- Work Days table
CREATE TABLE IF NOT EXISTS "WorkDays" (
    "Id" SERIAL PRIMARY KEY,
    "InvoiceId" INTEGER NOT NULL,
    "Date" TIMESTAMP NOT NULL,
    "DayType" VARCHAR(50) NOT NULL,
    "HoursWorked" DECIMAL(5,2) NULL,
    "Notes" TEXT NULL,
    CONSTRAINT "FK_WorkDays_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("Id") ON DELETE CASCADE
);

-- Invoice Sequence table
CREATE TABLE IF NOT EXISTS "InvoiceSequences" (
    "Id" SERIAL PRIMARY KEY,
    "Year" INTEGER NOT NULL,
    "Month" INTEGER NOT NULL,
    "LastNumber" INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT "UK_InvoiceSequences_Year_Month" UNIQUE ("Year", "Month")
);

-- Create indexes
CREATE INDEX IF NOT EXISTS "IX_Invoices_CustomerId" ON "Invoices" ("CustomerId");
CREATE INDEX IF NOT EXISTS "IX_Invoices_InvoiceNumber" ON "Invoices" ("InvoiceNumber");
CREATE INDEX IF NOT EXISTS "IX_InvoiceLines_InvoiceId" ON "InvoiceLines" ("InvoiceId");
CREATE INDEX IF NOT EXISTS "IX_InvoiceTemplates_CustomerId" ON "InvoiceTemplates" ("CustomerId");
CREATE INDEX IF NOT EXISTS "IX_MonthlyReportTemplates_CustomerId" ON "MonthlyReportTemplates" ("CustomerId");
CREATE INDEX IF NOT EXISTS "IX_WorkDays_InvoiceId" ON "WorkDays" ("InvoiceId");

-- Insert migration history
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES 
    ('20260123094131_InitialCreate', '10.0.0'),
    ('20260124075036_AddMonthlyReportTemplateAndDayType', '10.0.0'),
    ('20260124211626_AddInvoiceSequence', '10.0.0'),
    ('20260128155256_AddMonthlyReportTemplateIdToInvoice', '10.0.0'),
    ('20260128191700_AddNameToInvoiceTemplate', '10.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;
