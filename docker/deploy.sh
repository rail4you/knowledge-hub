#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "===== KnowledgeHub Docker Deployment Script ====="
echo ""

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "Error: Docker is not installed. Please install Docker first."
    exit 1
fi

# Check if Docker Compose is available (try both new and old versions)
DOCKER_COMPOSE=""
if docker compose version &> /dev/null; then
    DOCKER_COMPOSE="docker compose"
elif command -v docker-compose &> /dev/null; then
    DOCKER_COMPOSE="docker-compose"
else
    echo "Error: Docker Compose is not available. Please install Docker Compose."
    echo "Install with: sudo apt-get update && sudo apt-get install docker-compose-plugin"
    exit 1
fi

# Check if images exist
echo "Checking for Docker images..."
if [ ! -d "knowledgehub-images" ]; then
    echo "Error: knowledgehub-images directory not found!"
    echo "Please upload the knowledgehub-images folder to this server first."
    exit 1
fi

# Load images
echo ""
echo "===== Loading Docker Images ====="
for tarfile in knowledgehub-images/*.tar; do
    if [ -f "$tarfile" ]; then
        echo "Loading: $tarfile"
        docker load -i "$tarfile"
    fi
done

# Copy environment file if not exists
if [ ! -f ".env" ]; then
    echo ""
    echo "===== Creating .env file ====="
    cp .env.example .env
    echo "Created .env from template."
    echo "Please edit .env to configure your server URL!"
fi

# Start services
echo ""
echo "===== Starting Services ====="
$DOCKER_COMPOSE -f docker-compose.prod.yml up -d

echo ""
echo "===== Deployment Complete ====="
echo ""
echo "Services starting... Please wait 30 seconds for health checks."
echo ""
echo "To check status:"
echo "  $DOCKER_COMPOSE -f docker-compose.prod.yml ps"
echo ""
echo "To view logs:"
echo "  $DOCKER_COMPOSE -f docker-compose.prod.yml logs -f"
echo ""
echo "To stop:"
echo "  $DOCKER_COMPOSE -f docker-compose.prod.yml down"
