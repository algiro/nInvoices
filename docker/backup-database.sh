#!/usr/bin/env bash
#
# nInvoices PostgreSQL backup (Linux port of backup-database.ps1)
#
# Creates a timestamped, gzip-compressed pg_dump of the application database from
# a running Postgres container, and keeps the 10 most recent backups.
#
# Run this ON the server (or with DOCKER_HOST pointed at it).
#
# Usage:
#   ./backup-database.sh
#   ./backup-database.sh --container ninvoices-postgres-prod --database ninvoices_db --user ninvoices_user --dir ./volumes/backups
#
# Remote one-liner from your workstation:
#   ssh your-server 'bash -s' < backup-database.sh
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

CONTAINER="${PG_CONTAINER:-ninvoices-postgres-prod}"
DATABASE="${PG_DB:-ninvoices_db}"
USERNAME="${PG_USER:-ninvoices_user}"
BACKUP_DIR="${BACKUP_DIR:-./volumes/backups}"
KEEP="${KEEP:-10}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --container) CONTAINER="$2"; shift 2 ;;
    --database)  DATABASE="$2";  shift 2 ;;
    --user)      USERNAME="$2";  shift 2 ;;
    --dir)       BACKUP_DIR="$2"; shift 2 ;;
    --keep)      KEEP="$2"; shift 2 ;;
    -h|--help)   sed -n '2,16p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

if [[ -t 1 ]]; then C_G=$'\e[32m'; C_Y=$'\e[33m'; C_R=$'\e[31m'; C_GR=$'\e[90m'; C_0=$'\e[0m'; else C_G=; C_Y=; C_R=; C_GR=; C_0=; fi
ok()  { printf '%s%s%s\n' "$C_G" "$*" "$C_0"; }
die() { printf '%s%s%s\n' "$C_R" "$*" "$C_0" >&2; exit 1; }

printf '%s========================================%s\n' "$C_Y" "$C_0"
printf '%snInvoices Database Backup%s\n' "$C_Y" "$C_0"
printf '%s========================================%s\n\n' "$C_Y" "$C_0"

docker ps --filter "name=^/${CONTAINER}$" --format '{{.Names}}' | grep -q "^${CONTAINER}$" \
  || die "Container '${CONTAINER}' is not running."

mkdir -p "$BACKUP_DIR"
timestamp="$(date +%Y-%m-%d-%H-%M-%S)"
backup_file="${BACKUP_DIR}/ninvoices-backup-${timestamp}.sql.gz"

printf 'Container:   %s\n' "$CONTAINER"
printf 'Database:    %s\n' "$DATABASE"
printf 'Backup file: %s\n\n' "$backup_file"

echo "Creating backup..."
# --clean --if-exists makes the dump safe to restore over an existing database.
docker exec "$CONTAINER" pg_dump -U "$USERNAME" -d "$DATABASE" --clean --if-exists --no-owner \
  | gzip -c > "$backup_file" \
  || { rm -f "$backup_file"; die "pg_dump failed."; }

size="$(du -h "$backup_file" | cut -f1)"
printf '\n'
ok "Backup completed: ${backup_file} (${size})"

printf '\n%sRecent backups:%s\n' "$C_Y" "$C_0"
ls -1t "${BACKUP_DIR}"/ninvoices-backup-*.sql.gz 2>/dev/null | head -n 5 | while read -r f; do
  printf '  %s%s  %s%s\n' "$C_GR" "$(basename "$f")" "$(du -h "$f" | cut -f1)" "$C_0"
done

# rotate: keep the newest $KEEP
mapfile -t old < <(ls -1t "${BACKUP_DIR}"/ninvoices-backup-*.sql.gz 2>/dev/null | tail -n +"$((KEEP + 1))")
if [[ ${#old[@]} -gt 0 ]]; then
  printf '\n%sRemoving %d old backup(s) (keeping %d)...%s\n' "$C_Y" "${#old[@]}" "$KEEP" "$C_0"
  for f in "${old[@]}"; do rm -f "$f" && printf '  removed %s\n' "$(basename "$f")"; done
fi
