# nInvoices — Production Deployment Guide

A comprehensive guide for deploying nInvoices (.NET 10 + Vue 3 + Keycloak) to a remote server with HTTPS, based on real-world experience and every issue encountered.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Prerequisites](#2-prerequisites)
3. [Server Initial Setup](#3-server-initial-setup)
4. [SSL Certificate (Let's Encrypt)](#4-ssl-certificate-lets-encrypt)
5. [Docker Compose Configuration](#5-docker-compose-configuration)
6. [Nginx Reverse Proxy](#6-nginx-reverse-proxy)
7. [Keycloak Setup](#7-keycloak-setup)
8. [Building Docker Images](#8-building-docker-images)
9. [Database Considerations (SQLite → PostgreSQL)](#9-database-considerations-sqlite--postgresql)
10. [Local Development Setup](#10-local-development-setup)
11. [Redeployment Procedure](#11-redeployment-procedure)
12. [Troubleshooting & Lessons Learned](#12-troubleshooting--lessons-learned)

---

## 1. Architecture Overview

```
Internet → Nginx (SSL termination, port 80/443)
              ├── /nInvoices/api/*  →  API container (port 8080)
              ├── /nInvoices/*      →  Web container (port 80)
              ├── /realms/*         →  Keycloak (port 8080)
              ├── /admin/*          →  Keycloak Admin Console
              └── /resources/*      →  Keycloak static assets
                                         ↓
                                    PostgreSQL (port 5432)
```

**Key design decisions:**
- **Path-based routing** (`/nInvoices/`) — allows hosting multiple apps on the same domain.
- **Nginx as single entry point** — handles SSL, CORS, path rewriting, and proxying to all services.
- **Keycloak at root-level paths** — its admin console and realms endpoints must NOT be under `/nInvoices/` because Keycloak internally generates redirect URLs assuming root-level paths.
- **Vite env vars baked at build time** — the Vue app is a static SPA; environment variables are inlined during `npm run build` and cannot be changed at runtime.

---

## 2. Prerequisites

- A Linux server (tested on Debian/Ubuntu with Docker)
- Docker Engine + Docker Compose **V2** (plugin)
- A registered domain pointing to your server's IP
- SSH access configured (e.g., `ssh my-server`)
- Git installed on the server

> ⚠️ **Docker Compose V2**: Use `docker compose` (space), NOT `docker-compose` (hyphen). The legacy V1 binary is no longer shipped.

---

## 3. Server Initial Setup

### 3.1 Add user to docker group

```bash
sudo usermod -aG docker $USER
# Log out and back in, or use: newgrp docker
```

Without this, every `docker` command requires `sudo`, which breaks non-interactive scripts.

### 3.2 Create directory structure

```bash
mkdir -p ~/docker/volumes/{postgres,keycloak,logs}
mkdir -p ~/docker/init-scripts
```

### 3.3 Create PostgreSQL init script

Create `~/docker/init-scripts/init-keycloak-db.sql`:
```sql
SELECT 'CREATE DATABASE keycloak_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'keycloak_db')\gexec
```

This ensures the Keycloak database is created alongside the application database.

---

## 4. SSL Certificate (Let's Encrypt)

### 4.1 Install Certbot

```bash
sudo apt-get update && sudo apt-get install -y certbot
```

### 4.2 Obtain certificate (standalone mode)

Stop any service listening on port 80 first, then:

```bash
sudo certbot certonly --standalone -d YOUR_DOMAIN --agree-tos -m YOUR_EMAIL --non-interactive
```

### 4.3 Certificate renewal

Certificates expire every 90 days. Set up auto-renewal:

```bash
sudo certbot renew --dry-run  # Test first
```

Add to crontab:
```
0 0 1 * * certbot renew --quiet && docker compose -f ~/docker/docker-compose.yml restart nginx
```

> ⚠️ **Gotcha**: The nginx container mounts `/etc/letsencrypt` read-only. After renewal, restart nginx to pick up new certs.

---

## 5. Docker Compose Configuration

### 5.1 Environment file (`~/docker/.env`)

```env
# Environment
ENVIRONMENT=production
ASPNETCORE_ENVIRONMENT=Production

# PostgreSQL
POSTGRES_USER=ninvoices_user
POSTGRES_PASSWORD=<STRONG_PASSWORD>
POSTGRES_HOST=postgres
POSTGRES_PORT=5432
POSTGRES_DB=ninvoices_db

# Keycloak
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=<STRONG_PASSWORD>
KEYCLOAK_REALM=ninvoices
KEYCLOAK_CLIENT_ID=ninvoices-api

# Application (use YOUR domain)
DOMAIN=YOUR_DOMAIN
CORS_ORIGINS=https://YOUR_DOMAIN

# Docker Images
DOCKER_USERNAME=YOUR_DOCKERHUB_USER
IMAGE_TAG=latest
```

> ⚠️ **Never commit `.env` to git.** It contains secrets.

### 5.2 Docker Compose file (`~/docker/docker-compose.yml`)

Key points in the compose file:

**API service environment:**
```yaml
api:
  environment:
    # Connection string env var name must match appsettings.json key exactly
    ConnectionStrings__Default: Host=postgres;Port=5432;Database=${POSTGRES_DB};...
    Database__Type: PostgreSQL

    # Internal Keycloak URL (Docker network)
    Keycloak__Authority: http://keycloak:8080/realms/${KEYCLOAK_REALM}
    # External URL the browser sees (for JWT issuer validation)
    Keycloak__ExternalAuthority: https://${DOMAIN}/realms/${KEYCLOAK_REALM}
    # CRITICAL: Must be false — API talks to Keycloak over HTTP internally
    Keycloak__RequireHttpsMetadata: false
```

> ⚠️ **`ConnectionStrings__Default`** — The double-underscore `__` maps to `:` in .NET config. If your code reads `ConnectionStrings:Default`, the env var MUST be `ConnectionStrings__Default` (not `DefaultConnection` or any other variant).

> ⚠️ **`Keycloak__RequireHttpsMetadata: false`** — The API container connects to Keycloak over the Docker network via HTTP. Without this, JWT token validation fails because .NET requires HTTPS for OpenID metadata by default.

**Web service:**
```yaml
web:
  healthcheck:
    test: ["CMD-SHELL", "wget ... http://localhost:80/ ..."]
```

> ⚠️ **Web container listens on port 80**, not 3000. The nginx image serves static files on port 80 by default. Check your Dockerfile to confirm.

---

## 6. Nginx Reverse Proxy

### Critical routing rules

The nginx config (`~/docker/nginx.prod.conf`) must handle these paths **in this specific order** (more specific first):

```nginx
# 1. API endpoints — BEFORE the general /nInvoices catch-all
location /nInvoices/api/ {
    proxy_pass http://api:8080/api/;  # Strips /nInvoices prefix
    # CORS headers...
}

# 2. Keycloak — at ROOT level, NOT under /nInvoices
location /admin/   { proxy_pass http://keycloak:8080/admin/; }
location /realms/  { proxy_pass http://keycloak:8080/realms/; }
location /resources/ { proxy_pass http://keycloak:8080/resources/; }

# 3. Static assets (Vite-generated hashed files)
location ~ ^/(assets|vite\.svg|favicon\.ico) {
    proxy_pass http://web;
}

# 4. Frontend app — last, catches all /nInvoices/* routes
location /nInvoices {
    rewrite ^/nInvoices/(.*)$ /$1 break;
    proxy_pass http://web;
}
```

### Pitfalls

| Problem | Cause | Fix |
|---------|-------|-----|
| 502 Bad Gateway | Web upstream pointing to wrong port (3000 vs 80) | Check what port the web container actually listens on |
| Keycloak admin console 404 | `/admin/` under `/nInvoices/` | Keep Keycloak routes at root level |
| Auth callback 404 | Nginx had a `/nInvoices/auth/` block proxying to Keycloak, catching Vue's `/nInvoices/auth/callback` route | **Do NOT create a `/nInvoices/auth/` location** — the Vue SPA handles auth callbacks client-side |
| Static assets 404 | Vue built with base path but assets served from wrong location | Ensure Vite `base` matches nginx config |

---

## 7. Keycloak Setup

### 7.1 Access admin console

Navigate to `https://YOUR_DOMAIN/admin/` and login with admin credentials from `.env`.

### 7.2 Create realm

1. Click "Create Realm" → Name: `ninvoices` → Create

### 7.3 Create clients

**Web client (public — used by the browser):**
- Client ID: `ninvoices-web`
- Client authentication: **OFF** (public client)
- Standard flow: **ON** (authorization code flow)
- Direct access grants: **ON** (for testing with curl)
- Valid redirect URIs: `https://YOUR_DOMAIN/nInvoices/*`
- Valid post logout redirect URIs: `https://YOUR_DOMAIN/nInvoices/*`
- Web origins: `https://YOUR_DOMAIN`

**API client (bearer-only — validates tokens):**
- Client ID: `ninvoices-api`
- Client authentication: **ON**
- All authentication flows: **OFF**

> ⚠️ **Keycloak 26.x**: There is no "Access Type" dropdown. Use "Client authentication" toggle instead. Bearer-only = Client auth ON + all flows OFF.

### 7.4 Create test user

1. Go to Users → Create User
2. Username: `testuser`, Email verified: ON
3. Credentials tab → Set password (`Test123!`), Temporary: OFF

### 7.5 Verify

Test token acquisition from the command line:
```bash
curl -s -X POST "https://YOUR_DOMAIN/realms/ninvoices/protocol/openid-connect/token" \
  -d "grant_type=password" -d "client_id=ninvoices-web" \
  -d "username=testuser" -d "password=Test123!" \
  | python3 -c 'import sys,json; print(json.load(sys.stdin).get("access_token","FAILED")[:50])'
```

If this prints the start of a JWT token, Keycloak is working.

---

## 8. Building Docker Images

### ⚠️ CRITICAL: Vite build args

The Vue frontend bakes environment variables at build time. **You MUST pass all `VITE_*` args** when building the web image, otherwise the app will have empty config values and show a blank page.

### 8.1 Build API image

```bash
cd /tmp && git clone --depth 1 https://github.com/YOUR_USER/YOUR_REPO.git
cd YOUR_REPO

docker build -t YOUR_USER/ninvoices-api:latest \
  -f docker/Dockerfile.api .
```

> ⚠️ **Chromium dependencies**: If your app uses PuppeteerSharp for HTML→PDF conversion, the Dockerfile must install Chromium's system dependencies (`libnss3`, `libgbm1`, `libatk1.0-0t64`, etc.). Without these, PDF generation fails with `Failed to launch browser!` error. See `docker/Dockerfile.api` for the full list.

### 8.2 Build Web image

```bash
docker build -t YOUR_USER/ninvoices-web:latest \
  --build-arg VITE_API_URL=https://YOUR_DOMAIN/nInvoices \
  --build-arg VITE_KEYCLOAK_URL=https://YOUR_DOMAIN \
  --build-arg VITE_KEYCLOAK_REALM=ninvoices \
  --build-arg VITE_KEYCLOAK_CLIENT_ID=ninvoices-web \
  --build-arg VITE_BASE=/nInvoices/ \
  -f docker/Dockerfile.web .
```

| Argument | Value | Notes |
|----------|-------|-------|
| `VITE_API_URL` | `https://YOUR_DOMAIN/nInvoices` | **No** `/api` suffix — the API client appends `/api/*` paths |
| `VITE_KEYCLOAK_URL` | `https://YOUR_DOMAIN` | **No** `/auth` or `/nInvoices/auth` — the OIDC library appends `/realms/...` |
| `VITE_KEYCLOAK_REALM` | `ninvoices` | Must match Keycloak realm name exactly |
| `VITE_KEYCLOAK_CLIENT_ID` | `ninvoices-web` | Must match the **public** Keycloak client ID |
| `VITE_BASE` | `/nInvoices/` | Must include trailing slash; sets Vite's `base` config |

### 8.3 Verify the built JavaScript

After building, verify the correct values are baked in:
```bash
docker run --rm YOUR_USER/ninvoices-web cat /usr/share/nginx/html/assets/index-*.js \
  | grep -oP 'authority:"[^"]*"|client_id:"[^"]*"'
```

Expected: `authority:"https://YOUR_DOMAIN/realms/ninvoices"` and `client_id:"ninvoices-web"`.

### 8.4 Clean up

```bash
rm -rf /tmp/YOUR_REPO
```

---

## 9. Database Considerations (SQLite → PostgreSQL)

### 9.1 Schema mismatches

EF Core migrations target SQLite by default. When using PostgreSQL, the auto-generated schema may have type mismatches:

| Issue | Symptom | Fix |
|-------|---------|-----|
| Enum stored as `text` but `HasConversion<int>()` | `operator does not exist: text = integer` | `ALTER TABLE ... ALTER COLUMN ... TYPE integer USING ...::integer` |
| Missing columns (e.g., `CreatedAt`, `UpdatedAt`) | `column X does not exist` | Recreate the table with correct schema |
| Column name mismatch | `column X does not exist` | Check entity vs actual DB schema and alter/recreate |

### 9.2 Importing data from SQLite to PostgreSQL

When importing data via the `/api/importexport` endpoints, be aware:

- **Enum conversions**: Some enums are stored as strings (`HasConversion<string>()`), others as integers (`HasConversion<int>()`). Imported data must match.
- **MonthlyReportTemplates**: May need manual activation after import (`UPDATE "MonthlyReportTemplates" SET "IsActive" = true WHERE "Id" = X`)
- **WorkDays table**: Verify it has the correct schema (`CustomerId`, `CreatedAt`, `UpdatedAt` columns). The import migration may create it with wrong columns.

### 9.3 Verify schema after deployment

Run this to check for potential issues:
```bash
docker exec POSTGRES_CONTAINER psql -U DB_USER -d DB_NAME -c "
  SELECT table_name, column_name, data_type
  FROM information_schema.columns
  WHERE table_schema='public'
  ORDER BY table_name, ordinal_position;"
```

---

## 10. Local Development Setup

### 10.1 Running locally without Keycloak

The app supports a dev auth bypass for local development:

```bash
# Terminal 1 — API (uses SQLite, no Keycloak)
cd src/nInvoices.Api
# Ensure appsettings.Development.json has: "Authentication": { "UseDevAuth": true }
dotnet run

# Terminal 2 — Frontend (Vite dev server with proxy)
cd src/nInvoices.Web
npm run dev
```

The dev auth bypass skips JWT validation entirely. **Never enable it in production**.

### 10.2 Frontend API client

The `api/client.ts` Axios interceptor attaches the Bearer token to every request. Any store or component that makes API calls **must use `apiClient`**, not raw `axios`.

> ⚠️ **Common bug**: If a Pinia store imports `axios` directly instead of using `apiClient`, API calls will lack the Authorization header and return 401 in production (but work fine locally with dev auth).

---

## 11. Redeployment Procedure

After code changes:

```bash
# 1. Commit and push
git add . && git commit -m "description" && git push

# 2. SSH to server
ssh my-server

# 3. Clone fresh (or git pull)
cd /tmp && rm -rf myrepo && git clone --depth 1 https://github.com/USER/REPO.git && cd REPO

# 4. Build images (only the ones that changed)
# API:
docker build -t USER/ninvoices-api:latest -f docker/Dockerfile.api .
# Web (ALWAYS include build args!):
docker build -t USER/ninvoices-web:latest \
  --build-arg VITE_API_URL=https://DOMAIN/nInvoices \
  --build-arg VITE_KEYCLOAK_URL=https://DOMAIN \
  --build-arg VITE_KEYCLOAK_REALM=ninvoices \
  --build-arg VITE_KEYCLOAK_CLIENT_ID=ninvoices-web \
  --build-arg VITE_BASE=/nInvoices/ \
  -f docker/Dockerfile.web .

# 5. Restart changed containers
cd ~/docker
docker compose stop api web
docker compose rm -f api web
docker compose up -d api web

# 6. Clean up
rm -rf /tmp/myrepo

# 7. Verify
curl -s https://DOMAIN/nInvoices/ | head -5  # Should return HTML
curl -s -w "%{http_code}" https://DOMAIN/nInvoices/api/health  # Should return 200
```

---

## 12. Troubleshooting & Lessons Learned

### Authentication Issues

| Symptom | Cause | Solution |
|---------|-------|----------|
| **401 on all API calls** | JWT issuer validation fails | Add external authority URL to `ValidIssuers` via `Keycloak__ExternalAuthority` |
| **401 on all API calls** | JWT audience validation fails | Ensure `ValidAudiences` includes `account`, `ninvoices-web`, and the API client ID |
| **401 on specific store/page** | Store uses raw `axios` instead of `apiClient` | Import `apiClient` from `api/client.ts` |
| **Blank page after login** | Keycloak OIDC metadata unreachable from API container | Set `Keycloak__RequireHttpsMetadata: false` |
| **Auth callback 404** | Nginx catches `/nInvoices/auth/*` and proxies to Keycloak | Remove the `/nInvoices/auth/` nginx location block |
| **Invalid username or password** | User not created in Keycloak realm | Create user in Keycloak admin console under the correct realm |

### Blank Page Issues

| Symptom | Cause | Solution |
|---------|-------|----------|
| **Blank page, no JS errors** | VITE build args not passed | Rebuild web image with ALL `--build-arg VITE_*` flags |
| **Blank page, auth errors** | `authority` is empty/wrong in built JS | Verify with `grep 'authority:' index-*.js` |
| **Blank page, 404 on assets** | Base path mismatch | Ensure `VITE_BASE` matches nginx path prefix |

### Database Issues

| Symptom | Cause | Solution |
|---------|-------|----------|
| **`text = integer` operator error** | Enum column is `text` but `HasConversion<int>()` | `ALTER COLUMN ... TYPE integer` |
| **`column X does not exist`** | Schema mismatch between EF model and DB | Compare entity properties with DB columns, alter/recreate table |
| **500 on Settings page** | `InvoiceSequence` table has wrong schema | Recreate with correct columns (`Id`, `CurrentValue`, `CreatedAt`, `UpdatedAt`) |

### PDF Generation Issues

| Symptom | Cause | Solution |
|---------|-------|----------|
| **`Failed to launch browser!`** | Chromium dependencies missing in Docker image | Add `libnss3`, `libgbm1`, etc. to `Dockerfile.api` |
| **`No active monthly report template found`** | Templates not imported or not activated | Create/import templates and activate via API or direct SQL |

### Docker & Infrastructure

| Symptom | Cause | Solution |
|---------|-------|----------|
| **`docker-compose: command not found`** | Docker Compose V2 is a plugin | Use `docker compose` (space) not `docker-compose` (hyphen) |
| **Permission denied on docker socket** | User not in `docker` group | `sudo usermod -aG docker $USER` then re-login |
| **502 Bad Gateway** | Upstream port mismatch | Verify container's actual listening port in Dockerfile |
| **Container unhealthy** | Healthcheck hitting wrong port | Match healthcheck port to container's actual port |

### General Best Practices

1. **Never hardcode domain names in source code.** Use configuration/environment variables.
2. **Always verify JS bundle contents after building web image.** A quick `grep` saves hours of debugging blank pages.
3. **Use `--depth 1` when cloning for builds** — faster and uses less disk.
4. **Clean up `/tmp` build directories** — servers have limited disk space.
5. **Test API endpoints with curl before checking the UI** — isolates frontend vs backend issues.
6. **Check container logs first** — `docker logs CONTAINER --tail 50` reveals most errors instantly.
7. **Keep `.env` out of git** — use `.env.example` for documentation.

---

## Quick Reference: Key URLs

| Resource | URL |
|----------|-----|
| Application | `https://YOUR_DOMAIN/nInvoices/` |
| API Health | `https://YOUR_DOMAIN/nInvoices/api/health` |
| Keycloak Admin | `https://YOUR_DOMAIN/admin/` |
| Keycloak OIDC Config | `https://YOUR_DOMAIN/realms/ninvoices/.well-known/openid-configuration` |
| Token Endpoint | `https://YOUR_DOMAIN/realms/ninvoices/protocol/openid-connect/token` |
