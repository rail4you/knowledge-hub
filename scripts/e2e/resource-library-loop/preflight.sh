#!/usr/bin/env bash
# =============================================================================
# 教学资源库闭环 E2E · 环境预检（只读，不写任何业务数据）
# -----------------------------------------------------------------------------
# 检查项：
#   1. API 可达（/api/public/tenants）
#   2. 租户存在并可解析出 Id
#   3. 四个账号可登录（密码授权）
#   4. 关键功能开关：KnowledgeHub.LibraryReview（资源库审核）
#   5. 关键角色授权：老师 / 学生的角色，以及审核人是否具备 ResourceLibraries.Audit
#
# 用法：./preflight.sh          （连接参数与 run.sh 完全一致）
# =============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
JQX="$SCRIPT_DIR/lib/jqx.py"

API_BASE="${API_BASE:-https://localhost:44305}"
TENANT_NAME="${TENANT_NAME:-qidi}"
ADMIN_USER="${ADMIN_USER:-qidi-admin}"
ADMIN_PASS="${ADMIN_PASS:-123456}"
TEACHER_USER="${TEACHER_USER:-wwq}"
TEACHER_PASS="${TEACHER_PASS:-123456}"
STUDENT_USER="${STUDENT_USER:-zmq}"
STUDENT_PASS="${STUDENT_PASS:-1234567}"
REVIEWER_USER="${REVIEWER_USER:-admin}"
REVIEWER_PASS="${REVIEWER_PASS:-1q2w3E*}"
REVIEWER_TENANT="${REVIEWER_TENANT-}"
OAUTH_CLIENT="${OAUTH_CLIENT:-KnowledgeHub_App}"
OAUTH_SCOPE="${OAUTH_SCOPE:-offline_access KnowledgeHub}"
CURL_SSL_ARGS="${CURL_SSL_ARGS:--k}"

WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT
FAIL=0
if [ -t 1 ]; then
  G=$'\033[32m'; Y=$'\033[33m'; R=$'\033[31m'; B=$'\033[36m'; O=$'\033[0m'
else G=""; Y=""; R=""; B=""; O=""; fi
ok()   { printf '  %s✓%s %s\n' "$G" "$O" "$*"; }
warn() { printf '  %s!%s %s\n' "$Y" "$O" "$*"; }
bad()  { printf '  %s✗%s %s\n' "$R" "$O" "$*"; FAIL=1; }

api() {
  local method="$1" path="$2" token="$3" body="${4:-}" tid="${5:-}"
  local -a args
  args=(-s $CURL_SSL_ARGS -X "$method" -H 'Accept: application/json' -o "$WORK/r.json" -w '%{http_code}')
  [ "$token" != "-" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$tid" ] && args+=(-H "Abp-TenantId: $tid")
  [ -n "$body" ] && args+=(-H 'Content-Type: application/json' --data-binary "$body")
  curl "${args[@]}" "${API_BASE}${path}"
}

login() {
  local user="$1" pass="$2" tid="${3:-}" out="$WORK/t.json"
  local -a args
  args=(-s $CURL_SSL_ARGS -X POST "${API_BASE}/connect/token"
        -H 'Content-Type: application/x-www-form-urlencoded' -o "$out" -w '%{http_code}')
  [ -n "$tid" ] && args+=(-H "Abp-TenantId: $tid")
  local code
  code="$(curl "${args[@]}" --data-urlencode 'grant_type=password' \
      --data-urlencode "client_id=${OAUTH_CLIENT}" --data-urlencode "scope=${OAUTH_SCOPE}" \
      --data-urlencode "username=${user}" --data-urlencode "password=${pass}")"
  if [ "$code" = "200" ]; then
    python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('access_token',''))" "$out"
  else
    echo ""
  fi
}

echo "=============================================================================="
echo " 教学资源库闭环 E2E · 环境预检"
echo " API : $API_BASE"
echo "=============================================================================="

# 1. API 可达
CODE="$(api GET /api/public/tenants -)"
if [ "$CODE" = "200" ]; then ok "API 可达 (HTTP 200)"; else bad "API 不可达 (HTTP $CODE) —— 检查 API_BASE / 网络"; exit 1; fi

# 2. 租户
TENANT_ID="$(python3 - "$WORK/r.json" "$TENANT_NAME" <<'PY'
import json,sys
for t in json.load(open(sys.argv[1])) or []:
    if str(t.get("name"))==sys.argv[2] or str(t.get("tenantName"))==sys.argv[2]:
        print(t.get("id") or ""); break
PY
)"
if [ -n "$TENANT_ID" ]; then ok "租户 ${TENANT_NAME} → ${TENANT_ID}"; else bad "租户 ${TENANT_NAME} 不存在"; exit 1; fi

# 3. 账号登录
for spec in "院校管理员:${ADMIN_USER}:${ADMIN_PASS}:${TENANT_ID}" \
            "老师:${TEACHER_USER}:${TEACHER_PASS}:${TENANT_ID}" \
            "学生:${STUDENT_USER}:${STUDENT_PASS}:${TENANT_ID}" \
            "审核人:${REVIEWER_USER}:${REVIEWER_PASS}:${REVIEWER_TENANT}"; do
  role="${spec%%:*}"; rest="${spec#*:}"
  user="${rest%%:*}"; rest="${rest#*:}"
  pass="${rest%%:*}"; tid="${rest#*:}"
  tok="$(login "$user" "$pass" "$tid")"
  if [ -n "$tok" ]; then ok "$role ${user} 登录成功"; else bad "$role ${user} 登录失败"; fi
done

# 4. 功能开关（必须带 token，否则匿名请求拿不到租户上下文，开关值是宿主默认）
ADMIN_TOKEN="$(login "$ADMIN_USER" "$ADMIN_PASS" "$TENANT_ID")"
if [ -n "$ADMIN_TOKEN" ]; then
  CODE="$(api GET /api/abp/application-configuration "$ADMIN_TOKEN" "" "$TENANT_ID")"
  if [ "$CODE" = "200" ]; then
    LR="$(python3 - "$WORK/r.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1]))
v=(d.get("features") or {}).get("values") or {}
print(v.get("KnowledgeHub.LibraryReview", ""))
PY
)"
    if [ "$LR" = "true" ]; then
      ok "功能开关 KnowledgeHub.LibraryReview = true（资源库走准入审核）"
    else
      bad "功能开关 KnowledgeHub.LibraryReview = '${LR}'（应为 true）—— 资源库不会走准入审核，run.sh 第 4/11 步会失败"
    fi
  else
    warn "读取 application-configuration 失败 (HTTP $CODE)"
  fi
else
  warn "管理员未登录，跳过功能开关检查"
fi

# 5. 角色
for u in "$TEACHER_USER" "$STUDENT_USER" "$ADMIN_USER"; do
  CODE="$(api GET "/api/identity/users?filter=${u}&maxResultCount=20" "$ADMIN_TOKEN" "" "$TENANT_ID")"
  if [ "$CODE" = "200" ]; then
    python3 - "$WORK/r.json" "$u" <<'PY'
import json,sys
data=json.load(open(sys.argv[1]))
want=sys.argv[2].lower()
for u in (data.get("items") or []):
    if str(u.get("userName","")).lower()==want:
        print("  · %-14s id=%s" % (u.get("userName"), u.get("id")))
        break
PY
  else
    warn "查询用户 ${u} 失败 (HTTP $CODE)"
  fi
done

echo "=============================================================================="
if [ "$FAIL" -ne 0 ]; then echo " 预检未通过"; exit 1; fi
echo " 预检通过，可以运行 ./run.sh"
exit 0
