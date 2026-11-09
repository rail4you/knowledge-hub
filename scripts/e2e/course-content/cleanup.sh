#!/usr/bin/env bash
# =============================================================================
# 课程内容 E2E · 清理（删除 demo.sh 生成的习题 / 试卷 / 作业）
# -----------------------------------------------------------------------------
# demo.sh 是「追加式」的（每次跑都新建一套），重复跑会堆积。本脚本按标题标记清理。
#
# 用法：
#   ./cleanup.sh                        # 清理全部带标记的（默认标记 demo）
#   TAG=demo ./cleanup.sh               # 指定标记
#   API_BASE=http://127.0.0.1:4200 ./cleanup.sh    # 生产
# =============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
JQX="$SCRIPT_DIR/../lib/jqx.py"

API_BASE="${API_BASE:-https://localhost:44305}"
TENANT_NAME="${TENANT_NAME:-qidi}"
TEACHER_USER="${TEACHER_USER:-wwq}"
TEACHER_PASS="${TEACHER_PASS:-123456}"
OAUTH_CLIENT="${OAUTH_CLIENT:-KnowledgeHub_App}"
OAUTH_SCOPE="${OAUTH_SCOPE:-offline_access KnowledgeHub}"
TAG="${TAG:-demo}"
CURL_SSL_ARGS="${CURL_SSL_ARGS:--k}"

WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT
TENANT_ID=""; TOKEN=""; LAST_CODE=""; LAST_BODY=""

if [ -t 1 ]; then G=$'\033[32m'; Y=$'\033[33m'; R=$'\033[31m'; O=$'\033[0m'
else G=""; Y=""; R=""; O=""; fi
ok()   { printf '  %s✓%s %s\n' "$G" "$O" "$*"; }
skip() { printf '  %s·%s %s\n' "$Y" "$O" "$*"; }
bad()  { printf '  %s✗%s %s\n' "$R" "$O" "$*"; }

api() {
  local method="$1" path="$2" body="${3:-}"
  local -a args
  args=(-s $CURL_SSL_ARGS -X "$method" -H 'Accept: application/json'
        -o "$WORK/resp.json" -w '%{http_code}')
  [ -n "$TOKEN" ] && args+=(-H "Authorization: Bearer $TOKEN")
  [ -n "$TENANT_ID" ] && args+=(-H "Abp-TenantId: $TENANT_ID")
  [ -n "$body" ] && args+=(-H 'Content-Type: application/json' --data-binary "$body")
  LAST_CODE="$(curl "${args[@]}" "${API_BASE}${path}")"
  LAST_BODY="$(cat "$WORK/resp.json" 2>/dev/null || echo '')"
}
jget()  { python3 "$JQX" get "$WORK/resp.json" "${1:-}"; }
jlen()  { python3 "$JQX" len "$WORK/resp.json" "${1:-}"; }

echo "=============================================================================="
echo " 课程内容 E2E · 清理    API=$API_BASE  标记=[$TAG]"
echo "=============================================================================="

api GET /api/public/tenants
TENANT_ID="$(python3 - "$WORK/resp.json" "$TENANT_NAME" <<'PY'
import json,sys
for t in json.load(open(sys.argv[1])) or []:
    if str(t.get("name"))==sys.argv[2] or str(t.get("tenantName"))==sys.argv[2]:
        print(t.get("id") or ""); break
PY
)"
[ -n "$TENANT_ID" ] || { bad "找不到租户 $TENANT_NAME"; exit 1; }

TOKEN="$(curl -s $CURL_SSL_ARGS -X POST "${API_BASE}/connect/token" \
  -H 'Content-Type: application/x-www-form-urlencoded' -H "Abp-Tenant-Id: ${TENANT_ID}" \
  -H "Abp-TenantId: ${TENANT_ID}" \
  --data-urlencode 'grant_type=password' --data-urlencode "client_id=${OAUTH_CLIENT}" \
  --data-urlencode "scope=${OAUTH_SCOPE}" \
  --data-urlencode "username=${TEACHER_USER}" --data-urlencode "password=${TEACHER_PASS}" \
  | python3 -c 'import json,sys;print(json.load(sys.stdin).get("access_token",""))')"
[ -n "$TOKEN" ] || { bad "登录失败"; exit 1; }
ok "老师已登录"

# ── 作业：先删（作业下有提交记录，按作业清理）──────────────────────────────
echo "▶ 作业"
api GET "/api/app/assignment?status=0&maxResultCount=200"   # status 参数不参与过滤时也无妨
api GET "/api/app/assignment?maxResultCount=200"
python3 - "$WORK/resp.json" "$TAG" <<'PY' > "$WORK/assignments.txt"
import json,sys
items=(json.load(open(sys.argv[1])) or {}).get("items") or []
tag=sys.argv[2]
for it in items:
    if tag in str(it.get("title","")):
        print(it.get("id"))
PY
while read -r id; do
  [ -z "$id" ] && continue
  api DELETE "/api/app/assignment/${id}"
  case "$LAST_CODE" in 2*) ok "删除作业 $id" ;; *) skip "删除作业 $id → HTTP $LAST_CODE" ;; esac
done < "$WORK/assignments.txt"
[ -s "$WORK/assignments.txt" ] || skip "无带标记的作业"

# ── 试卷 ────────────────────────────────────────────────────────────────
echo "▶ 试卷"
api GET "/api/app/exam?maxResultCount=200"
python3 - "$WORK/resp.json" "$TAG" <<'PY' > "$WORK/exams.txt"
import json,sys
items=(json.load(open(sys.argv[1])) or {}).get("items") or []
tag=sys.argv[2]
for it in items:
    if tag in str(it.get("title","")):
        print(it.get("id"))
PY
while read -r id; do
  [ -z "$id" ] && continue
  api DELETE "/api/app/exam/${id}"
  case "$LAST_CODE" in 2*) ok "删除试卷 $id" ;; *) skip "删除试卷 $id → HTTP $LAST_CODE" ;; esac
done < "$WORK/exams.txt"
[ -s "$WORK/exams.txt" ] || skip "无带标记的试卷"

# ── 习题（按标题前缀识别 demo 出的题）─────────────────────────────────────
echo "▶ 习题"
api GET "/api/app/exercise?maxResultCount=500"
python3 - "$WORK/resp.json" <<'PY' > "$WORK/exercises.txt"
import json,sys
items=(json.load(open(sys.argv[1])) or {}).get("items") or []
titles={
 "Python 环境搭建 · 单选","venv 虚拟环境 · 判断",
 "Pandas 读取 CSV · 填空","DataFrame 去重 · 单选",
}
for it in items:
    if str(it.get("title","")) in titles:
        print(it.get("id"))
PY
CNT=0
while read -r id; do
  [ -z "$id" ] && continue
  api DELETE "/api/app/exercise/${id}"
  case "$LAST_CODE" in 2*) CNT=$((CNT+1)) ;; *) skip "删除习题 $id → HTTP $LAST_CODE" ;; esac
done < "$WORK/exercises.txt"
ok "删除习题 $CNT 道"

echo "=============================================================================="
echo " 清理完成"
echo "=============================================================================="
