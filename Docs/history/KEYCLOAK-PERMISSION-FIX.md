# Keycloak Permission Issue - Quick Fix

## Problem
Keycloak fails to start with error:
```
ERROR: ARJUNA012225: FileSystemStore::setupStore - cannot access root of object store: /opt/keycloak/bin/../data/transaction-logs/
```

## Root Cause
The Keycloak container runs as user ID 1000, but the mounted volume `./volumes/keycloak` doesn't have correct permissions.

## Solution

### Option 1: Fix Permissions (Recommended)

**On your production server:**

```bash
cd ~/docker  # or wherever your docker-compose.yml is

# Stop containers
sudo docker compose down

# Fix permissions (Keycloak runs as UID 1000)
sudo chown -R 1000:1000 volumes/keycloak
sudo chmod -R 755 volumes/keycloak

# Start again
sudo docker compose up -d

# Check status
sudo docker compose ps
sudo docker logs ninvoices-keycloak-prod --tail 50
```

### Option 2: Fresh Start (If Option 1 Doesn't Work)

```bash
cd ~/docker

# Stop containers
sudo docker compose down

# Remove keycloak volume completely
sudo rm -rf volumes/keycloak

# Recreate with correct ownership
mkdir -p volumes/keycloak
sudo chown 1000:1000 volumes/keycloak
sudo chmod 755 volumes/keycloak

# Start again
sudo docker compose up -d
```

### Option 3: Use Named Volume Instead of Bind Mount

Edit `docker-compose.yml` to use named volume:

```yaml
keycloak:
  # ... other settings ...
  volumes:
    # Change this:
    # - ./volumes/keycloak:/opt/keycloak/data
    
    # To this:
    - keycloak-data:/opt/keycloak/data

# At the bottom of file, add:
volumes:
  keycloak-data:
```

Named volumes handle permissions automatically.

## Verification

After applying fix:

```bash
# Check container status
sudo docker compose ps

# Should show all containers as "healthy"
# If Keycloak is still unhealthy, check logs:
sudo docker logs ninvoices-keycloak-prod --tail 100

# Should NOT see ARJUNA errors anymore
# Should see: "Keycloak 26.0.8 started"
```

## Why This Happens

1. **Docker volume mount**: `./volumes/keycloak` is created with root ownership
2. **Keycloak container**: Runs as non-root user (UID 1000)
3. **Permission denied**: Container can't write to root-owned directory
4. **Result**: Keycloak can't create transaction logs → fails to start

## Prevention

For future deployments, create volumes with correct permissions BEFORE starting:

```bash
# Before first docker-compose up:
mkdir -p volumes/{postgres,keycloak,logs}
sudo chown -R 1000:1000 volumes/keycloak
sudo chown -R 999:999 volumes/postgres
```

## Additional Notes

### Warning in Output
You may also see this warning:
```
WARN[0000] the attribute `version` is obsolete
```

**Fix**: Remove `version: '3.9'` from docker-compose.yml (line 1)

Modern Docker Compose doesn't need the version field.

### Health Check Timing

If Keycloak starts but health check fails:

```yaml
keycloak:
  healthcheck:
    start_period: 90s  # Increase from 60s
    interval: 40s      # Increase from 30s
```

Keycloak can take longer to start on slower servers.

## Complete Fix Script

Save this as `fix-keycloak.sh`:

```bash
#!/bin/bash
echo "Fixing Keycloak permissions..."

cd ~/docker

# Stop
sudo docker compose down

# Fix
sudo chown -R 1000:1000 volumes/keycloak
sudo chmod -R 755 volumes/keycloak

# Start
sudo docker compose up -d

# Wait
sleep 20

# Check
sudo docker compose ps
echo ""
echo "Check Keycloak logs:"
echo "sudo docker logs ninvoices-keycloak-prod --tail 50"
```

Run with:
```bash
chmod +x fix-keycloak.sh
./fix-keycloak.sh
```

## If Still Failing

Check:
1. **Disk space**: `df -h`
2. **Memory**: `free -h` (Keycloak needs ~512MB)
3. **Database connection**: 
   ```bash
   sudo docker exec ninvoices-keycloak-prod psql -h postgres -U ninvoices_user -d keycloak_db -c "SELECT 1;"
   ```
4. **Full logs**: 
   ```bash
   sudo docker logs ninvoices-keycloak-prod --tail 200
   ```

## Summary

**Quick fix:**
```bash
sudo docker compose down
sudo chown -R 1000:1000 volumes/keycloak
sudo docker compose up -d
```

This should resolve the issue! The key is making sure the Keycloak container (which runs as UID 1000) can write to its data directory.
