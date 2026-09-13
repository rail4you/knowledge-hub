#!/usr/bin/env bash
# 获取 KnowledgeHub 访问令牌（OpenIddict password 授权，public client）。
#
# 用法：
#   scripts/test/get-token.sh <username> <password> [tenant]
#
# 环境变量：
#   API_BASE   默认 https://localhost:44305
#   CLIENT_ID  默认 KnowledgeHub_App
#   SCOPE      默认 "KnowledgeHub offline_access"
#   CURL_OPTS  默认 "-sk"（开发自签名证书；生产改为 ""）
#
# 输出：仅 access_token（便于 `TOKEN=$(...)` 捕获）；失败时错误写到 stderr 并以 1 退出。
set -euo pipefail

API_BASE="${API_BASE:-https://localhost:44305}"
CLIENT_ID="${CLIENT_ID:-KnowledgeHub_App}"
SCOPE="${SCOPE:-KnowledgeHub offline_access}"
CURL_OPTS="${CURL_OPTS:--sk}"

USERNAME="${1:?用法: get-token.sh <username> <password> [tenant]}"
PASSWORD="${2:?用法: get-token.sh <username> <password> [tenant]}"
TENANT="${3:-}"

args=(
  $CURL_OPTS -X POST "$API_BASE/connect/token"
  -H 'Content-Type: application/x-www-form-urlencoded'
  --data-urlencode 'grant_type=password'
  --data-urlencode "client_id=$CLIENT_ID"
  --data-urlencode "username=$USERNAME"
  --data-urlencode "password=$PASSWORD"
  --data-urlencode "scope=$SCOPE"
)

if [ -n "$TENANT" ]; then
  args+=(-H "__tenant: $TENANT")
fi

resp="$(curl "${args[@]}")"
token="$(printf '%s' "$resp" | jq -r '.access_token // empty')"

if [ -z "$token" ]; then
  error="$(printf '%s' "$resp" | jq -r '.error_description // .error // "unknown error"')"
  echo "登录失败（$USERNAME${TENANT:+@$TENANT}）: $error" >&2
  exit 1
fi

printf '%s' "$token"
