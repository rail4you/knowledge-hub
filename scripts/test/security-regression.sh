#!/usr/bin/env bash
# 安全回归测试：把 issues/security-audit-2026.md 的负向用例固化为可执行断言。
#
# 用法：
#   scripts/test/security-regression.sh [adminToken] [tenant]
#
# 环境变量：
#   API_BASE            默认 https://localhost:44305
#   ADMIN_USER/PASS     未显式传 token 时用于自动获取（默认 admin / 1q2w3E*）
#   ENFORCE_REMOTE_INSTALL=1  启用安装接口检查（本机回环会被放行，默认跳过）
#
# 退出码：全部通过为 0，否则为失败数。
set -uo pipefail

API_BASE="${API_BASE:-https://localhost:44305}"
CURL_OPTS="${CURL_OPTS:--sk}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

TOKEN="${1:-}"
TENANT="${2:-}"
if [ -z "$TOKEN" ]; then
  TOKEN="$(API_BASE="$API_BASE" CURL_OPTS="$CURL_OPTS" \
    "$SCRIPT_DIR/get-token.sh" "${ADMIN_USER:-admin}" "${ADMIN_PASS:-1q2w3E*}" "$TENANT")" || {
    echo "无法获取管理员令牌，部分用例将跳过。"
    TOKEN=""
  }
fi

passed=0
failed=0

# check <描述> <期望状态码正则> <curl 参数...>
check() {
  local desc="$1"; shift
  local expect="$1"; shift
  local status
  status="$(curl $CURL_OPTS -o /dev/null -w '%{http_code}' "$@")"
  if [[ "$status" =~ ^($expect)$ ]]; then
    printf '  \033[32mPASS\033[0m %-70s [%s]\n' "$desc" "$status"
    passed=$((passed + 1))
  else
    printf '  \033[31mFAIL\033[0m %-70s [got=%s want=%s]\n' "$desc" "$status" "$expect"
    failed=$((failed + 1))
  fi
}

echo "== 安全回归 =="

# C-1 开放 SSRF 代理：匿名必须被拒
check "C-1 匿名 /api/proxy/http 内网" '401|403' \
  "$API_BASE/api/proxy/http/169.254.169.254/latest/meta-data/"

# C-2 OSS 匿名上传
check "C-2 匿名 /api/oss-upload/file" '401|403' \
  -X POST "$API_BASE/api/oss-upload/file" -F 'file=@/etc/hosts'

# C-3 安装接口：本机回环会被放行，需显式开启校验
if [ "${ENFORCE_REMOTE_INSTALL:-0}" = "1" ]; then
  check "C-3 匿名 /api/app/install/install" '403' \
    -X POST "$API_BASE/api/app/install/install" \
    -H 'Content-Type: application/json' \
    -d '{"edition":"Standard","licenseKey":"KH-STANDARD-x","adminUsername":"attacker","adminPassword":"123456","adminEmail":"a@b.c"}'
fi

# H-8 版本升级
check "H-8 匿名 /api/app/edition/upgrade-to-standard" '401|403' \
  -X POST "$API_BASE/api/app/edition/upgrade-to-standard" \
  -H 'Content-Type: application/json' -d '{"licenseKey":"KH-STANDARD-x"}'

# H-9 章节资源写入
check "H-9 匿名 POST /api/app/chapter-resource" '401|403' \
  -X POST "$API_BASE/api/app/chapter-resource" \
  -H 'Content-Type: application/json' -d '{}'

# H-10 搜索索引写入
check "H-10 匿名 POST /api/app/search/index-resource" '401|403' \
  -X POST "$API_BASE/api/app/search/index-resource" \
  -H 'Content-Type: application/json' -d '{}'

# H-13 学习统计导出
check "H-13 匿名 学习统计导出" '401|403' \
  -X POST "$API_BASE/api/app/student-exercise-record/export-learning-statistics" \
  -H 'Content-Type: application/json' -d '{}'

# H-3 实训聊天 SSE
check "H-3 匿名 practicum-chat stream" '401|403' \
  "$API_BASE/api/learning/practicum-chat/stream/00000000-0000-0000-0000-000000000000"

# H-6 热词接口
check "H-6 匿名 meili-search-admin/hot-words" '401|403' \
  "$API_BASE/api/app/meili-search-admin/hot-words?resourceId=00000000-0000-0000-0000-000000000000"

if [ -n "$TOKEN" ]; then
  # H-1 图片代理 SSRF（已登录访问内网应 403）
  check "H-1 已登录 image-proxy 内网" '403' \
    "$API_BASE/api/image-proxy?url=http://127.0.0.1:7700/health" \
    -H "Authorization: Bearer $TOKEN"

  # C-1 HTTP 代理 SSRF（已登录访问内网应 403）
  check "C-1 已登录 proxy/http 内网" '403' \
    "$API_BASE/api/proxy/http/127.0.0.1:7700/health" \
    -H "Authorization: Bearer $TOKEN"
fi

echo
echo "通过 $passed，失败 $failed"
[ "$failed" -eq 0 ]
