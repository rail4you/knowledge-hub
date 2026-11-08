#!/usr/bin/env bash
# =============================================================================
# 教学资源库闭环 E2E 测试
# -----------------------------------------------------------------------------
# 覆盖链路：
#   1. 管理员建「私有」教学资源库（数据分析资源库）→ 提交建库审核
#   2. 审核人通过建库审核（注意：审核内核「回避本人」，提交人不能自审）
#   3. 管理员把老师 wwq 分配到该库
#   4. 老师建课程「Python 数据分析」并挂进该库
#   5. 老师上传课程资源（真实文件，走分片上传接口）→ 关联课程
#   6. 老师建章节（含二级章节）→ 关联章节资源
#   7. 资源提交审核 → 管理员审核通过（学生侧可见的前提）
#   8. 课程资源「转换」为资源库资源 → 走库准入审核 → 审核通过
#   9. 老师发布课程 → 老师把学生 zmq 导入课程 → 学生自助选课
#  10. 学生侧验收：看到课程 / 章节 / 课程资源 / 章节资源 / 下载文件 / 上报学习进度
#
# 本地 / 远程通用：所有连接信息走环境变量，默认值针对本地开发环境。
#
# 用法：
#   ./run.sh                                    # 本地默认配置
#   API_BASE=http://1.2.3.4:44354 ./run.sh      # 远程服务器
#   RUN_TAG=fixed LIB_NAME=我的库 ./run.sh        # 固定命名，便于复跑
#   KEEP_TMP=1 ./run.sh                          # 保留临时目录便于排查
#
# 退出码：0 = 全部通过；非 0 = 某个环节失败（打印失败原因与响应体）
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LIB_DIR="$SCRIPT_DIR/lib"
JQX="$LIB_DIR/jqx.py"
MKPDF="$LIB_DIR/mkpdf.py"

# ----------------------------- 可配置项 -------------------------------------
API_BASE="${API_BASE:-https://localhost:44305}"
TENANT_NAME="${TENANT_NAME:-qidi}"
ADMIN_USER="${ADMIN_USER:-qidi-admin}"
ADMIN_PASS="${ADMIN_PASS:-123456}"
TEACHER_USER="${TEACHER_USER:-wwq}"
TEACHER_PASS="${TEACHER_PASS:-123456}"
STUDENT_USER="${STUDENT_USER:-zmq}"
STUDENT_PASS="${STUDENT_PASS:-1234567}"

# 审核人：审核内核「回避本人」，建库人不能审自己建的单。
# 默认用宿主 admin（租户为空）做「院校初审」——单管理员租户里唯一可行的做法。
# 若租户内有第二个院校管理员，可改成：
#   REVIEWER_USER=other-admin REVIEWER_PASS=xxx REVIEWER_TENANT=qidi ./run.sh
REVIEWER_USER="${REVIEWER_USER:-admin}"
REVIEWER_PASS="${REVIEWER_PASS:-1q2w3E*}"
REVIEWER_TENANT="${REVIEWER_TENANT-}"

OAUTH_CLIENT="${OAUTH_CLIENT:-KnowledgeHub_App}"
OAUTH_SCOPE="${OAUTH_SCOPE:-offline_access KnowledgeHub}"

LIB_NAME="${LIB_NAME:-数据分析资源库}"
COURSE_TITLE="${COURSE_TITLE:-Python 数据分析}"
RUN_TAG="${RUN_TAG:-$(date +%m%d-%H%M%S)}"
CURL_SSL_ARGS="${CURL_SSL_ARGS:--k}"
KEEP_TMP="${KEEP_TMP:-0}"

WORK="$(mktemp -d)"
cleanup() {
  if [ "$KEEP_TMP" = "1" ]; then
    echo "临时目录保留在: $WORK"
  else
    rm -rf "$WORK"
  fi
}
trap cleanup EXIT

TENANT_ID=""
ADMIN_TOKEN=""
TEACHER_TOKEN=""
STUDENT_TOKEN=""
REVIEWER_TOKEN=""
LAST_CODE=""
LAST_BODY=""

# ----------------------------- 输出 ------------------------------------------
if [ -t 1 ]; then
  C_RED=$'\033[31m'; C_GREEN=$'\033[32m'; C_YELLOW=$'\033[33m'
  C_BLUE=$'\033[36m'; C_BOLD=$'\033[1m'; C_OFF=$'\033[0m'
else
  C_RED=""; C_GREEN=""; C_YELLOW=""; C_BLUE=""; C_BOLD=""; C_OFF=""
fi

step() { printf '\n%s▶ %s%s\n' "$C_BOLD$C_BLUE" "$*" "$C_OFF"; }
pass() { printf '  %s✓%s %s\n' "$C_GREEN" "$C_OFF" "$*"; }
warn() { printf '  %s!%s %s\n' "$C_YELLOW" "$C_OFF" "$*"; }
die()  { printf '\n%s✗ 失败：%s%s\n' "$C_RED" "$*" "$C_OFF" >&2; dump_last >&2; exit 1; }
info() { printf '    %s· %s%s\n' "$C_BLUE" "$*" "$C_OFF"; }

dump_last() {
  [ -n "$LAST_BODY" ] || return 0
  printf '  最近一次响应 (HTTP %s):\n' "${LAST_CODE:-?}" >&2
  printf '%s\n' "$LAST_BODY" | head -c 1500 | sed 's/^/    /' >&2
  printf '\n' >&2
}

# ----------------------------- HTTP -----------------------------------------
# api <METHOD> <PATH> <TOKEN|-> [JSON_BODY] [TENANT_HEADER]
api() {
  local method="$1" path="$2" token="$3" body="${4:-}" tid="${5:-$TENANT_ID}"
  local -a args
  args=(-s $CURL_SSL_ARGS -X "$method" -H 'Accept: application/json'
        -o "$WORK/resp.json" -w '%{http_code}')
  [ "$token" != "-" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$tid" ] && args+=(-H "Abp-TenantId: $tid")
  if [ -n "$body" ]; then
    args+=(-H 'Content-Type: application/json' --data-binary "$body")
  fi
  LAST_CODE="$(curl "${args[@]}" "${API_BASE}${path}")"
  LAST_BODY="$(cat "$WORK/resp.json" 2>/dev/null || echo '')"
}

api_ok() {
  local method="$1" path="$2" token="$3" body="${4:-}" tid="${5:-$TENANT_ID}"
  api "$method" "$path" "$token" "$body" "$tid"
  case "$LAST_CODE" in
    2*) : ;;
    *) die "$method $path → HTTP $LAST_CODE" ;;
  esac
}

jget()  { python3 "$JQX" get   "$WORK/resp.json" "${1:-}"; }
jlen()  { python3 "$JQX" len   "$WORK/resp.json" "${1:-}"; }
jfind() { python3 "$JQX" find  "$WORK/resp.json" "$1" "$2" "$3" "${4:-id}"; }
jdeep() { python3 "$JQX" deep  "$WORK/resp.json" "$1" "$2" "${3:-id}"; }
jcount(){ python3 "$JQX" count "$WORK/resp.json" "$1" "$2" "$3"; }
# ----------------------------- 登录 ------------------------------------------
# login <user> <pass> <tenantId|''>
login() {
  local user="$1" pass="$2" tid="${3:-}"
  local out="$WORK/token.json"
  local -a args
  args=(-s $CURL_SSL_ARGS -X POST "${API_BASE}/connect/token"
        -H 'Content-Type: application/x-www-form-urlencoded'
        -o "$out" -w '%{http_code}')
  [ -n "$tid" ] && args+=(-H "Abp-TenantId: $tid")
  local code
  code="$(curl "${args[@]}" \
      --data-urlencode 'grant_type=password' \
      --data-urlencode "client_id=${OAUTH_CLIENT}" \
      --data-urlencode "scope=${OAUTH_SCOPE}" \
      --data-urlencode "username=${user}" \
      --data-urlencode "password=${pass}")"
  LAST_CODE="$code"
  LAST_BODY="$(cat "$out" 2>/dev/null || echo '')"
  case "$code" in
    2*) python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('access_token',''))" "$out" ;;
    *)  die "登录 ${user} 失败 (HTTP ${code})" ;;
  esac
}

# ----------------------------- 断言 ------------------------------------------
expect_eq() {
  if [ "$1" = "$2" ]; then pass "$3 = $2"; else die "$3 期望 $2，实际 '$1'"; fi
}
expect_ne() {
  if [ "$1" != "$2" ]; then pass "$3 ≠ $2（实际 $1）"; else die "$3 不应等于 $2"; fi
}
expect_nonempty() {
  if [ -n "$1" ]; then pass "$2 = $1"; else die "$2 为空"; fi
}
expect_ge() {
  if [ "${1:-0}" -ge "$2" ] 2>/dev/null; then
    pass "$3 = $1 (≥ $2)"
  else
    die "$3 期望 ≥ $2，实际 '${1:-0}'"
  fi
}

# =============================================================================
printf '%s\n' "=============================================================================="
printf '%s\n' " 教学资源库闭环 E2E"
printf ' API     : %s\n' "$API_BASE"
printf ' 租户    : %s\n' "$TENANT_NAME"
printf ' 账号    : %s(院校管理员) / %s(老师) / %s(学生)\n' "$ADMIN_USER" "$TEACHER_USER" "$STUDENT_USER"
printf ' 审核人  : %s (租户=%s)\n' "$REVIEWER_USER" "${REVIEWER_TENANT:-宿主}"
printf ' 资源库  : %s\n' "$LIB_NAME"
printf ' 课程    : %s\n' "$COURSE_TITLE"
printf ' 标记    : %s\n' "$RUN_TAG"
printf '%s\n' "=============================================================================="

# --- 0. 租户 ----------------------------------------------------------------
step "0/14 解析租户「${TENANT_NAME}」"
api_ok GET /api/public/tenants -
TENANT_ID="$(python3 - "$WORK/resp.json" "$TENANT_NAME" <<'PY'
import json,sys
data=json.load(open(sys.argv[1]))
for t in data or []:
    if str(t.get("name"))==sys.argv[2] or str(t.get("tenantName"))==sys.argv[2]:
        print(t.get("id") or ""); break
PY
)"
expect_nonempty "$TENANT_ID" "租户 Id"

# --- 1. 登录 ----------------------------------------------------------------
step "1/14 登录四个账号"
ADMIN_TOKEN="$(login "$ADMIN_USER" "$ADMIN_PASS" "$TENANT_ID")"
expect_nonempty "$ADMIN_TOKEN" "院校管理员 token"
TEACHER_TOKEN="$(login "$TEACHER_USER" "$TEACHER_PASS" "$TENANT_ID")"
expect_nonempty "$TEACHER_TOKEN" "老师 token"
STUDENT_TOKEN="$(login "$STUDENT_USER" "$STUDENT_PASS" "$TENANT_ID")"
expect_nonempty "$STUDENT_TOKEN" "学生 token"
REVIEWER_TOKEN="$(login "$REVIEWER_USER" "$REVIEWER_PASS" "$REVIEWER_TENANT")"
expect_nonempty "$REVIEWER_TOKEN" "审核人 token"

# --- 2. 用户 Id --------------------------------------------------------------
step "2/14 查询老师 / 学生用户 Id"
lookup_user() {
  local token="$1" user="$2"
  api_ok GET "/api/identity/users?filter=${user}&maxResultCount=50" "$token"
  python3 - "$WORK/resp.json" "$user" <<'PY'
import json,sys
data=json.load(open(sys.argv[1]))
want=sys.argv[2].lower()
items=(data or {}).get("items") or []
for u in items:
    if str(u.get("userName","")).lower()==want:
        print(u.get("id")); break
else:
    for u in items:
        if want in str(u.get("userName","")).lower():
            print(u.get("id")); break
PY
}
TEACHER_ID="$(lookup_user "$ADMIN_TOKEN" "$TEACHER_USER")"
expect_nonempty "$TEACHER_ID" "老师用户 Id ($TEACHER_USER)"
STUDENT_ID="$(lookup_user "$ADMIN_TOKEN" "$STUDENT_USER")"
expect_nonempty "$STUDENT_ID" "学生用户 Id ($STUDENT_USER)"

# --- 3. 建私有资源库 --------------------------------------------------------
step "3/14 院校管理员创建私有教学资源库「${LIB_NAME}」"
LIB_FULL_NAME="${LIB_NAME}-${RUN_TAG}"
api_ok POST /api/app/resource-library "$ADMIN_TOKEN" "$(python3 -c '
import json,sys
print(json.dumps({
  "name": sys.argv[1],
  "description": "E2E 回归：Python 数据分析课程资源库 " + sys.argv[2],
  "principal": "wwq",
  "visibility": 0,
  "isPublic": True,
  "openToExternalLearners": False,
  "sortOrder": 0
}, ensure_ascii=False))' "$LIB_FULL_NAME" "$RUN_TAG")"
LIB_ID="$(jget id)"
expect_nonempty "$LIB_ID" "资源库 Id"
info "资源库: $LIB_ID"

api_ok GET "/api/app/resource-library/${LIB_ID}" "$ADMIN_TOKEN"
expect_eq "$(jget status)" "3" "建库后状态（3 = PendingReview 待院校审核）"
expect_eq "$(jget visibility)" "0" "资源库可见性（0 = 私有）"

# --- 4. 建库审核 ------------------------------------------------------------
step "4/14 审核人通过「建库」审核"
api_ok GET "/api/app/review-ticket?targetType=0&targetId=${LIB_ID}&maxResultCount=50" "$REVIEWER_TOKEN" "$REVIEWER_TENANT"
LIB_TICKET="$(python3 - "$WORK/resp.json" "$LIB_ID" <<'PY'
import json,sys
data=json.load(open(sys.argv[1]))
items=(data or {}).get("items") or []
for t in items:
    if str(t.get("targetId"))==sys.argv[2]:
        print(t.get("id")); break
PY
)"
expect_nonempty "$LIB_TICKET" "建库审核单 Id"

api_ok POST /api/app/review-ticket/decide "$REVIEWER_TOKEN" \
  "$(python3 -c 'import json,sys;print(json.dumps({"ticketId":sys.argv[1],"decision":0}))' "$LIB_TICKET")" \
  "$REVIEWER_TENANT"
pass "建库审核单已通过"

api_ok GET "/api/app/resource-library/${LIB_ID}" "$ADMIN_TOKEN"
expect_eq "$(jget status)" "1" "资源库状态（1 = Active 已发布）"

# --- 5. 分配老师 ------------------------------------------------------------
step "5/14 院校管理员把老师 ${TEACHER_USER} 分配到资源库"
api_ok POST "/api/app/resource-library/${LIB_ID}/assign-teacher" "$ADMIN_TOKEN" \
  "$(python3 -c 'import json,sys;print(json.dumps({"userId":sys.argv[1]}))' "$TEACHER_ID")"
pass "已分配老师"

api_ok GET "/api/app/resource-library/${LIB_ID}/teachers" "$ADMIN_TOKEN"
expect_ge "$(jlen '')" "1" "库内教师数"
expect_eq "$(jfind '' userName "$TEACHER_USER" userId)" "$TEACHER_ID" "教师分配记录"

api_ok GET "/api/app/teaching-resource-library/my-libraries" "$TEACHER_TOKEN"
expect_eq "$(jfind '' id "$LIB_ID")" "$LIB_ID" "老师「我的教学资源库」可见"

# --- 6. 建课程 --------------------------------------------------------------
step "6/14 老师创建课程「${COURSE_TITLE}」并挂进资源库"
api_ok POST /api/app/course "$TEACHER_TOKEN" "$(python3 -c '
import json,sys
print(json.dumps({
  "title": sys.argv[1],
  "description": "E2E 回归课程：Python 数据分析（资源库闭环）",
  "libraryIds": [sys.argv[2]],
  "semester": "2026-2027-1",
  "credits": 4,
  "semesterHours": 48,
  "difficulty": 2,
  "majorIds": []
}, ensure_ascii=False))' "$COURSE_TITLE-$RUN_TAG" "$LIB_ID")"
COURSE_ID="$(jget id)"
expect_nonempty "$COURSE_ID" "课程 Id"
info "课程: $COURSE_ID"

api_ok GET "/api/app/course/${COURSE_ID}" "$TEACHER_TOKEN"
expect_eq "$(jget status)" "0" "新建课程状态（0 = Draft）"
expect_eq "$(jfind libraries id "$LIB_ID")" "$LIB_ID" "课程→资源库关联"

# --- 7. 上传课程资源 --------------------------------------------------------
step "7/14 老师上传课程资源文件（分片上传 → 建资源 → 关联课程）"

upload_resource() {
  local token="$1" file_path="$2" file_name="$3" res_name="$4" res_type="$5" desc="$6" with_course="${7:-yes}"
  local size chunk_size upload_id total i start len code
  size="$(wc -c < "$file_path" | tr -d ' ')"
  chunk_size=1048576

  api_ok POST /api/app/chunk-upload/initiate "$token" \
    "$(python3 -c 'import json,sys;print(json.dumps({"fileName":sys.argv[1],"totalSize":int(sys.argv[2]),"chunkSize":int(sys.argv[3])}))' \
       "$file_name" "$size" "$chunk_size")"
  upload_id="$(jget uploadId)"
  expect_nonempty "$upload_id" "上传会话 Id ($file_name)"

  total=$(( (size + chunk_size - 1) / chunk_size ))
  i=0
  while [ "$i" -lt "$total" ]; do
    start=$(( i * chunk_size ))
    len=$chunk_size
    [ $(( start + len )) -gt "$size" ] && len=$(( size - start ))
    tail -c +$(( start + 1 )) "$file_path" | head -c "$len" > "$WORK/chunk.bin"
    code="$(curl -s $CURL_SSL_ARGS -X POST "${API_BASE}/api/app/chunk-upload/upload" \
      -H "Authorization: Bearer ${token}" \
      ${TENANT_ID:+-H "Abp-TenantId: ${TENANT_ID}"} \
      -F "file=@${WORK}/chunk.bin;filename=${file_name}" \
      -F "uploadId=${upload_id}" -F "fileName=${file_name}" -F "chunkNumber=${i}" \
      -o "$WORK/chunk-resp.json" -w '%{http_code}')"
    [ "$code" = "200" ] || die "分片 ${i}/${total} 上传失败 HTTP $code"
    i=$(( i + 1 ))
  done

  api_ok POST /api/app/chunk-upload/complete "$token" \
    "$(python3 -c 'import json,sys;print(json.dumps({"uploadId":sys.argv[1],"fileName":sys.argv[2],"totalChunks":int(sys.argv[3])}))' \
       "$upload_id" "$file_name" "$total")"
  local f_path f_size f_ext f_name
  f_path="$(jget filePath)"; f_size="$(jget fileSize)"
  f_ext="$(jget fileExtension)"; f_name="$(jget originalFileName)"
  expect_nonempty "$f_path" "落盘路径 ($file_name)"

  api_ok POST /api/app/resource "$token" "$(python3 -c '
import json,sys
print(json.dumps({
  "name": sys.argv[1],
  "description": sys.argv[2],
  "resourceType": int(sys.argv[3]),
  "filePath": sys.argv[4],
  "fileSize": int(sys.argv[5]),
  "fileExtension": sys.argv[6],
  "originalFileName": sys.argv[7],
  "isDownloadable": True,
  "courseIds": [sys.argv[8]] if sys.argv[9] == "yes" else [],
  "libraryIds": [],
  "majorIds": []
}, ensure_ascii=False))' "$res_name" "$desc" "$res_type" "$f_path" "$f_size" \
       "$f_ext" "$f_name" "$COURSE_ID" "$with_course")"
  jget id
}

python3 "$MKPDF" "$WORK/handout.pdf" "Python Data Analysis - Lecture Notes"
cat > "$WORK/dataset.md" <<'MD'
# Python 数据分析 · 示例数据集说明

本文件由 KnowledgeHub 资源库闭环 E2E 自动生成，用于验证：

- 课程资源 → 教学资源库资源的「互转」
- 章节资源关联
- 学生端下载 / 预览

示例数据：sales.csv（2026-01 ~ 2026-06 电商销售明细）
MD

RES1_ID="$(upload_resource "$TEACHER_TOKEN" "$WORK/handout.pdf" \
  "python-data-analysis-lecture.pdf" "Python数据分析-环境搭建讲义" 0 \
  "第 1 章配套讲义（PDF）" yes | tail -1)"
expect_nonempty "$RES1_ID" "资源 1 Id"
info "资源 1: ${RES1_ID}（创建时直接带 courseIds）"

RES2_ID="$(upload_resource "$TEACHER_TOKEN" "$WORK/dataset.md" \
  "python-data-analysis-dataset.md" "Pandas数据处理实战案例" 0 \
  "第 2 章配套案例 + 数据集说明（Markdown）" no | tail -1)"
expect_nonempty "$RES2_ID" "资源 2 Id"
info "资源 2: ${RES2_ID}（创建时不带课程，后续手工关联）"

# 资源 2 走「课程资源」显式关联接口
api_ok POST /api/app/course-resource "$TEACHER_TOKEN" \
  "$(python3 -c 'import json,sys;print(json.dumps({"courseId":sys.argv[1],"resourceId":sys.argv[2],"sortOrder":0}))' \
     "$COURSE_ID" "$RES2_ID")"
pass "资源 2 已通过「课程资源」接口关联课程"
# 资源 1 创建时已带 courseIds，重复关联应被拒绝（幂等保护）
api POST /api/app/course-resource "$TEACHER_TOKEN" \
  "$(python3 -c 'import json,sys;print(json.dumps({"courseId":sys.argv[1],"resourceId":sys.argv[2],"sortOrder":0}))' \
     "$COURSE_ID" "$RES1_ID")"
case "$LAST_CODE" in
  403|400) pass "重复关联同一资源被正确拒绝（HTTP ${LAST_CODE}）" ;;
  *) warn "重复关联未被拒绝（HTTP ${LAST_CODE}）" ;;
esac

api_ok GET "/api/app/course-resource/by-course/${COURSE_ID}" "$TEACHER_TOKEN"
expect_eq "$(jlen '')" "2" "课程资源条数（老师视角）"

# --- 8. 章节 + 章节资源 -----------------------------------------------------
step "8/14 老师创建章节并关联章节资源"
mk_chapter() {
  local title="$1" desc="$2" order="$3" parent="${4:-}"
  if [ -n "$parent" ]; then
    api_ok POST /api/app/chapter "$TEACHER_TOKEN" \
      "$(python3 -c 'import json,sys;print(json.dumps({"courseId":sys.argv[1],"parentId":sys.argv[2],"title":sys.argv[3],"description":sys.argv[4],"sortOrder":int(sys.argv[5])}))' \
         "$COURSE_ID" "$parent" "$title" "$desc" "$order")"
  else
    api_ok POST /api/app/chapter "$TEACHER_TOKEN" \
      "$(python3 -c 'import json,sys;print(json.dumps({"courseId":sys.argv[1],"title":sys.argv[2],"description":sys.argv[3],"sortOrder":int(sys.argv[4])}))' \
         "$COURSE_ID" "$title" "$desc" "$order")"
  fi
  jget id
}

CH1_ID="$(mk_chapter "第1章 Python 环境搭建" "Anaconda / venv / Jupyter 快速上手" 1 | tail -1)"
expect_nonempty "$CH1_ID" "章节 1 Id"
CH2_ID="$(mk_chapter "第2章 Pandas 数据处理" "DataFrame 常用操作与数据清洗实战" 2 | tail -1)"
expect_nonempty "$CH2_ID" "章节 2 Id"
CH21_ID="$(mk_chapter "2.1 缺失值处理" "fillna / dropna / interpolate 对比" 1 "$CH2_ID" | tail -1)"
expect_nonempty "$CH21_ID" "章节 2.1 Id"
info "章节: $CH1_ID / $CH2_ID / $CH21_ID"

api_ok GET "/api/app/chapter/chapter-tree/${COURSE_ID}" "$TEACHER_TOKEN"
expect_eq "$(jfind '' id "$CH2_ID")" "$CH2_ID" "章节树含二级章节"

link_chapter_resource() {
  api_ok POST /api/app/chapter-resource "$TEACHER_TOKEN" \
    "$(python3 -c 'import json,sys;print(json.dumps({"chapterId":sys.argv[1],"resourceId":sys.argv[2],"displayName":sys.argv[3],"sortOrder":int(sys.argv[4])}))' \
       "$1" "$2" "$3" "$4")"
}
link_chapter_resource "$CH1_ID"  "$RES1_ID" "环境搭建讲义" 1
link_chapter_resource "$CH2_ID"  "$RES2_ID" "Pandas 实战案例" 1
link_chapter_resource "$CH21_ID" "$RES2_ID" "缺失值处理案例（子章节）" 1
pass "章节资源关联完成"

api_ok GET "/api/app/chapter-resource/by-chapter/${CH1_ID}" "$TEACHER_TOKEN"
expect_ge "$(jlen '')" "1" "章节 1 的资源数"

# 章节知识点资源（课程内容「章节资源」页管理的实体）
api_ok POST /api/app/knowledge-resource "$TEACHER_TOKEN" "$(python3 -c '
import json,sys
print(json.dumps({
  "courseId": sys.argv[1],
  "chapterId": sys.argv[2],
  "name": "1.1 Anaconda 环境安装（知识点）",
  "description": "E2E 自动生成的章节知识点资源",
  "content": "1. 下载 Anaconda\n2. 创建虚拟环境\n3. 验证 pandas 可用",
  "importanceLevel": "normal",
  "difficulty": 1,
  "sortOrder": 1,
  "tags": "python,env",
  "resourceId": sys.argv[3]
}, ensure_ascii=False))' "$COURSE_ID" "$CH1_ID" "$RES1_ID")"
KR_ID="$(jget id)"
expect_nonempty "$KR_ID" "知识点资源 Id"

api_ok GET "/api/app/knowledge-resource/by-chapter/${CH1_ID}" "$TEACHER_TOKEN"
expect_ge "$(jlen '')" "1" "章节 1 的知识点资源数"
expect_eq "$(jfind '' resourceId "$RES1_ID" resourceId)" "$RES1_ID" "知识点资源关联到课程资源"

# 回归：删除「带知识点资源」的章节必须成功（曾经因外键 NO ACTION 直接 500）
TMP_CH_ID="$(mk_chapter "临时章节（回归：删除带知识点资源的章节）" "用于验证章节删除不会被外键卡住" 99 | tail -1)"
api_ok POST /api/app/knowledge-resource "$TEACHER_TOKEN" "$(python3 -c '
import json,sys
print(json.dumps({
  "courseId": sys.argv[1], "chapterId": sys.argv[2],
  "name": "临时知识点", "content": "临时", "sortOrder": 1
}, ensure_ascii=False))' "$COURSE_ID" "$TMP_CH_ID")"
api DELETE "/api/app/chapter/${TMP_CH_ID}" "$TEACHER_TOKEN"
if [ "$LAST_CODE" = "204" ]; then
  pass "删除带知识点资源的章节成功（回归已修复：曾因 FK NO ACTION 报 500）"
else
  die "删除带知识点资源的章节期望 204，实际 HTTP $LAST_CODE：$LAST_BODY"
fi

# 反例：未关联课程的资源不能直接挂章节
api POST /api/app/chapter-resource "$TEACHER_TOKEN" \
  "$(python3 -c 'import json,sys;print(json.dumps({"chapterId":sys.argv[1],"resourceId":sys.argv[2],"sortOrder":9}))' \
     "$CH1_ID" "00000000-0000-0000-0000-000000000001")"
case "$LAST_CODE" in
  400|404|500) pass "未关联课程的资源被正确拦截（HTTP ${LAST_CODE}）" ;;
  *) warn "未关联课程的资源未被拦截（HTTP ${LAST_CODE}）" ;;
esac

# --- 9. 资源审核（学生可见前提）--------------------------------------------
step "9/14 资源提交审核 → 院校审核通过"
for rid in "$RES1_ID" "$RES2_ID"; do
  api_ok POST "/api/app/resource/submit-for-review/${rid}" "$TEACHER_TOKEN"
done
pass "两个资源已提交审核"

api_ok GET "/api/app/resource/pending-audit-list?maxResultCount=50" "$ADMIN_TOKEN"
info "待审资源数: $(jlen items)"

for rid in "$RES1_ID" "$RES2_ID"; do
  api_ok POST /api/app/resource/audit "$ADMIN_TOKEN" \
    "$(python3 -c 'import json,sys;print(json.dumps({"resourceId":sys.argv[1],"status":1,"comment":"E2E 自动校审通过"}))' "$rid")"
done
pass "资源校审通过"

api_ok GET "/api/app/resource/${RES2_ID}" "$ADMIN_TOKEN"
expect_eq "$(jget status)" "2" "资源状态（2 = SchoolApproved）"

# --- 10. 课程资源 → 资源库资源（互转）+ 准入审核 ---------------------------
step "10/14 课程资源转换进资源库 + 库准入审核"
api_ok POST /api/app/resource-conversion/convert-to-library "$TEACHER_TOKEN" \
  "$(python3 -c 'import json,sys;print(json.dumps({"resourceId":sys.argv[1],"sourceCourseId":sys.argv[2],"libraryId":sys.argv[3],"name":sys.argv[4]}))' \
     "$RES2_ID" "$COURSE_ID" "$LIB_ID" "Python数据分析-实战案例(库共享)")"
LIB_RES_ID="$(jget resourceId)"
expect_nonempty "$LIB_RES_ID" "转换后的库资源 Id"
info "库资源: $LIB_RES_ID"

api_ok GET "/api/app/teaching-resource-library/${LIB_ID}/contents?contentType=0&maxResultCount=100" "$TEACHER_TOKEN"
PUB_TICKET="$(jfind items id "$LIB_RES_ID" reviewTicketId)"
expect_nonempty "$PUB_TICKET" "库准入审核单 Id"

# --- 11. 通过库准入审核 ----------------------------------------------------
step "11/14 院校管理员通过「库准入」审核"
api_ok POST /api/app/review-ticket/decide "$ADMIN_TOKEN" \
  "$(python3 -c 'import json,sys;print(json.dumps({"ticketId":sys.argv[1],"decision":0}))' "$PUB_TICKET")"
pass "库准入审核通过"

api_ok GET "/api/app/teaching-resource-library/${LIB_ID}/contents?contentType=0&maxResultCount=100" "$TEACHER_TOKEN"
expect_eq "$(jfind items id "$LIB_RES_ID" publicationStatus)" "2" "库资源准入状态（2 = Published）"

api_ok GET "/api/app/resource/${LIB_RES_ID}" "$TEACHER_TOKEN"
expect_eq "$(jget libraryId)" "$LIB_ID" "库资源归属资源库"
expect_eq "$(jget sourceType)" "1" "库资源来源（1 = FromCourse 来自课程）"

# --- 12. 发布课程 + 导入学生 ------------------------------------------------
step "12/14 老师发布课程并把学生 ${STUDENT_USER} 导入课程"
api_ok POST "/api/app/course/${COURSE_ID}/publish" "$TEACHER_TOKEN" "{}"
api_ok GET "/api/app/course/${COURSE_ID}" "$TEACHER_TOKEN"
expect_eq "$(jget status)" "5" "课程状态（5 = Published 已发布）"

api_ok POST /api/app/student-course/enroll-student "$TEACHER_TOKEN" \
  "$(python3 -c 'import json,sys;print(json.dumps({"courseId":sys.argv[1],"studentId":sys.argv[2]}))' \
     "$COURSE_ID" "$STUDENT_ID")"
pass "学生已由老师导入课程（私有库学习闸门要求）"

# --- 13. 学生侧验收 --------------------------------------------------------
step "13/14 学生侧：选课 → 看课程 → 看章节 → 看资源 → 下载 → 上报进度"

# 私有库：学生无权查看（应 403）
api GET "/api/app/resource-library/${LIB_ID}" "$STUDENT_TOKEN"
if [ "$LAST_CODE" = "403" ] || [ "$LAST_CODE" = "404" ]; then
  pass "学生无权查看该私有资源库（HTTP ${LAST_CODE}，符合私有库语义）"
else
  die "学生访问私有资源库期望 403/404，实际 HTTP $LAST_CODE"
fi

api POST "/api/app/course/enroll/${COURSE_ID}" "$STUDENT_TOKEN" ""
case "$LAST_CODE" in
  2*) pass "学生自助选课成功" ;;
  403)
    # 私有库闸门：老师已把学生导入课程，选课记录已存在 —— 这正是私有库的预期路径
    if printf '%s' "$LAST_BODY" | grep -q '已经选修该课程'; then
      pass "学生已由老师导入课程（私有库闸门），自助选课返回「已经选修该课程」"
    else
      die "学生自助选课被拒：$LAST_BODY"
    fi
    ;;
  *) die "学生自助选课失败 HTTP $LAST_CODE：$LAST_BODY" ;;
esac

api_ok GET "/api/app/course/my-courses?maxResultCount=100" "$STUDENT_TOKEN"
expect_eq "$(jfind items id "$COURSE_ID")" "$COURSE_ID" "我的课程列表含该课程"

api_ok GET "/api/app/course/${COURSE_ID}/detail" "$STUDENT_TOKEN"
expect_eq "$(jget id)" "$COURSE_ID" "课程详情可读"
expect_ge "$(jlen chapters)" "2" "课程详情章节数"

api_ok GET "/api/app/chapter/chapter-tree/${COURSE_ID}" "$STUDENT_TOKEN"
expect_ge "$(jlen '')" "2" "章节树顶级节点数"
expect_eq "$(jdeep id "$CH21_ID")" "$CH21_ID" "章节树含二级章节"
expect_eq "$(jdeep resourceId "$RES1_ID" resourceId)" "$RES1_ID" "学生可见章节树含知识点资源关联"

api_ok GET "/api/app/course-resource/by-course/${COURSE_ID}" "$STUDENT_TOKEN"
expect_eq "$(jlen '')" "2" "学生可见的课程资源数（仅审核通过）"

api_ok GET "/api/app/chapter-resource/by-chapter/${CH1_ID}" "$STUDENT_TOKEN"
expect_ge "$(jlen '')" "1" "章节 1 资源数"

api_ok GET "/api/app/chapter-resource/by-chapter/${CH21_ID}" "$STUDENT_TOKEN"
expect_ge "$(jlen '')" "1" "子章节 2.1 资源数"

DL_CODE="$(curl -s $CURL_SSL_ARGS -o "$WORK/download.bin" -w '%{http_code}' \
  -H "Authorization: Bearer ${STUDENT_TOKEN}" \
  -H "Abp-TenantId: ${TENANT_ID}" \
  "${API_BASE}/api/resource-file/${RES2_ID}/download")"
expect_eq "$DL_CODE" "200" "学生下载课程资源文件"
expect_ge "$(wc -c < "$WORK/download.bin" | tr -d ' ')" "1" "下载文件大小"

api_ok POST /api/app/learning/record-progress "$STUDENT_TOKEN" \
  "$(python3 -c 'import json,sys;print(json.dumps({"courseId":sys.argv[1],"chapterId":sys.argv[2],"resourceId":sys.argv[3],"progress":50,"additionalMinutes":10}))' \
     "$COURSE_ID" "$CH1_ID" "$RES1_ID")"
pass "学生已上报学习进度"

api_ok GET "/api/app/learning/my-courses" "$STUDENT_TOKEN"
expect_eq "$(jfind '' courseId "$COURSE_ID" courseId)" "$COURSE_ID" "学生学习看板含该课程"

# --- 14. 汇总 --------------------------------------------------------------
step "14/14 汇总"
api_ok GET "/api/app/resource-library/${LIB_ID}" "$ADMIN_TOKEN"
info "资源库: $LIB_FULL_NAME ($LIB_ID)"
info "  状态=$(jget status) 可见性=$(jget visibility) 资源数=$(jget resourceCount) 课程数=$(jget courseCount) 教师数=$(jget teacherCount)"
info "课程: $COURSE_TITLE-$RUN_TAG ($COURSE_ID)"

cat > "$SCRIPT_DIR/.last-run-ids.json" <<EOF
{
  "runTag": "$RUN_TAG",
  "apiBase": "$API_BASE",
  "tenant": "$TENANT_NAME",
  "libraryId": "$LIB_ID",
  "libraryName": "$LIB_FULL_NAME",
  "courseId": "$COURSE_ID",
  "courseTitle": "$COURSE_TITLE-$RUN_TAG",
  "chapterIds": ["$CH1_ID", "$CH2_ID", "$CH21_ID"],
  "knowledgeResourceIds": ["$KR_ID"],
  "courseResourceIds": ["$RES1_ID", "$RES2_ID"],
  "libraryResourceId": "$LIB_RES_ID",
  "teacherId": "$TEACHER_ID",
  "studentId": "$STUDENT_ID"
}
EOF
info "对象 Id 已写入 $SCRIPT_DIR/.last-run-ids.json"

printf '\n%s\n' "------------------------------------------------------------------------------"
printf '%s\n' " 关键对象 Id（便于后续手工排查 / 清理）"
printf '   资源库   : %s\n' "$LIB_ID"
printf '   课程     : %s\n' "$COURSE_ID"
printf '   章节     : %s / %s / %s\n' "$CH1_ID" "$CH2_ID" "$CH21_ID"
printf '   课程资源 : %s / %s\n' "$RES1_ID" "$RES2_ID"
printf '   库资源   : %s\n' "$LIB_RES_ID"
printf '   老师/学生: %s / %s\n' "$TEACHER_ID" "$STUDENT_ID"
printf '%s\n' "------------------------------------------------------------------------------"

printf '\n%s\n' "✅ 教学资源库闭环 E2E 全部通过"
exit 0
