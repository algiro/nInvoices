-- nInvoices :: PostgreSQL migration
-- Runs first (its name sorts before every other script).
-- Every script records itself in "__EFMigrationsHistory", but a database built from the EF model
-- (Database:EnsureCreated: fresh Docker installs, Aspire) doesn't have that table, so on such a
-- database deploy.sh --migrate stopped at the first script. This creates it as EF Core does.
-- Production already has it. Idempotent: safe to run repeatedly.

\set ON_ERROR_STOP on
\connect ninvoices_db

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);
