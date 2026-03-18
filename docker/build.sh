#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "===== Building KnowledgeHub Docker Images ====="

# Enable Docker buildx for cross-platform builds
echo "Setting up cross-platform build..."
docker buildx create --name knowledgehub-builder --use 2>/dev/null || true
docker buildx inspect knowledgehub-builder --bootstrap 2>/dev/null || docker buildx create --name knowledgehub-builder --use

# Build for linux/amd64 (Ubuntu x86_64)
PLATFORM=linux/amd64

# Build API image
echo "Building API image for $PLATFORM..."
docker buildx build --platform $PLATFORM --load -f api.Dockerfile -t knowledgehub-api:latest ..

# Build Web image
echo "Building Web image for $PLATFORM..."
docker buildx build --platform $PLATFORM --load -f web.Dockerfile -t knowledgehub-web:latest ..

echo "===== Build Complete ====="
echo ""
echo "To start the application:"
echo "  docker compose up -d"
echo ""
echo "To view logs:"
echo "  docker compose logs -f"
echo ""
echo "To stop:"
echo "  docker compose down"
