# Deploy nInvoices to Production
#
# This script builds Docker images, pushes to Docker Hub, and deploys to the remote server.
# Requires: Docker Desktop running locally, SSH access to production server (he-it-tudes)
#
# Usage:
#   .\deploy.ps1                    # Build, push, and deploy everything
#   .\deploy.ps1 -SkipBuild        # Only pull and restart on remote server
#   .\deploy.ps1 -ApiOnly          # Only rebuild and deploy the API
#   .\deploy.ps1 -WebOnly          # Only rebuild and deploy the Web frontend
#
# Production Configuration:
#   Server:       he-it-tudes (SSH alias)
#   Compose dir:  ~/docker/
#   Base path:    /nInvoices

param(
    [switch]$SkipBuild,
    [switch]$ApiOnly,
    [switch]$WebOnly
)

$ErrorActionPreference = "Stop"
$SSH_HOST = "he-it-tudes"
$REMOTE_DIR = "~/docker"

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

# Step 1: Build and push
if (-not $SkipBuild) {
    if ($ApiOnly) {
        Write-Step "1/3" "Building API image only..."
        docker build -t "algiro/ninvoices-api:latest" -f Dockerfile.api ..
        if ($LASTEXITCODE -ne 0) { Write-Host "API build failed." -ForegroundColor Red; exit 1 }
        docker push "algiro/ninvoices-api:latest"
        if ($LASTEXITCODE -ne 0) { Write-Host "API push failed." -ForegroundColor Red; exit 1 }
    } elseif ($WebOnly) {
        Write-Step "1/3" "Building Web image only..."
        docker build `
            -t "algiro/ninvoices-web:latest" `
            --build-arg VITE_KEYCLOAK_URL=$KeycloakUrl `
            --build-arg VITE_KEYCLOAK_REALM=ninvoices `
            --build-arg VITE_KEYCLOAK_CLIENT_ID=ninvoices-web `
            --build-arg VITE_API_URL=$ApiUrl `
            --build-arg VITE_BASE=$Base `
            -f Dockerfile.web ..
        if ($LASTEXITCODE -ne 0) { Write-Host "Web build failed." -ForegroundColor Red; exit 1 }
        docker push "algiro/ninvoices-web:latest"
        if ($LASTEXITCODE -ne 0) { Write-Host "Web push failed." -ForegroundColor Red; exit 1 }
    } else {
        Write-Step "1/3" "Building and pushing all images..."
        & .\build-and-push.ps1 -KeycloakUrl $KeycloakUrl -ApiUrl $ApiUrl -Base $Base -Version "latest"
        if ($LASTEXITCODE -ne 0) { Write-Host "Build and push failed." -ForegroundColor Red; exit 1 }
    }
    Write-Host "Build and push completed." -ForegroundColor Green
} else {
    Write-Host "`nSkipping build (using existing images on Docker Hub)" -ForegroundColor Gray
}

# Step 2: Pull and restart on remote
Write-Step "2/3" "Pulling and restarting on remote server..."

if ($ApiOnly) {
    Invoke-Remote "cd $REMOTE_DIR && docker compose pull api && docker compose up -d api"
} elseif ($WebOnly) {
    Invoke-Remote "cd $REMOTE_DIR && docker compose pull web && docker compose up -d web"
} else {
    Invoke-Remote "cd $REMOTE_DIR && docker compose pull api web && docker compose up -d api web"
}

Write-Host "Containers restarted." -ForegroundColor Green

# Step 3: Verify
Write-Step "3/3" "Verifying deployment..."
Start-Sleep -Seconds 5

Invoke-Remote "cd $REMOTE_DIR && docker compose ps api web"

# Quick health check
$apiStatus = wsl ssh $SSH_HOST "curl -s -o /dev/null -w '%{http_code}' -k 'https://localhost/nInvoices/api/customers'" 2>$null
$webStatus = wsl ssh $SSH_HOST "curl -s -o /dev/null -w '%{http_code}' -k 'https://localhost/nInvoices/'" 2>$null

Write-Host "`nHealth Check:" -ForegroundColor Yellow
Write-Host "  Web:  HTTP $webStatus $(if ($webStatus -eq '200') {'OK'} else {'ISSUE'})" -ForegroundColor $(if ($webStatus -eq '200') {'Green'} else {'Red'})
Write-Host "  API:  HTTP $apiStatus $(if ($apiStatus -eq '401') {'OK (auth required)'} else {'ISSUE'})" -ForegroundColor $(if ($apiStatus -eq '401') {'Green'} else {'Red'})

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "   Deployment Complete!" -ForegroundColor Green
Write-Host "================================================================`n" -ForegroundColor Cyan
