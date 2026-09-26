# Quick start with Docker

Run the full nInvoices stack on your machine: **PostgreSQL**, **Keycloak** (sign-in), the **API** and the **web app**, all in Docker. It takes about 10 minutes, most of it the first image build.

> Just want to try the app without Docker or a login? Use the [SQLite quick start in the README](README.md#quick-start) instead.

## What you need

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) or Docker Engine with the Compose plugin (`docker compose version` should work)
- About 4 GB of free RAM and 5 GB of disk space
- Free ports **3000** (web), **5000** (API), **8080** (Keycloak) and **5432** (PostgreSQL). Other ports can be set in `.env`, see [Ports](#ports).

## 1. Get the code and create your settings

```bash
git clone https://github.com/algiro/nInvoices.git
cd nInvoices/docker
cp ../.env.example .env
```

Open `docker/.env` and **choose** two passwords. They don't exist yet: Docker creates PostgreSQL and Keycloak with them on the first start.

```env
POSTGRES_PASSWORD=ChooseADatabasePassword1
KEYCLOAK_ADMIN_PASSWORD=ChooseAKeycloakAdminPassword2
```

> **Use letters and digits only.** Special characters in these passwords break the connection strings the services build from them.

Leave everything else as it is for a local setup. `.env` holds your passwords: never commit it (it's already in `.gitignore`).

## 2. Start everything

```bash
docker compose -f docker-compose.dev.yml up -d --build
```

The first run builds the API and web images and downloads PostgreSQL and Keycloak, which takes a few minutes. Keycloak needs another minute or two before it's ready. Check progress with:

```bash
docker compose -f docker-compose.dev.yml ps
```

All four services (`ninvoices-postgres-dev`, `ninvoices-keycloak-dev`, `ninvoices-api-dev`, `ninvoices-web-dev`) should be **running**, and the first three **healthy**.

On the first start:
- Keycloak imports the `ninvoices` realm from [`keycloak/realm-export.json`](docker/keycloak/realm-export.json), with the login clients and a first user.
- The API creates the database schema, because the database is empty.

## 3. Sign in

Open **http://localhost:3000**. You're redirected to the nInvoices sign-in page.

| | |
|---|---|
| **Username** | `admin` |
| **Password** | `admin123` |

You're asked to **choose a new password** right away; after that you land on the dashboard.

## 4. Create your first invoice

1. **Customers → New customer:** name, VAT or fiscal ID, address and document language.
2. On the customer's page:
   - **Rates:** add a daily, hourly or monthly rate.
   - **Taxes:** add VAT or any other tax, if you charge one.
   - **Invoice templates:** create an invoice template and activate it. A new template starts from a working sample, so you can save it as is and adapt it later.
   - **Monthly reports:** do the same for the timesheet template, if you send timesheets.
3. **New invoice:** pick the customer and month, check the days (weekdays and public holidays are already filled in), add expenses, look at the preview, and **Generate**.

The first PDF takes a few extra seconds: the API downloads the headless Chrome it renders with, once.

## Everyday commands

Run these from the `docker` folder.

```bash
docker compose -f docker-compose.dev.yml ps                # status
docker compose -f docker-compose.dev.yml logs -f api       # follow the API log
docker compose -f docker-compose.dev.yml restart api       # restart one service
docker compose -f docker-compose.dev.yml down              # stop (your data is kept)
docker compose -f docker-compose.dev.yml up -d --build     # start again, rebuilding after an update
```

Your data lives in `docker/volumes/` (PostgreSQL, Keycloak and logs). To **start over from scratch**, stop the stack and delete that folder:

```bash
docker compose -f docker-compose.dev.yml down
rm -rf volumes/          # PowerShell: Remove-Item -Recurse -Force volumes
```

**Backups:** `./backup-database.sh --container ninvoices-postgres-dev` dumps the database to `docker/volumes/backups/` and keeps the 10 most recent. You can also export customers and invoices as JSON from **Settings → Backup and transfer**.

## Users

Manage users in the Keycloak admin console at **http://localhost:8080**. Sign in with `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD` from your `.env`, then switch to the **ninvoices** realm.

To add a user: **Users → Add user**, then **Credentials → Set password**, then **Role mapping → Assign role** `user` (or `admin`).

## Ports

Change these in `docker/.env` if the defaults are taken, then run `up -d --build` again:

| Service | Default | `.env` variable |
|---|---|---|
| Web app | http://localhost:3000 | `WEB_PORT` |
| API | http://localhost:5000 | `API_PORT`, and `API_URL` to match |
| Keycloak | http://localhost:8080 | `KEYCLOAK_PORT`, and `KEYCLOAK_URL` to match |
| PostgreSQL | localhost:5432 | `POSTGRES_PORT` |

> If you move the web app to a port other than 3000, 5173 or 5174, also add it to the `ninvoices-web` client's *Valid redirect URIs* and *Web origins* in Keycloak, and to `CORS_ORIGINS` in `.env`.

## Troubleshooting

**The sign-in page doesn't load, or you get a 502/504.** Keycloak is still starting. Wait a minute and check `docker compose -f docker-compose.dev.yml logs keycloak` for the "started" message.

**"Port is already allocated".** Another program uses one of the ports; change it as described in [Ports](#ports).

**You're signed in but the dashboard stays empty, or the API answers 401.** This usually happens after you reset `volumes/`: the browser still has a sign-in from the old Keycloak. Sign out from the user menu, or clear the site data for `localhost`, and sign in again.

**The API can't connect to PostgreSQL.** Check that `POSTGRES_PASSWORD` has only letters and digits. If you changed it after the first start, the database still has the old one: reset `volumes/` or change it back.

More help: [`docker/TROUBLESHOOTING.md`](docker/TROUBLESHOOTING.md).

## Going to production

This setup is meant for your own machine. For a server, use `docker-compose.prod.yml` (or `docker-compose.registry.yml` with pre-built images), behind HTTPS. See [`docker/README.md`](docker/README.md) and [`docker/PRODUCTION-DEPLOYMENT.md`](docker/PRODUCTION-DEPLOYMENT.md). In particular:

- **Create a realm for your domain rather than reusing the development one.** The bundled realm only allows `localhost` addresses, lets anyone register, and comes with a well-known first password.
- **Choose strong passwords** in `.env`, still letters and digits only.
- **First start:** the API creates the schema on an empty database.
- **Upgrades:** apply the SQL scripts in [`docker/migrations-postgres/`](docker/migrations-postgres/README.md). They are safe to run again.
