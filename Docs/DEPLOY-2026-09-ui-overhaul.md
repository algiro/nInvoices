# Deploying the September 2026 UI overhaul to production (he-it-tudes)

Covers everything on `feature/partial_day` (partial days and notes, calendar/list time entry,
design system and app shell, template editor with live preview, reworked screens, template
activation fix, headless Chrome baked into the API image).

## What production runs today (checked 2026-09-24, read-only)

| | |
|---|---|
| API / web images | `algiro/ninvoices-api:latest`, `algiro/ninvoices-web:latest`, built 2026-09-06 20:50, **before** the Aspire commits |
| Compose | `~/docker/docker-compose.yml` on the server, services `api` and `web`, shared `ninvoices-nginx-prod` in front; `.env` has `IMAGE_TAG=latest` |
| Database | PostgreSQL 17, 3 customers, 11 invoices, 209 work days |
| Schema | already has everything this release needs: `WorkDays.HoursWorked` and `Notes`, `Customers.Locale`, `ImageAssets`, `Projects`, `WorkDayProjects` |
| Customer locales | all set (`en-US`, `es-ES`) |
| Templates | at most one active template per customer and type |

## Database: no changes needed

This release has **no schema changes**. The last one (Projects, 2026-09-06) is already
applied. Don't run `deploy.sh --migrate`: it would only re-apply that idempotent file.

Two notes, neither blocking:

- `__EFMigrationsHistory` has no rows for `AddHoursWorkedToWorkDay`, `AddLocaleToCustomer` and
  `AddImageAssets`, although their columns and tables exist. It's bookkeeping only. PostgreSQL
  is migrated with the hand-written SQL in `docker/migrations-postgres/`, not with EF.
- Production has no unique index on active invoice templates (the local SQLite database does).
  The new activation code enforces "one active template per customer and type" itself, so
  nothing is needed.

## Fixes this deploy depends on (already made, not committed yet)

1. **`docker/Dockerfile.api`** copies `nInvoices.ServiceDefaults.csproj` before `dotnet restore`.
   Without it the API image fails to build: the API references that project since the Aspire
   work, and the images in production predate it.
2. **`.dockerignore`** excludes `**/bin`, `**/obj`, local databases and logs. Without it the
   build context includes Windows `obj` restore files, which can break the Linux build, and
   about 500 MB of Windows Chrome from `src/nInvoices.Api/bin`.
3. **`docker/deploy.ps1` and `docker/deploy.sh`** reload `ninvoices-nginx-prod` after recreating
   the containers. They get new internal IPs and the shared proxy keeps the old ones until it
   reloads, which shows up as 502s. `deploy.sh` also uses the `he-it-tudes` SSH alias again: the
   hard-coded `algiro@it-tudes.tech` fails from WSL with "Host key verification failed".
4. **`.gitattributes`** keeps `*.sh` files with LF line endings. With `core.autocrlf=true` they
   were checked out with CRLF, and bash on the server failed with
   `: invalid option nameipefail`.

## Steps

Docker Desktop must be running and logged in to Docker Hub (`docker login`). The `ssh` commands
below run in WSL, where the `he-it-tudes` alias is defined.

1. **Commit and push** the branch, so what runs in production exists on GitHub:
   ```bash
   git push -u origin feature/partial_day
   ```
   Merge it into `main` (PR) before or right after the deploy.

2. **Back up the database** (a gzipped `pg_dump`; the script keeps the 10 most recent). This
   script has never run on the server, and its default `./volumes/backups` would land under the
   home directory when run over SSH, so give it an explicit directory:
   ```bash
   ssh he-it-tudes 'BACKUP_DIR=~/backups/ninvoices bash -s' < backup-database.sh
   ssh he-it-tudes 'ls -lh ~/backups/ninvoices'
   ```
   Check the file is there and isn't a few bytes before going on.

3. **Keep the current images for rollback.** Everything is `:latest`, so without this the
   previous build is gone once the new one is pulled:
   ```bash
   ssh he-it-tudes 'docker tag algiro/ninvoices-api:latest algiro/ninvoices-api:pre-2026-09-24 && docker tag algiro/ninvoices-web:latest algiro/ninvoices-web:pre-2026-09-24'
   ```

4. **Build, push and deploy.** On Windows use the PowerShell script from `docker\`: it runs
   `docker` against Docker Desktop directly and reaches the server with `wsl ssh he-it-tudes`.
   ```powershell
   .\deploy.ps1
   ```
   `deploy.sh` only works in a WSL distro that has Docker Desktop's WSL integration switched on
   (Settings › Resources › WSL integration); without it, `docker` inside WSL can't find
   `/var/run/docker.sock`. Don't pass `--migrate` if you use it.
   The API build now downloads Linux headless Chrome into the image
   (`RUN dotnet nInvoices.Api.dll --download-chrome`). Expect it to take several minutes,
   depending on the connection. The web image is built with `/nInvoices` as base path and
   the it-tudes Keycloak settings, the same as before.

5. **Check** (the script prints the first three):
   - `https://it-tudes.tech/nInvoices/` loads, sign-in works, the sidebar shell appears.
   - `https://it-tudes.tech/nInvoices/api/health` returns `Healthy`.
   - An existing invoice opens with its document preview. Its PDF and monthly report
     download within a few seconds; the first one no longer waits for a Chrome download.
   - A customer's template opens in the full-page editor with a live preview. Don't save.
   - Generate a test invoice for a test period if you want an end-to-end check, then delete it.

6. **Clean up** once it's confirmed working (19 GB free on `/` at 75% use today):
   ```bash
   ssh he-it-tudes 'docker image prune -f'
   ```
   Remove the `pre-2026-09-24` tags a few days later.

## Rollback

There are no database changes, so rolling back is only the images:

```bash
ssh he-it-tudes 'docker tag algiro/ninvoices-api:pre-2026-09-24 algiro/ninvoices-api:latest && docker tag algiro/ninvoices-web:pre-2026-09-24 algiro/ninvoices-web:latest && cd ~/docker && docker compose up -d api web && docker exec ninvoices-nginx-prod nginx -s reload'
```

## Found while checking, not part of this deploy

- `ninvoices-api-prod`, `ninvoices-web-prod` and `ninvoices-nginx-prod` report **unhealthy**
  in `docker ps`, although the site and `/api/health` answer 200. Their health checks run
  `wget` against `localhost:8080/api/health`, `localhost:3000` and `localhost:80/health`, but
  the health log is empty. Worth a look: compose or Docker will act on unhealthy states if
  a restart policy or dependency ever relies on them.
- Tag images by commit as well as `:latest`, so production shows which commit it runs and
  older builds stay available for rollback. ChefNet on the same host already does this.
