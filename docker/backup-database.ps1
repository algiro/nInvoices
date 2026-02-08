#!/usr/bin/env pwsh
# Database Backup Script for nInvoices
# Creates timestamped backup of PostgreSQL database

param(
    [string]$ContainerName = "ninvoices-postgres-dev",
    [string]$Database = "ninvoices_db",
    [string]$Username = "ninvoices_user",
    [string]$BackupDir = "./volumes/backups"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "nInvoices Database Backup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create backup directory if it doesn't exist
if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
    Write-Host "Created backup directory: $BackupDir" -ForegroundColor Green
}

# Generate timestamp for backup filename
$timestamp = Get-Date -Format "yyyy-MM-dd-HH-mm-ss"
$backupFile = "$BackupDir/ninvoices-backup-$timestamp.sql"

Write-Host "Container: $ContainerName" -ForegroundColor Yellow
Write-Host "Database: $Database" -ForegroundColor Yellow
Write-Host "Backup file: $backupFile" -ForegroundColor Yellow
Write-Host ""

try {
    # Check if container is running
    $containerStatus = docker ps --filter "name=$ContainerName" --format "{{.Status}}"
    
    if (-not $containerStatus) {
        throw "Container '$ContainerName' is not running!"
    }

    Write-Host "Creating backup..." -ForegroundColor Green
    
    # Create backup
    docker exec $ContainerName pg_dump -U $Username -d $Database | Out-File -FilePath $backupFile -Encoding UTF8
    
    if ($LASTEXITCODE -ne 0) {
        throw "Backup failed with exit code $LASTEXITCODE"
    }

    # Get file size
    $fileSize = (Get-Item $backupFile).Length / 1MB
    $fileSizeFormatted = "{0:N2} MB" -f $fileSize

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Backup completed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Backup location: $backupFile" -ForegroundColor Yellow
    Write-Host "Backup size: $fileSizeFormatted" -ForegroundColor Yellow
    Write-Host ""
    
    # Compress backup
    Write-Host "Compressing backup..." -ForegroundColor Cyan
    $compressedFile = "$backupFile.gz"
    
    if (Get-Command "gzip" -ErrorAction SilentlyContinue) {
        gzip $backupFile
        $compressedSize = (Get-Item $compressedFile).Length / 1MB
        $compressedSizeFormatted = "{0:N2} MB" -f $compressedSize
        Write-Host "Compressed: $compressedFile ($compressedSizeFormatted)" -ForegroundColor Green
    } else {
        Write-Host "gzip not found, skipping compression" -ForegroundColor Yellow
    }
    
    # List recent backups
    Write-Host ""
    Write-Host "Recent backups:" -ForegroundColor Cyan
    Get-ChildItem -Path $BackupDir -Filter "ninvoices-backup-*.sql*" | 
        Sort-Object LastWriteTime -Descending | 
        Select-Object -First 5 | 
        ForEach-Object {
            $size = "{0:N2} MB" -f ($_.Length / 1MB)
            Write-Host "  $($_.Name) - $size" -ForegroundColor Gray
        }
    
    # Cleanup old backups (keep last 10)
    Write-Host ""
    $allBackups = Get-ChildItem -Path $BackupDir -Filter "ninvoices-backup-*.sql*" | Sort-Object LastWriteTime -Descending
    if ($allBackups.Count -gt 10) {
        Write-Host "Removing old backups (keeping last 10)..." -ForegroundColor Yellow
        $allBackups | Select-Object -Skip 10 | ForEach-Object {
            Remove-Item $_.FullName -Force
            Write-Host "  Removed: $($_.Name)" -ForegroundColor Gray
        }
    }

} catch {
    Write-Host ""
    Write-Host "Error during backup: $_" -ForegroundColor Red
    exit 1
}
