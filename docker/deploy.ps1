# Deploy nInvoices to Production
#
# This script builds Docker images, pushes to Docker Hub, and deploys to the remote server.
# With -Migrate it also applies the idempotent SQL in docker/migrations-postgres/ to the
# production database, before the new containers start (same as deploy.sh --migrate).
# Requires: Docker Desktop running locally, SSH access to production server (he-it-tudes) from WSL
#
# Usage:
#   .\deploy.ps1                    # Build, push, and deploy everything
#   .\deploy.ps1 -SkipBuild        # Only pull and restart on remote server
#   .\deploy.ps1 -ApiOnly          # Only rebuild and deploy the API
#   .\deploy.ps1 -WebOnly          # Only rebuild and deploy the Web frontend
#   .\deploy.ps1 -Migrate          # Also apply migrations-postgres/*.sql (idempotent)
#
# Image tags: images are tagged with the short commit hash (plus "-dirty-<timestamp>" when
# the working tree has uncommitted changes) and also as :latest, and the server runs that
# exact tag. Each deploy is appended to ~/docker/DEPLOYED-TAGS on the server. To roll back,
# redeploy an earlier tag without building:
#   .\deploy.ps1 -SkipBuild -ImageTag 4655aed
# -SkipBuild without -ImageTag redeploys the tag the server is already running.
#
# Production Configuration:
#   Server:       he-it-tudes (SSH alias, from WSL's ssh config)
#   Compose dir:  ~/docker/
#   Base path:    /nInvoices

param(
    [switch]$SkipBuild,
    [switch]$ApiOnly,
    [switch]$WebOnly,
    [switch]$Migrate,
    # Defaults to the current commit (see above)
    [string]$ImageTag = ""
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$SSH_HOST = "he-it-tudes"
$REMOTE_DIR = "~/docker"
$API_IMAGE = "algiro/ninvoices-api"
$WEB_IMAGE = "algiro/ninvoices-web"

$PG_CONTAINER = "ninvoices-postgres-prod"
$PG_USER = "ninvoices_user"
$PG_DB = "ninvoices_db"

# Production build arguments
$KeycloakUrl = "https://it-tudes.tech"
$ApiUrl = "/nInvoices"
$Base = "/nInvoices"

function Write-Step($step, $message) {
    Write-Host "`n[$step] $message" -ForegroundColor Yellow
    Write-Host ("-" * 50) -ForegroundColor Gray
}

function Invoke-Remote($command) {
    wsl ssh $SSH_HOST $command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Remote command failed: $command" -ForegroundColor Red
        exit 1
    }
}

# The commit being deployed; uncommitted changes get a unique suffix so a tag never
# claims to be a commit it doesn't match
function Get-CommitTag {
    $sha = git -C .. rev-parse --short HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $sha) { return "latest" }
    $dirty = git -C .. status --porcelain 2>$null
    if ($dirty) { return "$sha-dirty-$(Get-Date -Format 'yyyyMMddHHmm')" }
    return $sha
}

if ($ApiOnly -and $WebOnly) {
    Write-Host "-ApiOnly and -WebOnly are mutually exclusive." -ForegroundColor Red
    exit 1
}

# Fail fast if the deploy target is unreachable (before spending time on a build)
wsl ssh -o BatchMode=yes -o ConnectTimeout=8 $SSH_HOST true 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Cannot SSH to '$SSH_HOST' from WSL (check the network/VPN and 'wsl -- ssh -G $SSH_HOST')." -ForegroundColor Red
    exit 1
}

# The tag the server last deployed (last line of DEPLOYED-TAGS), or "latest"
function Get-DeployedTag {
    $line = wsl ssh $SSH_HOST "tail -n 1 $REMOTE_DIR/DEPLOYED-TAGS 2>/dev/null"
    $tag = if ($line) { ($line -split '\s+')[1] } else { $null }
    if ($tag) { return $tag }
    return "latest"
}

if (-not $ImageTag) {
    # Without a build there is no new image: redeploy the one already on the server. (A commit
    # tag computed now could name an image that was never built, e.g. a new "-dirty-" timestamp.)
    $ImageTag = if ($SkipBuild) { Get-DeployedTag } else { Get-CommitTag }
}
Write-Host "Image tag: $ImageTag" -ForegroundColor Cyan

# Step 1: Build and push
if (-not $SkipBuild) {
    if ($ApiOnly) {
        Write-Step "1/4" "Building API image only..."
        docker build -t "${API_IMAGE}:${ImageTag}" -f Dockerfile.api ..
        if ($LASTEXITCODE -ne 0) { Write-Host "API build failed." -ForegroundColor Red; exit 1 }
        docker tag "${API_IMAGE}:${ImageTag}" "${API_IMAGE}:latest"
        docker push "${API_IMAGE}:${ImageTag}"
        if ($LASTEXITCODE -ne 0) { Write-Host "API push failed." -ForegroundColor Red; exit 1 }
        docker push "${API_IMAGE}:latest"
        if ($LASTEXITCODE -ne 0) { Write-Host "API push failed." -ForegroundColor Red; exit 1 }
    } elseif ($WebOnly) {
        Write-Step "1/4" "Building Web image only..."
        docker build `
            -t "${WEB_IMAGE}:${ImageTag}" `
            --build-arg VITE_KEYCLOAK_URL=$KeycloakUrl `
            --build-arg VITE_KEYCLOAK_REALM=ninvoices `
            --build-arg VITE_KEYCLOAK_CLIENT_ID=ninvoices-web `
            --build-arg VITE_API_URL=$ApiUrl `
            --build-arg VITE_BASE=$Base `
            -f Dockerfile.web ..
        if ($LASTEXITCODE -ne 0) { Write-Host "Web build failed." -ForegroundColor Red; exit 1 }
        docker tag "${WEB_IMAGE}:${ImageTag}" "${WEB_IMAGE}:latest"
        docker push "${WEB_IMAGE}:${ImageTag}"
        if ($LASTEXITCODE -ne 0) { Write-Host "Web push failed." -ForegroundColor Red; exit 1 }
        docker push "${WEB_IMAGE}:latest"
        if ($LASTEXITCODE -ne 0) { Write-Host "Web push failed." -ForegroundColor Red; exit 1 }
    } else {
        Write-Step "1/4" "Building and pushing all images..."
        # Pushes both :$ImageTag and :latest
        & .\build-and-push.ps1 -KeycloakUrl $KeycloakUrl -ApiUrl $ApiUrl -Base $Base -Version $ImageTag
        if ($LASTEXITCODE -ne 0) { Write-Host "Build and push failed." -ForegroundColor Red; exit 1 }
    }
    Write-Host "Build and push completed." -ForegroundColor Green
} else {
    Write-Host "`nSkipping build (using existing images on Docker Hub: $ImageTag)" -ForegroundColor Gray
}

# Step 2: Apply DB migrations (optional)
# The EF Core migrations in this repo are SQLite-scaffolded and cannot be applied to
# PostgreSQL with `dotnet ef`; the hand-written idempotent files in migrations-postgres/
# are applied instead, in filename order (see migrations-postgres/README.md).
if ($Migrate) {
    Write-Step "2/4" "Applying PostgreSQL migrations from migrations-postgres/ ..."
    $sqlFiles = Get-ChildItem -Path "migrations-postgres" -Filter "*.sql" | Sort-Object Name
    if (-not $sqlFiles) { Write-Host "No .sql files found in migrations-postgres/" -ForegroundColor Red; exit 1 }
    foreach ($file in $sqlFiles) {
        Write-Host "  -> $($file.Name)" -ForegroundColor Gray
        # The file is fed to psql from inside WSL, so it reaches the server byte for byte
        $wslPath = wsl wslpath -a ($file.FullName.Replace('\', '/'))
        wsl bash -c "ssh $SSH_HOST 'docker exec -i $PG_CONTAINER psql -v ON_ERROR_STOP=1 -U $PG_USER -d $PG_DB' < '$wslPath'"
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Migration failed on $($file.Name) - database left partially updated." -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "PostgreSQL migrations applied." -ForegroundColor Green
} else {
    Write-Host "`nSkipping migrations (pass -Migrate to apply migrations-postgres/*.sql)." -ForegroundColor Gray
}

# Step 3: Pull and restart on remote
# The server's compose file reads ${IMAGE_TAG:-latest}; only the services being deployed are
# recreated, so -ApiOnly doesn't look for a web image with the new tag
Write-Step "3/4" "Pulling and restarting $ImageTag on remote server..."

$services = if ($ApiOnly) { "api" } elseif ($WebOnly) { "web" } else { "api web" }
Invoke-Remote "cd $REMOTE_DIR && IMAGE_TAG=$ImageTag docker compose pull $services && IMAGE_TAG=$ImageTag docker compose up -d $services"
Invoke-Remote "echo `"`$(date -u +%Y-%m-%dT%H:%M:%SZ) $ImageTag $services`" >> $REMOTE_DIR/DEPLOYED-TAGS"

# Recreated containers get new internal IPs; the shared proxy keeps the old ones
# until it reloads, which shows up as 502s (see infra hosts/ittudes/DEPLOY.md).
Invoke-Remote "docker exec ninvoices-nginx-prod nginx -s reload"

Write-Host "Containers restarted." -ForegroundColor Green

# Step 4: Verify
Write-Step "4/4" "Verifying deployment..."
Start-Sleep -Seconds 15

Invoke-Remote "cd $REMOTE_DIR && docker compose ps"

# Quick health check
$apiStatus = wsl ssh $SSH_HOST "curl -s -o /dev/null -w '%{http_code}' -k 'https://localhost/nInvoices/api/customers'" 2>$null
$webStatus = wsl ssh $SSH_HOST "curl -s -o /dev/null -w '%{http_code}' -k 'https://localhost/nInvoices/'" 2>$null

Write-Host "`nHealth Check:" -ForegroundColor Yellow
Write-Host "  Web:  HTTP $webStatus $(if ($webStatus -eq '200') {'OK'} else {'ISSUE'})" -ForegroundColor $(if ($webStatus -eq '200') {'Green'} else {'Red'})
Write-Host "  API:  HTTP $apiStatus $(if ($apiStatus -eq '401') {'OK (auth required)'} else {'ISSUE'})" -ForegroundColor $(if ($apiStatus -eq '401') {'Green'} else {'Red'})

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "   Deployment Complete: $ImageTag" -ForegroundColor Green
Write-Host "================================================================`n" -ForegroundColor Cyan
