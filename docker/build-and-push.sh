#!/usr/bin/env bash
#
# Build and push nInvoices images to Docker Hub (Linux port of build-and-push.ps1)
#
# Usage:
#   ./build-and-push.sh                 # interactive: prompts for a version tag
#   VERSION=latest ./build-and-push.sh  # non-interactive
#   ./build-and-push.sh --no-push       # build only, don't push
#
# Config (override via environment):
#   DOCKER_USERNAME=algiro
#   VERSION=                  # empty -> prompt (default "latest")
#   KEYCLOAK_URL=http://localhost:8080
#   API_URL=                  # API base path, empty = same origin
#   KEYCLOAK_REALM=ninvoices
#   KEYCLOAK_CLIENT_ID=ninvoices-web
#   BASE=/                    # web app base path (use /nInvoices for production)
#
# Production example:
#   KEYCLOAK_URL=https://your-domain.com API_URL=/nInvoices BASE=/nInvoices \
#   VERSION=latest ./build-and-push.sh
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

DOCKER_USERNAME="${DOCKER_USERNAME:-algiro}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8080}"
API_URL="${API_URL:-}"
KEYCLOAK_REALM="${KEYCLOAK_REALM:-ninvoices}"
KEYCLOAK_CLIENT_ID="${KEYCLOAK_CLIENT_ID:-ninvoices-web}"
BASE="${BASE:-/}"
VERSION="${VERSION:-}"

API_IMAGE="${DOCKER_USERNAME}/ninvoices-api"
WEB_IMAGE="${DOCKER_USERNAME}/ninvoices-web"

if [[ -t 1 ]]; then C_Y=$'\e[33m'; C_G=$'\e[32m'; C_R=$'\e[31m'; C_C=$'\e[36m'; C_GR=$'\e[90m'; C_0=$'\e[0m'
else C_Y=; C_G=; C_R=; C_C=; C_GR=; C_0=; fi
head() { printf '\n%s================================================================%s\n%s%s%s\n%s================================================================%s\n' "$C_C" "$C_0" "$C_Y" "$1" "$C_0" "$C_C" "$C_0"; }
info() { printf '%s%s%s\n' "$C_GR" "$*" "$C_0"; }
ok()   { printf '%s%s%s\n' "$C_G" "$*" "$C_0"; }
die()  { printf '%s%s%s\n' "$C_R" "$*" "$C_0" >&2; exit 1; }

NO_PUSH=0
for arg in "$@"; do
  case "$arg" in
    --no-push) NO_PUSH=1 ;;
    -h|--help) sed -n '2,22p' "$0"; exit 0 ;;
    *) die "Unknown option: $arg" ;;
  esac
done

head "Building nInvoices images (${DOCKER_USERNAME}/*)"

# [1] Docker Hub auth (only needed when pushing)
if [[ $NO_PUSH -eq 0 ]]; then
  info "[1/6] Checking Docker Hub authentication..."
  if ! docker info 2>/dev/null | grep -q "Username:"; then
    info "Not logged in. Running 'docker login'..."
    docker login || die "docker login failed."
  fi
  ok "Docker Hub authentication OK"
else
  info "[1/6] --no-push: skipping Docker Hub authentication"
fi

# version tag
if [[ -z "$VERSION" ]]; then
  read -rp "Enter version tag (default: latest): " VERSION || true
  VERSION="${VERSION:-latest}"
fi
info "Version tag: ${VERSION}"

# [2] build API
head "[2/6] Building API image  ${API_IMAGE}:${VERSION}"
docker build -t "${API_IMAGE}:${VERSION}" -f Dockerfile.api .. || die "API build failed."
[[ "$VERSION" != "latest" ]] && docker tag "${API_IMAGE}:${VERSION}" "${API_IMAGE}:latest"
ok "API image built"

# [3] build web
head "[3/6] Building Web image  ${WEB_IMAGE}:${VERSION}"
info "  VITE_KEYCLOAK_URL = ${KEYCLOAK_URL}"
info "  VITE_API_URL      = ${API_URL:-(same origin)}"
info "  VITE_BASE         = ${BASE}"
docker build \
  -t "${WEB_IMAGE}:${VERSION}" \
  --build-arg "VITE_KEYCLOAK_URL=${KEYCLOAK_URL}" \
  --build-arg "VITE_KEYCLOAK_REALM=${KEYCLOAK_REALM}" \
  --build-arg "VITE_KEYCLOAK_CLIENT_ID=${KEYCLOAK_CLIENT_ID}" \
  --build-arg "VITE_API_URL=${API_URL}" \
  --build-arg "VITE_BASE=${BASE}" \
  -f Dockerfile.web .. || die "Web build failed."
[[ "$VERSION" != "latest" ]] && docker tag "${WEB_IMAGE}:${VERSION}" "${WEB_IMAGE}:latest"
ok "Web image built"

if [[ $NO_PUSH -eq 1 ]]; then
  head "Done (--no-push). Local images:"
  docker images | grep "${DOCKER_USERNAME}/ninvoices" || true
  exit 0
fi

# [4] push API
head "[4/6] Pushing ${API_IMAGE}:${VERSION}"
docker push "${API_IMAGE}:${VERSION}" || die "Failed to push API image."
[[ "$VERSION" != "latest" ]] && docker push "${API_IMAGE}:latest"
ok "API image pushed"

# [5] push web
head "[5/6] Pushing ${WEB_IMAGE}:${VERSION}"
docker push "${WEB_IMAGE}:${VERSION}" || die "Failed to push Web image."
[[ "$VERSION" != "latest" ]] && docker push "${WEB_IMAGE}:latest"
ok "Web image pushed"

# [6] summary
head "[6/6] SUCCESS - images pushed"
info "Tags:"
info "  ${API_IMAGE}:${VERSION}"
info "  ${WEB_IMAGE}:${VERSION}"
[[ "$VERSION" != "latest" ]] && { info "  ${API_IMAGE}:latest"; info "  ${WEB_IMAGE}:latest"; }
printf '\n'
docker images | grep "${DOCKER_USERNAME}/ninvoices" || true
printf '\n%sNext: deploy with  ./deploy.sh --skip-build%s\n\n' "$C_GR" "$C_0"
