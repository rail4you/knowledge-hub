#!/bin/bash
set -e

# KnowledgeHub Docker Image Exporter
# This script exports all KnowledgeHub images as tar files for offline deployment

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

OUTPUT_DIR="./knowledgehub-images"

mkdir -p $OUTPUT_DIR

echo "===== Pulling Base Images ====="
echo "Pulling postgres:16-alpine..."
docker pull postgres:16-alpine

echo ""
echo "===== Building Images First ====="
docker compose -f docker-compose.yml build

echo ""
echo "===== Exporting KnowledgeHub Docker Images ====="

# Use fixed filenames for easier handling on remote server
echo "Exporting knowledgehub-db..."
docker save postgres:16-alpine -o $OUTPUT_DIR/postgres.tar

echo "Exporting knowledgehub-api..."
docker save knowledgehub-api:latest -o $OUTPUT_DIR/knowledgehub-api.tar

echo "Exporting knowledgehub-web..."
docker save knowledgehub-web:latest -o $OUTPUT_DIR/knowledgehub-web.tar

echo ""
echo "===== Export Complete ====="
echo "Images saved to: $OUTPUT_DIR"
echo ""
echo "Files:"
ls -lh $OUTPUT_DIR
echo ""
echo "To load on remote server:"
echo "  cd knowledgehub-images"
echo "  docker load -i postgres.tar"
echo "  docker load -i knowledgehub-api.tar"
echo "  docker load -i knowledgehub-web.tar"
echo ""
echo "Or use the deploy.sh script on remote server:"
