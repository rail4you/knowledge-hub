#!/usr/bin/env bash
# =============================================================================
# 清理「教学资源库闭环 E2E」产生的测试数据
# -----------------------------------------------------------------------------
# 用法：
#   ./cleanup.sh                      # 清理最近一次 run.sh 记录的对象
#   ./cleanup.sh --all                # 清理本机历史上所有 run.sh 记录的对象
#   ./cleanup.sh --library <libId>    # 只清理指定资源库（会连带解绑内容）
#
# 说明：
#   · 资源：DELETE /api/app/resource/{id}（硬删，会一并清掉课程/章节/资源库关联）
#   · 课程：DELETE /api/app/course/{id}（软删）
#   · 资源库：V2 语义是「归档」——先提交删除申请，再由审核人通过
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
JQX="$SCRIPT_DIR/lib/jqx.py"

API_BASE="${API_BASE:-https://localhost:44305}"
TENANT_NAME="${TENANT_NAME:-qidi}"
ADMIN_USER="${ADMIN_USER:-qidi-admin}"
ADMIN_PASS="${ADMIN_PASS:-123456}"
REVIEWER_USER="${REVIEWER_USER:-admin}"
REVIEWER_PASS="${REVIEWER_PASS:-1q2w3E*}"
REVIEWER_TENANT="${REVIEWER_TENANT-}"
OAUTH_CLIENT="${OAUTH_CLIENT:-KnowledgeHub_App}"
OAUTH_SCOPE="${OAUTH_SCOPE:-offline_access KnowledgeHub}"
CURL_SSL_ARGS="${CURL_SSL_ARGS:--k}"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
TENANT_ID=""
LAST_CODE=""; LAST_BODY=""

if [ -t 1 ]; then
  C_GREEN=$'\033[32m'; C_YELLOW=$'\033[33m'; C_RED=$'\033[31m'; C_OFF=$'\033[0m'
else C_GREEN=""; C_YELLOW=""; C_RED=""; C_OFF=""; fi
info() { printf '  %s·%s %s\n' "$C_YELLOW" "$C_OFF" "$*"; }
ok()   { printf '  %s✓%s %s\n' "$C_GREEN" "$C_OFF" "$*"; }
bad()  { printf '  %s✗%s %s\n' "$C_RED" "$C_OFF" "$*"; }

api() {
  local method="$1" path="$2" token="$3" body="${4:-}" tid="${5:-$TENANT_ID}"
  local -a args
  args=(-s $CURL_SSL_ARGS -X "$method" -H 'Accept: application/json'
        -o "$WORK/resp.json" -w '%{http_code}')
  [ "$token" != "-" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$tid" ] && args+=(-H "Abp-TenantId: $tid")
  [ -n "$body" ] && args+=(-H 'Content-Type: application/json' --data-binary "$body")
  LAST_CODE="$(curl "${args[@]}" "${API_BASE}${path}")"
  LAST_BODY="$(cat "$WORK/resp.json" 2>/dev/null || echo '')"
}

jget() { python3 "$JQX" get "$WORK/resp.json" "${1:-}"; }

login() {
  local user="$1" pass="$2" tid="${3:-}" out="$WORK/tok.json"
  local -a args
  args=(-s $CURL_SSL_ARGS -X POST "${API_BASE}/connect/token"
        -H 'Content-Type: application/x-www-form-urlencoded' -o "$out" -w '%{http_code}')
  [ -n "$tid" ] && args+=(-H "Abp-TenantId: $tid")
  local code
  code="$(curl "${args[@]}" --data-urlencode 'grant_type=password' \
      --data-urlencode "client_id=${OAUTH_CLIENT}" --data-urlencode "scope=${OAUTH_SCOPE}" \
      --data-urlencode "username=${user}" --data-urlencode "password=${pass}")"
  [ "$code" = "200" ] || { bad "登录 ${user} 失败 HTTP $code"; return 1; }
  python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('access_token',''))" "$out"
}

# ---- 解析租户 ---------------------------------------------------------------
api GET /api/public/tenants -
TENANT_ID="$(python3 - "$WORK/resp.json" "$TENANT_NAME" <<'PY'
import json,sys
for t in json.load(open(sys.argv[1])) or []:
    if str(t.get("name"))==sys.argv[2] or str(t.get("tenantName"))==sys.argv[2]:
        print(t.get("id") or ""); break
PY
)"
[ -n "$TENANT_ID" ] || { bad "找不到租户 ${TENANT_NAME}"; exit 1; }

ADMIN_TOKEN="$(login "$ADMIN_USER" "$ADMIN_PASS" "$TENANT_ID")"
REVIEWER_TOKEN="$(login "$REVIEWER_USER" "$REVIEWER_PASS" "$REVIEWER_TENANT")"

# ---- 清理单个资源库 ---------------------------------------------------------
cleanup_library() {
  local lib="$1"
  info "清理资源库 $lib"

  # 先删库内资源（从库里拿 id）
  api GET "/api/app/teaching-resource-library/${lib}/contents?contentType=0&maxResultCount=200" "$ADMIN_TOKEN"
  local res_ids
  res_ids="$(python3 - "$WORK/resp.json" <<'PY'
import json,sys
data=json.load(open(sys.argv[1]))
items=(data or {}).get("items") or []
print(" ".join(str(i.get("id")) for i in items if i.get("id")))
PY
)"
  for r in $res_ids; do
    api DELETE "/api/app/resource/${r}" "$ADMIN_TOKEN"
    [ "$LAST_CODE" = "204" ] && ok "删除库资源 $r" || info "删除库资源 $r → HTTP $LAST_CODE"
  done

  # 资源库删除 = 归档申请 → 审核通过
  api DELETE "/api/app/resource-library/${lib}" "$ADMIN_TOKEN"
  if [ "$LAST_CODE" = "204" ]; then
    ok "已提交资源库归档申请"
    api GET "/api/app/review-ticket?targetType=0&targetId=${lib}&maxResultCount=20" "$REVIEWER_TOKEN" "$REVIEWER_TENANT"
    local tid
    tid="$(python3 - "$WORK/resp.json" "$lib" <<'PY'
import json,sys
items=(json.load(open(sys.argv[1])) or {}).get("items") or []
for t in items:
    if str(t.get("targetId"))==sys.argv[2] and t.get("status")==0:
        print(t.get("id")); break
PY
)"
    if [ -n "$tid" ]; then
      api POST /api/app/review-ticket/decide "$REVIEWER_TOKEN" \
        "$(python3 -c 'import json,sys;print(json.dumps({"ticketId":sys.argv[1],"decision":0}))' "$tid")" "$REVIEWER_TENANT"
      [ "$LAST_CODE" = "200" ] && ok "归档审核已通过" || info "归档审核 → HTTP $LAST_CODE"
    fi
  else
    info "资源库归档申请 → HTTP $LAST_CODE"
  fi
}

cleanup_chapters() {
  local manifest="$1"
  local ch
  ch="$(python3 -c "
import json,sys
d=json.load(open(sys.argv[1]))
print(' '.join(d.get('chapterIds') or []))" "$manifest" 2>/dev/null || true)"
  # 先删章节资源（必须早于删资源：章节资源列表靠 join 资源表才能查到，
  # 资源被硬删后章节资源就“隐身”了，章节会因外键残留删不掉）
  for c in $ch; do
    api GET "/api/app/chapter-resource/by-chapter/${c}" "$ADMIN_TOKEN"
    local crs
    crs="$(python3 -c "
import json,sys
data=json.load(open(sys.argv[1]))
items=data if isinstance(data,list) else ((data or {}).get('items') or [])
print(' '.join(str(i.get('id')) for i in items if i.get('id')))" "$WORK/resp.json" 2>/dev/null || true)"
    for r in $crs; do
      api DELETE "/api/app/chapter-resource/${r}" "$ADMIN_TOKEN"
      [ "$LAST_CODE" = "204" ] && ok "删除章节资源 $r" || info "删除章节资源 $r → HTTP $LAST_CODE"
    done
  done
  # 章节知识点资源
  local kr
  kr="$(python3 -c "
import json,sys
d=json.load(open(sys.argv[1]))
print(' '.join(d.get('knowledgeResourceIds') or []))" "$manifest" 2>/dev/null || true)"
  for k in $kr; do
    api DELETE "/api/app/knowledge-resource/${k}" "$ADMIN_TOKEN"
    [ "$LAST_CODE" = "204" ] && ok "删除知识点资源 $k" || info "删除知识点资源 $k → HTTP $LAST_CODE"
  done
  # 章节（删父章节会级联删子章节，子章节再删会 404，忽略即可）
  for c in $ch; do
    api DELETE "/api/app/chapter/${c}" "$ADMIN_TOKEN"
    case "$LAST_CODE" in
      204) ok "删除章节 $c" ;;
      404) info "章节 $c 已随父章节级联删除" ;;
      *)   info "删除章节 $c → HTTP $LAST_CODE" ;;
    esac
  done
}

# 一次清理：章节资源 → 知识点资源 → 章节 → 资源 → 课程 → 资源库
cleanup_run() {
  local manifest="$1"
  local course lib res
  course="$(python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('courseId',''))" "$manifest")"
  lib="$(python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('libraryId',''))" "$manifest")"
  res="$(python3 -c "
import json,sys
d=json.load(open(sys.argv[1]))
ids=list(d.get('courseResourceIds') or [])
if d.get('libraryResourceId'): ids.append(d['libraryResourceId'])
print(' '.join(ids))" "$manifest")"

  cleanup_chapters "$manifest"

  for r in $res; do
    api DELETE "/api/app/resource/${r}" "$ADMIN_TOKEN"
    [ "$LAST_CODE" = "204" ] && ok "删除资源 $r" || info "删除资源 $r → HTTP $LAST_CODE"
  done

  [ -n "$course" ] && { api DELETE "/api/app/course/${course}" "$ADMIN_TOKEN"; [ "$LAST_CODE" = "204" ] && ok "删除课程 $course" || info "删除课程 $course → HTTP $LAST_CODE"; }
  [ -n "$lib" ] && cleanup_library "$lib"
}

# ---- 主流程 -----------------------------------------------------------------
MODE="${1:-latest}"
case "$MODE" in
  --library)
    LIB_ID="${2:-}"
    [ -n "$LIB_ID" ] || { bad "用法: $0 --library <libId>"; exit 1; }
    cleanup_library "$LIB_ID"
    ;;
  --all)
    printf '%s\n' "清理本目录下所有 .last-run-ids*.json 记录的对象"
    for f in "$SCRIPT_DIR"/.last-run-ids*.json; do
      [ -e "$f" ] || continue
      echo "--- $f"
      cleanup_run "$f"
    done
    ;;
  *)
    F="$SCRIPT_DIR/.last-run-ids.json"
    [ -f "$F" ] || { bad "没有 $F，请先运行 run.sh"; exit 1; }
    cleanup_run "$F"
    ;;
esac

printf '\n%s\n' "清理完成"
