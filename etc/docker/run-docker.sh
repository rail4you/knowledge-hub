#!/bin/bash

# ============================================================
# run-docker.sh - KnowledgeHub
# 自动生成 HTTPS 开发证书（供 API / AuthServer 使用）并启动
# Angular 容器本身运行在 HTTP，无需此证书
# ============================================================

CURRENT_FOLDER=$(cd "$(dirname "$0")" && pwd)
CERTS_FOLDER="$CURRENT_FOLDER/certs"

if [ ! -d "$CERTS_FOLDER" ]; then
  mkdir -p "$CERTS_FOLDER"
fi

if [ ! -f "$CERTS_FOLDER/localhost.pfx" ]; then
  echo ">>> 生成本地 HTTPS 开发证书 ..."
  cd "$CERTS_FOLDER"
  dotnet dev-certs https -v \
    -ep localhost.pfx \
    -p 91f91912-5ab0-49df-8166-23377efaf3cc \
    --trust
    chmod 644 localhost.pfx   # 确保容器内可读
fi

cd "$CURRENT_FOLDER"
echo ">>> 启动 docker compose ..."
docker compose up -d

echo ""
echo "服务启动后可通过以下地址访问："
echo "  Angular UI  : http://localhost:4200"
echo "  API (Swagger): https://localhost:44354/swagger"
echo "  AuthServer  : https://localhost:44334"
