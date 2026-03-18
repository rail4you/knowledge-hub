#!/bin/bash
set -e

# KnowledgeHub Docker Image Importer
# This script loads exported Docker images from tar files

echo "===== Importing KnowledgeHub Docker Images ====="

# Find and load all tar files
for tarfile in $(ls *.tar 2>/dev/null); do
    echo "Loading: $tarfile"
    docker load -i "$tarfile"
done

echo ""
echo "===== Import Complete ====="
echo ""
echo "To start the application:"
echo "  cd docker && docker compose up -d"
echo ""
echo "To verify images:"
echo "  docker images | grep -E 'knowledgehub|postgres'"
