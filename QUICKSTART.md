# Quick Start Guide - nInvoices with Docker & Keycloak

This guide will get you up and running with nInvoices in under 10 minutes.

## Prerequisites

✅ Docker Desktop or Docker Engine installed  
✅ At least 2GB free RAM  
✅ At least 5GB free disk space  

## Step 1: Clone and Configure (2 minutes)

```bash
# Clone the repository
git clone https://github.com/yourusername/nInvoices.git
cd nInvoices

# Copy the environment template (this creates the config file)
cp .env.example .env
```

**📝 Note:** The `.env` file will configure NEW services that Docker creates. You're not connecting to existing databases - Docker will create everything fresh.

## Step 2: Edit Configuration (1 minute)

Open `.env` and **choose** the passwords that will be set during first startup:

```env
# Choose YOUR passwords (these will be set when Docker creates the services)
# These are NOT existing passwords - you're picking what they WILL BE
POSTGRES_PASSWORD=MySecurePassword123!
KEYCLOAK_ADMIN_PASSWORD=AdminPassword456!

# Leave these as-is for development
POSTGRES_USER=ninvoices_user
POSTGRES_DB=ninvoices_db
KEYCLOAK_ADMIN=admin
KEYCLOAK_URL=http://localhost:8080
API_URL=http://localhost:5000
WEB_URL=http://localhost:3000
```

**💡 How this works:**
- These passwords don't exist yet - you're **choosing** them
- When Docker starts, it will **create** PostgreSQL and Keycloak with these passwords
- After startup, these ARE the passwords you'll use to connect

See `docker/ENV-FILE-FAQ.md` for detailed explanation.

## Step 3: Start Services (3 minutes)

```bash
cd docker
docker-compose -f docker-compose.dev.yml up -d
```

This will:
- Download Docker images (~500MB total)
- Start PostgreSQL, Keycloak, API, and Web services
- Initialize databases
- Configure Keycloak realm

**Watch the progress:**
```bash
docker-compose -f docker-compose.dev.yml logs -f
```

Wait for:
- ✅ PostgreSQL: `database system is ready to accept connections`
- ✅ Keycloak: `Keycloak 26.0 (WildFly Core xx.x.x.Final) started`
- ✅ API: `Now listening on: http://[::]:8080`
- ✅ Web: `start worker process`

Press `Ctrl+C` to stop following logs.

## Step 4: Verify Services (1 minute)

```bash
# Check all services are running
docker-compose -f docker-compose.dev.yml ps

# Should show 4 running services:
# ninvoices-postgres-dev
# ninvoices-keycloak-dev
# ninvoices-api-dev
# ninvoices-web-dev
```

**Test the API:**
```bash
curl http://localhost:5000/api/health
# Should return: {"status":"Healthy",...}
```

## Step 5: Access the Application (1 minute)

### Web Application
Open your browser to: **http://localhost:3000**

You'll be redirected to Keycloak login.

**Default Login:**
- Username: `admin`
- Password: `admin123`

⚠️ **Important:** You'll be prompted to change the password on first login.

### Keycloak Admin Console
If you need to manage users/roles:

1. Go to: **http://localhost:8080**
2. Login with:
   - Username: `admin` (from your .env KEYCLOAK_ADMIN)
   - Password: (from your .env KEYCLOAK_ADMIN_PASSWORD)
3. Select realm: **ninvoices**

## Step 6: Start Using nInvoices! 🎉

You can now:
- ✅ Create customers
- ✅ Add rates and taxes
- ✅ Generate invoices
- ✅ Export PDFs
- ✅ Manage templates

## Common Issues

### "Cannot connect to the Docker daemon"
```bash
# Make sure Docker is running
docker ps
```

### "Port already in use"
Another service is using ports 3000, 5000, 5432, or 8080.

**Solution:** Edit `.env` and change the port numbers:
```env
API_PORT=5001
WEB_PORT=3001
POSTGRES_PORT=5433
KEYCLOAK_PORT=8081
```

Then restart:
```bash
docker-compose -f docker-compose.dev.yml down
docker-compose -f docker-compose.dev.yml up -d
```

### "Keycloak not starting" or "504 Gateway Timeout"
Keycloak can take 1-2 minutes to fully start.

**Check logs:**
```bash
docker logs ninvoices-keycloak-dev --tail 50
```

Wait for `Keycloak xx.x started` message.

### "Authentication redirect loop"
Clear your browser cookies for localhost:
1. Open browser DevTools (F12)
2. Go to Application/Storage tab
3. Clear cookies for localhost
4. Refresh page

### "API returns 401 Unauthorized"
1. Check if Keycloak is running:
   ```bash
   docker ps | grep keycloak
   ```

2. Verify API can reach Keycloak:
   ```bash
   docker exec ninvoices-api-dev curl http://keycloak:8080/realms/ninvoices
   ```

## Stopping Services

```bash
cd docker
docker-compose -f docker-compose.dev.yml down
```

To remove all data (start fresh):
```bash
docker-compose -f docker-compose.dev.yml down -v
sudo rm -rf volumes/  # or: Remove-Item -Recurse -Force volumes/ (PowerShell)
```

## Next Steps

### Create a New User

1. Go to Keycloak Admin: http://localhost:8080
2. Select realm: **ninvoices**
3. Click **Users** → **Add user**
4. Fill in details:
   - Username: `john.doe`
   - Email: `john@example.com`
   - First Name: `John`
   - Last Name: `Doe`
5. Click **Create**
6. Go to **Credentials** tab
7. Set password and uncheck "Temporary"
8. Go to **Role Mappings** tab
9. Assign role: `user` or `admin`

### Backup Your Data

```bash
cd docker
.\backup-database.ps1
```

Backups saved to: `docker/volumes/backups/`

### Migrate Existing Data

If you have SQLite data:

```bash
cd docker
.\migrate-sqlite-to-postgres.ps1 -SqliteDbPath "../src/nInvoices.Api/nInvoices.db"
```

See `docker/MIGRATION.md` for details.

### Deploy to Production

See `docker/README.md` for complete production deployment guide.

## Getting Help

- 📖 **Full Documentation**: See `/docker/README.md`
- 🔄 **Migration Guide**: See `/docker/MIGRATION.md`
- 🐛 **Issues**: Check logs with `docker-compose logs`
- 💬 **Support**: Create GitHub issue

## Quick Reference

| Service | URL | Default Credentials |
|---------|-----|---------------------|
| Web App | http://localhost:3000 | admin / admin123 (change on first login) |
| API | http://localhost:5000 | N/A (uses Bearer token) |
| Keycloak Admin | http://localhost:8080 | admin / (from .env) |
| PostgreSQL | localhost:5432 | ninvoices_user / (from .env) |

## Useful Commands

```bash
# View all logs
docker-compose -f docker-compose.dev.yml logs -f

# View specific service logs
docker-compose -f docker-compose.dev.yml logs -f api

# Restart a service
docker-compose -f docker-compose.dev.yml restart api

# Check service status
docker-compose -f docker-compose.dev.yml ps

# Execute command in container
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db

# Backup database
.\backup-database.ps1

# Stop all services
docker-compose -f docker-compose.dev.yml down
```

---

**Ready to invoice!** 🚀💼📄
