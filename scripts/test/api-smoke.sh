#!/usr/bin/env bash
# API 冒烟：基于 /api/abp/api-definition 遍历无路径参数的 GET 端点。
#
# 用法：
#   scripts/test/api-smoke.sh [token] [tenant]
#
# 说明：
#   - 仅覆盖 httpMethod=GET 且 URL 不含路径占位符（{...}）的 action，避免误报。
#   - 2xx 记 PASS；4xx 记 WARN（多为参数/权限差异，需人工确认）；5xx 记 FAIL。
set -uo pipefail

API_BASE="${API_BASE:-https://localhost:44305}"
CURL_OPTS="${CURL_OPTS:--sk}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

TOKEN="${1:-}"
TENANT="${2:-}"
if [ -z "$TOKEN" ]; then
  TOKEN="$(API_BASE="$API_BASE" CURL_OPTS="$CURL_OPTS" \
    "$SCRIPT_DIR/get-token.sh" "${ADMIN_USER:-admin}" "${ADMIN_PASS:-1q2w3E*}" "$TENANT")"
fi

tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

echo "-> 拉取 api-definition ..."
curl $CURL_OPTS "$API_BASE/api/abp/api-definition" -o "$tmp"

headers=(-H "Authorization: Bearer $TOKEN")
[ -n "$TENANT" ] && headers+=(-H "__tenant: $TENANT")

pass=0; warn=0; fail=0

while IFS= read -r url; do
  full="${API_BASE}/${url#/}"
  status="$(curl $CURL_OPTS -o /dev/null -w '%{http_code}' "${headers[@]}" "$full")"

  if [[ "$status" =~ ^2 ]]; then
    printf '  \033[32mPASS\033[0m %-70s [%s]\n' "$url" "$status"
    pass=$((pass + 1))
  elif [[ "$status" =~ ^5 ]]; then
    printf '  \033[31mFAIL\033[0m %-70s [%s]\n' "$url" "$status"
    fail=$((fail + 1))
  else
    printf '  \033[33mWARN\033[0m %-70s [%s]\n' "$url" "$status"
    warn=$((warn + 1))
  fi
done < <(jq -r '
  .modules.app.controllers
  | to_entries[]
  | .value.actions
  | to_entries[]
  | select(.value.httpMethod == "GET")
  | select(.value.url | test("\\{") | not)
  | .value.url
' "$tmp" | sort -u)

echo
echo "PASS=$pass WARN=$warn FAIL=$fail"
[ "$fail" -eq 0 ]
