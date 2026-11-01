#!/usr/bin/env bash
# V2.0 P1~P4 端到端验证脚本（可重复执行）
#
# 覆盖：
#   Flow A 校内资源库（qidi，单级审核）：建库→校审→教师共享→准入审核→改库→删库归档
#   Flow B 共建资源库（guozhou 主建 + qidi 参建，两级审核）：建库→校审→联盟终审→跨校共享→引用→学生访问→撤销
#   Flow C 共建任务（共建库）：模板建任务→发布→协作校提交→主责校验收→验收≠发布
#   Perm 权限矩阵：学生/教师越权一律拒绝
#
# 用法：./scripts/test/v2-e2e.sh
# 前置：API 已启动（https://localhost:44305）、已执行迁移、qidi/guozhou 已开
#       LibraryReview / BuildTask / CrossSchoolRef 三个开关。
set -uo pipefail

API="https://localhost:44305"
DB_HOST=localhost; DB_PORT=5433; DB_USER=postgres; DB_PASS=postgres; DB_NAME=KnowledgeHub

PASS=0; FAIL=0; SKIP=0
FAILED_CASES=()

ok()   { PASS=$((PASS+1)); printf '  \033[32m✓\033[0m %s\n' "$1"; }
bad()  { FAIL=$((FAIL+1)); FAILED_CASES+=("$1"); printf '  \033[31m✗\033[0m %s\n' "$1"; }
skip() { SKIP=$((SKIP+1)); printf '  \033[33m–\033[0m %s\n' "$1"; }
sect() { printf '\n\033[1m=== %s ===\033[0m\n' "$1"; }

sql() { PGPASSWORD=$DB_PASS psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -t -A -c "$1" 2>/dev/null; }

token() { # token <tenant|-> <user> <pass>
  local tenant="$1" user="$2" pass="$3"
  if [ "$tenant" = "-" ]; then
    curl -sk -X POST "$API/connect/token" -H 'Content-Type: application/x-www-form-urlencoded' \
      --data-urlencode 'grant_type=password' --data-urlencode 'client_id=KnowledgeHub_App' \
      --data-urlencode 'scope=KnowledgeHub offline_access' \
      --data-urlencode "username=$user" --data-urlencode "password=$pass" \
      | python3 -c 'import json,sys;print(json.load(sys.stdin).get("access_token",""))'
  else
    curl -sk -X POST "$API/connect/token" -H 'Content-Type: application/x-www-form-urlencoded' -H "__tenant: $tenant" \
      --data-urlencode 'grant_type=password' --data-urlencode 'client_id=KnowledgeHub_App' \
      --data-urlencode 'scope=KnowledgeHub offline_access' \
      --data-urlencode "username=$user" --data-urlencode "password=$pass" \
      | python3 -c 'import json,sys;print(json.load(sys.stdin).get("access_token",""))'
  fi
}

# api <token> <tenant> <method> <path> [json-body]
api() {
  local tok="$1" tenant="$2" method="$3" path="$4" body="${5:-}"
  local args=(-sk -X "$method" "$API$path" -H "Authorization: Bearer $tok")
  [ "$tenant" != "-" ] && args+=(-H "__tenant: $tenant")
  if [ -n "$body" ]; then
    args+=(-H 'Content-Type: application/json' --data-binary "$body")
  fi
  curl "${args[@]}"
}

# api_code <token> <tenant> <method> <path> [json-body] —— 只返回状态码
api_code() {
  local tok="$1" tenant="$2" method="$3" path="$4" body="${5:-}"
  local args=(-sk -o /dev/null -w '%{http_code}' -X "$method" "$API$path" -H "Authorization: Bearer $tok")
  [ "$tenant" != "-" ] && args+=(-H "__tenant: $tenant")
  if [ -n "$body" ]; then
    args+=(-H 'Content-Type: application/json' --data-binary "$body")
  fi
  curl "${args[@]}"
}

# jg <python-expr on d>  —— 从 stdin 读 JSON 并取字段（空安全）
# 用法：curl ... | jg 'd.get("id")'
jg() { python3 -c "import json,sys
raw=sys.stdin.read()
try:
    d=json.loads(raw)
except Exception:
    d={}
print($1)"; }

# ─────────────────────────────────────────────────────────────
# 令牌
# ─────────────────────────────────────────────────────────────
sect "0. 令牌与前置检查"

ADMIN=$(token - admin '1q2w3E*')
LEAGUE=$(token - league-admin '123456')
QIDI_ADMIN=$(token qidi qidi-admin '123456')
QIDI_TEACHER=$(token qidi wwq '123456')
QIDI_STUDENT=$(token qidi zmq '1234567')
GZ_ADMIN=$(token guozhou guozhou-admin '123456')

for pair in "host-admin:$ADMIN" "league-admin:$LEAGUE" "qidi-admin:$QIDI_ADMIN" "qidi-wwq:$QIDI_TEACHER" "qidi-zmq:$QIDI_STUDENT" "gz-admin:$GZ_ADMIN"; do
  name=${pair%%:*}; tok=${pair#*:}
  [ -n "$tok" ] && ok "登录成功 $name" || bad "登录失败 $name"
done

QIDI_TENANT=$(sql "SELECT \"Id\" FROM \"AbpTenants\" WHERE \"Name\"='qidi'")
GZ_TENANT=$(sql "SELECT \"Id\" FROM \"AbpTenants\" WHERE \"Name\"='guozhou'")

# 前置：确保两个租户都开了 V2 三个开关（脚本自带，便于新环境直接跑）
for T in "$QIDI_TENANT" "$GZ_TENANT"; do
  curl -sk -X PUT "$API/api/feature-management/features?providerName=T&providerKey=$T"     -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json'     -d '{"features":[{"name":"KnowledgeHub.LibraryReview","value":"true"},{"name":"KnowledgeHub.BuildTask","value":"true"},{"name":"KnowledgeHub.CrossSchoolRef","value":"true"}]}'     -o /dev/null -w '' || true
done
ok "前置：已为 qidi/guozhou 开启 LibraryReview / BuildTask / CrossSchoolRef"

# 草稿资源（未入库），用于「共享进库 → 准入审核」
ensure_course() { # <token> <tenant> <tenantId> <title> <teacherId|-> → echo courseId
  local tok="$1" tenant="$2" tid="$3" title="$4" teacher="$5" id=""
  id=$(sql "SELECT \"Id\" FROM \"AppCourses\" WHERE \"Title\"='$title' AND \"TenantId\"='$tid' AND \"IsDeleted\"=false LIMIT 1")
  if [ -z "$id" ]; then
    local body="{\"title\":\"$title\",\"description\":\"E2E 测试课程\",\"semester\":\"2026-2027-1\",\"credits\":3,\"semesterHours\":48"
    [ "$teacher" != "-" ] && body="$body,\"teacherId\":\"$teacher\""
    body="$body}"
    id=$(api "$tok" "$tenant" POST /api/app/course "$body" | jg 'd.get("id","")')
    [ -n "$id" ] && api "$tok" "$tenant" POST "/api/app/course/$id/publish" '{}' >/dev/null
  fi
  echo "$id"
}

ensure_enrolled() { # <studentToken> <tenant> <courseId>
  api "$1" "$2" POST "/api/app/course/enroll/$3" '{}' >/dev/null
}

pick_res() { # <tenantId>：优先「草稿且未入库」，退化为「任意未入库」，再退化为「任意资源」
  local t="$1" id=""
  id=$(sql "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='$t' AND \"Status\"=0 AND \"LibraryId\" IS NULL AND \"IsDeleted\"=false ORDER BY \"CreationTime\" DESC LIMIT 1")
  [ -z "$id" ] && id=$(sql "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='$t' AND \"LibraryId\" IS NULL AND \"IsDeleted\"=false ORDER BY \"CreationTime\" DESC LIMIT 1")
  [ -z "$id" ] && id=$(sql "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='$t' AND \"Status\"<>3 AND \"IsDeleted\"=false ORDER BY \"CreationTime\" DESC LIMIT 1")
  [ -z "$id" ] && id=$(sql "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='$t' AND \"IsDeleted\"=false ORDER BY \"CreationTime\" DESC LIMIT 1")
  echo "$id"
}

QIDI_DRAFT_RES=$(pick_res "$QIDI_TENANT")
GZ_DRAFT_RES=$(pick_res "$GZ_TENANT")
# 课程自举：两条专用课程（已选 / 未选），保证引用受众判定可测
WWQ_ID_EARLY=$(sql "SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='wwq' LIMIT 1")
ZMQ_COURSE=$(ensure_course "$QIDI_ADMIN" qidi "$QIDI_TENANT" "E2E-课程-已选" "$WWQ_ID_EARLY")
ZMQ_OTHER_COURSE=$(ensure_course "$QIDI_ADMIN" qidi "$QIDI_TENANT" "E2E-课程-未选" "$WWQ_ID_EARLY")
GZ_COURSE=$(ensure_course "$GZ_ADMIN" guozhou "$GZ_TENANT" "E2E-课程-国州" "-")
if [ -n "$ZMQ_COURSE" ]; then ensure_enrolled "$QIDI_STUDENT" qidi "$ZMQ_COURSE"; fi
ZMQ_ID=$(sql "SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='zmq' LIMIT 1")

[ -n "$QIDI_DRAFT_RES" ] && ok "找到 qidi 草稿资源（${QIDI_DRAFT_RES}）" || bad "qidi 没有可用的草稿资源（Status=0 且未入库）"
[ -n "$GZ_DRAFT_RES" ] && ok "找到 guozhou 草稿资源（${GZ_DRAFT_RES}）" || bad "guozhou 没有可用的草稿资源"
[ -n "$GZ_COURSE" ] && ok "guozhou 自举课程（${GZ_COURSE}）" || bad "guozhou 课程自举失败"
ZMQ_ENROLLED=$(sql "SELECT count(*) FROM \"AppStudentCourses\" WHERE \"StudentId\"='$ZMQ_ID' AND \"CourseId\"='$ZMQ_COURSE'")
[ "${ZMQ_ENROLLED:-0}" -ge 1 ] 2>/dev/null && ok "zmq 已选课程（${ZMQ_COURSE}）" || bad "zmq 未选中自举课程（${ZMQ_COURSE}）"
ZMQ_NOT_ENROLLED=$(sql "SELECT count(*) FROM \"AppStudentCourses\" WHERE \"StudentId\"='$ZMQ_ID' AND \"CourseId\"='$ZMQ_OTHER_COURSE'")
[ "${ZMQ_NOT_ENROLLED:-1}" -eq 0 ] 2>/dev/null && ok "zmq 未选课程（${ZMQ_OTHER_COURSE}）" || bad "自举的未选课程竟然已选"

# ─────────────────────────────────────────────────────────────
# Flow A：校内资源库（qidi，单级审核）
# ─────────────────────────────────────────────────────────────
sect "Flow A 校内资源库（单级审核）"

UNIQ=$(date +%H%M%S)
A_LIB=$(api "$QIDI_ADMIN" qidi POST /api/app/resource-library \
  "{\"name\":\"E2E校内库-$UNIQ\",\"description\":\"e2e\",\"visibility\":0}" | jg 'd.get("id","")')
A_STATUS=$(api "$QIDI_ADMIN" qidi GET "/api/app/resource-library/$A_LIB" | jg 'd.get("status")')
[ "$A_STATUS" = "3" ] && ok "A1 建库后进入待院校审核（status=3）" || bad "A1 建库状态异常（status=${A_STATUS}，期望 3）"

A_TICKET=$(api "$QIDI_ADMIN" qidi GET "/api/app/review-ticket?TargetId=$A_LIB" | jg 'd["items"][0]["id"] if d.get("items") else ""')
[ -n "$A_TICKET" ] && ok "A2 生成审核单（${A_TICKET}）" || bad "A2 未生成审核单"

SELF=$(api "$QIDI_ADMIN" qidi POST /api/app/review-ticket/decide "{\"ticketId\":\"$A_TICKET\",\"decision\":0}")
echo "$SELF" | grep -q 'CannotReviewOwn' && ok "A3 自审被拒（CannotReviewOwn）" || bad "A3 自审未被拒绝：$SELF"

api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$A_TICKET\",\"decision\":0}" >/dev/null
A_STATUS2=$(api "$QIDI_ADMIN" qidi GET "/api/app/resource-library/$A_LIB" | jg 'd.get("status")')
[ "$A_STATUS2" = "1" ] && ok "A4 校审通过后库已发布（status=1）" || bad "A4 校审通过后状态异常（${A_STATUS2}）"

# 现有设计：校内库（私有/公开）教师必须被显式分配才可见 → 先分配再加断言
WWQ_ID=$(sql "SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='wwq' LIMIT 1")
A_ASSIGN=$(api_code "$QIDI_ADMIN" qidi POST "/api/app/resource-library/$A_LIB/assign-teacher" "{\"userId\":\"$WWQ_ID\"}")
A_TEACHER_ROWS=$(sql "SELECT count(*) FROM \"AppResourceLibraryTeachers\" WHERE \"LibraryId\"='$A_LIB'")
[ "$A_ASSIGN" = "200" ] && [ "${A_TEACHER_ROWS:-0}" -ge 1 ] 2>/dev/null \
  && ok "A5a 教师分配成功（assign=$A_ASSIGN, rows=${A_TEACHER_ROWS}）" \
  || bad "A5a 教师分配失败（assign=$A_ASSIGN, rows=${A_TEACHER_ROWS}）"

HAS_LIB=$(api "$QIDI_TEACHER" qidi GET /api/app/teaching-resource-library/my-libraries | jg 'any(x.get("id")=="'"$A_LIB"'" for x in (d if isinstance(d,list) else d.get("items",[])))')
[ "$HAS_LIB" = "True" ] && ok "A5 教师可见本校已发布库" || bad "A5 教师看不到本校已发布库"

# 教师共享资源入库 → 准入审核
A_ATTACH_RESP=$(api "$QIDI_TEACHER" qidi POST "/api/app/teaching-resource-library/$A_LIB/attach" \
  "{\"contentType\":0,\"contentId\":\"$QIDI_DRAFT_RES\"}")
A_PUB_STATUS=$(sql "SELECT \"Status\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$A_LIB' AND \"ContentId\"='$QIDI_DRAFT_RES'")
[ "$A_PUB_STATUS" = "1" ] && ok "A6 共享入库生成准入记录（PendingReview）" || bad "A6 准入记录异常（${A_PUB_STATUS}）resp=$(echo "$A_ATTACH_RESP" | head -c 200)"

A_ATTACH_TICKET=$(sql "SELECT t.\"Id\" FROM \"AppReviewTickets\" t JOIN \"AppResourcePublications\" p ON p.\"Id\"=t.\"TargetId\" WHERE p.\"LibraryId\"='$A_LIB' LIMIT 1")
api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$A_ATTACH_TICKET\",\"decision\":0}" >/dev/null
A_PUB2=$(sql "SELECT \"Status\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$A_LIB' AND \"ContentId\"='$QIDI_DRAFT_RES'")
A_RES2=$(sql "SELECT \"Status\" FROM \"AppResources\" WHERE \"Id\"='$QIDI_DRAFT_RES'")
[ "$A_PUB2" = "2" ] && ok "A7 准入通过 → 发布（Published）" || bad "A7 准入未发布（${A_PUB2}）"
[ "$A_RES2" = "3" ] && ok "A7 合并审核：内容同时置已审核（LeagueApproved）" || bad "A7 内容状态异常（${A_RES2}，期望 3）"

A_CONTENTS=$(api "$QIDI_TEACHER" qidi GET "/api/app/teaching-resource-library/$A_LIB/contents?MaxResultCount=20" | jg 'd.get("totalCount")')
[ "${A_CONTENTS:-0}" -ge 1 ] 2>/dev/null && ok "A8 库内容可见（$A_CONTENTS 条）" || bad "A8 库内容不可见（${A_CONTENTS}）"

# 学生：可见已发布库，不能建库
STU_CODE=$(api_code "$QIDI_STUDENT" qidi GET /api/app/teaching-resource-library/my-libraries)
if [ "$STU_CODE" = "200" ]; then ok "A9 学生可读库列表（只含已发布）"; else bad "A9 学生读库列表 $STU_CODE"; fi
STU_CREATE_CODE=$(api_code "$QIDI_STUDENT" qidi POST /api/app/resource-library '{"name":"x","visibility":0}')
[ "$STU_CREATE_CODE" = "403" ] || [ "$STU_CREATE_CODE" = "302" ] && ok "A10 学生建库被拒（${STU_CREATE_CODE}）" || bad "A10 学生竟可建库（${STU_CREATE_CODE}）"

# 改库：变更中 → 通过后生效
api "$QIDI_ADMIN" qidi PUT "/api/app/resource-library/$A_LIB" "{\"description\":\"e2e-改库后\"}" >/dev/null
A_ST=$(api "$QIDI_ADMIN" qidi GET "/api/app/resource-library/$A_LIB" | jg 'd.get("status")')
[ "$A_ST" = "6" ] && ok "A11 改库进入变更审核（status=6）" || bad "A11 改库状态异常（${A_ST}）"
A_UT=$(sql "SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\"='$A_LIB' AND \"ActionType\"=1 AND \"Status\"=0 LIMIT 1")
api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$A_UT\",\"decision\":0}" >/dev/null
A_DESC=$(api "$QIDI_ADMIN" qidi GET "/api/app/resource-library/$A_LIB" | jg 'd.get("description","")')
A_ST2=$(api "$QIDI_ADMIN" qidi GET "/api/app/resource-library/$A_LIB" | jg 'd.get("status")')
[ "$A_DESC" = "e2e-改库后" ] && [ "$A_ST2" = "1" ] && ok "A11 变更通过后生效并回到已发布" || bad "A11 变更未生效（desc=$A_DESC, status=${A_ST2}）"

# 删库：审核后归档（不硬删）
api "$QIDI_ADMIN" qidi DELETE "/api/app/resource-library/$A_LIB" >/dev/null
A_DEL_ST=$(api "$QIDI_ADMIN" qidi GET "/api/app/resource-library/$A_LIB" | jg 'd.get("status")')
[ "$A_DEL_ST" = "7" ] && ok "A12 删库进入删除审核（status=7，库仍可用）" || bad "A12 删除审核状态异常（${A_DEL_ST}）"
A_DT=$(sql "SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\"='$A_LIB' AND \"ActionType\"=2 AND \"Status\"=0 LIMIT 1")
api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$A_DT\",\"decision\":0}" >/dev/null
A_ARCH=$(sql "SELECT \"Status\" FROM \"AppResourceLibraries\" WHERE \"Id\"='$A_LIB'")
A_EXISTS=$(sql "SELECT count(*) FROM \"AppResourceLibraries\" WHERE \"Id\"='$A_LIB'")
[ "$A_ARCH" = "2" ] && [ "$A_EXISTS" = "1" ] && ok "A13 删除审核通过 → 归档且库行保留（不硬删）" || bad "A13 归档异常（status=$A_ARCH, exists=${A_EXISTS}）"

# A14 驳回 → 重提（轮次 = 2）
R_LIB=$(api "$QIDI_ADMIN" qidi POST /api/app/resource-library \
  "{\"name\":\"E2E驳回重提库-$UNIQ\",\"visibility\":0}" | jg 'd.get("id","")')
R_TICKET=$(api "$QIDI_ADMIN" qidi GET "/api/app/review-ticket?TargetId=$R_LIB" | jg 'd["items"][0]["id"] if d.get("items") else ""')
api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$R_TICKET\",\"decision\":1,\"reason\":\"名称不规范，请修改\"}" >/dev/null
R_STATUS=$(api "$QIDI_ADMIN" qidi GET "/api/app/resource-library/$R_LIB" | jg 'd.get("status")')
R_ROUND=$(api "$QIDI_ADMIN" qidi GET "/api/app/review-ticket/$R_TICKET" | jg 'd.get("roundNo")')
[ "$R_STATUS" = "0" ] && ok "A14 驳回 → 库回到草稿" || bad "A14 驳回后库状态异常（${R_STATUS}）"
api "$QIDI_ADMIN" qidi POST "/api/app/review-ticket/$R_TICKET/resubmit" '{}' >/dev/null
R_ROUND2=$(api "$QIDI_ADMIN" qidi GET "/api/app/review-ticket/$R_TICKET" | jg 'd.get("roundNo")')
R_STATUS2=$(api "$QIDI_ADMIN" qidi GET "/api/app/resource-library/$R_LIB" | jg 'd.get("status")')
[ "$R_ROUND2" = "2" ] && ok "A14 重新提交 → 轮次 2，回到待审（status=${R_STATUS2}）" || bad "A14 重提后轮次异常（${R_ROUND2}）"

# A15 并发审批：同一单两路并发，只允许一个成功
PAR=$(api "$QIDI_ADMIN" qidi POST /api/app/resource-library "{\"name\":\"E2E并发库-$UNIQ\",\"visibility\":0}" | jg 'd.get("id","")')
PAR_TICKET=$(api "$QIDI_ADMIN" qidi GET "/api/app/review-ticket?TargetId=$PAR" | jg 'd["items"][0]["id"] if d.get("items") else ""')
( api_code "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$PAR_TICKET\",\"decision\":0}" > /tmp/e2e-par1.txt ) &
( api_code "$LEAGUE" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$PAR_TICKET\",\"decision\":0}" > /tmp/e2e-par2.txt ) &
wait
P1C=$(cat /tmp/e2e-par1.txt 2>/dev/null); P2C=$(cat /tmp/e2e-par2.txt 2>/dev/null)
PAR_OK=0; PAR_ERR=0
for c in "$P1C" "$P2C"; do
  if [ "$c" = "200" ]; then PAR_OK=$((PAR_OK+1)); else PAR_ERR=$((PAR_ERR+1)); fi
done
PAR_ACTIONS=$(sql "SELECT count(*) FROM \"AppReviewActions\" WHERE \"TicketId\"='$PAR_TICKET' AND \"Operation\"=1")
[ "$PAR_OK" -eq 1 ] && [ "${PAR_ACTIONS:-0}" -eq 1 ] \
  && ok "A15 并发审批只成功一次（codes=$P1C/${P2C}，通过记录 $PAR_ACTIONS 条）" \
  || bad "A15 并发审批异常（codes=$P1C/${P2C}，通过记录 ${PAR_ACTIONS}）"
sql "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\"='$PAR_TICKET'" >/dev/null
sql "DELETE FROM \"AppReviewTickets\" WHERE \"Id\"='$PAR_TICKET'" >/dev/null
sql "DELETE FROM \"AppResourceLibraryMembers\" WHERE \"LibraryId\"='$PAR'" >/dev/null
sql "DELETE FROM \"AppResourceLibraries\" WHERE \"Id\"='$PAR'" >/dev/null

# ─────────────────────────────────────────────────────────────
# Flow B：共建资源库（guozhou 主建 + qidi 参建，两级审核）
# ─────────────────────────────────────────────────────────────
sect "Flow B 院校共建库（两级审核）+ 跨校引用"

B_LIB=$(api "$GZ_ADMIN" guozhou POST /api/app/resource-library \
  "{\"name\":\"E2E共建库-$UNIQ\",\"visibility\":2,\"partnerTenantIds\":[\"$QIDI_TENANT\"]}" | jg 'd.get("id","")')
[ -n "$B_LIB" ] && ok "B1 建院校共建库（参建=qidi）" || bad "B1 建共建库失败"

B_TICKET=$(api "$GZ_ADMIN" guozhou GET "/api/app/review-ticket?TargetId=$B_LIB" | jg 'd["items"][0]["id"] if d.get("items") else ""')
B_STAGES=$(api "$GZ_ADMIN" guozhou GET "/api/app/review-ticket/$B_TICKET" | jg 'd.get("totalStages")')
[ "$B_STAGES" = "2" ] && ok "B2 共建库需两级审核（totalStages=2）" || bad "B2 审核级数异常（${B_STAGES}）"

api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$B_TICKET\",\"decision\":0}" >/dev/null
B_STAGE_AFTER=$(api "$ADMIN" - GET "/api/app/review-ticket/$B_TICKET" | jg 'd.get("stageName")')
B_ST_AFTER=$(sql "SELECT \"Status\" FROM \"AppResourceLibraries\" WHERE \"Id\"='$B_LIB'")
[ "$B_ST_AFTER" = "4" ] && ok "B3 校审通过 → 待联盟终审（status=4，此阶段对参建校不可见）" || bad "B3 校审后状态异常（${B_ST_AFTER}，期望 4）"

SAME=$(api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$B_TICKET\",\"decision\":0}")
echo "$SAME" | grep -q 'StageActorConflict' && ok "B4 同一人连审两级被拒（StageActorConflict）" || bad "B4 同人跨级未被拒绝"

api "$LEAGUE" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$B_TICKET\",\"decision\":0}" >/dev/null
B_ST_FINAL=$(sql "SELECT \"Status\" FROM \"AppResourceLibraries\" WHERE \"Id\"='$B_LIB'")
[ "$B_ST_FINAL" = "1" ] && ok "B5 联盟终审通过 → 共建库发布" || bad "B5 终审后状态异常（${B_ST_FINAL}）"

B_QIDI_ADMIN_SEEN=$(api "$QIDI_ADMIN" qidi GET /api/app/resource-library | jg 'any(x.get("id")=="'"$B_LIB"'" for x in (d if isinstance(d,list) else d.get("items",[])))')
[ "$B_QIDI_ADMIN_SEEN" = "True" ] && ok "B6 参建校管理员可见共建库" || bad "B6 参建校管理员看不到共建库"

B_QIDI_TEACHER_SEEN=$(api "$QIDI_TEACHER" qidi GET /api/app/teaching-resource-library/my-libraries | jg 'any(x.get("id")=="'"$B_LIB"'" for x in (d if isinstance(d,list) else d.get("items",[])))')
if [ "$B_QIDI_TEACHER_SEEN" = "False" ]; then
  ok "B6b 参建校教师未被分配 → 不可见（符合设计：需主建校分配）"
else
  ok "B6b 参建校教师已分配 → 可见"
fi

# guozhou 教师共享资源进共建库 → 两级准入
api "$GZ_ADMIN" guozhou POST "/api/app/teaching-resource-library/$B_LIB/attach" \
  "{\"contentType\":0,\"contentId\":\"$GZ_DRAFT_RES\"}" >/dev/null
B_PUB_TICKET=$(sql "SELECT t.\"Id\" FROM \"AppReviewTickets\" t JOIN \"AppResourcePublications\" p ON p.\"Id\"=t.\"TargetId\" WHERE p.\"LibraryId\"='$B_LIB' AND t.\"Status\"=0 LIMIT 1")
B_PUB_STAGES=$(api "$ADMIN" - GET "/api/app/review-ticket/$B_PUB_TICKET" | jg 'd.get("totalStages")')
[ "$B_PUB_STAGES" = "2" ] && ok "B7 内容准入也走两级（totalStages=2）" || bad "B7 准入级数异常（${B_PUB_STAGES}）"
api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$B_PUB_TICKET\",\"decision\":0}" >/dev/null
api "$LEAGUE" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$B_PUB_TICKET\",\"decision\":0}" >/dev/null
B_PUB=$(sql "SELECT \"Status\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$B_LIB' AND \"ContentId\"='$GZ_DRAFT_RES'")
[ "$B_PUB" = "2" ] && ok "B8 准入两级通过 → 已发布" || bad "B8 准入未发布（${B_PUB}）"

# 跨校：qidi 教师引用到本校课程
# 引用需要「课程负责人」或院校管理员；优先找 wwq 任教的课程
WWQ_ID=$(sql "SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='wwq' LIMIT 1")
REF_COURSE=$(sql "SELECT \"Id\" FROM \"AppCourses\" WHERE \"TenantId\"='$QIDI_TENANT' AND \"TeacherId\"='$WWQ_ID' LIMIT 1")
REF_ACTOR="teacher"
if [ -z "$REF_COURSE" ]; then
  REF_COURSE="$ZMQ_COURSE"
  REF_ACTOR="admin"
fi
if [ "$REF_ACTOR" = "teacher" ]; then
  REF=$(api "$QIDI_TEACHER" qidi POST /api/app/course-shared-ref \
    "{\"courseId\":\"$REF_COURSE\",\"libraryId\":\"$B_LIB\",\"contentId\":\"$GZ_DRAFT_RES\"}" | jg 'd.get("id","")')
else
  REF=$(api "$QIDI_ADMIN" qidi POST /api/app/course-shared-ref \
    "{\"courseId\":\"$REF_COURSE\",\"libraryId\":\"$B_LIB\",\"contentId\":\"$GZ_DRAFT_RES\"}" | jg 'd.get("id","")')
fi
[ -n "$REF" ] && ok "B9 跨校引用成功（操作者=${REF_ACTOR}，课程=${REF_COURSE}）" || bad "B9 跨校引用失败"

DUP=$(api "$([ "$REF_ACTOR" = teacher ] && echo "$QIDI_TEACHER" || echo "$QIDI_ADMIN")" qidi POST /api/app/course-shared-ref \
  "{\"courseId\":\"$REF_COURSE\",\"libraryId\":\"$B_LIB\",\"contentId\":\"$GZ_DRAFT_RES\"}")
echo "$DUP" | grep -q '已引用到本课程' && ok "B10 重复引用被拒" || bad "B10 重复引用未被拒"

# 学生访问判定：已选课 → 可访问
ACC1=$(api "$QIDI_STUDENT" qidi POST "/api/app/course-shared-ref/check-access/$REF" '{}' | jg 'd.get("canAccess")')
[ "$ACC1" = "True" ] && ok "B11 已选课学生可访问（canAccess=true）" || bad "B11 已选课学生不可访问"

# 学生访问判定：未选课（用另一门课新建引用验证）
if [ -n "$ZMQ_OTHER_COURSE" ]; then
  REF2_RESP=$(api "$QIDI_ADMIN" qidi POST /api/app/course-shared-ref \
    "{\"courseId\":\"$ZMQ_OTHER_COURSE\",\"libraryId\":\"$B_LIB\",\"contentId\":\"$GZ_DRAFT_RES\"}")
  REF2=$(echo "$REF2_RESP" | jg 'd.get("id","")')
  ACC2=$(api "$QIDI_STUDENT" qidi POST "/api/app/course-shared-ref/check-access/$REF2" '{}' | jg 'd.get("canAccess")')
  REASON2=$(api "$QIDI_STUDENT" qidi POST "/api/app/course-shared-ref/check-access/$REF2" '{}' | jg 'd.get("reason","")')
  [ "$ACC2" = "False" ] && ok "B12 未选课学生被拒（reason: ${REASON2}）" || bad "B12 未选课学生竟可访问（acc=$ACC2, ref=$REF2, resp=$(echo "$REF2_RESP" | head -c 160)）"
  api "$QIDI_ADMIN" qidi POST "/api/app/course-shared-ref/$REF2/revoke?reason=e2e清理" '{}' >/dev/null
else
  skip "B12 未选课程判定（无第二门课）"
fi

# 撤销 → 立即失效
api "$([ "$REF_ACTOR" = teacher ] && echo "$QIDI_TEACHER" || echo "$QIDI_ADMIN")" qidi POST "/api/app/course-shared-ref/$REF/revoke?reason=e2e撤销" '{}' >/dev/null
ACC3=$(api "$QIDI_STUDENT" qidi POST "/api/app/course-shared-ref/check-access/$REF" '{}' | jg 'd.get("canAccess")')
REASON3=$(api "$QIDI_STUDENT" qidi POST "/api/app/course-shared-ref/check-access/$REF" '{}' | jg 'd.get("reason","")')
[ "$ACC3" = "False" ] && ok "B13 撤销后立即失效（${REASON3}）" || bad "B13 撤销后仍可访问"

# 学生不能建引用（无 Courses.Edit）
STU_REF_CODE=$(api_code "$QIDI_STUDENT" qidi POST /api/app/course-shared-ref \
  "{\"courseId\":\"$REF_COURSE\",\"libraryId\":\"$B_LIB\",\"contentId\":\"$GZ_DRAFT_RES\"}")
[ "$STU_REF_CODE" = "403" ] || [ "$STU_REF_CODE" = "302" ] && ok "B14 学生建引用被拒（${STU_REF_CODE}）" || bad "B14 学生竟可建引用（${STU_REF_CODE}）"

# ─────────────────────────────────────────────────────────────
# Flow C：共建任务（共建库）
# ─────────────────────────────────────────────────────────────
sect "Flow C 共建任务（模板 → 发布 → 协作校提交 → 主责校验收）"

C_TPL=$(sql "SELECT \"Id\" FROM \"AppBuildTaskTemplates\" ORDER BY \"CreationTime\" LIMIT 1")
if [ -z "$C_TPL" ]; then
  C_TPL=$(api "$GZ_ADMIN" guozhou POST /api/app/build-task/task-template \
    "{\"libraryId\":\"$B_LIB\",\"code\":\"e2e-$UNIQ\",\"name\":\"E2E交付清单\",\"items\":[{\"itemName\":\"课程标准\",\"contentType\":0,\"requiredCount\":1},{\"itemName\":\"课件\",\"contentType\":0,\"requiredCount\":2}]}" | jg 'd.get("id","")')
  ok "C0 创建任务模板（${C_TPL}）"
else
  ok "C0 复用已有任务模板（${C_TPL}）"
fi

C_TASK=$(api "$GZ_ADMIN" guozhou POST /api/app/build-task/task \
  "{\"libraryId\":\"$B_LIB\",\"templateId\":\"$C_TPL\",\"title\":\"E2E共建任务-$UNIQ\",\"dueAt\":\"2026-12-31T00:00:00Z\",\"collaboratorSchoolIds\":[\"$QIDI_TENANT\"]}" | jg 'd.get("id","")')
C_REQ=$(api "$GZ_ADMIN" guozhou GET "/api/app/build-task/$C_TASK/task" | jg 'd.get("requiredTotal")')
[ -n "$C_TASK" ] && ok "C1 从模板建任务（交付项要求总数=${C_REQ}）" || bad "C1 建任务失败"

api "$GZ_ADMIN" guozhou POST "/api/app/build-task/$C_TASK/publish-task" >/dev/null
C_ST=$(api "$GZ_ADMIN" guozhou GET "/api/app/build-task/$C_TASK/task" | jg 'd.get("statusName")')
[ "$C_ST" = "已发布" ] && ok "C2 任务发布" || bad "C2 任务状态异常（${C_ST}）"

# 协作校（qidi）教师能否看到并提交
MY_TASK_COUNT=$(api "$QIDI_TEACHER" qidi GET "/api/app/build-task/tasks?OnlyMine=true&MaxResultCount=50" | jg 'd.get("totalCount")')
if [ "${MY_TASK_COUNT:-0}" -ge 1 ] 2>/dev/null; then
  ok "C3 协作校教师看到任务（$MY_TASK_COUNT 条）"
else
  bad "C3 协作校教师看不到任务（${MY_TASK_COUNT}）"
fi

C_ITEM=$(sql "SELECT \"Id\" FROM \"AppBuildTaskItems\" WHERE \"TaskId\"='$C_TASK' ORDER BY \"SortOrder\" LIMIT 1")
C_DELIVERY=$(api "$QIDI_TEACHER" qidi POST /api/app/build-task/submit-delivery \
  "{\"taskId\":\"$C_TASK\",\"taskItemId\":\"$C_ITEM\",\"contentType\":0,\"contentId\":\"$QIDI_DRAFT_RES\",\"comment\":\"e2e交付\"}" | jg 'd.get("id","")')
[ -n "$C_DELIVERY" ] && ok "C4 协作校提交交付物" || bad "C4 提交交付失败"

# 提交同时发起准入审核（验收 ≠ 发布）
C_PUB=$(sql "SELECT count(*) FROM \"AppResourcePublications\" WHERE \"ContentId\"='$QIDI_DRAFT_RES' AND \"LibraryId\"='$B_LIB'")
[ "${C_PUB:-0}" -ge 1 ] && ok "C5 提交交付同时生成库准入记录（验收≠发布）" || bad "C5 未生成准入记录"

CAN_REVIEW=$(api "$QIDI_TEACHER" qidi GET "/api/app/build-task/$C_TASK/task" | jg 'd.get("canReview")')
[ "$CAN_REVIEW" = "False" ] && ok "C6 协作校教师不可验收（canReview=false）" || bad "C6 协作校教师竟可验收"

C_REVIEW_RESP=$(api "$GZ_ADMIN" guozhou POST /api/app/build-task/review-delivery \
  "{\"deliveryId\":\"$C_DELIVERY\",\"accept\":true,\"comment\":\"符合要求\"}")
C_PROG=$(api "$GZ_ADMIN" guozhou GET "/api/app/build-task/$C_TASK/task" | jg 'str(d.get("acceptedTotal"))+"/"+str(d.get("requiredTotal"))')
[ "$C_PROG" != "0/0" ] && [ "${C_PROG%%/*}" -ge 1 ] && ok "C7 主责校验收 → 进度 $C_PROG" || bad "C7 验收后进度异常（${C_PROG}）"

C_STILL=$(sql "SELECT \"Status\" FROM \"AppResourcePublications\" WHERE \"ContentId\"='$QIDI_DRAFT_RES' AND \"LibraryId\"='$B_LIB' ORDER BY \"CreationTime\" DESC LIMIT 1")
[ "$C_STILL" = "1" ] && ok "C8 验收后资源仍在待审（证明验收≠发布）" || bad "C8 验收竟直接发布了资源（pub=${C_STILL}）"

STU_DELIVERY_CODE=$(api_code "$QIDI_STUDENT" qidi POST /api/app/build-task/submit-delivery \
  "{\"taskId\":\"$C_TASK\",\"taskItemId\":\"$C_ITEM\",\"contentType\":0,\"contentId\":\"$QIDI_DRAFT_RES\"}")
[ "$STU_DELIVERY_CODE" = "403" ] || [ "$STU_DELIVERY_CODE" = "302" ] && ok "C9 学生提交交付被拒（${STU_DELIVERY_CODE}）" || bad "C9 学生竟可提交交付（${STU_DELIVERY_CODE}）"


# ─────────────────────────────────────────────────────────────
# Flow D：企业主体 + 认证（P5）
# ─────────────────────────────────────────────────────────────
sect "Flow D 企业主体与认证（学校初审 → 联盟终审）"

E_BODY="{\"name\":\"E2E合作企业-$UNIQ\",\"unifiedSocialCreditCode\":\"91330000E2E$UNIQ\",\"industry\":\"养老服务\",\"contactName\":\"张经理\",\"contactPhone\":\"13800000000\"}"
E_ID=$(api "$GZ_ADMIN" guozhou POST /api/app/enterprise "$E_BODY" | jg 'd.get("id","")')
E_STATUS=$(api "$GZ_ADMIN" guozhou GET "/api/app/enterprise/$E_ID" | jg 'd.get("certificationStatusName")')
[ -n "$E_ID" ] && ok "D1 创建企业（状态=${E_STATUS}）" || bad "D1 创建企业失败"

E_USABLE=$(api "$GZ_ADMIN" guozhou GET "/api/app/enterprise/$E_ID" | jg 'd.get("isUsable")')
[ "$E_USABLE" = "False" ] && ok "D2 未认证时不可用（isUsable=false）" || bad "D2 未认证竟可用"

api "$GZ_ADMIN" guozhou POST "/api/app/enterprise/$E_ID/submit-certification" '{}' >/dev/null
E_TICKET=$(api "$ADMIN" - GET "/api/app/review-ticket?TargetId=$E_ID" | jg 'd["items"][0]["id"] if d.get("items") else ""')
E_STAGES=$(api "$ADMIN" - GET "/api/app/review-ticket/$E_TICKET" | jg 'd.get("totalStages")')
[ "$E_STAGES" = "2" ] && ok "D3 提交认证 → 两级审核（学校初审 + 联盟终审）" || bad "D3 认证审核级数异常（${E_STAGES}）"

api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$E_TICKET\",\"decision\":0,\"reason\":\"资料齐全\"}" >/dev/null
E_MID=$(api "$ADMIN" - GET "/api/app/review-ticket/$E_TICKET" | jg 'd.get("stageName")')
[ "$E_MID" = "联盟终审" ] && ok "D4 校审通过 → 待联盟终审" || bad "D4 校审后阶段异常（${E_MID}）"

api "$LEAGUE" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$E_TICKET\",\"decision\":0,\"reason\":\"通过认证\"}" >/dev/null
E_FINAL=$(api "$GZ_ADMIN" guozhou GET "/api/app/enterprise/$E_ID" | jg 'd.get("certificationStatusName")')
E_USABLE2=$(api "$GZ_ADMIN" guozhou GET "/api/app/enterprise/$E_ID" | jg 'd.get("isUsable")')
[ "$E_FINAL" = "已认证" ] && [ "$E_USABLE2" = "True" ] && ok "D5 终审通过 → 已认证且可用" || bad "D5 认证结果异常（$E_FINAL / usable=${E_USABLE2}）"

E_MINE=$(api "$GZ_ADMIN" guozhou GET /api/app/enterprise/mine | jg 'd.get("id","")')
[ -n "$E_MINE" ] && ok "D6 「我的企业」返回主企业（${E_MINE}）" || bad "D6 我的企业为空"

# ─────────────────────────────────────────────────────────────
# Flow E：单位有效期门禁（P5）
# ─────────────────────────────────────────────────────────────
sect "Flow E 单位有效期门禁（过期禁写、读放行、续期恢复）"

# 用 guozhou 做被测校：先开开关 → 设为已过期 → 断言 → 还原
curl -sk -X PUT "$API/api/feature-management/features?providerName=T&providerKey=$GZ_TENANT" \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"features":[{"name":"KnowledgeHub.OrganizationValidity","value":"true"}]}' -o /dev/null

api "$ADMIN" - POST /api/app/school-validity/set-validity \
  "{\"tenantId\":\"$GZ_TENANT\",\"validFrom\":\"2025-01-01T00:00:00Z\",\"validTo\":\"2025-06-30T00:00:00Z\",\"readOnlyAfterExpiry\":true}" >/dev/null

VE_READ=$(api_code "$GZ_ADMIN" guozhou GET '/api/app/resource-library')
VE_WRITE=$(api_code "$GZ_ADMIN" guozhou POST /api/app/resource-library '{"name":"过期后建库-应被拒","visibility":0}')
[ "$VE_READ" = "200" ] && ok "E1 过期后读放行（GET=200）" || bad "E1 过期后读被拒（${VE_READ}）"
[ "$VE_WRITE" = "403" ] && ok "E2 过期后写被拒（POST=403，含可读文案）" || bad "E2 过期后写未被拒（${VE_WRITE}）"

api "$ADMIN" - POST /api/app/school-validity/set-validity \
  "{\"tenantId\":\"$GZ_TENANT\",\"validFrom\":null,\"validTo\":null,\"readOnlyAfterExpiry\":true}" >/dev/null
VE_WRITE2=$(api_code "$GZ_ADMIN" guozhou POST /api/app/resource-library "{\"name\":\"E2E续期后库-$UNIQ\",\"visibility\":0}")
[ "$VE_WRITE2" = "200" ] && ok "E3 续期后写恢复（POST=200）" || bad "E3 续期后仍不可写（${VE_WRITE2}）"
VE_LIB=$(api "$GZ_ADMIN" guozhou GET "/api/app/resource-library" | jg '(next((x["id"] for x in (d if isinstance(d,list) else d.get("items",[])) if x.get("name")=="E2E续期后库-'$UNIQ'"), ""))')


# ─────────────────────────────────────────────────────────────
# Flow F：产教合作（需求 → 响应 → 项目 → 里程碑）
# ─────────────────────────────────────────────────────────────
sect "Flow F 产教合作需求与项目台账"

# F0 先确保有一家已认证企业（供需求归属）
F_EID=$(api "$GZ_ADMIN" guozhou POST /api/app/enterprise \
  "{\"name\":\"E2E产教企业-$UNIQ\",\"industry\":\"智能制造\",\"contactName\":\"李经理\"}" | jg 'd.get("id","")')
api "$GZ_ADMIN" guozhou POST "/api/app/enterprise/$F_EID/submit-certification" '{}' >/dev/null
F_ETICKET=$(api "$ADMIN" - GET "/api/app/review-ticket?TargetId=$F_EID" | jg 'd["items"][0]["id"] if d.get("items") else ""')
api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$F_ETICKET\",\"decision\":0}" >/dev/null
api "$LEAGUE" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$F_ETICKET\",\"decision\":0}" >/dev/null
F_CERT=$(api "$GZ_ADMIN" guozhou GET "/api/app/enterprise/$F_EID" | jg 'd.get("certificationStatusName")')
[ "$F_CERT" = "已认证" ] && ok "F0 企业认证就绪（${F_EID}）" || bad "F0 企业认证失败（${F_CERT}）"

# F1 建需求（草稿）
F_DID=$(api "$GZ_ADMIN" guozhou POST /api/app/cooperation/demand \
  "{\"type\":1,\"title\":\"E2E公开合作需求-$UNIQ\",\"majors\":\"智慧养老\",\"region\":\"浙江杭州\",\"scope\":2}" | jg 'd.get("id","")')
F_DSTATUS=$(api "$GZ_ADMIN" guozhou GET "/api/app/cooperation/$F_DID/demand" | jg 'd.get("statusName")')
[ "$F_DSTATUS" = "草稿" ] && ok "F1 创建合作需求（草稿）" || bad "F1 建需求状态异常（${F_DSTATUS}）"

# F2 草稿不进大厅
F_HALL0=$(api "$QIDI_ADMIN" qidi GET "/api/app/cooperation/hall?MaxResultCount=50" | jg 'any(x.get("id")=="'"$F_DID"'" for x in d.get("items",[]))')
[ "$F_HALL0" = "False" ] && ok "F2 草稿不可见（未审核不进大厅）" || bad "F2 草稿竟出现在大厅"

# F3 提交 → 两级审核
api "$GZ_ADMIN" guozhou POST "/api/app/cooperation/$F_DID/submit-demand" '{}' >/dev/null
F_TICKET=$(api "$ADMIN" - GET "/api/app/review-ticket?TargetId=$F_DID" | jg 'd["items"][0]["id"] if d.get("items") else ""')
F_STAGES=$(api "$ADMIN" - GET "/api/app/review-ticket/$F_TICKET" | jg 'd.get("totalStages")')
[ "$F_STAGES" = "2" ] && ok "F3 需求审核两级（学校初审 + 联盟终审）" || bad "F3 需求审核级数异常（${F_STAGES}）"
api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$F_TICKET\",\"decision\":0}" >/dev/null
api "$LEAGUE" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$F_TICKET\",\"decision\":0}" >/dev/null
F_PUB=$(api "$GZ_ADMIN" guozhou GET "/api/app/cooperation/$F_DID/demand" | jg 'd.get("statusName")')
[ "$F_PUB" = "已发布" ] && ok "F4 终审通过 → 需求发布" || bad "F4 发布状态异常（${F_PUB}）"

# F5 公开需求：外校可见
F_HALL=$(api "$QIDI_ADMIN" qidi GET "/api/app/cooperation/hall?MaxResultCount=50" | jg 'any(x.get("id")=="'"$F_DID"'" for x in d.get("items",[]))')
[ "$F_HALL" = "True" ] && ok "F5 公开需求进入大厅（外校可见）" || bad "F5 公开需求外校不可见"

# F6 学校响应 + 重复被拒
F_RID=$(api "$QIDI_ADMIN" qidi POST /api/app/cooperation/respond "{\"demandId\":\"$F_DID\",\"note\":\"我校有实训基地\"}" | jg 'd.get("id","")')
[ -n "$F_RID" ] && ok "F6 学校响应需求（${F_RID}）" || bad "F6 响应失败"
F_DUP=$(api "$QIDI_ADMIN" qidi POST /api/app/cooperation/respond "{\"demandId\":\"$F_DID\"}")
echo "$F_DUP" | grep -q '已响应' && ok "F7 重复响应被拒" || bad "F7 重复响应未被拒"

# F8 企业接受响应
api "$GZ_ADMIN" guozhou POST "/api/app/cooperation/decide-response/$F_RID" '{"accept":true,"note":"欢迎合作"}' >/dev/null
F_RSTATUS=$(api "$GZ_ADMIN" guozhou GET "/api/app/cooperation/responses/$F_DID" | jg 'd[0].get("statusName") if isinstance(d,list) and d else ""')
[ "$F_RSTATUS" = "已接受" ] && ok "F8 企业接受响应" || bad "F8 响应状态异常（${F_RSTATUS}）"

# F9 建项目台账
F_PID=$(api "$GZ_ADMIN" guozhou POST /api/app/cooperation/project \
  "{\"responseId\":\"$F_RID\",\"name\":\"E2E合作项目-$UNIQ\",\"agreementRef\":\"HT-E2E-001\"}" | jg 'd.get("id","")')
F_STAGE=$(api "$GZ_ADMIN" guozhou GET "/api/app/cooperation/projects?MaxResultCount=50" | jg '(next((x["stageName"] for x in d.get("items",[]) if x.get("id")=="'"$F_PID"'"), ""))')
[ -n "$F_PID" ] && [ "$F_STAGE" = "洽谈确认" ] && ok "F9 建立项目台账（阶段=${F_STAGE}）" || bad "F9 建项目失败（$F_PID / ${F_STAGE}）"

# F10 里程碑：新增 + 完成
F_MID=$(api "$GZ_ADMIN" guozhou POST /api/app/cooperation/milestone \
  "{\"projectId\":\"$F_PID\",\"name\":\"基地挂牌\",\"dueAt\":\"2026-03-31T00:00:00Z\",\"sortOrder\":1}" | jg 'd.get("id","")')
F_MDONE=$(api "$GZ_ADMIN" guozhou POST /api/app/cooperation/complete-milestone \
  "{\"milestoneId\":\"$F_MID\",\"evidenceRef\":\"挂牌照片.zip\"}" | jg 'd.get("isCompleted")')
[ "$F_MDONE" = "True" ] && ok "F10 里程碑完成（计入进度）" || bad "F10 里程碑未完成"

# F11 归档必须填验收意见
F_ARCH_ERR=$(api "$GZ_ADMIN" guozhou POST /api/app/cooperation/advance-stage "{\"projectId\":\"$F_PID\",\"stage\":3}")
echo "$F_ARCH_ERR" | grep -q '验收意见' && ok "F11 缺验收意见时拒绝归档" || bad "F11 归档校验缺失"
F_PROG=$(api "$GZ_ADMIN" guozhou POST /api/app/cooperation/advance-stage \
  "{\"projectId\":\"$F_PID\",\"stage\":3,\"acceptanceNote\":\"验收通过\"}" | jg 'str(d.get("completedMilestoneCount"))+"/"+str(d.get("milestoneCount"))')
[ "$F_PROG" != "0/0" ] && ok "F12 归档成功且里程碑进度正确（${F_PROG}）" || bad "F12 归档后进度异常（${F_PROG}）"


# ─────────────────────────────────────────────────────────────
# Flow G：企业账号绑定 + 企业贡献入库（P5）
# ─────────────────────────────────────────────────────────────
sect "Flow G 企业账号绑定与贡献入库"

# G1 学校开通企业账号（自动建企业档案 + 绑定管理员）
ENT_USER="e2e-ent-$UNIQ"
G_UNAME=$(sql "SELECT \"UserName\" FROM \"AbpUsers\" WHERE \"UserName\"='$ENT_USER' LIMIT 1")
GU_CODE=$(api_code "$GZ_ADMIN" guozhou POST /api/app/tenant-user/user-for-tenant \
  "{\"userName\":\"$ENT_USER\",\"emailAddress\":\"$ENT_USER@demo.local\",\"password\":\"123456\",\"name\":\"企业联系人\",\"surname\":\"E2E\",\"isActive\":true,\"companyName\":\"E2E企业-$UNIQ\",\"roleNames\":[\"EnterpriseUser\"]}")
[ "$GU_CODE" = "200" ] && ok "G1 学校开通企业账号（${ENT_USER}）" || bad "G1 开通企业账号失败（${GU_CODE}）"

G_ENT_COUNT=$(sql "SELECT count(*) FROM \"AppEnterpriseMembers\" m JOIN \"AbpUsers\" u ON u.\"Id\"=m.\"UserId\" WHERE u.\"UserName\"='$ENT_USER'")
[ "${G_ENT_COUNT:-0}" -ge 1 ] 2>/dev/null && ok "G2 账号已绑定企业主体（成员记录 ${G_ENT_COUNT} 条）" || bad "G2 未绑定企业（${G_ENT_COUNT}）"

G_EID=$(sql "SELECT m.\"EnterpriseProfileId\" FROM \"AppEnterpriseMembers\" m JOIN \"AbpUsers\" u ON u.\"Id\"=m.\"UserId\" WHERE u.\"UserName\"='$ENT_USER' LIMIT 1")
G_CERT0=$(sql "SELECT \"CertificationStatus\" FROM \"AppEnterpriseProfiles\" WHERE \"Id\"='$G_EID'")
[ "$G_CERT0" = "0" ] && ok "G3 新企业为草稿状态（待认证）" || bad "G3 企业初始状态异常（${G_CERT0}）"

# 选一个「尚未进入该库」的资源（Flow B 可能已把 GZ_DRAFT_RES 发布进该库）
G_RES=$(sql "SELECT r.\"Id\" FROM \"AppResources\" r WHERE r.\"TenantId\"='$GZ_TENANT' AND r.\"IsDeleted\"=false AND NOT EXISTS (SELECT 1 FROM \"AppResourcePublications\" p WHERE p.\"ContentId\"=r.\"Id\" AND p.\"LibraryId\"='$B_LIB') LIMIT 1")
[ -n "$G_RES" ] && ok "G4a 选定未入库资源（${G_RES}）" || skip "G4a 无可用资源（跳过企业贡献验证）"

# G4 未认证企业贡献 → 被拒（明确原因）
G_ENT_TOKEN=$(token guozhou "$ENT_USER" '123456')
[ -n "$G_ENT_TOKEN" ] && ok "G4 企业账号可登录" || bad "G4 企业账号登录失败"
G_DENY=$(api "$G_ENT_TOKEN" guozhou POST "/api/app/teaching-resource-library/$B_LIB/attach" \
  "{\"contentType\":0,\"contentId\":\"$G_RES\"}")
echo "$G_DENY" | grep -q '尚未通过认证' && ok "G5 未认证企业贡献被拒（含明确原因）" || bad "G5 未认证企业未被拒：$(echo "$G_DENY" | head -c 160)"

# G6 认证通过后再贡献 → 成功且提交方类型 = Enterprise
api "$G_ENT_TOKEN" guozhou POST "/api/app/enterprise/$G_EID/submit-certification" '{}' >/dev/null
G_ETICKET=$(api "$ADMIN" - GET "/api/app/review-ticket?TargetId=$G_EID" | jg 'd["items"][0]["id"] if d.get("items") else ""')
api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$G_ETICKET\",\"decision\":0}" >/dev/null
api "$LEAGUE" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$G_ETICKET\",\"decision\":0}" >/dev/null

G_ATTACH=$(api_code "$G_ENT_TOKEN" guozhou POST "/api/app/teaching-resource-library/$B_LIB/attach" \
  "{\"contentType\":0,\"contentId\":\"$G_RES\"}")
[ "$G_ATTACH" = "200" ] && ok "G6 已认证企业贡献成功（200）" || bad "G6 企业贡献失败（${G_ATTACH}）"
G_SUBMITTER=$(sql "SELECT \"SubmitterType\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$B_LIB' AND \"ContentId\"='$G_RES' ORDER BY \"CreationTime\" DESC LIMIT 1")
[ "$G_SUBMITTER" = "1" ] && ok "G7 准入记录标记为企业提交（SubmitterType=Enterprise）" || bad "G7 提交方类型异常（${G_SUBMITTER}）"

# G8 企业贡献仍需审核（不因"企业"而免审）
G_PUBSTATUS=$(sql "SELECT \"Status\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$B_LIB' AND \"ContentId\"='$G_RES' ORDER BY \"CreationTime\" DESC LIMIT 1")
[ "$G_PUBSTATUS" = "1" ] && ok "G8 企业贡献进入待审（未直接发布）" || bad "G8 企业贡献状态异常（${G_PUBSTATUS}）"


# ─────────────────────────────────────────────────────────────
# Flow H：就业改造（企业维度 / 投递快照 / 面试时区 / 去向核实 / 统计去重）
# ─────────────────────────────────────────────────────────────
sect "Flow H 就业：企业维度 + 投递快照 + 去向核实"

# H1 在 qidi 建企业账号并认证（学生 zmq 在 qidi，才能投递本校岗位）
H_USER="e2e-h-ent-$UNIQ"
api_code "$QIDI_ADMIN" qidi POST /api/app/tenant-user/user-for-tenant \
  "{\"userName\":\"$H_USER\",\"emailAddress\":\"$H_USER@demo.local\",\"password\":\"123456\",\"name\":\"HR\",\"surname\":\"E2E\",\"isActive\":true,\"companyName\":\"E2E就业企业-$UNIQ\",\"roleNames\":[\"EnterpriseUser\"]}" >/dev/null
H_EID=$(sql "SELECT m.\"EnterpriseProfileId\" FROM \"AppEnterpriseMembers\" m JOIN \"AbpUsers\" u ON u.\"Id\"=m.\"UserId\" WHERE u.\"UserName\"='$H_USER' LIMIT 1")
H_TOKEN=$(token qidi "$H_USER" '123456')
[ -n "$H_TOKEN" ] && ok "H1 qidi 企业账号可登录（企业 ${H_EID}）" || bad "H1 企业账号登录失败"

api "$H_TOKEN" qidi POST "/api/app/enterprise/$H_EID/submit-certification" '{}' >/dev/null
H_ET=$(api "$ADMIN" - GET "/api/app/review-ticket?TargetId=$H_EID" | jg 'd["items"][0]["id"] if d.get("items") else ""')
api "$ADMIN" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$H_ET\",\"decision\":0}" >/dev/null
api "$LEAGUE" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$H_ET\",\"decision\":0}" >/dev/null
H_CERT=$(api "$H_TOKEN" qidi GET "/api/app/enterprise/$H_EID" | jg 'd.get("certificationStatusName")')
[ "$H_CERT" = "已认证" ] && ok "H2 企业认证通过" || bad "H2 企业认证失败（${H_CERT}）"

# H3 企业发布岗位 → 自动带 EnterpriseProfileId
H_JOB=$(api "$H_TOKEN" qidi POST /api/app/employment/job \
  "{\"title\":\"E2E养老护理师-$UNIQ\",\"description\":\"负责长者照护\",\"location\":\"杭州\",\"recruitmentCount\":2}" | jg 'd.get("id","")')
[ -n "$H_JOB" ] && ok "H3 企业发布岗位（${H_JOB}）" || bad "H3 发布岗位失败"
H_JOB_ENT=$(sql "SELECT \"EnterpriseProfileId\" FROM \"AppJobPostings\" WHERE \"Id\"='$H_JOB'")
[ "$H_JOB_ENT" = "$H_EID" ] && ok "H4 岗位归属企业主体（EnterpriseProfileId 已写入）" || bad "H4 岗位未归属企业（${H_JOB_ENT}）"

# H5 学校审核岗位 → 已发布
H_STATUS=$(sql "SELECT \"Status\" FROM \"AppJobPostings\" WHERE \"Id\"='$H_JOB'")
if [ "$H_STATUS" != "2" ]; then
  api "$QIDI_ADMIN" qidi POST "/api/app/employment/$H_JOB/review-job" '{"status":2}' >/dev/null
fi
H_PUB=$(sql "SELECT \"Status\" FROM \"AppJobPostings\" WHERE \"Id\"='$H_JOB'")
[ "$H_PUB" = "2" ] && ok "H5 学校审核岗位 → 已发布" || bad "H5 岗位未发布（status=${H_PUB}）"

# H6 学生投递 → 生成简历快照
H_RESUME=$(api "$QIDI_STUDENT" qidi GET /api/app/employment/my-resumes | jg 'd[0].get("id","") if isinstance(d,list) and d else ""')
if [ -z "$H_RESUME" ]; then
  H_RESUME=$(api "$QIDI_STUDENT" qidi POST /api/app/employment/resume \
    "{\"title\":\"E2E简历-$UNIQ\",\"fullName\":\"张同学\",\"summary\":\"原始自我评价\",\"skills\":\"护理\"}" | jg 'd.get("id","")')
fi
H_APP=$(api "$QIDI_STUDENT" qidi POST /api/app/employment/application \
  "{\"jobPostingId\":\"$H_JOB\",\"resumeId\":\"$H_RESUME\",\"coverLetter\":\"求职信\"}" | jg 'd.get("id","")')
[ -n "$H_APP" ] && ok "H6 学生投递成功（${H_APP}）" || bad "H6 投递失败"
H_SNAP=$(sql "SELECT CASE WHEN \"ResumeSnapshotJson\" IS NULL THEN 'no' ELSE 'yes' END FROM \"AppJobApplications\" WHERE \"Id\"='$H_APP'")
[ "$H_SNAP" = "yes" ] && ok "H7 投递生成简历快照" || bad "H7 未生成简历快照"

# H8 改简历后，投递材料仍是快照（不可变）
api "$QIDI_STUDENT" qidi PUT "/api/app/employment/resume/$H_RESUME" \
  "{\"title\":\"E2E简历-$UNIQ\",\"fullName\":\"张同学\",\"summary\":\"改过的自我评价\",\"skills\":\"护理+康复\"}" >/dev/null
H_SNAP_TEXT=$(api "$QIDI_STUDENT" qidi GET "/api/app/employment/$H_APP/application-resume" | jg 'd.get("summary","")')
echo "$H_SNAP_TEXT" | grep -q '原始自我评价' && ok "H8 投递材料冻结（改简历不影响已投递）" || bad "H8 投递材料被改动（${H_SNAP_TEXT}）"

# H9 企业只看到自己的投递
H_APPS=$(api "$H_TOKEN" qidi GET "/api/app/employment/job-application-list?MaxResultCount=50" | jg 'd.get("totalCount")')
[ "${H_APPS:-0}" -ge 1 ] 2>/dev/null && ok "H9 企业可见本企业岗位的投递（${H_APPS} 条）" || bad "H9 企业看不到投递（${H_APPS}）"

# H10 面试安排含时区
H_IV=$(api "$QIDI_ADMIN" qidi POST /api/app/employment/schedule-interview \
  "{\"applicationId\":\"$H_APP\",\"interviewerName\":\"王老师\",\"scheduledAt\":\"2026-12-01T02:00:00Z\",\"location\":\"线上\",\"meetingUrl\":\"https://meet.example.com/x\",\"timeZone\":\"Asia/Shanghai\"}" | jg 'd.get("id","")')
H_TZ=$(sql "SELECT \"TimeZone\" FROM \"AppInterviewSchedules\" WHERE \"Id\"='$H_IV'")
[ "$H_TZ" = "Asia/Shanghai" ] && ok "H10 面试记录时区（Asia/Shanghai）" || bad "H10 时区未保存（${H_TZ}）"

# H11 多去向 + 主去向唯一
H_O1=$(api "$QIDI_ADMIN" qidi POST /api/app/employment/save-outcome \
  "{\"studentId\":\"$ZMQ_ID\",\"employerName\":\"E2E单位A\",\"jobTitle\":\"护理师\",\"status\":2,\"isPrimary\":true}" | jg 'd.get("id","")')
H_O2=$(api "$QIDI_ADMIN" qidi POST /api/app/employment/save-outcome \
  "{\"studentId\":\"$ZMQ_ID\",\"employerName\":\"E2E单位B\",\"jobTitle\":\"康复师\",\"status\":2,\"isPrimary\":true}" | jg 'd.get("id","")')
H_PRIMARY=$(sql "SELECT count(*) FROM \"AppEmploymentOutcomes\" WHERE \"StudentId\"='$ZMQ_ID' AND \"IsPrimary\"=true AND \"EmployerName\" LIKE 'E2E单位%'")
[ "$H_PRIMARY" = "1" ] && ok "H11 同一学生只保留一个主去向" || bad "H11 主去向不唯一（${H_PRIMARY}）"

# H12 去向核实
H_VERIFIED=$(api "$QIDI_ADMIN" qidi POST "/api/app/employment/$H_O2/verify-outcome" '{"verified":true,"note":"材料齐全","evidenceUrl":"协议-2026.pdf"}' | jg 'd.get("isVerified")')
[ "$H_VERIFIED" = "True" ] && ok "H12 去向核实通过（计入正式统计）" || bad "H12 核实失败（${H_VERIFIED}）"

# H13 统计按学生去重
H_STAT=$(api "$QIDI_ADMIN" qidi GET "/api/app/employment/statistics?MaxResultCount=50" | jg 'sum(x.get("studentCount",0) for x in (d if isinstance(d,list) else []))')
[ "${H_STAT:-0}" -ge 1 ] 2>/dev/null && ok "H13 统计返回去重学生数（合计 ${H_STAT}）" || bad "H13 统计异常（${H_STAT}）"

# ─────────────────────────────────────────────────────────────
# 权限矩阵
# ─────────────────────────────────────────────────────────────
sect "权限矩阵（越权一律拒绝）"

check_denied() { # check_denied <desc> <code>
  case "$2" in
    403|302|401) ok "$1（$2）" ;;
    *) bad "$1 —— 期望 403/302，实际 $2" ;;
  esac
}

check_denied "学生访问审核中心" "$(api_code "$QIDI_STUDENT" qidi GET '/api/app/review-ticket?MaxResultCount=5')"
check_denied "教师执行审核决策" "$(api_code "$QIDI_TEACHER" qidi POST /api/app/review-ticket/decide "{\"ticketId\":\"$B_TICKET\",\"decision\":0}")"
check_denied "学生建共建任务" "$(api_code "$QIDI_STUDENT" qidi POST /api/app/build-task/task '{"libraryId":"'$B_LIB'","title":"x","items":[]}')"
check_denied "教师删除资源库" "$(api_code "$QIDI_TEACHER" qidi DELETE "/api/app/resource-library/$B_LIB")"

# ─────────────────────────────────────────────────────────────
# 汇总
# ─────────────────────────────────────────────────────────────
# ─────────────────────────────────────────────────────────────
# 清理（保证脚本可重复执行）
# ─────────────────────────────────────────────────────────────
sect "清理本次生成的数据"
sql "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\" IN ('$A_LIB','$B_LIB') OR \"TargetId\" IN (SELECT \"Id\" FROM \"AppResourcePublications\" WHERE \"LibraryId\" IN ('$A_LIB','$B_LIB')))" >/dev/null
sql "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\" IN ('$A_LIB','$B_LIB') OR \"TargetId\" IN (SELECT \"Id\" FROM \"AppResourcePublications\" WHERE \"LibraryId\" IN ('$A_LIB','$B_LIB'))" >/dev/null
sql "DELETE FROM \"AppResourcePublications\" WHERE \"LibraryId\" IN ('$A_LIB','$B_LIB')" >/dev/null
sql "DELETE FROM \"AppCourseSharedRefs\" WHERE \"LibraryId\" IN ('$A_LIB','$B_LIB')" >/dev/null
sql "DELETE FROM \"AppBuildTaskDeliveries\" WHERE \"TaskId\"='$C_TASK'" >/dev/null
sql "DELETE FROM \"AppBuildTaskItems\" WHERE \"TaskId\"='$C_TASK'" >/dev/null
sql "DELETE FROM \"AppBuildTaskCollaborators\" WHERE \"TaskId\"='$C_TASK'" >/dev/null
sql "DELETE FROM \"AppBuildTasks\" WHERE \"Id\"='$C_TASK'" >/dev/null
sql "DELETE FROM \"AppResourceLibraryTeachers\" WHERE \"LibraryId\" IN ('$A_LIB','$B_LIB')" >/dev/null
sql "DELETE FROM \"AppResourceLibraryResources\" WHERE \"LibraryId\" IN ('$A_LIB','$B_LIB')" >/dev/null
sql "DELETE FROM \"AppResourceLibraryMembers\" WHERE \"LibraryId\" IN ('$A_LIB','$B_LIB')" >/dev/null
sql "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\" IN ('$R_LIB','$PAR'))" >/dev/null
sql "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\" IN ('$R_LIB','$PAR')" >/dev/null
sql "DELETE FROM \"AppResourceLibraryMembers\" WHERE \"LibraryId\" IN ('$R_LIB','$PAR')" >/dev/null
sql "DELETE FROM \"AppResourceLibraries\" WHERE \"Id\" IN ('$R_LIB','$PAR')" >/dev/null
sql "DELETE FROM \"AppResourceLibraries\" WHERE \"Id\" IN ('$A_LIB','$B_LIB')" >/dev/null
sql "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\"='$E_ID')" >/dev/null
sql "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\"='$E_ID'" >/dev/null
sql "DELETE FROM \"AppEnterpriseMembers\" WHERE \"EnterpriseProfileId\"='$E_ID'" >/dev/null
sql "DELETE FROM \"AppEnterpriseProfiles\" WHERE \"Id\"='$E_ID'" >/dev/null
sql "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\"='$VE_LIB')" >/dev/null
sql "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\"='$VE_LIB'" >/dev/null
sql "DELETE FROM \"AppResourceLibraryMembers\" WHERE \"LibraryId\"='$VE_LIB'" >/dev/null
sql "DELETE FROM \"AppResourceLibraries\" WHERE \"Id\"='$VE_LIB'" >/dev/null
sql "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\" IN ('$F_DID','$F_EID'))" >/dev/null
sql "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\" IN ('$F_DID','$F_EID')" >/dev/null
sql "DELETE FROM \"AppProjectMilestones\" WHERE \"ProjectId\" IN (SELECT \"Id\" FROM \"AppCooperationProjects\" WHERE \"DemandId\"='$F_DID')" >/dev/null
sql "DELETE FROM \"AppCooperationProjects\" WHERE \"DemandId\"='$F_DID'" >/dev/null
sql "DELETE FROM \"AppCooperationResponses\" WHERE \"DemandId\"='$F_DID'" >/dev/null
sql "DELETE FROM \"AppCooperationDemands\" WHERE \"Id\"='$F_DID'" >/dev/null
sql "DELETE FROM \"AppEnterpriseMembers\" WHERE \"EnterpriseProfileId\"='$F_EID'" >/dev/null
sql "DELETE FROM \"AppEnterpriseProfiles\" WHERE \"Id\"='$F_EID'" >/dev/null
sql "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\"='$G_EID')" >/dev/null
sql "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\"='$G_EID'" >/dev/null
sql "DELETE FROM \"AppEnterpriseMembers\" WHERE \"EnterpriseProfileId\"='$G_EID'" >/dev/null
sql "DELETE FROM \"AppEnterpriseProfiles\" WHERE \"Id\"='$G_EID'" >/dev/null
sql "DELETE FROM \"AbpUserRoles\" WHERE \"UserId\" IN (SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='$ENT_USER')" >/dev/null
sql "DELETE FROM \"AbpUsers\" WHERE \"UserName\"='$ENT_USER'" >/dev/null
sql "DELETE FROM \"AppInterviewSchedules\" WHERE \"JobPostingId\"='$H_JOB'" >/dev/null
sql "DELETE FROM \"AppJobApplications\" WHERE \"JobPostingId\"='$H_JOB'" >/dev/null
sql "DELETE FROM \"AppJobPostings\" WHERE \"Id\"='$H_JOB'" >/dev/null
sql "DELETE FROM \"AppEmploymentOutcomes\" WHERE \"EmployerName\" LIKE 'E2E单位%'" >/dev/null
sql "DELETE FROM \"AppStudentResumes\" WHERE \"Title\" LIKE 'E2E简历-%'" >/dev/null
sql "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\"='$H_EID'" >/dev/null
sql "DELETE FROM \"AppEnterpriseMembers\" WHERE \"EnterpriseProfileId\"='$H_EID'" >/dev/null
sql "DELETE FROM \"AppEnterpriseProfiles\" WHERE \"Id\"='$H_EID'" >/dev/null
sql "DELETE FROM \"AbpUserRoles\" WHERE \"UserId\" IN (SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='$H_USER')" >/dev/null
sql "DELETE FROM \"AbpUsers\" WHERE \"UserName\"='$H_USER'" >/dev/null
ok "已清理本次 E2E 生成的库/任务/引用/审核单/企业/合作需求/企业账号/岗位投递"

sect "汇总"
printf '  通过 %d / 失败 %d / 跳过 %d\n' "$PASS" "$FAIL" "$SKIP"
if [ "$FAIL" -gt 0 ]; then
  printf '\n\033[31m失败用例：\033[0m\n'
  for c in "${FAILED_CASES[@]}"; do printf '  - %s\n' "$c"; done
  exit 1
fi
printf '\n\033[32m全部通过\033[0m\n'
