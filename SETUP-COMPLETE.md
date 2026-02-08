# ✅ nInvoices Setup Complete!

## Your Application is Ready

All Docker services are running and configured. You can now start using nInvoices!

---

## 🌐 Access Your Application

### Main Application
**URL**: http://localhost:3000

**Test User Credentials**:
- Username: `testuser`
- Password: `Test123!`

### Keycloak Admin Console
**URL**: http://localhost:8080/admin

**Important**: Select `ninvoices` realm from the dropdown (top-left)

**Admin Credentials**:
- Username: `admin`  
- Password: `Albagnano2026Pass`

### API Health Check
**URL**: http://localhost:5000/api/health

### PostgreSQL Database
- **Host**: localhost:5432
- **User**: ninvoices_user
- **Password**: Albagnano2026Pass
- **Databases**:
  - `ninvoices_db` - Application data
  - `keycloak_db` - Authentication data

---

## ✅ What's Working

- ✅ PostgreSQL database with schema initialized (8 tables)
- ✅ Keycloak authentication server configured
- ✅ API backend running with JWT authentication
- ✅ Web frontend serving application
- ✅ JWT signature validation (via BackchannelHandler fix)
- ✅ Audience mapper configured for token validation
- ✅ Test user created and ready to use
- ✅ All Docker containers healthy and communicating

---

## 🔧 Critical Components

### BackchannelHttpHandler (Required for Authentication)
The API uses a custom `KeycloakBackchannelHandler` that rewrites HTTP requests from `localhost:8080` to `keycloak:8080`. This is **essential** for JWT signature validation in Docker environments.

**Location**: `src/nInvoices.Api/Infrastructure/KeycloakBackchannelHandler.cs`

**Why It's Needed**:
- JWT tokens have issuer: `http://localhost:8080/realms/ninvoices`
- API container cannot reach `localhost:8080` (that's the host)
- Handler rewrites to `http://keycloak:8080` for internal Docker communication
- Without this, authentication will fail with "signature key not found"

---

## 🚀 Quick Start

1. **Open the application**: http://localhost:3000

2. **Login** with test user:
   - Username: `testuser`
   - Password: `Test123!`

3. **Start using nInvoices!**
   - Create customers
   - Generate invoices
   - Manage templates

---

## 🔧 Managing Services

### View Service Status
```bash
cd docker
docker-compose -f docker-compose.dev.yml ps
```

### View Logs
```bash
# All services
docker-compose -f docker-compose.dev.yml logs -f

# Specific service
docker-compose -f docker-compose.dev.yml logs -f api
```

### Stop Services
```bash
docker-compose -f docker-compose.dev.yml down
```

### Start Services
```bash
docker-compose -f docker-compose.dev.yml up -d
```

### Restart a Service
```bash
docker-compose -f docker-compose.dev.yml restart api
```

---

## 👥 User Management

### Create Additional Users

1. Go to Keycloak Admin Console: http://localhost:8080/admin
2. Select `ninvoices` realm (top-left dropdown)
3. Click **Users** in the left menu
4. Click **Add user**
5. Fill in details and click **Create**
6. Go to **Credentials** tab
7. Set password and toggle "Temporary" to OFF

### Assign Roles (Optional)

For future role-based access:
1. Select user
2. Go to **Role mapping** tab
3. Assign roles as needed

---

## 🗄️ Database Management

### Connect to PostgreSQL
```bash
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db
```

### Backup Database
```bash
docker exec ninvoices-postgres-dev pg_dump -U ninvoices_user ninvoices_db > backup.sql
```

### Restore Database
```bash
docker exec -i ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db < backup.sql
```

---

## 🔐 Security Notes

### Current Configuration (Development)

⚠️ **This is a development setup** - for production use:

1. **Change all passwords** in `.env`:
   - Use strong, unique passwords
   - Minimum 16 characters
   - Mix of letters, numbers, and symbols
   - **Avoid special characters** that break JDBC (!, @, #, etc.)

2. **Enable HTTPS**:
   - Use `docker-compose.prod.yml`
   - Configure SSL certificates
   - Update Keycloak URLs to HTTPS

3. **Configure CORS** properly:
   - Update allowed origins in API configuration
   - Update Keycloak web origins

4. **Secure Keycloak**:
   - Enable admin user MFA
   - Configure session timeouts
   - Review client configurations

---

## 🐛 Troubleshooting

### Can't Login

**Check**: Are you using the correct credentials?
- Username: `testuser` (lowercase)
- Password: `Test123!` (case-sensitive)

**Check**: Are all services healthy?
```bash
docker-compose -f docker-compose.dev.yml ps
```

### API Returns 401 Errors

**Error: "The signature key was not found"**

This happens when the API cannot reach Keycloak to validate JWT tokens.

**Solution**: The API must use the Docker service name `keycloak` instead of `localhost`:
- Correct: `http://keycloak:8080/realms/ninvoices`
- Wrong: `http://localhost:8080/realms/ninvoices`

This is already configured in `docker-compose.dev.yml`.

**If still getting 401 after login:**
1. Logout completely
2. Clear browser cache/cookies
3. Login again
4. Refresh the page

### Database Connection Errors

**Check**: Is PostgreSQL healthy?
```bash
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db -c "SELECT 1;"
```

### More Help

See `docker/TROUBLESHOOTING.md` for complete troubleshooting guide.

---

## 📚 Documentation

- **Quick Start**: `QUICKSTART.md`
- **Deployment Guide**: `docker/README.md`
- **Database Migration**: `docker/MIGRATION.md`
- **Environment Config**: `docker/ENV-FILE-FAQ.md`
- **Troubleshooting**: `docker/TROUBLESHOOTING.md`
- **Technical Summary**: `DOCKERIZATION-SUMMARY.md`

---

## 🎉 Next Steps

1. **Test the application** thoroughly
2. **Create real users** in Keycloak
3. **Customize invoice templates**
4. **Import your data** (if migrating)
5. **Configure production** when ready

---

## 📊 Issues Resolved During Setup

### 1. Password Special Characters
- **Issue**: `!` in passwords broke JDBC connections
- **Fix**: Changed to alphanumeric passwords

### 2. Database Schema
- **Issue**: SQLite migrations incompatible with PostgreSQL
- **Fix**: Created PostgreSQL-specific schema script

### 3. Keycloak Admin Access
- **Issue**: Admin user created with unknown password
- **Fix**: Reset database with correct environment variables

### 4. JWT Audience Validation
- **Issue**: Access tokens missing required audience
- **Fix**: Created audience mapper for ninvoices-web client

### 5. Health Checks
- **Issue**: curl not available in Keycloak container
- **Fix**: Used TCP-based health check

---

## 💬 Support

If you encounter issues:

1. Check the logs: `docker-compose -f docker-compose.dev.yml logs`
2. Review `docker/TROUBLESHOOTING.md`
3. Verify environment variables: `docker exec <container> env`
4. Check service health: `docker-compose ps`

---

**🎊 Congratulations! Your nInvoices application is fully configured and ready to use.**

Happy invoicing! 🧾
