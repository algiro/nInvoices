# SQLite to PostgreSQL Migration Guide

This guide explains how to migrate your existing nInvoices data from SQLite to PostgreSQL.

## Prerequisites

- .NET 10 SDK installed
- Access to both SQLite and PostgreSQL databases
- PowerShell (Windows) or Bash (Linux/macOS)

## Migration Options

### Option 1: Automated Migration Script (Recommended)

#### Windows (PowerShell):
```powershell
cd docker
.\migrate-sqlite-to-postgres.ps1 `
  -SqliteDbPath "C:\path\to\nInvoices.db" `
  -PostgresConnectionString "Host=localhost;Port=5432;Database=ninvoices_db;Username=ninvoices_user;Password=your_password"
```

#### Linux/macOS (Bash):
```bash
cd docker
./migrate-sqlite-to-postgres.sh \
  --sqlite-db "../src/nInvoices.Api/nInvoices.db" \
  --postgres-conn "Host=localhost;Port=5432;Database=ninvoices_db;Username=ninvoices_user;Password=your_password"
```

### Option 2: Manual Migration

If you have a small dataset, you can manually migrate:

#### Step 1: Backup SQLite Data
```powershell
# Make a backup of your SQLite database
Copy-Item "src\nInvoices.Api\nInvoices.db" "nInvoices.db.backup"
```

#### Step 2: Create PostgreSQL Schema
```powershell
cd src/nInvoices.Api

# Set environment variables
$env:ConnectionStrings__Default="Host=localhost;Port=5432;Database=ninvoices_db;Username=ninvoices_user;Password=your_password"
$env:Database__Type="PostgreSQL"

# Create migrations for PostgreSQL
dotnet ef migrations add InitialPostgreSQL --context ApplicationDbContext

# Apply migrations
dotnet ef database update
```

#### Step 3: Export Data from SQLite
Use any SQLite client or tool to export your data as CSV/JSON:

**Using SQLite CLI:**
```bash
sqlite3 nInvoices.db
.mode csv
.headers on
.output customers.csv
SELECT * FROM Customers;
.output rates.csv
SELECT * FROM Rates;
# Repeat for other tables
.quit
```

#### Step 4: Import Data to PostgreSQL
Use `psql` or any PostgreSQL client to import the data:

```bash
psql -h localhost -U ninvoices_user -d ninvoices_db

# Import CSV files
\copy Customers FROM 'customers.csv' CSV HEADER;
\copy Rates FROM 'rates.csv' CSV HEADER;
# Repeat for other tables
\q
```

### Option 3: Using Docker Environment

#### Step 1: Start PostgreSQL container
```powershell
cd docker
docker-compose -f docker-compose.dev.yml up -d postgres
```

#### Step 2: Wait for PostgreSQL to be ready
```powershell
docker exec ninvoices-postgres-dev pg_isready -U ninvoices_user
```

#### Step 3: Run migration
```powershell
.\migrate-sqlite-to-postgres.ps1 `
  -SqliteDbPath "..\src\nInvoices.Api\nInvoices.db" `
  -PostgresConnectionString "Host=localhost;Port=5432;Database=ninvoices_db;Username=ninvoices_user;Password=change_me_secure_password_123"
```

## Post-Migration Verification

### 1. Check PostgreSQL Data
```bash
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db

# List all tables
\dt

# Count records
SELECT COUNT(*) FROM "Customers";
SELECT COUNT(*) FROM "Rates";
SELECT COUNT(*) FROM "Invoices";

# Exit
\q
```

### 2. Test API Endpoints
```powershell
# Update appsettings.json to use PostgreSQL
# Start the API
cd src/nInvoices.Api
dotnet run

# In another terminal, test endpoints
curl http://localhost:5000/api/customers
curl http://localhost:5000/api/invoices
```

### 3. Verify Data Integrity
- Check that all customers are present
- Verify rates are linked correctly
- Ensure invoices display properly
- Test generating new invoices

## Troubleshooting

### Connection Issues
If you can't connect to PostgreSQL:

```powershell
# Check if PostgreSQL is running
docker ps | Select-String postgres

# Check logs
docker logs ninvoices-postgres-dev

# Test connection
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db -c "SELECT version();"
```

### Migration Errors
If migration fails:

1. Check the error message in the console
2. Verify connection string is correct
3. Ensure PostgreSQL user has proper permissions
4. Check PostgreSQL logs:
   ```powershell
   docker logs ninvoices-postgres-dev
   ```

### Data Issues
If data doesn't appear correctly:

1. Check the migration-data.json backup file
2. Verify table names match (case-sensitive in PostgreSQL)
3. Check for foreign key constraint violations
4. Review PostgreSQL logs for errors

## Rolling Back

If you need to rollback to SQLite:

1. Stop the API application
2. Update `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "Default": "Data Source=nInvoices.db"
     },
     "Database": {
       "Type": "SQLite"
     }
   }
   ```
3. Restore your SQLite backup:
   ```powershell
   Copy-Item "nInvoices.db.backup" "nInvoices.db" -Force
   ```
4. Restart the API

## Best Practices

1. **Always backup** your SQLite database before migration
2. **Test migration** in a development environment first
3. **Verify data integrity** after migration
4. **Keep backups** for at least 30 days
5. **Update connection strings** in all environments
6. **Monitor performance** after switching to PostgreSQL

## Need Help?

If you encounter issues:
1. Check the logs in `docker/volumes/logs/`
2. Review PostgreSQL logs: `docker logs ninvoices-postgres-dev`
3. Verify network connectivity: `docker network inspect ninvoices-network`
4. Check GitHub issues or create a new one
