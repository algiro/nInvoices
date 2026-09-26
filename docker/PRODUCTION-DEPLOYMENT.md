# Production Deployment Guide - Docker Registry Method

This guide covers deploying nInvoices to production using pre-built Docker images from Docker Hub.

## Overview

### Deployment Strategy

Instead of building images on the production server, we:
1. **Build** images on development machine
2. **Push** to Docker Hub registry
3. **Pull** from registry on production server
4. **Deploy** with docker-compose

### Benefits

✅ **Faster deployments** - No build time on production  
✅ **Consistent builds** - Same image tested in dev  
✅ **Easy rollbacks** - Pull previous image version  
✅ **Multiple servers** - Deploy same image to many servers  
✅ **CI/CD ready** - Integrate with automated pipelines

---

## Prerequisites

### On Development Machine
- Docker Desktop or Docker Engine
- Docker Hub account (username: `algiro`)
- Access to nInvoices source code
- PowerShell or Bash

### On Production Server
- Docker Engine 20.10+ installed
- Docker Compose 2.0+ installed
- Internet connection (to pull images)
- At least 2GB RAM
- At least 10GB disk space

---

## Part 1: Build and Push Images (Development Machine)

### Step 1: Login to Docker Hub

```bash
docker login
```

Enter your Docker Hub credentials:
- Username: `algiro`
- Password: [Your Docker Hub password]

### Step 2: Deploy with One Command

```powershell
# Windows PowerShell — from the docker/ directory
cd docker

# Full deploy (build + push + pull on remote + restart)
.\deploy.ps1

# Deploy only API changes
.\deploy.ps1 -ApiOnly

# Deploy only frontend changes
.\deploy.ps1 -WebOnly

# Skip build, just pull and restart on remote server
.\deploy.ps1 -SkipBuild
```

The script will:
1. ✅ Build API and Web Docker images with correct production config
2. ✅ Push images to Docker Hub
3. ✅ SSH to the remote server and pull new images
4. ✅ Restart containers
5. ✅ Run health checks

> ⚠️ **Do NOT run `build-and-push.ps1` without parameters for production!** It defaults to localhost URLs. Always use `deploy.ps1` which has production values hardcoded.

**Expected output:**
```
╔═══════════════════════════════════════════════════════════════════╗
║         Building and Pushing nInvoices Images to Docker Hub       ║
╚═══════════════════════════════════════════════════════════════════╝

🔐 Checking Docker Hub authentication...
✅ Docker Hub authentication OK

Enter version tag (default: latest): 1.0.0

═══════════════════════════════════════════════════════════════════
📦 Building API Image
═══════════════════════════════════════════════════════════════════

Image: algiro/ninvoices-api:1.0.0
Building...
[+] Building 45.2s
✅ API image built successfully

═══════════════════════════════════════════════════════════════════
📦 Building Web Image
═══════════════════════════════════════════════════════════════════

Image: algiro/ninvoices-web:1.0.0
Building...
[+] Building 52.1s
✅ Web image built successfully

═══════════════════════════════════════════════════════════════════
☁️  Pushing Images to Docker Hub
═══════════════════════════════════════════════════════════════════

Pushing API image (1.0.0)...
✅ API image pushed

Pushing API image (latest)...
✅ API image (latest) pushed

Pushing Web image (1.0.0)...
✅ Web image pushed

Pushing Web image (latest)...
✅ Web image (latest) pushed

╔═══════════════════════════════════════════════════════════════════╗
║                    ✅ SUCCESS - Images Pushed!                     ║
╚═══════════════════════════════════════════════════════════════════╝
```

### Step 3: Verify Images on Docker Hub

Visit:
- API: https://hub.docker.com/r/algiro/ninvoices-api
- Web: https://hub.docker.com/r/algiro/ninvoices-web

You should see your images with tags `latest` and your version number.

---

## Part 2: Deploy on Production Server

### Step 1: Prepare Production Server

#### Install Docker

**Ubuntu/Debian:**
```bash
# Update package list
sudo apt update

# Install dependencies
sudo apt install -y apt-transport-https ca-certificates curl software-properties-common

# Add Docker GPG key
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg

# Add Docker repository
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Install Docker
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io

# Install Docker Compose
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose

# Add user to docker group (optional - run docker without sudo)
sudo usermod -aG docker $USER
newgrp docker

# Verify installation
docker --version
docker-compose --version
```

**CentOS/RHEL:**
```bash
sudo yum install -y yum-utils
sudo yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
sudo yum install -y docker-ce docker-ce-cli containerd.io
sudo systemctl start docker
sudo systemctl enable docker

# Install Docker Compose
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose
```

#### Create Application Directory

```bash
# Create directory structure
sudo mkdir -p /opt/ninvoices/{volumes/{postgres,keycloak,logs,ssl},init-scripts}
cd /opt/ninvoices

# Set permissions
sudo chown -R $USER:$USER /opt/ninvoices
```

### Step 2: Copy Required Files to Production Server

From your **development machine**, copy these files to production:

```bash
# Using SCP
scp docker/docker-compose.registry.yml user@production-server:/opt/ninvoices/docker-compose.yml
scp docker/.env.production.example user@production-server:/opt/ninvoices/.env.example
scp docker/init-scripts/* user@production-server:/opt/ninvoices/init-scripts/
scp docker/nginx.prod.conf user@production-server:/opt/ninvoices/nginx.prod.conf

# Or using rsync
rsync -av docker/ user@production-server:/opt/ninvoices/ \
  --include='docker-compose.registry.yml' \
  --include='.env.production.example' \
  --include='init-scripts/*' \
  --include='nginx.prod.conf' \
  --exclude='*'
```

**Files needed on production:**
```
/opt/ninvoices/
├── docker-compose.yml          (renamed from docker-compose.registry.yml)
├── .env                        (created from .env.production.example)
├── nginx.prod.conf             (if using Nginx)
├── init-scripts/
│   ├── 01-init-databases.sql
│   └── 02-ninvoices-schema.sql
└── volumes/                    (created automatically)
    ├── postgres/
    ├── keycloak/
    ├── logs/
    └── ssl/                    (if using HTTPS)
```

### Step 3: Configure Production Environment

**On production server:**

```bash
cd /opt/ninvoices

# Copy example .env
cp .env.example .env

# Edit with your production settings
nano .env
```

**Critical settings to configure:**

```env
# Docker Hub configuration
DOCKER_USERNAME=algiro
IMAGE_TAG=latest  # or specific version like 1.0.0

# Strong passwords (alphanumeric only!)
POSTGRES_PASSWORD=YourProductionPassword123
KEYCLOAK_ADMIN_PASSWORD=YourAdminPassword123

# Production domain (if using)
DOMAIN=yourdomain.com
KC_HOSTNAME_STRICT=true

# Ports (adjust if needed)
WEB_PORT=3000
API_PORT=8080
KEYCLOAK_PORT=8080
```

**⚠️ Security Notes:**
- ✅ Use strong, unique passwords
- ✅ Avoid special characters in passwords (`!@#$%` break JDBC)
- ✅ Never commit .env to version control
- ✅ Consider using secrets management (Azure Key Vault, AWS Secrets Manager)
- ✅ Restrict file permissions: `chmod 600 .env`

### Step 4: Pull Images from Docker Hub

```bash
cd /opt/ninvoices

# Pull images (no authentication needed for public images)
docker-compose pull

# Verify images downloaded
docker images | grep ninvoices
```

**Expected output:**
```
algiro/ninvoices-api    latest    abc123def456    2 hours ago     450MB
algiro/ninvoices-web    latest    def456ghi789    2 hours ago     150MB
postgres                17-alpine 123abc456def    3 days ago      250MB
```

### Step 5: Start Services

```bash
# Start in detached mode
docker-compose up -d

# Watch logs
docker-compose logs -f

# Check status (wait for all to be "healthy")
docker-compose ps
```

**Expected output:**
```
NAME                        STATUS              PORTS
ninvoices-postgres-prod     Up (healthy)        5432/tcp
ninvoices-keycloak-prod     Up (healthy)        0.0.0.0:8080->8080/tcp
ninvoices-api-prod          Up (healthy)        0.0.0.0:8080->8080/tcp
ninvoices-web-prod          Up (healthy)        0.0.0.0:3000->3000/tcp
```

### Step 6: Verify Deployment

```bash
# Check API health
curl http://localhost:8080/api/health

# Check Keycloak
curl http://localhost:8080/realms/ninvoices/.well-known/openid-configuration

# Check Web app
curl http://localhost:3000
```

### Step 7: Configure Keycloak

1. **Access Keycloak admin console:**
   - URL: `http://your-server-ip:8080/admin`
   - Login: `admin` / (password from .env)

2. **Import realm or configure manually:**
   ```bash
   # Option A: Import realm JSON (if you have it)
   docker cp keycloak/realm-export.json ninvoices-keycloak-prod:/tmp/
   docker exec ninvoices-keycloak-prod /opt/keycloak/bin/kc.sh import --file /tmp/realm-export.json
   
   # Option B: Configure manually via admin console
   # Follow steps in docker/KEYCLOAK-DOCKER-GUIDE.md
   ```

3. **Create application clients:** (if not imported)
   - `ninvoices-api` - Service account
   - `ninvoices-web` - Public client with PKCE

4. **Create test user:**
   - Username: `testuser`
   - Password: `Test123!`

5. **Add audience mapper** (critical!):
   - Client: `ninvoices-web`
   - Mapper type: Audience
   - Included Client Audience: `ninvoices-api`

---

## Part 3: Production Hardening

### Enable HTTPS with Nginx (Recommended)

#### Step 1: Obtain SSL Certificate

**Using Let's Encrypt (recommended):**
```bash
# Install certbot
sudo apt install -y certbot

# Get certificate
sudo certbot certonly --standalone -d yourdomain.com

# Certificates will be in:
# /etc/letsencrypt/live/yourdomain.com/fullchain.pem
# /etc/letsencrypt/live/yourdomain.com/privkey.pem

# Copy to volumes
sudo cp /etc/letsencrypt/live/yourdomain.com/fullchain.pem /opt/ninvoices/volumes/ssl/
sudo cp /etc/letsencrypt/live/yourdomain.com/privkey.pem /opt/ninvoices/volumes/ssl/
```

#### Step 2: Enable Nginx in docker-compose.yml

Uncomment the nginx service in `docker-compose.yml`

#### Step 3: Update nginx.prod.conf

```nginx
# Update server_name
server_name yourdomain.com;

# Update SSL certificate paths
ssl_certificate /etc/nginx/ssl/fullchain.pem;
ssl_certificate_key /etc/nginx/ssl/privkey.pem;
```

#### Step 4: Restart with Nginx

```bash
docker-compose up -d
```

Now access via:
- **Frontend**: https://yourdomain.com
- **API**: https://yourdomain.com/api
- **Keycloak**: https://yourdomain.com/auth

### Configure Firewall

```bash
# Allow HTTP/HTTPS
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# Allow SSH (if needed)
sudo ufw allow 22/tcp

# Enable firewall
sudo ufw enable
```

### Set Up Automated Backups

```bash
# Create backup script
cat > /opt/ninvoices/backup.sh << 'EOF'
#!/bin/bash
BACKUP_DIR="/opt/ninvoices/backups"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p $BACKUP_DIR

# Backup database
docker exec ninvoices-postgres-prod pg_dump -U ninvoices_user ninvoices_db > $BACKUP_DIR/ninvoices_${DATE}.sql

# Backup Keycloak database
docker exec ninvoices-postgres-prod pg_dump -U ninvoices_user keycloak_db > $BACKUP_DIR/keycloak_${DATE}.sql

# Keep only last 7 days
find $BACKUP_DIR -name "*.sql" -mtime +7 -delete

echo "Backup completed: $DATE"
EOF

chmod +x /opt/ninvoices/backup.sh

# Schedule daily backup at 2 AM
(crontab -l 2>/dev/null; echo "0 2 * * * /opt/ninvoices/backup.sh") | crontab -
```

### Enable Log Rotation

```bash
# Create logrotate config
sudo cat > /etc/logrotate.d/ninvoices << 'EOF'
/opt/ninvoices/volumes/logs/*.log {
    daily
    rotate 14
    compress
    delaycompress
    notifempty
    create 0644 root root
    sharedscripts
}
EOF
```

### Monitor Container Health

```bash
# Create health check script
cat > /opt/ninvoices/healthcheck.sh << 'EOF'
#!/bin/bash

# Check container health
unhealthy=$(docker ps --filter "name=ninvoices" --format "{{.Names}}: {{.Status}}" | grep -v "healthy")

if [ -n "$unhealthy" ]; then
    echo "⚠️ Unhealthy containers detected:"
    echo "$unhealthy"
    
    # Optional: Send alert (email, Slack, etc.)
    # curl -X POST https://hooks.slack.com/... -d "..."
    
    exit 1
fi

echo "✅ All containers healthy"
EOF

chmod +x /opt/ninvoices/healthcheck.sh

# Run every 5 minutes
(crontab -l 2>/dev/null; echo "*/5 * * * * /opt/ninvoices/healthcheck.sh") | crontab -
```

---

## Part 4: Updates and Maintenance

### Deploying New Version

**On development machine:**
```powershell
# Build, push, and deploy in one step
cd docker
.\deploy.ps1
```

**On production server:**
```bash
cd /opt/ninvoices

# Update .env with new version
nano .env
# Change: IMAGE_TAG=1.1.0

# Pull new images
docker-compose pull

# Stop and remove old containers
docker-compose down

# Start with new images
docker-compose up -d

# Verify
docker-compose ps
docker-compose logs -f
```

### Rolling Back

```bash
# Change to previous version
nano .env
# Change: IMAGE_TAG=1.0.0

# Pull previous version
docker-compose pull

# Restart
docker-compose down
docker-compose up -d
```

### Viewing Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker logs ninvoices-api-prod -f
docker logs ninvoices-keycloak-prod -f

# Last 100 lines
docker logs ninvoices-api-prod --tail 100

# Since timestamp
docker logs ninvoices-api-prod --since 2024-01-01T00:00:00
```

### Database Backup and Restore

**Backup:**
```bash
# Full backup
docker exec ninvoices-postgres-prod pg_dump -U ninvoices_user ninvoices_db > backup.sql

# Compressed backup
docker exec ninvoices-postgres-prod pg_dump -U ninvoices_user ninvoices_db | gzip > backup.sql.gz
```

**Restore:**
```bash
# From SQL file
cat backup.sql | docker exec -i ninvoices-postgres-prod psql -U ninvoices_user -d ninvoices_db

# From compressed
gunzip -c backup.sql.gz | docker exec -i ninvoices-postgres-prod psql -U ninvoices_user -d ninvoices_db
```

### Scaling (Multiple Servers)

Since images are in Docker Hub, you can deploy to multiple servers:

```bash
# Server 1
docker-compose up -d

# Server 2 (same commands)
docker-compose up -d

# Server 3 (same commands)
docker-compose up -d
```

All servers pull the same images and run identically.

---

## Part 5: Troubleshooting

### Images Won't Pull

**Problem:** `unauthorized: authentication required`

**Solution:**
```bash
# Public images don't need auth, but if you made them private:
docker login
# Enter Docker Hub credentials
docker-compose pull
```

### Container Won't Start

**Check logs:**
```bash
docker logs ninvoices-api-prod
docker logs ninvoices-keycloak-prod
```

**Common issues:**
- Database not ready: Wait for PostgreSQL health check
- Wrong credentials: Check .env file
- Port already in use: Change port in .env

### Authentication Fails

**Verify BackchannelHandler:**
```bash
# Check API logs for cert fetching
docker logs ninvoices-api-prod | grep "certs"

# Should see: GET http://keycloak:8080/.../certs - 200
# NOT: GET http://localhost:8080/.../certs - 404
```

**Solution:** Ensure `KeycloakBackchannelHandler.cs` is in the image.

### Database Connection Issues

**Test connection:**
```bash
docker exec ninvoices-api-prod \
  psql -h postgres -U ninvoices_user -d ninvoices_db -c "SELECT 1;"
```

**Check credentials in .env match**

---

## Part 6: Monitoring

### Basic Monitoring

```bash
# Resource usage
docker stats ninvoices-api-prod ninvoices-keycloak-prod ninvoices-postgres-prod

# Disk usage
docker system df

# Container uptime
docker ps --format "table {{.Names}}\t{{.Status}}"
```

### Advanced Monitoring

**Prometheus + Grafana:**
- Add Prometheus exporters to docker-compose
- Configure Grafana dashboards
- Set up alerts

**Cloud Monitoring:**
- Azure Monitor
- AWS CloudWatch
- Google Cloud Monitoring

---

## Summary

### Deployment Checklist

**Development Machine:**
- [ ] Build images with `build-and-push.ps1`
- [ ] Verify images on Docker Hub
- [ ] Test images locally

**Production Server:**
- [ ] Install Docker and Docker Compose
- [ ] Create application directory
- [ ] Copy required files
- [ ] Configure .env with production settings
- [ ] Pull images from Docker Hub
- [ ] Start services
- [ ] Configure Keycloak realm and clients
- [ ] Test authentication
- [ ] Enable HTTPS (recommended)
- [ ] Configure firewall
- [ ] Set up automated backups
- [ ] Configure monitoring

### Key Commands

```bash
# Pull images
docker-compose pull

# Start services
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f

# Stop services
docker-compose down

# Update to new version
# 1. Edit .env (change IMAGE_TAG)
# 2. docker-compose pull
# 3. docker-compose down
# 4. docker-compose up -d
```

### Access Points

- **Frontend**: http://server-ip:3000 (or https://domain.com with Nginx)
- **API**: http://server-ip:8080 (or https://domain.com/api)
- **Keycloak**: http://server-ip:8080/admin

### Support

For issues, consult:
- `docker/TROUBLESHOOTING.md`
- `docker/KEYCLOAK-DOCKER-GUIDE.md`
- `docker/JWT-AUTHENTICATION-FIX.md`

---

**Deployment Status**: ✅ Ready for production with Docker Registry
