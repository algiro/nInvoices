# Docker Registry Deployment - Quick Start

## TL;DR

```powershell
# Development Machine
cd docker
.\build-and-push.ps1
# Images pushed to: algiro/ninvoices-api, algiro/ninvoices-web

# Production Server
docker-compose pull
docker-compose up -d
# Done! ✅
```

---

## Workflow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    DEVELOPMENT MACHINE                          │
│                                                                 │
│  1. Build Images                                                │
│     ├─ API Image  (algiro/ninvoices-api)                       │
│     └─ Web Image  (algiro/ninvoices-web)                       │
│                                                                 │
│  2. Push to Docker Hub                                          │
│     ├─ algiro/ninvoices-api:latest                             │
│     ├─ algiro/ninvoices-api:1.0.0                              │
│     ├─ algiro/ninvoices-web:latest                             │
│     └─ algiro/ninvoices-web:1.0.0                              │
│                                                                 │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      │ Internet
                      ▼
           ┌──────────────────────┐
           │    Docker Hub        │
           │  (Public Registry)   │
           │                      │
           │  🐳 algiro/          │
           │    ninvoices-api     │
           │    ninvoices-web     │
           └──────────┬───────────┘
                      │
                      │ Pull images
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                    PRODUCTION SERVER(S)                         │
│                                                                 │
│  3. Pull Images from Docker Hub                                 │
│     docker-compose pull                                         │
│                                                                 │
│  4. Start Services                                              │
│     docker-compose up -d                                        │
│                                                                 │
│  ✅ Application Running!                                         │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

Multiple servers can pull the same images → Horizontal scaling
```

---

## Step-by-Step

### On Development Machine

#### 1. Login to Docker Hub (first time only)

```bash
docker login
# Username: algiro
# Password: [your password]
```

#### 2. Build and Push

```powershell
cd docker
.\build-and-push.ps1
```

**What it does:**
- ✅ Checks authentication
- ✅ Builds API image
- ✅ Builds Web image  
- ✅ Tags with version + latest
- ✅ Pushes to Docker Hub

**Time:** ~5-10 minutes (depending on internet speed)

#### 3. Verify on Docker Hub

Open browser:
- https://hub.docker.com/r/algiro/ninvoices-api
- https://hub.docker.com/r/algiro/ninvoices-web

You should see your images!

---

### On Production Server

#### 1. Copy Files

**Option A: Manual Copy**
```bash
# Create directory
mkdir -p /opt/ninvoices/init-scripts

# Copy files from dev machine
scp docker/docker-compose.registry.yml user@prod:/opt/ninvoices/docker-compose.yml
scp docker/.env.production.example user@prod:/opt/ninvoices/.env
scp docker/init-scripts/* user@prod:/opt/ninvoices/init-scripts/
```

**Option B: Git Clone**
```bash
cd /opt/ninvoices
git clone https://github.com/yourusername/ninvoices.git .
cp docker/docker-compose.registry.yml docker-compose.yml
cp docker/.env.production.example .env
```

#### 2. Configure Environment

```bash
cd /opt/ninvoices
nano .env
```

**Required changes:**
```env
# Passwords (use strong passwords!)
POSTGRES_PASSWORD=YourProductionPassword123
KEYCLOAK_ADMIN_PASSWORD=YourAdminPassword123

# Docker Hub settings
DOCKER_USERNAME=algiro
IMAGE_TAG=latest  # or specific version like 1.0.0

# Domain (if using)
DOMAIN=yourdomain.com
```

#### 3. Pull Images

```bash
# No authentication needed for public images
docker-compose pull
```

**Output:**
```
Pulling postgres  ... done
Pulling keycloak  ... done
Pulling api       ... done
Pulling web       ... done
```

#### 4. Start Services

```bash
docker-compose up -d
```

#### 5. Verify

```bash
# Check status
docker-compose ps

# View logs
docker-compose logs -f

# Test health
curl http://localhost:8080/api/health
curl http://localhost:3000
```

---

## Updating to New Version

### Development Machine

```powershell
# Build and push new version
cd docker
.\build-and-push.ps1
# Enter version: 1.1.0
```

### Production Server

```bash
cd /opt/ninvoices

# Update version in .env
nano .env
# Change: IMAGE_TAG=1.1.0

# Pull new version
docker-compose pull

# Restart with new images
docker-compose down
docker-compose up -d
```

**Zero-downtime update** (advanced):
```bash
# Pull new images while old ones run
docker-compose pull

# Recreate only changed containers
docker-compose up -d
```

---

## Multi-Server Deployment

Same images can be deployed to multiple servers:

```bash
# Server 1
cd /opt/ninvoices
docker-compose pull
docker-compose up -d

# Server 2 (identical commands)
cd /opt/ninvoices
docker-compose pull
docker-compose up -d

# Server 3 (identical commands)
cd /opt/ninvoices
docker-compose pull
docker-compose up -d
```

**Benefits:**
- ✅ Consistent across all servers
- ✅ Easy horizontal scaling
- ✅ Load balancing ready
- ✅ Fast deployment (no build time)

---

## Rollback

```bash
# Change to previous version
nano .env
# Set: IMAGE_TAG=1.0.0

# Pull previous version
docker-compose pull

# Restart
docker-compose down
docker-compose up -d
```

---

## File Checklist

**On Production Server you need:**

```
/opt/ninvoices/
├── docker-compose.yml         ✅ (from docker-compose.registry.yml)
├── .env                       ✅ (configured with your settings)
├── init-scripts/              ✅
│   ├── 01-init-databases.sql
│   └── 02-ninvoices-schema.sql
└── volumes/                   ⚠️ (created automatically, don't copy)
    ├── postgres/
    ├── keycloak/
    └── logs/
```

**You DON'T need:**
- ❌ Source code
- ❌ Dockerfile.api / Dockerfile.web
- ❌ node_modules
- ❌ Build artifacts

**Why?** Images are pre-built and available in Docker Hub!

---

## Advantages of This Approach

### vs Building on Production

| Aspect | Registry Method | Build on Prod |
|--------|----------------|---------------|
| **Deployment Speed** | Fast (pull only) | Slow (build every time) |
| **Build Resources** | Not needed | Need build tools |
| **Consistency** | Same image everywhere | Different builds |
| **Rollback** | Easy (pull old tag) | Must rebuild |
| **Multiple Servers** | Easy (same image) | Build on each |
| **CI/CD** | Perfect fit | Complex |

### vs Copying Images

| Aspect | Registry Method | Copy Files |
|--------|----------------|------------|
| **Transfer Size** | Only changed layers | Full image every time |
| **Internet Required** | Yes | No |
| **Version Control** | Built-in (tags) | Manual |
| **Distribution** | One push, many pulls | Copy to each server |

---

## Troubleshooting

### "Image not found"

```bash
# Check image name
docker pull algiro/ninvoices-api:latest
docker pull algiro/ninvoices-web:latest

# Check Docker Hub
curl -s https://hub.docker.com/v2/repositories/algiro/ninvoices-api/tags | jq
```

### "Authentication required"

If you made images private:
```bash
docker login
docker-compose pull
```

### "No such file: docker-compose.yml"

```bash
# Rename registry compose file
cp docker-compose.registry.yml docker-compose.yml
```

### Build Failed During Push

```bash
# Check Docker build context
docker build -t test -f Dockerfile.api ..

# Check network connectivity
docker login
```

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build and Push

on:
  push:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      
      - name: Login to Docker Hub
        uses: docker/login-action@v1
        with:
          username: ${{ secrets.DOCKER_USERNAME }}
          password: ${{ secrets.DOCKER_PASSWORD }}
      
      - name: Build and push API
        uses: docker/build-push-action@v2
        with:
          context: .
          file: docker/Dockerfile.api
          push: true
          tags: algiro/ninvoices-api:latest
      
      - name: Build and push Web
        uses: docker/build-push-action@v2
        with:
          context: .
          file: docker/Dockerfile.web
          push: true
          tags: algiro/ninvoices-web:latest
```

---

## Best Practices

### Versioning

✅ **Do:**
- Use semantic versioning: `1.0.0`, `1.1.0`, `2.0.0`
- Always tag with version + `latest`
- Document changes in release notes

❌ **Don't:**
- Use only `latest` in production
- Reuse version tags
- Skip versioning

### Security

✅ **Do:**
- Use secrets management for .env
- Scan images for vulnerabilities
- Keep base images updated
- Restrict Docker Hub access

❌ **Don't:**
- Commit .env to git
- Expose sensitive data in images
- Use `latest` without version backup

### Testing

✅ **Do:**
- Test images locally before pushing
- Test pulled images before deployment
- Have rollback plan ready

❌ **Don't:**
- Deploy untested images
- Skip staging environment
- Update production without backup

---

## Quick Commands Reference

```bash
# Build and push
cd docker && .\build-and-push.ps1

# Pull on production
docker-compose pull

# Deploy
docker-compose up -d

# Update to new version
# 1. Edit .env (change IMAGE_TAG)
# 2. docker-compose pull
# 3. docker-compose up -d

# Rollback
# 1. Edit .env (previous IMAGE_TAG)
# 2. docker-compose pull
# 3. docker-compose down && docker-compose up -d

# View logs
docker-compose logs -f

# Check status
docker-compose ps

# Stop all
docker-compose down
```

---

## Support

**Detailed guides:**
- `docker/PRODUCTION-DEPLOYMENT.md` - Complete deployment guide
- `docker/TROUBLESHOOTING.md` - Common issues
- `docker/QUICK-REFERENCE.md` - Command reference

**Docker Hub:**
- https://hub.docker.com/r/algiro/ninvoices-api
- https://hub.docker.com/r/algiro/ninvoices-web

---

**Status:** ✅ Ready for production deployment with Docker Registry
