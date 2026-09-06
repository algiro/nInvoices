#!/usr/bin/env bash
#
# nInvoices PostgreSQL restore (Linux port of restore-database.ps1)
#
# Restores the application database from a pg_dump backup (.sql or .sql.gz).
# Takes a pre-restore safety backup, drops existing connections, recreates the
# database, then loads the dump.
#
# Run this ON the server (or with DOCKER_HOST pointed at it).
#
# Usage:
#   ./restore-database.sh ./volumes/backups/ninvoices-backup-2026-09-06-12-00-00.sql.gz
#   ./restore-database.sh <file> --container ninvoices-postgres-prod --database ninvoices_db --user ninvoices_user
#   FORCE=1 ./restore-database.sh <file>      # skip the interactive confirmation
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

CONTAINER="${PG_CONTAINER:-ninvoices-postgres-prod}"
DATABASE="${PG_DB:-ninvoices_db}"
USERNAME="${PG_USER:-ninvoices_user}"
BACKUP_FILE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --container) CONTAINER="$2"; shift 2 ;;
    --database)  DATABASE="$2";  shift 2 ;;
    --user)      USERNAME="$2";  shift 2 ;;
    -h|--help)   sed -n '2,15p' "$0"; exit 0 ;;
    -*)          echo "Unknown option: $1" >&2; exit 1 ;;
    *)           BACKUP_FILE="$1"; shift ;;
  esac
done

if [[ -t 1 ]]; then C_G=$'\e[32m'; C_Y=$'\e[33m'; C_R=$'\e[31m'; C_C=$'\e[36m'; C_0=$'\e[0m'; else C_G=; C_Y=; C_R=; C_C=; C_0=; fi
ok()  { printf '%s%s%s\n' "$C_G" "$*" "$C_0"; }
die() { printf '%s%s%s\n' "$C_R" "$*" "$C_0" >&2; exit 1; }

printf '%s========================================%s\n' "$C_C" "$C_0"
printf '%snInvoices Database Restore%s\n' "$C_C" "$C_0"
printf '%s========================================%s\n\n' "$C_C" "$C_0"

[[ -n "$BACKUP_FILE" ]] || die "Usage: ./restore-database.sh <backup-file> [--container N] [--database N] [--user N]"
[[ -f "$BACKUP_FILE" ]] || die "Backup file not found: $BACKUP_FILE"

printf 'Container:   %s\n' "$CONTAINER"
printf 'Database:    %s\n' "$DATABASE"
printf 'Backup file: %s\n\n' "$BACKUP_FILE"

docker ps --filter "name=^/${CONTAINER}$" --format '{{.Names}}' | grep -q "^${CONTAINER}$" \
  || die "Container '${CONTAINER}' is not running."

printf '%s WARNING: this overwrites ALL data in database "%s".%s\n' "$C_R" "$DATABASE" "$C_0"
if [[ "${FORCE:-0}" != "1" ]]; then
  read -rp "Type 'yes' to continue: " confirm
  [[ "$confirm" == "yes" ]] || { echo "Cancelled."; exit 0; }
fi

# decompress if needed
reader=(cat --)
[[ "$BACKUP_FILE" == *.gz ]] && reader=(gzip -dc --)

# pre-restore safety dump
pre="./volumes/backups/pre-restore-$(date +%Y-%m-%d-%H-%M-%S).sql.gz"
mkdir -p "$(dirname "$pre")"
echo "Creating pre-restore backup: $pre"
docker exec "$CONTAINER" pg_dump -U "$USERNAME" -d "$DATABASE" --clean --if-exists --no-owner | gzip -c > "$pre" \
  || die "Pre-restore backup failed - aborting."
ok "Pre-restore backup saved."

echo "Terminating active connections..."
docker exec "$CONTAINER" psql -U "$USERNAME" -d postgres -v ON_ERROR_STOP=1 -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='${DATABASE}' AND pid <> pg_backend_pid();" >/dev/null

echo "Recreating database..."
docker exec "$CONTAINER" psql -U "$USERNAME" -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS \"${DATABASE}\";" >/dev/null
docker exec "$CONTAINER" psql -U "$USERNAME" -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"${DATABASE}\";" >/dev/null

echo "Restoring from backup..."
"${reader[@]}" "$BACKUP_FILE" | docker exec -i "$CONTAINER" psql -v ON_ERROR_STOP=1 -U "$USERNAME" -d "$DATABASE" >/dev/null \
  || die "Restore failed. Recover with:  ./restore-database.sh $pre"

printf '\n'
ok "Restore completed successfully."
printf '%sPre-restore backup kept at: %s%s\n' "$C_Y" "$pre" "$C_0"
