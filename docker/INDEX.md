# Documentation Index - nInvoices Docker + Keycloak

This directory contains comprehensive documentation for deploying and troubleshooting the nInvoices application with Docker and Keycloak authentication.

---

## 📋 Quick Navigation

### 🚀 Getting Started
- **[QUICK-REFERENCE.md](./QUICK-REFERENCE.md)** - Start here! Quick commands, credentials, and common tasks
- **[REGISTRY-QUICKSTART.md](./REGISTRY-QUICKSTART.md)** - Docker Registry deployment quick start
- **[README.md](./README.md)** - Docker deployment overview

### 📖 Complete Guides
- **[KEYCLOAK-DOCKER-GUIDE.md](./KEYCLOAK-DOCKER-GUIDE.md)** - Complete step-by-step implementation guide
  - All issues encountered and solutions
  - Architecture diagrams
  - Phase-by-phase implementation
  - Testing procedures
  - Production considerations
- **[PRODUCTION-DEPLOYMENT.md](./PRODUCTION-DEPLOYMENT.md)** - Docker Registry deployment guide
  - Build and push to Docker Hub
  - Deploy on production servers
  - Production hardening (HTTPS, backups, monitoring)
  - Updates and maintenance
  - Multi-server deployment

### 🔧 Technical Solutions
- **[JWT-AUTHENTICATION-FIX.md](./JWT-AUTHENTICATION-FIX.md)** - BackchannelHandler solution explained
  - Why the Docker networking issue occurs
  - How BackchannelHandler solves it
  - Implementation details
  - Why other solutions don't work

### ✅ Status & Resolution
- **[RESOLUTION-SUMMARY.md](./RESOLUTION-SUMMARY.md)** - Complete resolution summary
  - All issues resolved
  - Verification steps
  - Current application status
  - Access information

### 🐛 Troubleshooting
- **[TROUBLESHOOTING.md](./TROUBLESHOOTING.md)** - Common issues and solutions
  - Authentication errors
  - Password problems
  - Database issues
  - Network connectivity
  - Health check failures

### ⚙️ Configuration
- **[ENV-FILE-FAQ.md](./ENV-FILE-FAQ.md)** - Environment configuration guide
  - .env file structure
  - Password rules
  - Common mistakes
  - Troubleshooting

### 🗄️ Database
- **[MIGRATION.md](./MIGRATION.md)** - Database migration guide
  - SQLite to PostgreSQL migration
  - Schema creation
  - Data migration

### 🔨 Build Issues
- **[BUILD-ISSUES.md](./BUILD-ISSUES.md)** - Build-related problems
  - npm/node issues
  - Docker build failures
  - Dockerfile troubleshooting

---

## 🎯 Documentation by Task

### I want to deploy the application
1. Read **[QUICK-REFERENCE.md](./QUICK-REFERENCE.md)** for quick start
2. Follow **[README.md](./README.md)** for deployment steps
3. Configure `.env` using **[ENV-FILE-FAQ.md](./ENV-FILE-FAQ.md)**

### I'm getting authentication errors
1. Check **[TROUBLESHOOTING.md](./TROUBLESHOOTING.md)** for common solutions
2. Read **[JWT-AUTHENTICATION-FIX.md](./JWT-AUTHENTICATION-FIX.md)** to understand the fix
3. Verify **BackchannelHandler** is in place

### I want to deploy to production with Docker Hub
1. Read **[REGISTRY-QUICKSTART.md](./REGISTRY-QUICKSTART.md)** for quick workflow
2. Run `build-and-push.ps1` to push images
3. Follow **[PRODUCTION-DEPLOYMENT.md](./PRODUCTION-DEPLOYMENT.md)** for complete guide
4. Use `docker-compose.registry.yml` on production server
### I want to understand the complete implementation
1. Read **[KEYCLOAK-DOCKER-GUIDE.md](./KEYCLOAK-DOCKER-GUIDE.md)** for full details
2. Review **[RESOLUTION-SUMMARY.md](./RESOLUTION-SUMMARY.md)** for what was done
3. Check **[JWT-AUTHENTICATION-FIX.md](./JWT-AUTHENTICATION-FIX.md)** for technical details
4. See **[PRODUCTION-DEPLOYMENT.md](./PRODUCTION-DEPLOYMENT.md)** for registry workflow

### I'm setting up for production
1. Read **[KEYCLOAK-DOCKER-GUIDE.md](./KEYCLOAK-DOCKER-GUIDE.md)** - Production section
2. Review **[README.md](./README.md)** - Production setup
3. Implement security best practices from guides

### I need to migrate data
1. Follow **[MIGRATION.md](./MIGRATION.md)** for database migration
2. Use scripts provided in the guide
3. Verify data integrity after migration

---

## 📊 Document Summary

| Document | Size | Purpose | Audience |
|----------|------|---------|----------|
| **REGISTRY-QUICKSTART.md** | 11 KB | Docker Hub deployment | DevOps |
| **PRODUCTION-DEPLOYMENT.md** | 18 KB | Complete prod guide | DevOps |
| **QUICK-REFERENCE.md** | 9 KB | Fast command reference | All users |
| **KEYCLOAK-DOCKER-GUIDE.md** | 26 KB | Complete implementation | Developers/DevOps |
| **JWT-AUTHENTICATION-FIX.md** | 9 KB | Technical solution | Developers |
| **RESOLUTION-SUMMARY.md** | 8 KB | Status overview | All users |
| **TROUBLESHOOTING.md** | 10 KB | Problem solving | All users |
| **ENV-FILE-FAQ.md** | 7 KB | Configuration help | All users |
| **MIGRATION.md** | 5 KB | Data migration | DevOps |
| **BUILD-ISSUES.md** | 5 KB | Build problems | Developers |
| **README.md** | 10 KB | Deployment overview | All users |

**Total**: ~120 KB of comprehensive documentation

---

## 🔑 Critical Information

### BackchannelHttpHandler (REQUIRED)

**Location**: `src/nInvoices.Api/Infrastructure/KeycloakBackchannelHandler.cs`

**Purpose**: Solves Docker networking issue where JWT tokens have issuer `localhost:8080` but API container needs to fetch keys from `keycloak:8080`.

**Without this fix**: Authentication will fail with "signature key not found"

**More details**: See **[JWT-AUTHENTICATION-FIX.md](./JWT-AUTHENTICATION-FIX.md)**

### Password Rules

⚠️ **CRITICAL**: Do NOT use special characters in passwords!

❌ **Avoid**: `!`, `@`, `#`, `$`, `%`, `&`, `*`  
✅ **Use**: Letters, numbers, `-`, `_`

**Reason**: Special characters break JDBC connection strings in Keycloak

**More details**: See **[ENV-FILE-FAQ.md](./ENV-FILE-FAQ.md)**

### .env File Location

**.env MUST be in `docker/` directory** (same location as docker-compose.yml)

```
✅ Correct: docker/.env
❌ Wrong: .env (root directory)
```

**More details**: See **[ENV-FILE-FAQ.md](./ENV-FILE-FAQ.md)**

---

## 🎓 Learning Path

### Beginner (Just want it working)
1. **[QUICK-REFERENCE.md](./QUICK-REFERENCE.md)** - Copy/paste commands
2. **[TROUBLESHOOTING.md](./TROUBLESHOOTING.md)** - When things don't work

### Intermediate (Want to understand)
1. **[KEYCLOAK-DOCKER-GUIDE.md](./KEYCLOAK-DOCKER-GUIDE.md)** - Complete walkthrough
2. **[JWT-AUTHENTICATION-FIX.md](./JWT-AUTHENTICATION-FIX.md)** - Technical solution
3. **[RESOLUTION-SUMMARY.md](./RESOLUTION-SUMMARY.md)** - What was implemented

### Advanced (Production deployment)
1. **[KEYCLOAK-DOCKER-GUIDE.md](./KEYCLOAK-DOCKER-GUIDE.md)** - Full guide + production section
2. **[README.md](./README.md)** - Production configuration
3. All troubleshooting guides for reference

---

## 🆘 Getting Help

### Step 1: Check Logs
```bash
docker logs ninvoices-api-dev
docker logs ninvoices-keycloak-dev
docker logs ninvoices-postgres-dev
```

### Step 2: Review Troubleshooting
- **[TROUBLESHOOTING.md](./TROUBLESHOOTING.md)** for common issues
- **[QUICK-REFERENCE.md](./QUICK-REFERENCE.md)** for quick fixes

### Step 3: Verify Configuration
- Check `.env` file location and content
- Verify BackchannelHandler is in place
- Check container health status

### Step 4: Consult Guides
- **[KEYCLOAK-DOCKER-GUIDE.md](./KEYCLOAK-DOCKER-GUIDE.md)** for complete reference
- **[JWT-AUTHENTICATION-FIX.md](./JWT-AUTHENTICATION-FIX.md)** for auth issues

---

## ✅ Verification Checklist

After deployment, verify:

- [ ] All containers are healthy: `docker ps`
- [ ] Database has 8 tables
- [ ] Keycloak is accessible at http://localhost:8080
- [ ] Test user can login: `testuser` / `Test123!`
- [ ] Frontend loads at http://localhost:3000
- [ ] API responds to authenticated requests
- [ ] JWT tokens validated successfully
- [ ] BackchannelHandler is in place

**See**: **[RESOLUTION-SUMMARY.md](./RESOLUTION-SUMMARY.md)** for complete checklist

---

## 🔄 Quick Commands

```bash
# Start
cd docker
docker-compose -f docker-compose.dev.yml up -d

# Status
docker-compose -f docker-compose.dev.yml ps

# Logs
docker-compose -f docker-compose.dev.yml logs -f

# Restart
docker-compose -f docker-compose.dev.yml restart api

# Stop
docker-compose -f docker-compose.dev.yml down

# Reset
docker-compose -f docker-compose.dev.yml down -v
rm -rf volumes/*
```

**See**: **[QUICK-REFERENCE.md](./QUICK-REFERENCE.md)** for complete command reference

---

## 📞 Access Information

### Application URLs
- Frontend: http://localhost:3000
- API: http://localhost:8080
- Keycloak Admin: http://localhost:8080/admin
- PostgreSQL: localhost:5432

### Credentials
- **Test User**: `testuser` / `Test123!`
- **Admin**: `admin` / `Albagnano2026Pass`
- **Database**: `ninvoices_user` / `Albagnano2026Pass`

---

## 🎯 Key Takeaways

1. **BackchannelHttpHandler is mandatory** for JWT authentication in Docker
2. **Never use special characters** in passwords for JDBC connections
3. **Audience mapper must be configured** in Keycloak for proper validation
4. **.env location matters** - must be in docker/ directory
5. **PostgreSQL syntax differs** from SQLite - use appropriate schema

---

## 📝 Document Maintenance

### Last Updated
February 5, 2026

### Version
1.0 - Initial comprehensive documentation set

### Contributors
- Complete Docker + Keycloak implementation
- All issues resolved and documented
- 9 comprehensive guides created
- 90+ KB of documentation

---

## 🌟 Status

✅ **Authentication**: WORKING  
✅ **Database**: INITIALIZED  
✅ **Keycloak**: CONFIGURED  
✅ **Application**: FULLY OPERATIONAL  
✅ **Documentation**: COMPLETE  

---

**For latest updates and issues, check the main repository README.**
