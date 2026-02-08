CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "Customers" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "FiscalId" TEXT NOT NULL,
    "Address_Street" TEXT NOT NULL,
    "Address_HouseNumber" TEXT NOT NULL,
    "Address_City" TEXT NOT NULL,
    "Address_ZipCode" TEXT NOT NULL,
    "Address_State" TEXT NULL,
    "Address_Country" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL
);

CREATE TABLE "Invoices" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Invoices" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "InvoiceNumber" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "Status" TEXT NOT NULL DEFAULT 'Draft',
    "IssueDate" TEXT NOT NULL,
    "DueDate" TEXT NULL,
    "WorkedDays" INTEGER NULL,
    "Year" INTEGER NULL,
    "Month" INTEGER NULL,
    "SubtotalAmount" TEXT NOT NULL,
    "SubtotalCurrency" TEXT NOT NULL,
    "TotalExpensesAmount" TEXT NOT NULL,
    "TotalExpensesCurrency" TEXT NOT NULL,
    "TotalTaxesAmount" TEXT NOT NULL,
    "TotalTaxesCurrency" TEXT NOT NULL,
    "TotalAmount" TEXT NOT NULL,
    "TotalCurrency" TEXT NOT NULL,
    "RenderedContent" TEXT NULL,
    "Notes" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_Invoices_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "InvoiceTemplates" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_InvoiceTemplates" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "InvoiceType" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "Format" TEXT NOT NULL DEFAULT 'html',
    "Version" INTEGER NOT NULL DEFAULT 1,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_InvoiceTemplates_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Rates" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Rates" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "Type" TEXT NOT NULL,
    "PriceAmount" TEXT NOT NULL,
    "PriceCurrency" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_Rates_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Taxes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Taxes" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "TaxId" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "HandlerId" TEXT NOT NULL,
    "Rate" TEXT NOT NULL,
    "ApplicationType" TEXT NOT NULL,
    "AppliedToTaxId" INTEGER NULL,
    "Order" INTEGER NOT NULL DEFAULT 0,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_Taxes_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Taxes_Taxes_AppliedToTaxId" FOREIGN KEY ("AppliedToTaxId") REFERENCES "Taxes" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkDays" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkDays" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "Date" TEXT NOT NULL,
    "Notes" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_WorkDays_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Expenses" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Expenses" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "InvoiceId" INTEGER NULL,
    "Date" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "Amount" TEXT NOT NULL,
    "Currency" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_Expenses_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Expenses_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("Id") ON DELETE SET NULL
);

CREATE TABLE "InvoiceTaxLines" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_InvoiceTaxLines" PRIMARY KEY AUTOINCREMENT,
    "InvoiceId" INTEGER NOT NULL,
    "TaxId" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "Rate" TEXT NOT NULL,
    "BaseAmount" TEXT NOT NULL,
    "BaseCurrency" TEXT NOT NULL,
    "TaxAmount" TEXT NOT NULL,
    "TaxCurrency" TEXT NOT NULL,
    "Order" INTEGER NOT NULL,
    CONSTRAINT "FK_InvoiceTaxLines_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Customers_FiscalId" ON "Customers" ("FiscalId");

CREATE INDEX "IX_Expenses_CustomerId_Date" ON "Expenses" ("CustomerId", "Date");

CREATE INDEX "IX_Expenses_InvoiceId" ON "Expenses" ("InvoiceId");

CREATE INDEX "IX_Invoices_CustomerId_Year_Month" ON "Invoices" ("CustomerId", "Year", "Month");

CREATE INDEX "IX_Invoices_IssueDate" ON "Invoices" ("IssueDate");

CREATE INDEX "IX_Invoices_Status" ON "Invoices" ("Status");

CREATE INDEX "IX_InvoiceTaxLines_InvoiceId" ON "InvoiceTaxLines" ("InvoiceId");

CREATE UNIQUE INDEX "IX_InvoiceTemplates_CustomerId_InvoiceType_IsActive" ON "InvoiceTemplates" ("CustomerId", "InvoiceType", "IsActive") WHERE [IsActive] = 1;

CREATE UNIQUE INDEX "IX_Rates_CustomerId_Type" ON "Rates" ("CustomerId", "Type");

CREATE INDEX "IX_Taxes_AppliedToTaxId" ON "Taxes" ("AppliedToTaxId");

CREATE UNIQUE INDEX "IX_Taxes_CustomerId_TaxId" ON "Taxes" ("CustomerId", "TaxId");

CREATE UNIQUE INDEX "IX_WorkDays_CustomerId_Date" ON "WorkDays" ("CustomerId", "Date");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260123094131_InitialCreate', '10.0.2');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "WorkDays" ADD "DayType" INTEGER NOT NULL DEFAULT 0;

CREATE TABLE "MonthlyReportTemplates" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_MonthlyReportTemplates" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "InvoiceType" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_MonthlyReportTemplates_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_MonthlyReportTemplates_CustomerId_InvoiceType_IsActive" ON "MonthlyReportTemplates" ("CustomerId", "InvoiceType", "IsActive");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260124075036_AddMonthlyReportTemplateAndDayType', '10.0.2');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "InvoiceSequence" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_InvoiceSequence" PRIMARY KEY AUTOINCREMENT,
    "CurrentValue" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL
);

INSERT INTO "InvoiceSequence" ("Id", "CreatedAt", "CurrentValue", "UpdatedAt")
VALUES (1, '2026-01-01 00:00:00', 1, NULL);
SELECT changes();


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260124211626_AddInvoiceSequence', '10.0.2');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Invoices" ADD "MonthlyReportTemplateId" INTEGER NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260128155256_AddMonthlyReportTemplateIdToInvoice', '10.0.2');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "InvoiceTemplates" ADD "Name" TEXT NOT NULL DEFAULT '';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260128191700_AddNameToInvoiceTemplate', '10.0.2');

COMMIT;

