-- Create Keycloak database (separate from application database)
-- This script runs on PostgreSQL container initialization

-- Check if keycloak_db exists, if not create it
SELECT 'CREATE DATABASE keycloak_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'keycloak_db')\gexec

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE keycloak_db TO ninvoices_user;

-- The main application database (ninvoices_db) is created automatically
-- by POSTGRES_DB environment variable

\c ninvoices_db

-- Add any custom extensions for the application database here
-- Example: CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c keycloak_db

-- Keycloak will create its own schema automatically
