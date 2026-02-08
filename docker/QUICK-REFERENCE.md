# Quick Reference - nInvoices Docker + Keycloak

## 🚀 Quick Start

```bash
cd docker
cp ../.env.example .env
# Edit .env (avoid special chars in passwords!)
docker-compose -f docker-compose.dev.yml up -d
```

**Access**: http://localhost:3000  
**Login**: `testuser` / `Test123!`

---

## 📦 Services

| Service    | Port | URL                        | Status Check                       |
|------------|------|----------------------------|------------------------------------|
| Frontend   | 3000 | http://localhost:3000      | Open in browser                    |
| API        | 8080 | http://localhost:8080      | `curl localhost:8080/api/health`   |
| Keycloak   | 8080 | http://localhost:8080      | Same as API (shared port)          |
| PostgreSQL | 5432 | localhost:5432             | `docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db` |

---

## 🔐 Credentials

### Test User (Application)
- Username: `testuser`
- Password: `Test123!`

### Admin (Keycloak)
- Username: `admin`
- Password: `Albagnano2026Pass`
- URL: http://localhost:8080/admin

### Database
- User: `ninvoices_user`
- Password: `Albagnano2026Pass`
- Databases: `ninvoices_db`, `keycloak_db`

---

## 🛠️ Common Commands

### Start Services
```bash
cd docker
docker-compose -f docker-compose.dev.yml up -d
```

### Stop Services
```bash
docker-compose -f docker-compose.dev.yml down
```

### View Logs
```bash
# All services
docker-compose -f docker-compose.dev.yml logs -f

# Specific service
docker logs ninvoices-api-dev -f
docker logs ninvoices-keycloak-dev -f
docker logs ninvoices-postgres-dev -f
docker logs ninvoices-web-dev -f
```

### Check Status
```bash
docker-compose -f docker-compose.dev.yml ps
docker ps --filter "name=ninvoices"
```

### Restart Service
```bash
docker-compose -f docker-compose.dev.yml restart api
docker-compose -f docker-compose.dev.yml restart keycloak
```

### Rebuild Service
```bash
cd docker
docker-compose -f docker-compose.dev.yml build api
docker-compose -f docker-compose.dev.yml up -d api
```

### Database Access
```bash
# Connect to PostgreSQL
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db

# List tables
\dt

# Exit
\q
```

### Reset Everything
```bash
cd docker
docker-compose -f docker-compose.dev.yml down -v
rm -rf volumes/*
docker-compose -f docker-compose.dev.yml up -d
```

---

## 🐛 Quick Troubleshooting

### Authentication Error: "signature key not found"

**Cause**: Docker networking issue  
**Fix**: Ensure `KeycloakBackchannelHandler` is in place  
**Verify**:
```bash
docker logs ninvoices-api-dev | grep "certs"
# Should see: GET http://keycloak:8080/... - 200
```

### Can't Login to Keycloak

**Check password**: No special characters (`!@#$%`)  
**Check .env location**: Must be in `docker/` directory  
**Reset Keycloak DB**:
```bash
docker-compose -f docker-compose.dev.yml stop keycloak
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d postgres \
  -c "DROP DATABASE IF EXISTS keycloak_db;"
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d postgres \
  -c "CREATE DATABASE keycloak_db;"
docker-compose -f docker-compose.dev.yml up -d keycloak
```

### Container Won't Start

**Check logs**:
```bash
docker logs ninvoices-<service>-dev
```

**Check health**:
```bash
docker inspect ninvoices-<service>-dev | grep -A 10 "Health"
```

### Database Tables Missing

**Check if tables exist**:
```bash
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db -c "\dt"
```

**Apply schema**:
```bash
docker cp docker/init-scripts/02-ninvoices-schema.sql ninvoices-postgres-dev:/tmp/
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db -f /tmp/02-ninvoices-schema.sql
```

### Port Already in Use

**Find and stop conflicting process**:
```bash
# Windows
netstat -ano | findstr :8080
taskkill /PID <pid> /F

# Linux/Mac
lsof -ti:8080 | xargs kill -9
```

---

## 📚 Documentation

| Document | Purpose |
|----------|---------|
| **[KEYCLOAK-DOCKER-GUIDE.md](./KEYCLOAK-DOCKER-GUIDE.md)** | Complete implementation guide |
| **[JWT-AUTHENTICATION-FIX.md](./JWT-AUTHENTICATION-FIX.md)** | BackchannelHandler explanation |
| **[TROUBLESHOOTING.md](./TROUBLESHOOTING.md)** | Common issues & solutions |
| **[RESOLUTION-SUMMARY.md](./RESOLUTION-SUMMARY.md)** | Implementation summary |
| **[ENV-FILE-FAQ.md](./ENV-FILE-FAQ.md)** | Environment configuration |
| **[MIGRATION.md](./MIGRATION.md)** | Database migration guide |

---

## ⚙️ Configuration Files

### .env (in docker/ directory)
```env
POSTGRES_USER=ninvoices_user
POSTGRES_PASSWORD=Albagnano2026Pass
POSTGRES_DB=ninvoices_db

KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=Albagnano2026Pass

DATABASE_URL=Host=postgres;Port=5432;Database=ninvoices_db;Username=ninvoices_user;Password=Albagnano2026Pass
```

**⚠️ PASSWORD RULES**:
- ✅ Alphanumeric: `MyPass123`
- ❌ Special chars: `MyPass!123` (breaks JDBC)

### docker-compose.dev.yml
```yaml
services:
  postgres:
    image: postgres:17-alpine
    environment: # From .env
    volumes:
      - ./volumes/postgres:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d
    ports:
      - "5432:5432"

  keycloak:
    image: quay.io/keycloak/keycloak:26.0.8
    environment: # From .env
    command: start-dev
    ports:
      - "8080:8080"

  api:
    build:
      context: ..
      dockerfile: docker/Dockerfile.api
    environment:
      Keycloak__Authority: http://keycloak:8080/realms/ninvoices
    ports:
      - "8080:8080"

  web:
    build:
      context: ..
      dockerfile: docker/Dockerfile.web
      args:
        VITE_KEYCLOAK_URL: http://localhost:8080
    ports:
      - "3000:3000"
```

---

## 🔍 Health Checks

### Quick Test All Services
```bash
# API
curl http://localhost:8080/api/health

# Keycloak
curl http://localhost:8080/realms/ninvoices/.well-known/openid-configuration

# Frontend
curl http://localhost:3000

# Database
docker exec ninvoices-postgres-dev pg_isready -U ninvoices_user
```

### Get Detailed Status
```bash
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

---

## 🎯 Testing Authentication

### 1. Login via Browser
1. Open http://localhost:3000
2. Click login
3. Use: `testuser` / `Test123!`
4. Should redirect back to app

### 2. API Test with Token
```bash
# Get token
TOKEN=$(curl -X POST http://localhost:8080/realms/ninvoices/protocol/openid-connect/token \
  -d "client_id=ninvoices-web" \
  -d "username=testuser" \
  -d "password=Test123!" \
  -d "grant_type=password" \
  | jq -r '.access_token')

# Test API
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/customers
```

### 3. Check JWT Token
Copy token and paste at: https://jwt.io

Should see:
- **Issuer**: `http://localhost:8080/realms/ninvoices`
- **Audience**: `["ninvoices-api", "account"]`
- **Subject**: User ID
- Valid signature ✅

---

## 📊 Monitoring

### Watch Logs in Real-Time
```bash
# Terminal 1 - API logs
docker logs ninvoices-api-dev -f | grep -E "Authentication|Token|Error"

# Terminal 2 - Keycloak logs
docker logs ninvoices-keycloak-dev -f | grep -E "ERROR|WARN"

# Terminal 3 - All services
docker-compose -f docker-compose.dev.yml logs -f
```

### Check Resource Usage
```bash
docker stats ninvoices-api-dev ninvoices-keycloak-dev ninvoices-postgres-dev ninvoices-web-dev
```

---

## 🎓 Key Concepts

### BackchannelHttpHandler
**Purpose**: Solves Docker networking issue  
**Location**: `src/nInvoices.Api/Infrastructure/KeycloakBackchannelHandler.cs`  
**Function**: Rewrites `localhost:8080` → `keycloak:8080` for internal requests

### JWT Flow
1. User logs in via Keycloak (browser)
2. Keycloak issues JWT token (issuer: localhost:8080)
3. Browser sends token to API
4. API validates token signature
5. **BackchannelHandler** fetches keys from keycloak:8080
6. Token validated ✅

### Docker Networking
- **External**: Browser → `localhost:8080` → Keycloak
- **Internal**: API → `keycloak:8080` → Keycloak
- **BackchannelHandler**: Bridges the gap

---

## 💡 Tips

1. Always place `.env` in `docker/` directory
2. Avoid special characters in passwords
3. Check logs when things don't work
4. Use `docker-compose restart <service>` instead of full restart
5. Keep `BackchannelHandler` in place (required!)
6. Export Keycloak realm for backup
7. Use volumes for data persistence

---

## 🆘 Get Help

1. Check **[TROUBLESHOOTING.md](./TROUBLESHOOTING.md)**
2. Read **[KEYCLOAK-DOCKER-GUIDE.md](./KEYCLOAK-DOCKER-GUIDE.md)**
3. Review logs: `docker logs <container-name>`
4. Test network: `docker network inspect ninvoices-network`
5. Verify JWT: https://jwt.io

---

**Status**: ✅ WORKING  
**Last Updated**: February 5, 2026
