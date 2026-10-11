# Refresh the production schema fixture of the PostgreSQL tests
#
# tests/nInvoices.Infrastructure.Tests/PostgreSql/production-schema.sql is a picture of the production
# database's structure (no data), plus its migration history. PostgreSqlMigrationScriptsTests restores it,
# applies the pending migrations and checks the result is the EF model, so it must follow production: run
# this after a deploy that changed the database (one with a new migration), then review and commit the file.
#
# It only reads from production: pg_dump of the structure, and of the "__EFMigrationsHistory" rows
# (migration names only; without them the tests would re-apply migrations production already has).
# Requires: SSH access to the server from WSL; Docker Desktop running locally for the tests.
#
# Usage:
#   .\refresh-production-schema.ps1               # dump, write the fixture, run the PostgreSQL tests
#   .\refresh-production-schema.ps1 -SkipTests    # dump and write only
#
# Configuration: docker/deploy.local.env, as for deploy.ps1 (SSH_HOST required; PG_CONTAINER, PG_USER and
# PG_DB default to the production compose values).

param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# Local, git-ignored values (server...): KEY=VALUE lines, # for comments
$LocalConfig = @{}
if (Test-Path "deploy.local.env") {
    foreach ($line in Get-Content "deploy.local.env") {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') { $LocalConfig[$Matches[1]] = $Matches[2] }
    }
}
function Get-Setting($name, $default) {
    if ($LocalConfig.ContainsKey($name) -and $LocalConfig[$name]) { return $LocalConfig[$name] }
    return $default
}

$SSH_HOST = Get-Setting "SSH_HOST" ""
$PG_CONTAINER = Get-Setting "PG_CONTAINER" "ninvoices-postgres-prod"
$PG_USER = Get-Setting "PG_USER" "ninvoices_user"
$PG_DB = Get-Setting "PG_DB" "ninvoices_db"

$RepoRoot = Split-Path $PSScriptRoot -Parent
$Fixture = Join-Path $RepoRoot "tests/nInvoices.Infrastructure.Tests/PostgreSql/production-schema.sql"
$Marker = "-- @@MIGRATION-HISTORY@@"

if (-not $SSH_HOST) {
    Write-Host "SSH_HOST is not set: copy deploy.local.env.example to deploy.local.env and fill it in." -ForegroundColor Red
    exit 1
}

function Write-Step($step, $message) {
    Write-Host "`n[$step] $message" -ForegroundColor Yellow
    Write-Host ("-" * 50) -ForegroundColor Gray
}

Write-Step "1/4" "Checking SSH access to $SSH_HOST"
wsl ssh -o BatchMode=yes -o ConnectTimeout=8 $SSH_HOST true 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Cannot SSH to '$SSH_HOST' from WSL (check the network/VPN and 'wsl -- ssh -G $SSH_HOST')." -ForegroundColor Red
    exit 1
}

Write-Step "2/4" "Dumping the production schema and migration history (read-only)"
# Sent base64-encoded: the quoted table name survives PowerShell, WSL and SSH untouched
$remoteScript = @"
set -e
docker exec $PG_CONTAINER pg_dump -U $PG_USER -d $PG_DB --schema-only --no-owner --no-privileges
echo '$Marker'
docker exec $PG_CONTAINER pg_dump -U $PG_USER -d $PG_DB --data-only --inserts --no-owner --no-privileges -t 'public."__EFMigrationsHistory"'
"@ -replace "`r", ""
$encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($remoteScript))

$previousEncoding = [Console]::OutputEncoding
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
try {
    $output = wsl ssh $SSH_HOST "echo $encoded | base64 -d | sh"
    $exitCode = $LASTEXITCODE
} finally {
    [Console]::OutputEncoding = $previousEncoding
}
if ($exitCode -ne 0) {
    Write-Host "pg_dump failed on the server (exit code $exitCode)." -ForegroundColor Red
    exit 1
}

$lines = @($output | ForEach-Object { $_.TrimEnd("`r") })
$markerIndex = [Array]::IndexOf($lines, $Marker)
if ($markerIndex -lt 0) {
    Write-Host "Unexpected output from the server: the migration history part is missing." -ForegroundColor Red
    exit 1
}

# pg_dump 17.6+ adds \restrict / \unrestrict lines with a random key: not kept, as in earlier dumps
$schema = $lines[0..($markerIndex - 1)] | Where-Object { $_ -notmatch '^\\(un)?restrict ' }
$history = $lines[($markerIndex + 1)..($lines.Count - 1)] | Where-Object { $_ -like 'INSERT INTO*' }

$tables = @($schema | Where-Object { $_ -like 'CREATE TABLE*' }).Count
if ($tables -eq 0 -or $history.Count -eq 0 -or -not ($history -match '_PostgreSqlBaseline')) {
    Write-Host "The dump doesn't look right ($tables tables, $($history.Count) migration rows): the fixture was not changed." -ForegroundColor Red
    exit 1
}
$latest = ([regex]::Matches(($history -join "`n"), "VALUES \('([^']+)'") | ForEach-Object { $_.Groups[1].Value } | Sort-Object | Select-Object -Last 1)
Write-Host "  $tables tables, $($history.Count) applied migrations (latest: $latest)" -ForegroundColor Green

Write-Step "3/4" "Writing $([IO.Path]::GetRelativePath($RepoRoot, $Fixture))"
$date = Get-Date -Format "yyyy-MM-dd"
$header = @(
    "-- Schema of the production database (pg_dump --schema-only --no-owner --no-privileges, no data),"
    "-- taken $date (latest migration $latest), plus"
    "-- the rows of `"__EFMigrationsHistory`" (migration names only, so the tests know what production has"
    "-- applied). PostgreSqlMigrationScriptsTests restores it, runs docker/migrations-postgres/*.sql on it like"
    "-- deploy.sh, then the pending EF migrations like the API at startup, and compares the result with the"
    "-- EF model. Refresh it with docker/refresh-production-schema.ps1 after a deploy that changes the schema."
)
$body = @($header) + @($schema) + @(
    ""
    "--"
    "-- Applied migrations (pg_dump --data-only --inserts -t `"__EFMigrationsHistory`")"
    "--"
    ""
) + @($history)
# Trailing blank lines of the schema part are collapsed, then CRLF and UTF-8 without BOM, like the committed file
$text = (($body -join "`n") -replace "\n{3,}--\n-- Applied", "`n`n--`n-- Applied") + "`n"
[IO.File]::WriteAllText($Fixture, ($text -replace "`n", "`r`n"), [Text.UTF8Encoding]::new($false))

git -C $RepoRoot diff --stat -- $Fixture
$added = git -C $RepoRoot diff -U0 -- $Fixture | Where-Object { $_ -match '^\+CREATE TABLE' }
$removed = git -C $RepoRoot diff -U0 -- $Fixture | Where-Object { $_ -match '^-CREATE TABLE' }
if (-not $added -and -not $removed) { Write-Host "  No table added or removed since the last refresh." -ForegroundColor Gray }
$added | ForEach-Object { Write-Host "  new: $($_.Substring(1))" -ForegroundColor Green }
$removed | ForEach-Object { Write-Host "  gone: $($_.Substring(1))" -ForegroundColor Red }

if ($SkipTests) {
    Write-Host "`nTests skipped. Run them before committing:" -ForegroundColor Yellow
    Write-Host "  dotnet test tests/nInvoices.Infrastructure.Tests --filter Category=PostgreSql"
    exit 0
}

Write-Step "4/4" "Running the PostgreSQL tests (needs Docker Desktop)"
dotnet test (Join-Path $RepoRoot "tests/nInvoices.Infrastructure.Tests") --filter "Category=PostgreSql" 2>&1 |
    Tee-Object -Variable testLog
$testsExit = $LASTEXITCODE
if ($testsExit -ne 0) {
    Write-Host "`nThe PostgreSQL tests failed: production and the migrations don't agree. Don't commit the fixture" -ForegroundColor Red
    Write-Host "before understanding why (git checkout -- the file to undo)." -ForegroundColor Red
    exit 1
}
# Without Docker the tests are skipped, and dotnet test still succeeds
if (($testLog -join "`n") -notmatch 'Passed:\s+[1-9]') {
    Write-Host "`nThe PostgreSQL tests didn't run (is Docker Desktop started?). Start it and run them before committing:" -ForegroundColor Red
    Write-Host "  dotnet test tests/nInvoices.Infrastructure.Tests --filter Category=PostgreSql"
    exit 1
}

Write-Host "`nDone. Review the change and commit it:" -ForegroundColor Green
Write-Host "  git add $([IO.Path]::GetRelativePath($RepoRoot, $Fixture))"
Write-Host "  git commit -m `"Refresh the production schema fixture`""
