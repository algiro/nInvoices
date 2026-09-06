#!/usr/bin/env bash
#
# One-off SQLite -> PostgreSQL data migration (Linux port / rework of
# migrate-sqlite-to-postgres.ps1, whose data-copy step was never implemented).
#
# Only needed when moving an EXISTING SQLite deployment onto PostgreSQL. A brand
# new PostgreSQL deployment does not need this - start the stack (which runs
# docker/init-scripts/) and then apply docker/migrations-postgres/ via
# `deploy.sh --migrate`.
#
# What this does:
#   1. Build the target schema:  docker/init-scripts/02-ninvoices-schema.sql
#      followed by docker/migrations-postgres/*.sql, piped into the target DB.
#      (`dotnet ef database update` is NOT usable here - the migrations are
#       SQLite-scaffolded and crash under Npgsql.)
#   2. Copy row data with `pgloader` (handles type mapping + sequence resets).
#
# NOTE: docker/init-scripts/02-ninvoices-schema.sql is known to be out of sync
#       with the current EF model. Review the resulting schema before trusting it
#       for a production cut-over.
#
# Usage:
#   ./migrate-sqlite-to-postgres.sh \
#     --sqlite ../src/nInvoices.Api/nInvoices.db \
#     --container ninvoices-postgres-prod --db ninvoices_db --user ninvoices_user \
#     --pg "postgresql://ninvoices_user:PASS@localhost:5432/ninvoices_db"   # for pgloader
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

SQLITE_DB="${SQLITE_DB:-../src/nInvoices.Api/nInvoices.db}"
CONTAINER="${PG_CONTAINER:-ninvoices-postgres-prod}"
PG_DB="${PG_DB:-ninvoices_db}"
PG_USER="${PG_USER:-ninvoices_user}"
PG_URL="${PG_URL:-}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --sqlite)    SQLITE_DB="$2"; shift 2 ;;
    --container) CONTAINER="$2"; shift 2 ;;
    --db)        PG_DB="$2";     shift 2 ;;
    --user)      PG_USER="$2";   shift 2 ;;
    --pg)        PG_URL="$2";    shift 2 ;;
    -h|--help)   sed -n '2,27p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

if [[ -t 1 ]]; then C_G=$'\e[32m'; C_Y=$'\e[33m'; C_R=$'\e[31m'; C_C=$'\e[36m'; C_0=$'\e[0m'; else C_G=; C_Y=; C_R=; C_C=; C_0=; fi
ok()  { printf '%s%s%s\n' "$C_G" "$*" "$C_0"; }
die() { printf '%s%s%s\n' "$C_R" "$*" "$C_0" >&2; exit 1; }

printf '%s========================================%s\n' "$C_C" "$C_0"
printf '%sSQLite -> PostgreSQL migration%s\n' "$C_C" "$C_0"
printf '%s========================================%s\n\n' "$C_C" "$C_0"

[[ -f "$SQLITE_DB" ]] || die "SQLite database not found: $SQLITE_DB"
docker ps --filter "name=^/${CONTAINER}$" --format '{{.Names}}' | grep -q "^${CONTAINER}$" \
  || die "Container '${CONTAINER}' is not running."

printf 'SQLite:    %s\n' "$SQLITE_DB"
printf 'Postgres:  container=%s db=%s user=%s\n\n' "$CONTAINER" "$PG_DB" "$PG_USER"
read -rp "Proceed? (yes/no) " confirm
[[ "$confirm" == "yes" ]] || { echo "Cancelled."; exit 0; }

psql_in() { docker exec -i "$CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB"; }

# ---- step 1: schema -----------------------------------------------------------------
printf '\n%s[1/2] Building PostgreSQL schema...%s\n' "$C_Y" "$C_0"
if [[ -f init-scripts/02-ninvoices-schema.sql ]]; then
  # strip the leading "\c ninvoices_db" meta-command; we target -d explicitly
  grep -v '^\\c ' init-scripts/02-ninvoices-schema.sql | psql_in || die "Base schema failed."
fi
for f in $(ls -1 migrations-postgres/*.sql 2>/dev/null | sort); do
  printf '  -> %s\n' "$f"
  grep -vE '^\\(set|connect) ' "$f" | psql_in || die "Migration $f failed."
done
ok "Schema applied."

# ---- step 2: data -----------------------------------------------------------------
printf '\n%s[2/2] Copying data...%s\n' "$C_Y" "$C_0"
[[ -n "$PG_URL" ]] || die "Pass --pg 'postgresql://user:pass@host:port/db' (reachable from this machine) for the data copy."

sqlite_abs="$(cd "$(dirname "$SQLITE_DB")" && pwd)/$(basename "$SQLITE_DB")"
if command -v pgloader >/dev/null; then
  pgloader --with "data only" --with "reset sequences" --with "quote identifiers" \
    "sqlite://${sqlite_abs}" "$PG_URL" || die "pgloader failed."
else
  info() { printf '%s%s%s\n' "$C_Y" "$*" "$C_0"; }
  info "pgloader not installed - running it from a container instead:"
  docker run --rm --network host -v "${sqlite_abs}:/data/src.db:ro" dimitri/pgloader:latest \
    pgloader --with "data only" --with "reset sequences" --with "quote identifiers" \
    "sqlite:///data/src.db" "$PG_URL" || die "pgloader (container) failed."
fi
ok "Data copied."

printf '\n%sDone.%s Verify row counts, then repoint the app:\n' "$C_G" "$C_0"
printf '  Database__Type=PostgreSQL\n'
printf '  ConnectionStrings__Default=Host=...;Port=5432;Database=%s;Username=%s;Password=...\n' "$PG_DB" "$PG_USER"
