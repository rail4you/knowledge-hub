#!/usr/bin/env bash
# =============================================================================
# B1–B3 整体回归 · 只读预检
# -----------------------------------------------------------------------------
# 在跑 run.sh 之前确认「环境具备，失败原因不会是环境问题」：
#   ① API 可达、租户能解析
#   ② 三个账号（宿主 admin / 老师 / 学生）都能登录
#   ③ B1–B3 新增的权限已授予（Classroom.* / Courses.Video / Courses.Edit）
#   ④ B1–B3 新增的控制器都已出现在 api-definition（漏注册会 404）
#   ⑤ Gotenberg 可达（打印 PDF 依赖）
#   ⑥ ffmpeg 可用（生成测试视频；没有就设 SKIP_VIDEO=1）
#
# 用法：
#   ./preflight.sh
#   SKIP_VIDEO=1 ./preflight.sh
# =============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LIB_DIR="$SCRIPT_DIR/../lib"

API_BASE="${API_BASE:-https://localhost:44305}"
TENANT_NAME="${TENANT_NAME:-qidi}"
ADMIN_USER="${ADMIN_USER:-admin}";        ADMIN_PASS="${ADMIN_PASS:-1q2w3E*}"
TEACHER_USER="${TEACHER_USER:-wwq}";      TEACHER_PASS="${TEACHER_PASS:-123456}"
STUDENT_USER="${STUDENT_USER:-zmq}";      STUDENT_PASS="${STUDENT_PASS:-1234567}"
OAUTH_CLIENT="${OAUTH_CLIENT:-KnowledgeHub_App}"
OAUTH_SCOPE="${OAUTH_SCOPE:-offline_access KnowledgeHub}"
CURL_SSL_ARGS="${CURL_SSL_ARGS:--k}"
GOTENBERG_URL="${GOTENBERG_URL:-http://localhost:3000}"
SKIP_VIDEO="${SKIP_VIDEO:-0}"

WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT

if [ -t 1 ]; then C_OK=$'\033[32m'; C_BAD=$'\033[31m'; C_WARN=$'\033[33m'; C_B=$'\033[1m'; C_OFF=$'\033[0m'
else C_OK=""; C_BAD=""; C_WARN=""; C_B=""; C_OFF=""; fi

FAILED=0; WARNED=0
ok()   { printf '  %s✓%s %s\n' "$C_OK" "$C_OFF" "$*"; }
bad()  { printf '  %s✗%s %s\n' "$C_BAD" "$C_OFF" "$*"; FAILED=$((FAILED+1)); }
warn() { printf '  %s!%s %s\n' "$C_WARN" "$C_OFF" "$*"; WARNED=$((WARNED+1)); }
step() { printf '\n%s▶ %s%s\n' "$C_B$C_B" "$*" "$C_OFF"; }

printf '%s\n' "=============================================================================="
printf ' B1–B3 整体回归 · 预检    API=%s  租户=%s\n' "$API_BASE" "$TENANT_NAME"
printf '%s\n' "=============================================================================="

# ─────────────────────────────────────────────────────────────
step "① API 可达"
CODE="$(curl -s $CURL_SSL_ARGS -o /dev/null -w '%{http_code}' --max-time 10 "$API_BASE/api/abp/api-definition")"
[ "$CODE" = "200" ] && ok "api-definition → 200" || bad "api-definition → ${CODE}（API 没起？）"

curl -s $CURL_SSL_ARGS --max-time 10 "$API_BASE/api/abp/api-definition" -o "$WORK/apid.json"

# ─────────────────────────────────────────────────────────────
step "② 解析租户「${TENANT_NAME}」"
curl -s $CURL_SSL_ARGS --max-time 10 "$API_BASE/api/public/tenants" -o "$WORK/tenants.json"
TENANT_ID="$(python3 - "$WORK/tenants.json" "$TENANT_NAME" <<'PY'
import json,sys
try: data=json.load(open(sys.argv[1]))
except Exception: data=[]
name=sys.argv[2]
items=data if isinstance(data,list) else data.get('items',[])
for t in items:
    if t.get('name')==name or t.get('tenancyName')==name:
        print(t.get('id') or t.get('tenantId') or ''); break
PY
)"
if [ -n "$TENANT_ID" ]; then ok "tenantId = $TENANT_ID"; else bad "租户 ${TENANT_NAME} 不存在"; fi

# ─────────────────────────────────────────────────────────────
step "③ 三个账号登录"
login_probe() {
  local user="$1" pass="$2" hdr="${3:-}"
  local -a args=(-s $CURL_SSL_ARGS -X POST "$API_BASE/connect/token"
    -H 'Content-Type: application/x-www-form-urlencoded' -o "$WORK/t.json" -w '%{http_code}')
  [ -n "$hdr" ] && args+=(-H "Abp-TenantId: $hdr")
  curl "${args[@]}" \
    --data-urlencode 'grant_type=password' \
    --data-urlencode "client_id=$OAUTH_CLIENT" \
    --data-urlencode "scope=$OAUTH_SCOPE" \
    --data-urlencode "username=$user" \
    --data-urlencode "password=$pass" >/dev/null
  python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('access_token','') if True else '')" "$WORK/t.json" 2>/dev/null
}

ADMIN_TOKEN="$(login_probe "$ADMIN_USER" "$ADMIN_PASS" "")"
[ -n "$ADMIN_TOKEN" ] && ok "宿主管理员 $ADMIN_USER" || bad "宿主管理员 $ADMIN_USER 登录失败"

TEACHER_TOKEN="$(login_probe "$TEACHER_USER" "$TEACHER_PASS" "$TENANT_ID")"
[ -n "$TEACHER_TOKEN" ] && ok "老师 $TEACHER_USER" || bad "老师 $TEACHER_USER 登录失败"

STUDENT_TOKEN="$(login_probe "$STUDENT_USER" "$STUDENT_PASS" "$TENANT_ID")"
[ -n "$STUDENT_TOKEN" ] && ok "学生 $STUDENT_USER" || bad "学生 $STUDENT_USER 登录失败"

# ─────────────────────────────────────────────────────────────
step "④ 权限是否已授予（缺失会运行时 500「AuthorizationPolicy not found」）"
check_perm() {
  local token="$1" tenant="$2" perm="$3" who="$4"
  local val
  val="$(curl -s $CURL_SSL_ARGS --max-time 10 "$API_BASE/api/abp/application-configuration" \
      -H "Authorization: Bearer $token" ${tenant:+-H "Abp-TenantId: $tenant"} \
      | python3 -c "
import json,sys
d=json.load(sys.stdin)
print(d.get('auth',{}).get('grantedPolicies',{}).get(sys.argv[1], False))
" "$perm")"
  if [ "$val" = "True" ]; then ok "$who 拥有 $perm"; else bad "$who 缺少 $perm"; fi
}
# 注意：权限常量 Classroom.Default 的值就是 "KnowledgeHub.Classroom"（不含 .Default 后缀）
for p in KnowledgeHub.Classroom KnowledgeHub.Classroom.CheckIn KnowledgeHub.Classroom.Discussion \
         KnowledgeHub.Classroom.GroupTask KnowledgeHub.Classroom.Practice KnowledgeHub.Courses.Video; do
  check_perm "$TEACHER_TOKEN" "$TENANT_ID" "$p" "老师"
done
check_perm "$STUDENT_TOKEN" "$TENANT_ID" KnowledgeHub.Classroom.Participate "学生"
check_perm "$TEACHER_TOKEN" "$TENANT_ID" KnowledgeHub.Courses.Edit "老师"

# ─────────────────────────────────────────────────────────────
step "⑤ 新增控制器是否已注册（漏注册 = 404）"
python3 - "$WORK/apid.json" <<'PY' > "$WORK/ctrls.txt"
import json,sys
d=json.load(open(sys.argv[1]))
ctrls=d.get('modules',{}).get('app',{}).get('controllers',{})
urls=set()
for c in ctrls.values():
    for a in c.get('actions',{}).values():
        urls.add((a.get('httpMethod','').upper(), a.get('url','')))
for u in sorted(urls):
    print(u[0], u[1])
PY
need_routes() {
  local method="$1" pattern="$2" label="$3"
  if grep -qE "^${method} .*${pattern}" "$WORK/ctrls.txt"; then ok "$label"; else bad "$label（api-definition 里找不到 ${method} ${pattern}）"; fi
}
need_routes GET  "knowledge-resource/tree/"          "B1 knowledge-resource/tree"
need_routes POST "course-info/save"                  "B1 course-info/save"
need_routes POST "course-textbook"                   "B1 course-textbook"
need_routes GET  "learning/knowledge-mastery/"       "B1 learning/knowledge-mastery"
need_routes POST "check-in/do-check-in"              "B2 check-in/do-check-in"
need_routes POST "classroom-token/resolve"           "B2 classroom-token/resolve"
need_routes POST "discussion/reply"                  "B2 discussion/reply"
need_routes POST "classroom-practice/submit"         "B2 classroom-practice/submit"
need_routes POST "group-task/submit"                 "B2 group-task/submit"
need_routes POST "course-video/upload"               "B3 course-video/upload"
need_routes GET  "course-video/\\{id\\}/stream"        "B3 course-video/{id}/stream"
need_routes POST "exam/export-paper-pdf/"            "B3 exam/export-paper-pdf"
need_routes POST "exam/export-scores-pdf/"           "B3 exam/export-scores-pdf"

# ─────────────────────────────────────────────────────────────
step "⑥ Gotenberg（打印 PDF 依赖）"
GCODE="$(curl -s -o /dev/null -w '%{http_code}' --max-time 8 "$GOTENBERG_URL/health")"
[ "$GCODE" = "200" ] && ok "Gotenberg $GOTENBERG_URL → 200" || bad "Gotenberg $GOTENBERG_URL → ${GCODE}（打印会失败）"

# ─────────────────────────────────────────────────────────────
step "⑦ 测试视频生成能力"
if [ "$SKIP_VIDEO" = "1" ]; then
  warn "SKIP_VIDEO=1：将跳过视频上传/流式播放两项（仍测打点与打印）"
elif command -v ffmpeg >/dev/null 2>&1; then
  ok "ffmpeg 可用（$(ffmpeg -version 2>/dev/null | head -1 | cut -c1-40)…）"
else
  warn "没有 ffmpeg：设 SKIP_VIDEO=1 可跳过视频上传"
fi

# ─────────────────────────────────────────────────────────────
printf '\n==============================================================================\n'
if [ "$FAILED" -eq 0 ]; then
  printf ' %s预检通过%s（告警 %d 项）→ 可以跑 ./run.sh\n' "$C_OK" "$C_OFF" "$WARNED"
  exit 0
fi
printf ' %s预检未通过：%d 项失败%s（告警 %d 项）\n' "$C_BAD" "$FAILED" "$C_OFF" "$WARNED"
exit 1
