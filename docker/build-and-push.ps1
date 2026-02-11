# Build and Push Images to Docker Hub
# 
# This script builds nInvoices API and Web images and pushes them to Docker Hub
#
# Usage (local/dev):
#   .\build-and-push.ps1
#
# Usage (production):
#   .\build-and-push.ps1 -KeycloakUrl "https://it-tudes.tech" -Base "/nInvoices"
#
# Parameters:
#   -KeycloakUrl      Keycloak base URL (default: http://localhost:8080)
#   -ApiUrl           API base URL, empty = same origin (default: "")
#   -KeycloakRealm    Keycloak realm name (default: ninvoices)
#   -KeycloakClientId Keycloak client ID (default: ninvoices-web)
#   -Base             Base path for the web app (default: /)

param(
    [string]$KeycloakUrl = "http://localhost:8080",
    [string]$ApiUrl = "",
    [string]$KeycloakRealm = "ninvoices",
    [string]$KeycloakClientId = "ninvoices-web",
    [string]$Base = "/",
    [string]$Version = ""
)

# Configuration
$DOCKER_USERNAME = "algiro"
$API_IMAGE = "$DOCKER_USERNAME/ninvoices-api"
$WEB_IMAGE = "$DOCKER_USERNAME/ninvoices-web"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   Building and Pushing nInvoices Images to Docker Hub" -ForegroundColor Cyan
Write-Host "================================================================`n" -ForegroundColor Cyan

# Check if logged into Docker Hub
Write-Host "[1/6] Checking Docker Hub authentication..." -ForegroundColor Yellow
$dockerInfo = docker info 2>&1 | Select-String "Username"
if (-not $dockerInfo) {
    Write-Host "Not logged into Docker Hub. Please login:" -ForegroundColor Red
    Write-Host "   docker login`n" -ForegroundColor White
    docker login
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nDocker login failed. Exiting." -ForegroundColor Red
        exit 1
    }
}
Write-Host "Docker Hub authentication OK`n" -ForegroundColor Green

# Get version tag
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Read-Host "Enter version tag (default: latest)"
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = "latest"
    }
}
$version = $Version

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "[2/6] Building API Image" -ForegroundColor Yellow
Write-Host "================================================================`n" -ForegroundColor Cyan

Write-Host "Image: $API_IMAGE`:$version" -ForegroundColor White
Write-Host "Building...`n" -ForegroundColor Gray

docker build -t "${API_IMAGE}:${version}" -f Dockerfile.api ..
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nAPI build failed. Exiting." -ForegroundColor Red
    exit 1
}

Write-Host "`nAPI image built successfully" -ForegroundColor Green

# Tag as latest if not already
if ($version -ne "latest") {
    Write-Host "Tagging as latest..." -ForegroundColor Gray
    docker tag "${API_IMAGE}:${version}" "${API_IMAGE}:latest"
}

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "[3/6] Building Web Image" -ForegroundColor Yellow
Write-Host "================================================================`n" -ForegroundColor Cyan

Write-Host "Image: $WEB_IMAGE`:$version" -ForegroundColor White
Write-Host "  Keycloak URL: $KeycloakUrl" -ForegroundColor Gray
Write-Host "  API URL: $ApiUrl" -ForegroundColor Gray
Write-Host "  Base path: $Base" -ForegroundColor Gray
Write-Host "Building...`n" -ForegroundColor Gray

docker build `
    -t "${WEB_IMAGE}:${version}" `
    --build-arg VITE_KEYCLOAK_URL=$KeycloakUrl `
    --build-arg VITE_KEYCLOAK_REALM=$KeycloakRealm `
    --build-arg VITE_KEYCLOAK_CLIENT_ID=$KeycloakClientId `
    --build-arg VITE_API_URL=$ApiUrl `
    --build-arg VITE_BASE=$Base `
    -f Dockerfile.web ..

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nWeb build failed. Exiting." -ForegroundColor Red
    exit 1
}

Write-Host "`nWeb image built successfully" -ForegroundColor Green

# Tag as latest if not already
if ($version -ne "latest") {
    Write-Host "Tagging as latest..." -ForegroundColor Gray
    docker tag "${WEB_IMAGE}:${version}" "${WEB_IMAGE}:latest"
}

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "[4/6] Pushing API Image to Docker Hub" -ForegroundColor Yellow
Write-Host "================================================================`n" -ForegroundColor Cyan

Write-Host "Pushing $API_IMAGE`:$version..." -ForegroundColor White
docker push "${API_IMAGE}:${version}"
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nFailed to push API image. Exiting." -ForegroundColor Red
    exit 1
}
Write-Host "API image ($version) pushed successfully" -ForegroundColor Green

if ($version -ne "latest") {
    Write-Host "`nPushing $API_IMAGE`:latest..." -ForegroundColor White
    docker push "${API_IMAGE}:latest"
    Write-Host "API image (latest) pushed successfully" -ForegroundColor Green
}

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "[5/6] Pushing Web Image to Docker Hub" -ForegroundColor Yellow
Write-Host "================================================================`n" -ForegroundColor Cyan

Write-Host "Pushing $WEB_IMAGE`:$version..." -ForegroundColor White
docker push "${WEB_IMAGE}:${version}"
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nFailed to push Web image. Exiting." -ForegroundColor Red
    exit 1
}
Write-Host "Web image ($version) pushed successfully" -ForegroundColor Green

if ($version -ne "latest") {
    Write-Host "`nPushing $WEB_IMAGE`:latest..." -ForegroundColor White
    docker push "${WEB_IMAGE}:latest"
    Write-Host "Web image (latest) pushed successfully" -ForegroundColor Green
}

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "[6/6] SUCCESS - Images Pushed to Docker Hub!" -ForegroundColor Green
Write-Host "================================================================`n" -ForegroundColor Cyan

Write-Host "Images available at Docker Hub:`n" -ForegroundColor Yellow
Write-Host "   API:  https://hub.docker.com/r/$DOCKER_USERNAME/ninvoices-api" -ForegroundColor Cyan
Write-Host "   Web:  https://hub.docker.com/r/$DOCKER_USERNAME/ninvoices-web`n" -ForegroundColor Cyan

Write-Host "Tags pushed:" -ForegroundColor Yellow
Write-Host "   * ${API_IMAGE}:${version}" -ForegroundColor White
Write-Host "   * ${WEB_IMAGE}:${version}" -ForegroundColor White
if ($version -ne "latest") {
    Write-Host "   * ${API_IMAGE}:latest" -ForegroundColor White
    Write-Host "   * ${WEB_IMAGE}:latest" -ForegroundColor White
}

Write-Host "`nImage sizes:" -ForegroundColor Yellow
docker images | Select-String "$DOCKER_USERNAME/ninvoices"

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "Next steps for production deployment:" -ForegroundColor Yellow
Write-Host "================================================================`n" -ForegroundColor Cyan
Write-Host "1. Copy docker-compose.registry.yml to production server" -ForegroundColor White
Write-Host "2. Copy .env file and configure production settings" -ForegroundColor White
Write-Host "3. Copy nginx.prod.conf if using Nginx" -ForegroundColor White
Write-Host "4. Copy init-scripts/ directory" -ForegroundColor White
Write-Host "5. On production server run:" -ForegroundColor White
Write-Host "   docker-compose pull" -ForegroundColor Cyan
Write-Host "   docker-compose up -d`n" -ForegroundColor Cyan

Write-Host "For detailed instructions, see:" -ForegroundColor Gray
Write-Host "   docker/PRODUCTION-DEPLOYMENT.md" -ForegroundColor Gray
Write-Host "   docker/REGISTRY-QUICKSTART.md`n" -ForegroundColor Gray

Write-Host "================================================================`n" -ForegroundColor Cyan
