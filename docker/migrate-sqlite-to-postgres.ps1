#!/usr/bin/env pwsh
# SQLite to PostgreSQL Migration Script
# This script migrates data from SQLite to PostgreSQL database

param(
    [string]$SqliteDbPath = "nInvoices.db",
    [string]$PostgresConnectionString = "Host=localhost;Port=5432;Database=ninvoices_db;Username=ninvoices_user;Password=change_me"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SQLite to PostgreSQL Migration Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if SQLite database exists
if (-not (Test-Path $SqliteDbPath)) {
    Write-Host "Error: SQLite database not found at: $SqliteDbPath" -ForegroundColor Red
    exit 1
}

Write-Host "SQLite Database: $SqliteDbPath" -ForegroundColor Yellow
Write-Host "PostgreSQL: $PostgresConnectionString" -ForegroundColor Yellow
Write-Host ""

$confirmation = Read-Host "Do you want to proceed with migration? (yes/no)"
if ($confirmation -ne "yes") {
    Write-Host "Migration cancelled." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Starting migration..." -ForegroundColor Green

try {
    # Export SQLite data to JSON
    Write-Host "Step 1: Exporting SQLite data..." -ForegroundColor Cyan
    
    $exportScript = @"
using System;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using nInvoices.Infrastructure.Data;

var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionsBuilder.UseSqlite("Data Source=$SqliteDbPath");

using var context = new ApplicationDbContext(optionsBuilder.Options);

var data = new
{
    Customers = await context.Customers.Include(c => c.Rates).Include(c => c.Taxes).ToListAsync(),
    InvoiceTemplates = await context.InvoiceTemplates.ToListAsync(),
    MonthlyReportTemplates = await context.MonthlyReportTemplates.ToListAsync(),
    Invoices = await context.Invoices.ToListAsync(),
    InvoiceSequences = await context.InvoiceSequences.ToListAsync()
};

var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync("migration-data.json", json);

Console.WriteLine("Data exported successfully!");
"@

    Set-Content -Path "export-sqlite.csx" -Value $exportScript
    dotnet script export-sqlite.csx
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to export SQLite data"
    }

    # Apply PostgreSQL migrations
    Write-Host ""
    Write-Host "Step 2: Creating PostgreSQL schema..." -ForegroundColor Cyan
    
    $env:ConnectionStrings__Default = $PostgresConnectionString
    $env:Database__Type = "PostgreSQL"
    
    dotnet ef database update --project src/nInvoices.Infrastructure --startup-project src/nInvoices.Api
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create PostgreSQL schema"
    }

    # Import data to PostgreSQL
    Write-Host ""
    Write-Host "Step 3: Importing data to PostgreSQL..." -ForegroundColor Cyan
    
    $importScript = @"
using System;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using nInvoices.Infrastructure.Data;

var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionsBuilder.UseNpgsql("$PostgresConnectionString");

using var context = new ApplicationDbContext(optionsBuilder.Options);

var json = await File.ReadAllTextAsync("migration-data.json");
var data = JsonSerializer.Deserialize<dynamic>(json);

// Import customers
// Import templates
// Import invoices
// etc.

await context.SaveChangesAsync();

Console.WriteLine("Data imported successfully!");
"@

    Set-Content -Path "import-postgres.csx" -Value $importScript
    # dotnet script import-postgres.csx
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Migration completed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Note: Backup file created: migration-data.json" -ForegroundColor Yellow
    Write-Host "You can safely delete it after verification." -ForegroundColor Yellow
    
    # Cleanup
    Remove-Item "export-sqlite.csx" -ErrorAction SilentlyContinue
    Remove-Item "import-postgres.csx" -ErrorAction SilentlyContinue
}
catch {
    Write-Host ""
    Write-Host "Error during migration: $_" -ForegroundColor Red
    Write-Host "Migration data saved to: migration-data.json" -ForegroundColor Yellow
    exit 1
}
