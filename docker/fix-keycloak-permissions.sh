#!/bin/bash
# Fix Keycloak Permissions Issue
# This script fixes the permission problem preventing Keycloak from starting

echo "========================================="
echo "Fixing Keycloak Permissions"
echo "========================================="
echo ""

# Stop all containers
echo "[1/4] Stopping containers..."
sudo docker compose down

# Fix volume permissions
echo "[2/4] Fixing volume permissions..."
sudo chown -R 1000:1000 volumes/keycloak
sudo chmod -R 755 volumes/keycloak

# Alternative: Remove and recreate volume (if above doesn't work)
# echo "Removing keycloak volume..."
# sudo rm -rf volumes/keycloak
# mkdir -p volumes/keycloak
# sudo chown 1000:1000 volumes/keycloak

# Start containers
echo "[3/4] Starting containers..."
sudo docker compose up -d

# Wait and check status
echo "[4/4] Waiting for containers to start..."
sleep 15

echo ""
echo "Container status:"
sudo docker compose ps

echo ""
echo "========================================="
echo "If Keycloak is still unhealthy, check logs:"
echo "  sudo docker logs ninvoices-keycloak-prod"
echo "========================================="
