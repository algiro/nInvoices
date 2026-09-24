#!/usr/bin/env bash
#
# Deploy nInvoices to production (Linux port of deploy.ps1)
#
# Builds the Docker images locally, pushes them to Docker Hub, then pulls and
# restarts the stack on the production server over SSH. With --migrate it also
# applies the idempotent SQL in docker/migrations-postgres/ to the prod database.
#
# Requirements on the machine you run this from:
#   - docker (with buildx) and a Docker Hub login (docker login)
#   - ssh access to the production host (SSH alias, default: he-it-tudes)
#   - --migrate applies docker/migrations-postgres/*.sql to the prod DB over SSH
#     (no local .NET needed; see migrations-postgres/README.md)
#
# Production topology (https://it-tudes.tech/nInvoices):
#   - shared nginx on the host terminates TLS and routes
#       /nInvoices/      -> ninvoices web container
#       /nInvoices/api/  -> ninvoices api container
#   - shared Keycloak is served at https://it-tudes.tech/realms/ninvoices
#   - the server holds its own compose file in $REMOTE_DIR (default ~/docker)
#     with services named "api" and "web"
#
# Usage:
#   ./deploy.sh                 # build + push + deploy web & api
#   ./deploy.sh --skip-build    # just pull & restart on the server
#   ./deploy.sh --api-only      # rebuild/deploy the API only
#   ./deploy.sh --web-only      # rebuild/deploy the web frontend only
#   ./deploy.sh --no-push       # build locally but do not push (dry run-ish)
#   ./deploy.sh --migrate       # also apply pending DB migrations (idempotent)
#
# Config (override via environment):
#   SSH_HOST=he-it-tudes  REMOTE_DIR=~/docker
#   DOCKER_USERNAME=algiro  IMAGE_TAG=latest
#   KEYCLOAK_URL=https://it-tudes.tech  API_URL=/nInvoices  BASE=/nInvoices
#   KEYCLOAK_REALM=ninvoices  KEYCLOAK_CLIENT_ID=ninvoices-web
#   PG_CONTAINER=ninvoices-postgres-prod  PG_USER=ninvoices_user  PG_DB=ninvoices_db
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

# ---- config -----------------------------------------------------------------
SSH_HOST="${SSH_HOST:-he-it-tudes}"
REMOTE_DIR="${REMOTE_DIR:-~/docker}"
DOCKER_USERNAME="${DOCKER_USERNAME:-algiro}"
IMAGE_TAG="${IMAGE_TAG:-latest}"

KEYCLOAK_URL="${KEYCLOAK_URL:-https://it-tudes.tech}"
API_URL="${API_URL:-/nInvoices}"
BASE="${BASE:-/nInvoices}"
KEYCLOAK_REALM="${KEYCLOAK_REALM:-ninvoices}"
KEYCLOAK_CLIENT_ID="${KEYCLOAK_CLIENT_ID:-ninvoices-web}"

PG_CONTAINER="${PG_CONTAINER:-ninvoices-postgres-prod}"
PG_USER="${PG_USER:-ninvoices_user}"
PG_DB="${PG_DB:-ninvoices_db}"

API_IMAGE="${DOCKER_USERNAME}/ninvoices-api"
WEB_IMAGE="${DOCKER_USERNAME}/ninvoices-web"

# ---- ui helpers -----------------------------------------------------------------
if [[ -t 1 ]]; then C_Y=$'\e[33m'; C_G=$'\e[32m'; C_R=$'\e[31m'; C_C=$'\e[36m'; C_GR=$'\e[90m'; C_0=$'\e[0m'
else C_Y=; C_G=; C_R=; C_C=; C_GR=; C_0=; fi
step() { printf '\n%s[%s]%s %s\n%s\n' "$C_Y" "$1" "$C_0" "$2" "${C_GR}--------------------------------------------------${C_0}"; }
info() { printf '%s%s%s\n' "$C_GR" "$*" "$C_0"; }
ok()   { printf '%s%s%s\n' "$C_G" "$*" "$C_0"; }
die()  { printf '%s%s%s\n' "$C_R" "$*" "$C_0" >&2; exit 1; }

# ---- args -----------------------------------------------------------------
SKIP_BUILD=0 API_ONLY=0 WEB_ONLY=0 NO_PUSH=0 MIGRATE=0
for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=1 ;;
    --api-only)   API_ONLY=1 ;;
    --web-only)   WEB_ONLY=1 ;;
    --no-push)    NO_PUSH=1 ;;
    --migrate)    MIGRATE=1 ;;
    -h|--help)    sed -n '2,37p' "$0"; exit 0 ;;
    *) die "Unknown option: $arg (try --help)" ;;
  esac
done
[[ $API_ONLY -eq 1 && $WEB_ONLY -eq 1 ]] && die "--api-only and --web-only are mutually exclusive"

remote() {
  # shellcheck disable=SC2029  (we want client-side expansion of the command string)
  ssh "$SSH_HOST" "$1" || die "Remote command failed: $1"
}

# Fail fast if the deploy target is unreachable (before spending time on a build).
ssh -o BatchMode=yes -o ConnectTimeout=8 "$SSH_HOST" true 2>/dev/null || die \
"Cannot SSH to '$SSH_HOST'.
  - add it to ~/.ssh/config, or
  - run with the real host:  SSH_HOST=user@host ./deploy.sh $*
  (deploy.ps1 uses WSL's ssh config; 'wsl -- ssh -G $SSH_HOST' shows its hostname/user/key)"

# ---- 1: build & push -----------------------------------------------------------------
if [[ $SKIP_BUILD -eq 0 ]]; then
  push_flag=(); [[ $NO_PUSH -eq 1 ]] && push_flag=(--no-push)

  if [[ $API_ONLY -eq 1 ]]; then
    step "1/4" "Building API image only..."
    docker build -t "${API_IMAGE}:${IMAGE_TAG}" -f Dockerfile.api .. || die "API build failed."
    [[ $NO_PUSH -eq 0 ]] && { docker push "${API_IMAGE}:${IMAGE_TAG}" || die "API push failed."; }
  elif [[ $WEB_ONLY -eq 1 ]]; then
    step "1/4" "Building Web image only..."
    docker build \
      -t "${WEB_IMAGE}:${IMAGE_TAG}" \
      --build-arg "VITE_KEYCLOAK_URL=${KEYCLOAK_URL}" \
      --build-arg "VITE_KEYCLOAK_REALM=${KEYCLOAK_REALM}" \
      --build-arg "VITE_KEYCLOAK_CLIENT_ID=${KEYCLOAK_CLIENT_ID}" \
      --build-arg "VITE_API_URL=${API_URL}" \
      --build-arg "VITE_BASE=${BASE}" \
      -f Dockerfile.web .. || die "Web build failed."
    [[ $NO_PUSH -eq 0 ]] && { docker push "${WEB_IMAGE}:${IMAGE_TAG}" || die "Web push failed."; }
  else
    step "1/4" "Building and pushing all images..."
    KEYCLOAK_URL="$KEYCLOAK_URL" API_URL="$API_URL" BASE="$BASE" \
    KEYCLOAK_REALM="$KEYCLOAK_REALM" KEYCLOAK_CLIENT_ID="$KEYCLOAK_CLIENT_ID" \
    DOCKER_USERNAME="$DOCKER_USERNAME" VERSION="$IMAGE_TAG" \
      ./build-and-push.sh "${push_flag[@]}" || die "Build and push failed."
  fi
  ok "Build step completed."
else
  info "Skipping build (using existing images: ${IMAGE_TAG})"
fi

# ---- 2: apply DB migrations (optional) -----------------------------------------------------------------
# NOTE: the EF Core migrations in this repo are SQLite-scaffolded and cannot be
# applied to PostgreSQL with `dotnet ef` (it throws on the AddInvoiceSequence
# InsertData). Instead we apply the hand-written idempotent files in
# migrations-postgres/  (see migrations-postgres/README.md).
if [[ $MIGRATE -eq 1 ]]; then
  step "2/4" "Applying PostgreSQL migrations from migrations-postgres/ ..."
  shopt -s nullglob
  sql_files=(migrations-postgres/*.sql)
  shopt -u nullglob
  [[ ${#sql_files[@]} -gt 0 ]] || die "No .sql files found in migrations-postgres/"
  for f in $(printf '%s\n' "${sql_files[@]}" | sort); do
    info "  -> $f"
    ssh "$SSH_HOST" "docker exec -i ${PG_CONTAINER} psql -v ON_ERROR_STOP=1 -U ${PG_USER} -d ${PG_DB}" < "$f" \
      || die "Migration failed on $f - database left partially updated."
  done
  ok "PostgreSQL migrations applied."
else
  info "Skipping migrations (pass --migrate to apply migrations-postgres/*.sql)."
fi

# ---- 3: pull & restart on the server -----------------------------------------------------------------
step "3/4" "Pulling and restarting on ${SSH_HOST}:${REMOTE_DIR}..."
if [[ $API_ONLY -eq 1 ]]; then
  remote "cd ${REMOTE_DIR} && docker compose pull api && docker compose up -d"
elif [[ $WEB_ONLY -eq 1 ]]; then
  remote "cd ${REMOTE_DIR} && docker compose pull web && docker compose up -d"
else
  remote "cd ${REMOTE_DIR} && docker compose pull api web && docker compose up -d"
fi
# Recreated containers get new internal IPs; the shared proxy keeps the old ones
# until it reloads, which shows up as 502s (see infra hosts/ittudes/DEPLOY.md).
remote "docker exec ninvoices-nginx-prod nginx -s reload"
ok "Containers restarted."

# ---- 4: verify -----------------------------------------------------------------
step "4/4" "Verifying deployment..."
sleep 15
remote "cd ${REMOTE_DIR} && docker compose ps"

code() { ssh "$SSH_HOST" "curl -s -o /dev/null -w '%{http_code}' -k '$1'" 2>/dev/null || echo "000"; }
health=$(code "https://localhost${BASE}/api/health")
web=$(code "https://localhost${BASE}/")
api=$(code "https://localhost${BASE}/api/customers")

printf '\n%sHealth check:%s\n' "$C_Y" "$C_0"
[[ "$web"    == "200" ]] && ok   "  Web        HTTP $web  OK"                 || printf '%s  Web        HTTP %s  CHECK%s\n' "$C_R" "$web" "$C_0"
[[ "$health" == "200" ]] && ok   "  API health HTTP $health  OK"              || printf '%s  API health HTTP %s  CHECK%s\n' "$C_R" "$health" "$C_0"
[[ "$api" == "401" || "$api" == "403" ]] && ok "  API auth   HTTP $api  OK (auth enforced)" \
                                            || printf '%s  API auth   HTTP %s  CHECK%s\n' "$C_R" "$api" "$C_0"

printf '\n%s================================================================%s\n' "$C_C" "$C_0"
ok   "   Deployment complete."
printf '%s   https://it-tudes.tech%s%s\n' "$C_C" "$BASE" "$C_0"
printf '%s================================================================%s\n\n' "$C_C" "$C_0"
