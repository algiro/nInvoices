# nInvoices Docker Deployment Guide

This guide covers deploying nInvoices with Docker, including Keycloak authentication and PostgreSQL database.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       Docker Network                         │
│                                                              │
│  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌─────────┐ │
│  │PostgreSQL│◄──│ Keycloak │   │   API    │   │   Web   │ │
│  │  (5432)  │   │  (8080)  │   │  (8080)  │   │  (80)   │ │
│  └──────────┘   └──────────┘   └──────────┘   └─────────┘ │
│       ▲              ▲              ▲              ▲        │
│       │              │              │              │        │
└───────┼──────────────┼──────────────┼──────────────┼────────┘
        │              │              │              │
        │         (Production Only)   │              │
        │              │              │              │
        │         ┌────┴──────────────┴──────────────┘
        │         │         Nginx Reverse Proxy
        │         │              (80/443)
        │         └────────────────┬──────────────
        │                          │
    External                   External
    Volumes                    Access
```

## Prerequisites

- Docker Engine 20.10+
- Docker Compose 2.0+
- At least 2GB free RAM
- At least 5GB free disk space

## Quick Start (Development)

### 1. Clone and Configure

```bash
# Clone repository
git clone https://github.com/yourusername/nInvoices.git
cd nInvoices

# Copy environment file
cp .env.example .env

# Edit .env with your preferred editor
nano .env
```

### 2. Configure Environment Variables

Update `.env` with your settings:

```env
# PostgreSQL
POSTGRES_USER=ninvoices_user
POSTGRES_PASSWORD=your_secure_password_here
POSTGRES_DB=ninvoices_db

# Keycloak Admin
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=your_admin_password_here

# Application URLs (for development)
KEYCLOAK_URL=http://localhost:8080
API_URL=http://localhost:5000
WEB_URL=http://localhost:3000
```

### 3. Start Services

```bash
cd docker
docker-compose -f docker-compose.dev.yml up -d
```

### 4. Verify Deployment

```bash
# Check all services are running
docker-compose -f docker-compose.dev.yml ps

# Check logs
docker-compose -f docker-compose.dev.yml logs -f

# Test API health
curl http://localhost:5000/api/health

# Access services:
# - Web App: http://localhost:3000
# - API: http://localhost:5000
# - Keycloak Admin: http://localhost:8080
```

### 5. Initial Setup

1. **Access Keycloak Admin Console**
   - URL: http://localhost:8080
   - Username: admin (from .env)
   - Password: (from .env)

2. **Configure Keycloak Realm**
   - The `ninvoices` realm should be auto-created
   - Default admin user: admin / admin123 (change on first login)

3. **Access Web Application**
   - URL: http://localhost:3000
   - Login with credentials: admin / admin123
   - You'll be prompted to change password on first login

## Production Deployment

### 1. SSL/TLS Certificates

Before deploying to production, obtain SSL certificates:

```bash
# Using Let's Encrypt (recommended)
sudo apt-get install certbot

# Generate certificates
sudo certbot certonly --standalone -d your-domain.com

# Copy certificates to docker volumes
mkdir -p docker/volumes/ssl
sudo cp /etc/letsencrypt/live/your-domain.com/fullchain.pem docker/volumes/ssl/cert.pem
sudo cp /etc/letsencrypt/live/your-domain.com/privkey.pem docker/volumes/ssl/key.pem
```

### 2. Configure Production Environment

Update `.env` for production:

```env
ENVIRONMENT=production

# Your domain
DOMAIN=your-domain.com

# Strong passwords (use password manager)
POSTGRES_PASSWORD=<generate-strong-password>
KEYCLOAK_ADMIN_PASSWORD=<generate-strong-password>

# Production URLs (HTTPS)
KEYCLOAK_URL=https://your-domain.com/auth
API_URL=https://your-domain.com/api
WEB_URL=https://your-domain.com

# Don't expose ports (Nginx handles external access)
POSTGRES_PORT=
KEYCLOAK_PORT=
```

### 3. Update Keycloak Realm Configuration

Edit `docker/keycloak/realm-export.json`:

```json
{
  "clients": [
    {
      "clientId": "ninvoices-web",
      "redirectUris": [
        "https://your-domain.com/*"
      ],
      "webOrigins": [
        "https://your-domain.com"
      ]
    }
  ]
}
```

### 4. Deploy to Production

```bash
cd docker
docker-compose -f docker-compose.prod.yml up -d
```

### 5. Verify Production Deployment

```bash
# Check services
docker-compose -f docker-compose.prod.yml ps

# Test endpoints
curl https://your-domain.com
curl https://your-domain.com/api/health
curl https://your-domain.com/auth/realms/ninvoices
```

## Database Management

### Backup Database

```bash
cd docker
./backup-database.sh
```

This creates a backup in `docker/volumes/backups/`

### Restore Database

```bash
cd docker
./restore-database.sh backup-YYYY-MM-DD-HH-MM-SS.sql
```

### Manual Backup/Restore

```bash
# Backup
docker exec ninvoices-postgres-dev pg_dump -U ninvoices_user ninvoices_db > backup.sql

# Restore
docker exec -i ninvoices-postgres-dev psql -U ninvoices_user ninvoices_db < backup.sql
```

## Troubleshooting

### Services Won't Start

```bash
# Check logs
docker-compose -f docker-compose.dev.yml logs

# Check specific service
docker-compose -f docker-compose.dev.yml logs postgres
docker-compose -f docker-compose.dev.yml logs keycloak
docker-compose -f docker-compose.dev.yml logs api

# Restart services
docker-compose -f docker-compose.dev.yml restart
```

### PostgreSQL Connection Issues

```bash
# Test PostgreSQL connectivity
docker exec ninvoices-postgres-dev pg_isready -U ninvoices_user

# Check if database exists
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -l

# Connect to database
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db
```

### Keycloak Issues

```bash
# Check Keycloak logs
docker logs ninvoices-keycloak-dev

# Restart Keycloak
docker restart ninvoices-keycloak-dev

# Access Keycloak shell
docker exec -it ninvoices-keycloak-dev bash
```

### API Authentication Errors

1. **Check Keycloak is accessible from API container:**
   ```bash
   docker exec ninvoices-api-dev curl http://keycloak:8080/realms/ninvoices
   ```

2. **Verify JWT configuration in appsettings:**
   ```bash
   docker exec ninvoices-api-dev cat /app/appsettings.json
   ```

3. **Check API logs:**
   ```bash
   docker logs ninvoices-api-dev --tail 100 -f
   ```

### Frontend Can't Authenticate

1. **Check browser console for errors**

2. **Verify environment variables in built app:**
   ```bash
   docker exec ninvoices-web-dev cat /usr/share/nginx/html/assets/index-*.js | grep VITE
   ```

3. **Check redirect URIs match in Keycloak**

### Reset Everything

```bash
cd docker

# Stop all services
docker-compose -f docker-compose.dev.yml down

# Remove volumes (WARNING: This deletes all data!)
sudo rm -rf volumes/

# Restart
docker-compose -f docker-compose.dev.yml up -d
```

## Updating

### Update Application

```bash
# Pull latest code
git pull

# Rebuild and restart
cd docker
docker-compose -f docker-compose.dev.yml up -d --build
```

### Update Database Schema

```bash
# The API automatically applies migrations on startup
# Or manually:
docker exec ninvoices-api-dev dotnet ef database update
```

## Performance Tuning

### PostgreSQL

Edit `docker/volumes/postgres/postgresql.conf`:

```conf
# Increase for better performance
shared_buffers = 256MB
effective_cache_size = 1GB
maintenance_work_mem = 64MB
work_mem = 16MB
```

Restart PostgreSQL after changes:
```bash
docker restart ninvoices-postgres-dev
```

### Nginx

For production, update `docker/nginx.prod.conf`:

```nginx
# Enable caching
proxy_cache_path /var/cache/nginx levels=1:2 keys_zone=api_cache:10m;

# In location blocks
proxy_cache api_cache;
proxy_cache_valid 200 5m;
```

## Security Best Practices

1. **Change all default passwords immediately**
2. **Use strong passwords (20+ characters)**
3. **Keep certificates up to date**
4. **Regular database backups (automated daily)**
5. **Monitor logs for suspicious activity**
6. **Keep Docker images updated**
7. **Use firewall to restrict access**
8. **Enable HTTPS only in production**
9. **Set up automated security updates**
10. **Regular security audits**

## Monitoring

### Health Checks

```bash
# API health
curl http://localhost:5000/api/health

# Keycloak health
curl http://localhost:8080/health/ready

# PostgreSQL health
docker exec ninvoices-postgres-dev pg_isready
```

### Resource Usage

```bash
# Container stats
docker stats

# Disk usage
docker system df
```

### Logs

```bash
# All logs
docker-compose -f docker-compose.dev.yml logs -f

# Specific service
docker-compose -f docker-compose.dev.yml logs -f api

# Save logs to file
docker-compose -f docker-compose.dev.yml logs > logs.txt
```

## Support

For issues or questions:
- Check logs first
- Review troubleshooting section
- Search existing GitHub issues
- Create new issue with logs and configuration (remove sensitive data!)
