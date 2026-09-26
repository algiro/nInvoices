#!/usr/bin/env pwsh
# Database Restore Script for nInvoices
# Restores PostgreSQL database from backup file

param(
    [Parameter(Mandatory=$true)]
    [string]$BackupFile,
    [string]$ContainerName = "ninvoices-postgres-dev",
    [string]$Database = "ninvoices_db",
    [string]$Username = "ninvoices_user"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "nInvoices Database Restore" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if backup file exists
if (-not (Test-Path $BackupFile)) {
    Write-Host "Error: Backup file not found: $BackupFile" -ForegroundColor Red
    exit 1
}

Write-Host "Container: $ContainerName" -ForegroundColor Yellow
Write-Host "Database: $Database" -ForegroundColor Yellow
Write-Host "Backup file: $BackupFile" -ForegroundColor Yellow
Write-Host ""

# Check if file is compressed
$isCompressed = $BackupFile.EndsWith(".gz")
if ($isCompressed) {
    Write-Host "Backup is compressed, decompressing..." -ForegroundColor Cyan
    $decompressedFile = $BackupFile.Replace(".gz", "")
    
    if (Get-Command "gunzip" -ErrorAction SilentlyContinue) {
        gunzip -c $BackupFile | Out-File -FilePath $decompressedFile -Encoding UTF8
        $BackupFile = $decompressedFile
    } else {
        Write-Host "Error: gunzip not found. Please decompress the file manually." -ForegroundColor Red
        exit 1
    }
}

# Warning message
Write-Host "⚠️  WARNING: This will overwrite all data in the '$Database' database!" -ForegroundColor Red
Write-Host ""
$confirmation = Read-Host "Type 'yes' to continue or any other key to cancel"

if ($confirmation -ne "yes") {
    Write-Host "Restore cancelled." -ForegroundColor Yellow
    exit 0
}

try {
    # Check if container is running
    $containerStatus = docker ps --filter "name=$ContainerName" --format "{{.Status}}"
    
    if (-not $containerStatus) {
        throw "Container '$ContainerName' is not running!"
    }

    Write-Host ""
    Write-Host "Creating backup of current database..." -ForegroundColor Cyan
    $preRestoreBackup = "./volumes/backups/pre-restore-backup-$(Get-Date -Format 'yyyy-MM-dd-HH-mm-ss').sql"
    docker exec $ContainerName pg_dump -U $Username -d $Database | Out-File -FilePath $preRestoreBackup -Encoding UTF8
    Write-Host "Pre-restore backup created: $preRestoreBackup" -ForegroundColor Green

    Write-Host ""
    Write-Host "Dropping existing connections..." -ForegroundColor Cyan
    $dropConnectionsQuery = @"
SELECT pg_terminate_backend(pg_stat_activity.pid)
FROM pg_stat_activity
WHERE pg_stat_activity.datname = '$Database'
  AND pid <> pg_backend_pid();
"@
    
    docker exec $ContainerName psql -U $Username -d postgres -c $dropConnectionsQuery | Out-Null

    Write-Host "Dropping database..." -ForegroundColor Cyan
    docker exec $ContainerName psql -U $Username -d postgres -c "DROP DATABASE IF EXISTS $Database;" | Out-Null

    Write-Host "Creating database..." -ForegroundColor Cyan
    docker exec $ContainerName psql -U $Username -d postgres -c "CREATE DATABASE $Database;" | Out-Null

    Write-Host "Restoring from backup..." -ForegroundColor Green
    Get-Content $BackupFile | docker exec -i $ContainerName psql -U $Username -d $Database
    
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed with exit code $LASTEXITCODE"
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Restore completed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Pre-restore backup saved at: $preRestoreBackup" -ForegroundColor Yellow

    # Cleanup decompressed file if it was created
    if ($isCompressed -and (Test-Path $decompressedFile)) {
        Remove-Item $decompressedFile -Force
    }

} catch {
    Write-Host ""
    Write-Host "Error during restore: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "You can restore from pre-restore backup: $preRestoreBackup" -ForegroundColor Yellow
    exit 1
}
