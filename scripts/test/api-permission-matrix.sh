#!/usr/bin/env bash
# 权限矩阵：用不同角色令牌访问同一端点，断言 2xx / 403。
#
# 用法：
#   scripts/test/api-permission-matrix.sh
#
# 账号（缺失的角色自动跳过）：
#   ADMIN_USER/ADMIN_PASS/ADMIN_TENANT       默认 admin / 1q2w3E*
#   TEACHER_USER/TEACHER_PASS/TEACHER_TENANT
#   STUDENT_USER/STUDENT_PASS/STUDENT_TENANT
#
# 期望值语法：2xx（任意 2xx）或正则（如 "401|403"）。
set -uo pipefail

API_BASE="${API_BASE:-https://localhost:44305}"
CURL_OPTS="${CURL_OPTS:--sk}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

declare -A TOKENS=()

fetch_token() {
  local role="$1" user="$2" pass="$3" tenant="$4"
  if [ -z "$user" ] || [ -z "$pass" ]; then
    return 1
  fi
  local t
  if t="$(API_BASE="$API_BASE" CURL_OPTS="$CURL_OPTS" "$SCRIPT_DIR/get-token.sh" "$user" "$pass" "$tenant" 2>/dev/null)"; then
    TOKENS["$role"]="$t"
    TOKENS["${role}_tenant"]="$tenant"
    return 0
  fi
  return 1
}

fetch_token admin   "${ADMIN_USER:-admin}"        "${ADMIN_PASS:-1q2w3E*}"  "${ADMIN_TENANT:-}"   && echo "admin 令牌就绪"   || echo "跳过 admin（未配置）"
fetch_token teacher "${TEACHER_USER:-}"           "${TEACHER_PASS:-}"       "${TEACHER_TENANT:-}" && echo "teacher 令牌就绪" || echo "跳过 teacher（未配置）"
fetch_token student "${STUDENT_USER:-}"           "${STUDENT_PASS:-}"       "${STUDENT_TENANT:-}" && echo "student 令牌就绪" || echo "跳过 student（未配置）"

TENANT_HEADER_DEFAULT="${MATRIX_TENANT:-}"

pass=0; fail=0; skip=0

# expect <method> <path> <role> <期望>
expect() {
  local method="$1" path="$2" role="$3" want="$4"
  if [ -z "${TOKENS[$role]:-}" ]; then
    skip=$((skip + 1))
    return
  fi

  local -a args=($CURL_OPTS -o /dev/null -w '%{http_code}' -X "$method"
    "$API_BASE${path}" -H "Authorization: Bearer ${TOKENS[$role]}")
  local tenant="${TOKENS[${role}_tenant]:-$TENANT_HEADER_DEFAULT}"
  [ -n "$tenant" ] && args+=(-H "__tenant: $tenant")

  local status
  status="$(curl "${args[@]}")"
  local branch="$want"
  if [ "$want" = "2xx" ]; then
    branch='2[0-9][0-9]'
  fi

  if [[ "$status" =~ ^($branch)$ ]]; then
    printf '  \033[32mPASS\033[0m %-6s %-55s %-8s [%s]\n' "$method" "$path" "$role" "$status"
    pass=$((pass + 1))
  else
    printf '  \033[31mFAIL\033[0m %-6s %-55s %-8s [got=%s want=%s]\n' "$method" "$path" "$role" "$status" "$want"
    fail=$((fail + 1))
  fi
}

echo "== 权限矩阵 =="

# 资源列表：所有已登录用户可用
expect GET '/api/app/resource/filtered-list?skipCount=0&maxResultCount=5' admin   2xx
expect GET '/api/app/resource/filtered-list?skipCount=0&maxResultCount=5' teacher 2xx

# 专业 lookup：匿名可读
expect GET '/api/app/major/lookup-list' admin 2xx

# Meili 管理：Teacher/SchoolAdmin 按 Seeder 持有 ManageIndex；Student 期望 403
expect GET '/api/app/meili-search-admin/indexes' admin   2xx
expect GET '/api/app/meili-search-admin/indexes' teacher 2xx
expect GET '/api/app/meili-search-admin/indexes' student '401|403'

expect GET '/api/app/meili-search-admin/dashboard' admin   2xx
expect GET '/api/app/meili-search-admin/dashboard' student '401|403'

echo
echo "PASS=$pass FAIL=$fail SKIP=$skip"
[ "$fail" -eq 0 ]
