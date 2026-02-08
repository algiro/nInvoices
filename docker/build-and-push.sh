#!/bin/bash
# Build and Push Images to Docker Hub
# 
# This script builds nInvoices API and Web images and pushes them to Docker Hub
# Usage: ./build-and-push.sh

# Configuration
DOCKER_USERNAME="algiro"
API_IMAGE="$DOCKER_USERNAME/ninvoices-api"
WEB_IMAGE="$DOCKER_USERNAME/ninvoices-web"

echo "================================================================"
echo "   Building and Pushing nInvoices Images to Docker Hub"
echo "================================================================"
echo ""

# Check if logged into Docker Hub
echo "[1/6] Checking Docker Hub authentication..."
if ! docker info | grep -q "Username"; then
    echo "Not logged into Docker Hub. Please login:"
    docker login
    if [ $? -ne 0 ]; then
        echo ""
        echo "Docker login failed. Exiting."
        exit 1
    fi
fi
echo "Docker Hub authentication OK"
echo ""

# Get version tag
read -p "Enter version tag (default: latest): " version
version=${version:-latest}

echo ""
echo "================================================================"
echo "[2/6] Building API Image"
echo "================================================================"
echo ""

echo "Image: $API_IMAGE:$version"
echo "Building..."
echo ""

docker build -t "${API_IMAGE}:${version}" -f Dockerfile.api ..
if [ $? -ne 0 ]; then
    echo ""
    echo "API build failed. Exiting."
    exit 1
fi

echo ""
echo "API image built successfully"

# Tag as latest if not already
if [ "$version" != "latest" ]; then
    echo "Tagging as latest..."
    docker tag "${API_IMAGE}:${version}" "${API_IMAGE}:latest"
fi

echo ""
echo "================================================================"
echo "[3/6] Building Web Image"
echo "================================================================"
echo ""

echo "Image: $WEB_IMAGE:$version"
echo "Building..."
echo ""

docker build \
    -t "${WEB_IMAGE}:${version}" \
    --build-arg VITE_KEYCLOAK_URL=http://localhost:8080 \
    --build-arg VITE_KEYCLOAK_REALM=ninvoices \
    --build-arg VITE_KEYCLOAK_CLIENT_ID=ninvoices-web \
    --build-arg VITE_API_URL=http://localhost:8080 \
    -f Dockerfile.web ..

if [ $? -ne 0 ]; then
    echo ""
    echo "Web build failed. Exiting."
    exit 1
fi

echo ""
echo "Web image built successfully"

# Tag as latest if not already
if [ "$version" != "latest" ]; then
    echo "Tagging as latest..."
    docker tag "${WEB_IMAGE}:${version}" "${WEB_IMAGE}:latest"
fi

echo ""
echo "================================================================"
echo "[4/6] Pushing API Image to Docker Hub"
echo "================================================================"
echo ""

echo "Pushing $API_IMAGE:$version..."
docker push "${API_IMAGE}:${version}"
if [ $? -ne 0 ]; then
    echo ""
    echo "Failed to push API image. Exiting."
    exit 1
fi
echo "API image ($version) pushed successfully"

if [ "$version" != "latest" ]; then
    echo ""
    echo "Pushing $API_IMAGE:latest..."
    docker push "${API_IMAGE}:latest"
    echo "API image (latest) pushed successfully"
fi

echo ""
echo "================================================================"
echo "[5/6] Pushing Web Image to Docker Hub"
echo "================================================================"
echo ""

echo "Pushing $WEB_IMAGE:$version..."
docker push "${WEB_IMAGE}:${version}"
if [ $? -ne 0 ]; then
    echo ""
    echo "Failed to push Web image. Exiting."
    exit 1
fi
echo "Web image ($version) pushed successfully"

if [ "$version" != "latest" ]; then
    echo ""
    echo "Pushing $WEB_IMAGE:latest..."
    docker push "${WEB_IMAGE}:latest"
    echo "Web image (latest) pushed successfully"
fi

echo ""
echo "================================================================"
echo "[6/6] SUCCESS - Images Pushed to Docker Hub!"
echo "================================================================"
echo ""

echo "Images available at Docker Hub:"
echo ""
echo "   API:  https://hub.docker.com/r/$DOCKER_USERNAME/ninvoices-api"
echo "   Web:  https://hub.docker.com/r/$DOCKER_USERNAME/ninvoices-web"
echo ""

echo "Tags pushed:"
echo "   * ${API_IMAGE}:${version}"
echo "   * ${WEB_IMAGE}:${version}"
if [ "$version" != "latest" ]; then
    echo "   * ${API_IMAGE}:latest"
    echo "   * ${WEB_IMAGE}:latest"
fi

echo ""
echo "Image sizes:"
docker images | grep "$DOCKER_USERNAME/ninvoices"

echo ""
echo "================================================================"
echo "Next steps for production deployment:"
echo "================================================================"
echo ""
echo "1. Copy docker-compose.registry.yml to production server"
echo "2. Copy .env file and configure production settings"
echo "3. Copy nginx.prod.conf if using Nginx"
echo "4. Copy init-scripts/ directory"
echo "5. On production server run:"
echo "   docker-compose pull"
echo "   docker-compose up -d"
echo ""

echo "For detailed instructions, see:"
echo "   docker/PRODUCTION-DEPLOYMENT.md"
echo "   docker/REGISTRY-QUICKSTART.md"
echo ""

echo "================================================================"
echo ""
