#!/bin/bash
#
# 一键配置 KnowledgeHub 本地开发密钥到 .NET User Secrets。
#
# 用法：
#   1. 复制 .env.example 为 ~/.env.knowledgehub（或直接编辑下面 SET_* 变量）
#   2. 填入真实密钥
#   3. 执行 ./scripts/setup-secrets.sh
#
# 密钥会被存到 ~/.microsoft/usersecrets/knowledgehub-httpapi-host-secrets/secrets.json，
# 不会进入 git。
#
# 生产环境请通过环境变量注入（Key 格式：Meilisearch__EmbeddingApiKey）。

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/../src/KnowledgeHub.HttpApi.Host"

# ====== 在这里填入真实密钥（也可从 ~/.env.knowledgehub 加载）======
SET_EMBEDDING_API_KEY="${SET_EMBEDDING_API_KEY:-YOUR_EMBEDDING_API_KEY_HERE}"
SET_QWEN_API_KEY="${SET_QWEN_API_KEY:-YOUR_QWEN_API_KEY_HERE}"
SET_CERT_PASSPHRASE="${SET_CERT_PASSPHRASE:-YOUR_CERTIFICATE_PASSPHRASE_HERE}"
SET_DEFAULT_PASSPHRASE="${SET_DEFAULT_PASSPHRASE:-YOUR_DEFAULT_PASSPHRASE_HERE}"
SET_OSS_ACCESS_KEY_ID="${SET_OSS_ACCESS_KEY_ID:-YOUR_OSS_ACCESS_KEY_ID_HERE}"
SET_OSS_ACCESS_KEY_SECRET="${SET_OSS_ACCESS_KEY_SECRET:-YOUR_OSS_ACCESS_KEY_SECRET_HERE}"

cd "$PROJECT_DIR"

echo "==> Setting KnowledgeHub local secrets..."
dotnet user-secrets set "Meilisearch:EmbeddingApiKey" "$SET_EMBEDDING_API_KEY" >/dev/null
dotnet user-secrets set "Qwen:ApiKey"                "$SET_QWEN_API_KEY"          >/dev/null
dotnet user-secrets set "AuthServer:CertificatePassPhrase" "$SET_CERT_PASSPHRASE" >/dev/null
dotnet user-secrets set "StringEncryption:DefaultPassPhrase" "$SET_DEFAULT_PASSPHRASE" >/dev/null
dotnet user-secrets set "Oss:AccessKeyId"     "$SET_OSS_ACCESS_KEY_ID"     >/dev/null
dotnet user-secrets set "Oss:AccessKeySecret" "$SET_OSS_ACCESS_KEY_SECRET" >/dev/null

echo ""
echo "✓ Done. Stored secrets:"
dotnet user-secrets list

echo ""
echo "Secrets location:"
echo "  ~/.microsoft/usersecrets/knowledgehub-httpapi-host-secrets/secrets.json"
echo ""
echo "Production deployments should inject via env vars instead, e.g.:"
echo "  Meilisearch__EmbeddingApiKey=xxx"
echo "  Qwen__ApiKey=xxx"