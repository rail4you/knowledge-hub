#!/usr/bin/env bash
# 一键启动「统一账户服务 + 知识库后端」，供安卓客户端联调。
# 租户账号（如 qidi/zmq）必须走这条链路才能登录。
set -euo pipefail

KH_ROOT="/Users/bai/projects/KnowledgeHub"
AUTH_ROOT="$HOME/projects/auth-server"
# knowledgehub 应用在统一账户里的 client_secret 不入库：
# 从本地密钥管理读取，或从管理后台 /admin/api/apps/{id}/rotate 轮换后临时导出。
if [[ -z "${KH_CLIENT_SECRET:-}" ]]; then
  echo "请先设置环境变量 KH_CLIENT_SECRET（统一账户 app_knowledgehub 的 client_secret）" >&2
  exit 1
fi

echo "==> 1/3 检查账户数据库容器 account-postgres (55433)"
if ! nc -z -w2 localhost 55433 2>/dev/null; then
  echo "  账户数据库未运行，启动 docker compose..."
  (cd "$AUTH_ROOT" && docker compose up -d --wait)
else
  echo "  OK 已运行"
fi

echo "==> 2/3 启动统一账户服务 (8080)"
if ! nc -z -w2 localhost 8080 2>/dev/null; then
  (cd "$AUTH_ROOT" && set -a && source .env && set +a && ./bin/accountctl serve) >/tmp/kh-account-server.log 2>&1 &
  echo "  已在后台启动，日志 /tmp/kh-account-server.log"
  for i in $(seq 1 20); do nc -z -w1 localhost 8080 2>/dev/null && break; sleep 1; done
fi
nc -z -w2 localhost 8080 2>/dev/null && echo "  OK 8080 已监听" || { echo "  账户服务启动失败"; exit 1; }

echo "==> 3/3 启动知识库后端 (44305，开启 UnifiedAuth)"
lsof -ti:44305 2>/dev/null | xargs kill -9 2>/dev/null || true
cd "$KH_ROOT/src/KnowledgeHub.HttpApi.Host"
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="https://localhost:44305"
export WasmMirror__RootPath="$KH_ROOT/etc/docker/wasm-mirrors"
export ASPNETCORE_Kestrel__Certificates__Default__Path="$HOME/.aspnet/https/aspnetapp.pfx"
export ASPNETCORE_Kestrel__Certificates__Default__Password="devcert"
export UnifiedAuth__Enabled=true
export UnifiedAuth__BaseUrl="http://127.0.0.1:8080"
export UnifiedAuth__ClientId="app_knowledgehub"
export UnifiedAuth__ClientSecret="$KH_CLIENT_SECRET"
echo "  后端将在前台运行（Ctrl+C 停止）；用 --background 可后台运行"
if [[ "${1:-}" == "--background" ]]; then
  nohup dotnet bin/Debug/net10.0/KnowledgeHub.HttpApi.Host.dll >/tmp/kh-api.log 2>&1 &
  echo "  已后台启动，日志 /tmp/kh-api.log"
else
  exec dotnet bin/Debug/net10.0/KnowledgeHub.HttpApi.Host.dll
fi
