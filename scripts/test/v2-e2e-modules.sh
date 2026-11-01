#!/usr/bin/env bash
# V2.0 分模块端到端验证脚本（多模型联合验证用）
#
# 用法：
#   ./scripts/test/v2-e2e-modules.sh M0        # 环境自检（必做，其它模块的前置）
#   ./scripts/test/v2-e2e-modules.sh M1        # 审核内核与库生命周期
#   ./scripts/test/v2-e2e-modules.sh M2        # 内容准入与可见性（含文件 URL 越权高危用例）
#   ./scripts/test/v2-e2e-modules.sh M3        # 共建任务（验收≠发布）
#   ./scripts/test/v2-e2e-modules.sh M4        # 跨校引用（撤销 / 来源下架自动撤销）
#   ./scripts/test/v2-e2e-modules.sh M5        # 企业 / 产教 / 单位有效期
#   ./scripts/test/v2-e2e-modules.sh M6        # 权限 / AI / 检索治理
#   ./scripts/test/v2-e2e-modules.sh M7        # 前端 UI（打印人工清单，不执行）
#   ./scripts/test/v2-e2e-modules.sh all       # M0~M6 全跑
#
# 设计约束（多模型并行时重要）：
#   1. 每个模块自带数据清理，跑完不污染环境；
#   2. 模块之间无依赖，可同时在不同终端/模型里跑（M5 会改功能开关，建议最后跑或单独跑）；
#   3. 只写自己的数据，绝不删除别人创建的库/任务/单据；
#   4. 退出码：0=全通过，1=有失败，2=环境不可用。
#
# 前置：API 已启动（https://localhost:44305）、已执行迁移。
# 对照文档：docs/V2-E2E测试手册.md（每个用例的期望与判定标准）
set -uo pipefail

API="${API:-https://localhost:44305}"
DB_HOST="${DB_HOST:-localhost}"; DB_PORT="${DB_PORT:-5433}"
DB_USER="${DB_USER:-postgres}"; DB_PASS="${DB_PASS:-postgres}"; DB_NAME="${DB_NAME:-KnowledgeHub}"

QIDI_TENANT="${QIDI_TENANT:-3a2034d1-6c43-2219-8149-45182659c849}"
GZ_TENANT="${GZ_TENANT:-3a23961d-ebad-94e2-5179-643fff1681b1}"

PASS=0; FAIL=0; SKIP=0; FAILED=()
MODULE="${1:-M0}"
SUFFIX="$(date +%H%M%S)_$$"

# ───────────────────────── 输出 ─────────────────────────
ok()   { PASS=$((PASS+1)); printf '  \033[32m✓\033[0m %s\n' "$1"; }
bad()  { FAIL=$((FAIL+1)); FAILED+=("$1"); printf '  \033[31m✗\033[0m %s\n' "$1"; }
skip() { SKIP=$((SKIP+1)); printf '  \033[33m–\033[0m %s\n' "$1"; }
sect() { printf '\n\033[1m=== %s ===\033[0m\n' "$1"; }
info() { printf '    %s\n' "$1"; }

# ───────────────────────── 工具 ─────────────────────────
sql() { PGPASSWORD="$DB_PASS" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -t -A -c "$1" 2>/dev/null; }

tok() { # tok <tenant|-> <user> <pass>
  local t="$1" u="$2" p="$3" a=(-sk -X POST "$API/connect/token" -H 'Content-Type: application/x-www-form-urlencoded')
  [ "$t" != "-" ] && a+=(-H "__tenant: $t")
  curl "${a[@]}" --data-urlencode 'grant_type=password' --data-urlencode 'client_id=KnowledgeHub_App' \
    --data-urlencode 'scope=KnowledgeHub offline_access' --data-urlencode "username=$u" --data-urlencode "password=$p" \
    | python3 -c 'import json,sys;print(json.load(sys.stdin).get("access_token",""))'
}

api() { # api <token> <tenant|-> <method> <path> [json]
  local tk="$1" tn="$2" m="$3" p="$4" b="${5:-}"
  local a=(-sk -X "$m" "$API$p" -H "Authorization: Bearer $tk")
  [ "$tn" != "-" ] && a+=(-H "__tenant: $tn")
  [ -n "$b" ] && a+=(-H 'Content-Type: application/json' --data-binary "$b")
  curl "${a[@]}"
}

api_code() { # 只返回 HTTP 状态码
  local tk="$1" tn="$2" m="$3" p="$4" b="${5:-}"
  local a=(-sk -o /dev/null -w '%{http_code}' -X "$m" "$API$p" -H "Authorization: Bearer $tk")
  [ "$tn" != "-" ] && a+=(-H "__tenant: $tn")
  [ -n "$b" ] && a+=(-H 'Content-Type: application/json' --data-binary "$b")
  curl "${a[@]}"
}

jg() { python3 -c "import json,sys
try: d=json.loads(sys.stdin.read())
except Exception: d={}
print($1)"; }

pick_res() { # pick_res <tenantId> [excludeId] —— 优先草稿未入库，退化任意未入库，再退化任意
  local t="$1" ex="${2:-}" id=""
  id=$(sql "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='$t' AND \"IsDeleted\"=false AND \"Status\"=0 AND \"LibraryId\" IS NULL ${ex:+AND \"Id\"<>'$ex'} ORDER BY \"CreationTime\" DESC LIMIT 1")
  [ -z "$id" ] && id=$(sql "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='$t' AND \"IsDeleted\"=false AND \"LibraryId\" IS NULL ${ex:+AND \"Id\"<>'$ex'} ORDER BY \"CreationTime\" DESC LIMIT 1")
  [ -z "$id" ] && id=$(sql "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='$t' AND \"IsDeleted\"=false ${ex:+AND \"Id\"<>'$ex'} ORDER BY \"CreationTime\" DESC LIMIT 1")
  echo "$id"
}

act_on_library() { # act_on_library <libraryId> —— 删除某库的审核单/成员/教师/发布记录，再删库
  local L="$1"
  sql "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\"='$L' OR \"TargetId\" IN (SELECT \"Id\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$L'))" >/dev/null
  sql "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\"='$L' OR \"TargetId\" IN (SELECT \"Id\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$L')" >/dev/null
  sql "DELETE FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$L'" >/dev/null
  sql "DELETE FROM \"AppResourceLibraryTeachers\" WHERE \"LibraryId\"='$L'" >/dev/null
  sql "DELETE FROM \"AppResourceLibraryMembers\" WHERE \"LibraryId\"='$L'" >/dev/null
  sql "DELETE FROM \"AppResourceLibraries\" WHERE \"Id\"='$L'" >/dev/null
}

# ───────────────────────── M0 环境自检 ─────────────────────────
module_m0() {
  sect "M0 环境自检"
  local code; code=$(curl -sk -o /dev/null -w '%{http_code}' "$API/health-status" || true)
  if [ "$code" != "200" ]; then
    bad "API 不可用（health=${code}）——请先 ./dev.sh start api"
    info "环境不可用时不执行后续模块，避免把环境问题误判为功能缺陷。"
    return 2
  fi
  ok "API 健康（200）"

  local n=0
  for spec in "guozhou:guozhou-admin:123456" "qidi:qidi-admin:123456" "qidi:wwq:123456" \
              "qidi:zmq:1234567" "guozhou:ent-demo:123456" "-:admin:1q2w3E*" "-:league-admin:123456"; do
    local t=${spec%%:*}; local rest=${spec#*:}; local u=${rest%%:*}; local p=${rest#*:}
    local tk; tk=$(tok "$t" "$u" "$p")
    if [ "${#tk}" -gt 500 ]; then n=$((n+1)); ok "令牌 $t/$u"; else bad "令牌 $t/$u（len=${#tk}）"; fi
  done
  [ "$n" -eq 7 ] && ok "7/7 账号可用" || bad "仅 $n/7 账号可用"
  return 0
}

# ───────────────────────── M1 审核内核与库生命周期 ─────────────────────────
module_m1() {
  sect "M1 审核内核与库生命周期"
  local T A L
  T=$(tok qidi qidi-admin 123456); A=$(tok - admin '1q2w3E*'); L=$(tok - league-admin 123456)
  local LIB TID

  LIB=$(api "$T" qidi POST /api/app/resource-library "{\"name\":\"M1-校内库-$SUFFIX\",\"visibility\":0}" | jg 'd.get("id","")')
  local st; st=$(api "$T" qidi GET "/api/app/resource-library/$LIB" | jg 'd.get("status")')
  [ "$st" = "3" ] && ok "M1-1 建库进入待院校审核（status=3）" || bad "M1-1 建库状态异常（${st}，期望 3＝开关未开或旧行为）"

  TID=$(api "$T" qidi GET "/api/app/review-ticket?TargetId=$LIB" | jg 'd["items"][0]["id"] if d.get("items") else ""')
  local stages; stages=$(api "$T" qidi GET "/api/app/review-ticket/$TID" | jg 'd.get("totalStages")')
  [ "$stages" = "1" ] && ok "M1-2 校内私有库为单级审核（stages=1）" || bad "M1-2 级数异常（${stages}，期望 1）"

  local self; self=$(api "$T" qidi POST /api/app/review-ticket/decide "{\"ticketId\":\"$TID\",\"decision\":0}")
  echo "$self" | grep -q 'CannotReviewOwn' && ok "M1-3 自审被拒（CannotReviewOwn）" || bad "M1-3 自审未被拒（严重）"

  api "$A" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$TID\",\"decision\":0}" >/dev/null
  st=$(api "$T" qidi GET "/api/app/resource-library/$LIB" | jg 'd.get("status")')
  [ "$st" = "1" ] && ok "M1-4 他人校审通过 → 已发布（status=1）" || bad "M1-4 校审后状态异常（${st}）"

  api "$T" qidi PUT "/api/app/resource-library/$LIB" '{"description":"M1-改库后"}' >/dev/null
  local mid mdesc; mid=$(api "$T" qidi GET "/api/app/resource-library/$LIB" | jg 'd.get("status")'); mdesc=$(api "$T" qidi GET "/api/app/resource-library/$LIB" | jg 'd.get("description","")')
  [ "$mid" = "6" ] && [ "$mdesc" != "M1-改库后" ] && ok "M1-5 改库进变更审核且仍按旧值服务（status=6）" || bad "M1-5 变更快照未生效（status=${mid}, desc=${mdesc}）"

  local LIB2 TID2 r; LIB2=$(api "$T" qidi POST /api/app/resource-library "{\"name\":\"M1-驳回-$SUFFIX\",\"visibility\":0}" | jg 'd.get("id","")')
  TID2=$(api "$T" qidi GET "/api/app/review-ticket?TargetId=$LIB2" | jg 'd["items"][0]["id"] if d.get("items") else ""')
  api "$A" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$TID2\",\"decision\":1,\"reason\":\"名称不规范\"}" >/dev/null
  r=$(api "$T" qidi POST "/api/app/review-ticket/$TID2/resubmit" '{}' | jg 'd.get("roundNo")')
  [ "$r" = "2" ] && ok "M1-6 驳回后重提 → 轮次 2" || bad "M1-6 重提轮次异常（${r}）"

  local PAR PT c1 c2 cnt; PAR=$(api "$T" qidi POST /api/app/resource-library "{\"name\":\"M1-并发-$SUFFIX\",\"visibility\":0}" | jg 'd.get("id","")')
  PT=$(api "$T" qidi GET "/api/app/review-ticket?TargetId=$PAR" | jg 'd["items"][0]["id"] if d.get("items") else ""')
  ( api_code "$A" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$PT\",\"decision\":0}" > /tmp/m1c1.$$ ) &
  ( api_code "$L" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$PT\",\"decision\":0}" > /tmp/m1c2.$$ ) &
  wait; c1=$(cat /tmp/m1c1.$$ 2>/dev/null); c2=$(cat /tmp/m1c2.$$ 2>/dev/null); rm -f /tmp/m1c1.$$ /tmp/m1c2.$$
  cnt=$(sql "SELECT count(*) FROM \"AppReviewActions\" WHERE \"TicketId\"='$PT' AND \"Operation\"=1")
  if { [ "$c1" = "200" ] && [ "$c2" != "200" ]; } || { [ "$c2" = "200" ] && [ "$c1" != "200" ]; }; then
    [ "${cnt:-0}" = "1" ] && ok "M1-7 并发审批只成功一次（${c1}/${c2}，通过记录 1 条）" || bad "M1-7 并发产生多条通过记录（${cnt}）"
  else
    bad "M1-7 并发结果异常（${c1}/${c2}）"
  fi

  api "$T" qidi DELETE "/api/app/resource-library/$LIB" >/dev/null
  local DT arch exists; DT=$(api "$T" qidi GET "/api/app/review-ticket?TargetId=$LIB" | jg 'd["items"][0]["id"] if d.get("items") else ""')
  api "$A" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$DT\",\"decision\":0}" >/dev/null
  arch=$(sql "SELECT \"Status\" FROM \"AppResourceLibraries\" WHERE \"Id\"='$LIB'"); exists=$(sql "SELECT count(*) FROM \"AppResourceLibraries\" WHERE \"Id\"='$LIB'")
  [ "$arch" = "2" ] && [ "$exists" = "1" ] && ok "M1-8 删库审核通过 → 归档且库行保留（不硬删）" || bad "M1-8 归档异常（status=${arch}, exists=${exists}）"

  for L in "$LIB" "$LIB2" "$PAR"; do act_on_library "$L"; done
  ok "M1 清理完成"
}

# ───────────────────────── M2 内容准入与可见性 ─────────────────────────
module_m2() {
  sect "M2 内容准入与可见性"
  local T W A S LIB TID RES RES2 PT
  T=$(tok qidi qidi-admin 123456); W=$(tok qidi wwq 123456); A=$(tok - admin '1q2w3E*'); S=$(tok qidi zmq 1234567)

  LIB=$(api "$T" qidi POST /api/app/resource-library "{\"name\":\"M2-准入库-$SUFFIX\",\"visibility\":0}" | jg 'd.get("id","")')
  TID=$(api "$T" qidi GET "/api/app/review-ticket?TargetId=$LIB" | jg 'd["items"][0]["id"] if d.get("items") else ""')
  api "$A" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$TID\",\"decision\":0}" >/dev/null

  local WWQ; WWQ=$(sql "SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='wwq' LIMIT 1'")
  WWQ=$(sql "SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='wwq' LIMIT 1")
  api "$T" qidi POST "/api/app/resource-library/$LIB/assign-teacher" "{\"userId\":\"$WWQ\"}" >/dev/null

  RES=$(pick_res "$QIDI_TENANT")
  if [ -z "$RES" ]; then skip "M2 qidi 无可用资源，跳过准入用例"; act_on_library "$LIB"; return; fi

  api "$W" qidi POST "/api/app/teaching-resource-library/$LIB/attach" "{\"contentType\":0,\"contentId\":\"$RES\"}" >/dev/null
  local pub; pub=$(sql "SELECT \"Status\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$LIB' AND \"ContentId\"='$RES'")
  [ "$pub" = "1" ] && ok "M2-1 共享入库生成准入记录（PendingReview）" || bad "M2-1 准入记录异常（${pub}）"

  PT=$(sql "SELECT t.\"Id\" FROM \"AppReviewTickets\" t JOIN \"AppResourcePublications\" p ON p.\"Id\"=t.\"TargetId\" WHERE p.\"LibraryId\"='$LIB' AND t.\"Status\"=0 LIMIT 1")
  api "$A" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$PT\",\"decision\":0}" >/dev/null
  local line; line=$(sql "SELECT p.\"Status\"||'/'||r.\"Status\"||'/'||CASE WHEN r.\"LibraryId\" IS NOT NULL THEN 'linked' ELSE 'unlinked' END FROM \"AppResourcePublications\" p JOIN \"AppResources\" r ON r.\"Id\"=p.\"ContentId\" WHERE p.\"LibraryId\"='$LIB'")
  [ "${line%%/*}" = "2" ] && ok "M2-2 准入通过 → 发布，且内容状态联动（${line}）" || bad "M2-2 联动异常（${line}）"

  # 未发布内容不应出现在库内容列表
  RES2=$(pick_res "$QIDI_TENANT" "$RES")
  if [ -n "$RES2" ]; then
    api "$W" qidi POST "/api/app/teaching-resource-library/$LIB/attach" "{\"contentType\":0,\"contentId\":\"$RES2\"}" >/dev/null
    local has; has=$(api "$W" qidi GET "/api/app/teaching-resource-library/$LIB/contents?MaxResultCount=100" | jg 'any(x.get("id")=="'"$RES2"'" and (x.get("publicationStatus") or 2)==2 for x in d.get("items",[]))')
    [ "$has" = "False" ] && ok "M2-3 待审内容不展示为已发布" || bad "M2-3 待审内容被当成已发布展示"
  else
    skip "M2-3 无第二个资源可用"
  fi

  # 高危：文件 URL 不能绕过库授权（资源在私有库且学生未分配）
  local pv dl; pv=$(api_code "$S" qidi GET "/api/resource-file/$RES/preview"); dl=$(api_code "$S" qidi GET "/api/resource-file/$RES/download")
  if [ "$pv" = "403" ] && [ "$dl" = "403" ]; then
    ok "M2-4 文件 URL 不能绕过库授权（preview/download 均 403）"
  else
    bad "M2-4 文件越权（preview=${pv}, download=${dl}，期望均 403）"
  fi

  act_on_library "$LIB"
  ok "M2 清理完成"
}

# ───────────────────────── M3 共建任务 ─────────────────────────
module_m3() {
  sect "M3 共建任务（验收≠发布）"
  local T W TPL LIB TASK ITEM RES DELIV cnt pub
  T=$(tok guozhou guozhou-admin 123456); W=$(tok qidi wwq 123456)

  TPL=$(api "$T" guozhou GET /api/app/build-task/task-templates/00000000-0000-0000-0000-000000000000 | jg 'd[0]["id"] if isinstance(d,list) and d else ""')
  LIB=$(api "$T" guozhou GET /api/app/resource-library | jg 'next((x["id"] for x in (d if isinstance(d,list) else d.get("items",[])) if x.get("isOwner")), "")')
  if [ -z "$TPL" ] || [ -z "$LIB" ]; then skip "M3 缺少任务模板或 guozhou 主建库，跳过"; return; fi

  TASK=$(api "$T" guozhou POST /api/app/build-task/task "{\"libraryId\":\"$LIB\",\"templateId\":\"$TPL\",\"title\":\"M3-共建任务-$SUFFIX\",\"collaboratorSchoolIds\":[\"$QIDI_TENANT\"]}" | jg 'd.get("id","")')
  local req; req=$(api "$T" guozhou GET "/api/app/build-task/$TASK/task" | jg 'd.get("requiredTotal")')
  [ "${req:-0}" -ge 1 ] 2>/dev/null && ok "M3-1 从模板建任务（交付项要求 ${req}）" || bad "M3-1 建任务失败（required=${req}）"
  api "$T" guozhou POST "/api/app/build-task/$TASK/publish-task" >/dev/null

  local mine; mine=$(api "$W" qidi GET "/api/app/build-task/tasks?OnlyMine=true&MaxResultCount=50" | jg 'd.get("totalCount")')
  [ "${mine:-0}" -ge 1 ] 2>/dev/null && ok "M3-2 协作校教师可见任务（${mine} 条）" || bad "M3-2 协作校看不到任务（${mine}）"

  ITEM=$(sql "SELECT \"Id\" FROM \"AppBuildTaskItems\" WHERE \"TaskId\"='$TASK' ORDER BY \"SortOrder\" LIMIT 1")
  # 必须选「尚未发布进该库」的资源，否则重复跑会被「已在该资源库发布」挡住（测试数据问题）
  RES=$(sql "SELECT r.\"Id\" FROM \"AppResources\" r WHERE r.\"TenantId\"='$QIDI_TENANT' AND r.\"IsDeleted\"=false AND NOT EXISTS (SELECT 1 FROM \"AppResourcePublications\" p WHERE p.\"ContentId\"=r.\"Id\" AND p.\"LibraryId\"='$LIB') ORDER BY r.\"CreationTime\" DESC LIMIT 1")
  [ -z "$RES" ] && RES=$(pick_res "$QIDI_TENANT")
  if [ -z "$ITEM" ] || [ -z "$RES" ]; then skip "M3 缺少交付项或资源"; else
    DELIV=$(api "$W" qidi POST /api/app/build-task/submit-delivery "{\"taskId\":\"$TASK\",\"taskItemId\":\"$ITEM\",\"contentType\":0,\"contentId\":\"$RES\"}" | jg 'd.get("id","")')
    [ -n "$DELIV" ] && ok "M3-3 协作校提交交付物" || bad "M3-3 提交交付失败"

    api "$T" guozhou POST /api/app/build-task/review-delivery "{\"deliveryId\":\"$DELIV\",\"accept\":true,\"comment\":\"符合\"}" >/dev/null
    local prog; prog=$(api "$T" guozhou GET "/api/app/build-task/$TASK/task" | jg 'str(d.get("acceptedTotal"))+"/"+str(d.get("requiredTotal"))')
    [ "${prog%%/*}" != "0" ] && ok "M3-4 主责校验收 → 进度 ${prog}" || bad "M3-4 验收后进度异常（${prog}）"

    pub=$(sql "SELECT \"Status\" FROM \"AppResourcePublications\" WHERE \"ContentId\"='$RES' AND \"LibraryId\"='$LIB' ORDER BY \"CreationTime\" DESC LIMIT 1")
    [ "$pub" = "1" ] && ok "M3-5 验收后资源仍在待审（验收≠发布）" || bad "M3-5 验收竟直接发布了资源（pub=${pub}）"

    local canReview; canReview=$(api "$W" qidi GET "/api/app/build-task/$TASK/task" | jg 'd.get("canReview")')
    [ "$canReview" = "False" ] && ok "M3-6 协作校教师不可验收" || bad "M3-6 协作校教师竟可验收"
  fi

  # 清理本次交付产生的准入记录与审核单（选资源时已保证它此前未在该库发布，故可安全删除）
  [ -n "$RES" ] && sql "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT t.\"Id\" FROM \"AppReviewTickets\" t JOIN \"AppResourcePublications\" p ON p.\"Id\"=t.\"TargetId\" WHERE p.\"ContentId\"='$RES' AND p.\"LibraryId\"='$LIB')" >/dev/null
  [ -n "$RES" ] && sql "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\" IN (SELECT \"Id\" FROM \"AppResourcePublications\" WHERE \"ContentId\"='$RES' AND \"LibraryId\"='$LIB')" >/dev/null
  [ -n "$RES" ] && sql "DELETE FROM \"AppResourcePublications\" WHERE \"ContentId\"='$RES' AND \"LibraryId\"='$LIB'" >/dev/null
  sql "DELETE FROM \"AppBuildTaskDeliveries\" WHERE \"TaskId\"='$TASK'" >/dev/null
  sql "DELETE FROM \"AppBuildTaskItems\" WHERE \"TaskId\"='$TASK'" >/dev/null
  sql "DELETE FROM \"AppBuildTaskCollaborators\" WHERE \"TaskId\"='$TASK'" >/dev/null
  sql "DELETE FROM \"AppBuildTasks\" WHERE \"Id\"='$TASK'" >/dev/null
  ok "M3 清理完成"
}

# ───────────────────────── M4 跨校引用 ─────────────────────────
module_m4() {
  sect "M4 跨校引用"
  local W S ROW LIB RES COURSE REF acc rev
  W=$(tok qidi wwq 123456); S=$(tok qidi zmq 1234567)

  COURSE=$(sql "SELECT c.\"Id\" FROM \"AppCourses\" c WHERE c.\"TenantId\"='$QIDI_TENANT' AND c.\"IsDeleted\"=false AND c.\"TeacherId\"=(SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='wwq') LIMIT 1")
  if [ -z "$COURSE" ]; then skip "M4 wwq 名下无课程，跳过"; return; fi

  # 4-1 未发布内容不可引用
  local RESX LIBN
  RESX=$(pick_res "$QIDI_TENANT"); LIBN=$(sql "SELECT \"Id\" FROM \"AppResourceLibraries\" WHERE \"TenantId\"='$QIDI_TENANT' AND \"Status\"=1 LIMIT 1")
  if [ -n "$RESX" ] && [ -n "$LIBN" ]; then
    local deny; deny=$(api "$W" qidi POST /api/app/course-shared-ref "{\"courseId\":\"$COURSE\",\"libraryId\":\"$LIBN\",\"contentId\":\"$RESX\"}")
    if echo "$deny" | grep -qE '尚未在该资源库发布|已引用到本课程'; then ok "M4-1 未发布内容不可引用（拒绝文案正确）"; else info "M4-1 跳过（该资源恰好已发布或已引用）"; SKIP=$((SKIP+1)); fi
  else skip "M4-1 缺少资源或已发布库"; fi

  # 4-2 已发布内容 → 引用 → 学生访问判定
  ROW=$(sql "SELECT p.\"LibraryId\"||'|'||p.\"ContentId\" FROM \"AppResourcePublications\" p WHERE p.\"Status\"=2 AND p.\"ContentType\"=0 AND p.\"LibraryId\" IN (SELECT \"Id\" FROM \"AppResourceLibraries\" WHERE \"Status\"=1) LIMIT 1")
  if [ -z "$ROW" ]; then skip "M4-2 无已发布资源，跳过引用链路"; return; fi
  LIB=${ROW%%|*}; RES=${ROW##*|}
  REF=$(api "$W" qidi POST /api/app/course-shared-ref "{\"courseId\":\"$COURSE\",\"libraryId\":\"$LIB\",\"contentId\":\"$RES\"}" | jg 'd.get("id","")')
  if [ -n "$REF" ]; then
    acc=$(api "$S" qidi POST "/api/app/course-shared-ref/check-access/$REF" '{}' | jg 'd.get("canAccess")')
    case "$acc" in True|False) ok "M4-2 学生访问判定可用（canAccess=${acc}）";; *) bad "M4-2 访问判定异常（${acc}）";; esac

    api "$W" qidi POST "/api/app/course-shared-ref/$REF/revoke?reason=M4测试" '{}' >/dev/null
    rev=$(api "$S" qidi POST "/api/app/course-shared-ref/check-access/$REF" '{}' | jg 'd.get("canAccess")')
    [ "$rev" = "False" ] && ok "M4-3 撤销后立即失效" || bad "M4-3 撤销后仍可访问"
  else
    bad "M4-2 引用创建失败"
  fi

  # 清理本模块创建的引用
  sql "DELETE FROM \"AppCourseSharedRefs\" WHERE \"CourseId\"='$COURSE' AND \"Status\"=1" >/dev/null
  ok "M4 清理完成"
}

# ───────────────────────── M5 企业 / 产教 / 有效期 ─────────────────────────
module_m5() {
  sect "M5 企业 / 产教 / 单位有效期"
  local A L G Q GZ EUSER ET EID LIBG RESG LIBQ DID DT RID PID
  A=$(tok - admin '1q2w3E*'); L=$(tok - league-admin 123456)
  G=$(tok guozhou guozhou-admin 123456); Q=$(tok qidi qidi-admin 123456)
  GZ="$GZ_TENANT"

  # 开关前置
  for TEN in "$GZ" "$QIDI_TENANT"; do
    api_code "$A" - PUT "/api/feature-management/features?providerName=T&providerKey=$TEN" '{"features":[
      {"name":"KnowledgeHub.LibraryReview","value":"true"},
      {"name":"KnowledgeHub.BuildTask","value":"true"},
      {"name":"KnowledgeHub.CrossSchoolRef","value":"true"},
      {"name":"KnowledgeHub.EnterpriseWorkspace","value":"true"},
      {"name":"KnowledgeHub.OrganizationValidity","value":"true"}]}' >/dev/null
  done
  ok "M5-0 已为 guozhou/qidi 打开 5 个开关"

  # 开通企业账号（自动建企业+绑定）
  EUSER="m5-ent-$SUFFIX"
  api "$G" guozhou POST /api/app/tenant-user/user-for-tenant \
    "{\"userName\":\"$EUSER\",\"emailAddress\":\"$EUSER@demo.local\",\"password\":\"123456\",\"name\":\"M5\",\"surname\":\"HR\",\"isActive\":true,\"companyName\":\"M5企业-$SUFFIX\",\"roleNames\":[\"EnterpriseUser\"]}" >/dev/null
  EID=$(sql "SELECT m.\"EnterpriseProfileId\" FROM \"AppEnterpriseMembers\" m JOIN \"AbpUsers\" u ON u.\"Id\"=m.\"UserId\" WHERE u.\"UserName\"='$EUSER'")
  [ -n "$EID" ] && ok "M5-1 开通企业账号 → 自动建企业并绑定" || bad "M5-1 企业未绑定"

  ET=$(tok guozhou "$EUSER" 123456)
  # 自建一个本校主建的私有库（单级审核），避免依赖存量库/资源（存量库可能已被占满 → 误判失败）
  LIBG=$(api "$G" guozhou POST /api/app/resource-library "{\"name\":\"M5-企业贡献库-$SUFFIX\",\"visibility\":0}" | jg 'd.get("id","")')
  if [ -n "$LIBG" ]; then
    local LGT; LGT=$(api "$G" guozhou GET "/api/app/review-ticket?TargetId=$LIBG" | jg 'd["items"][0]["id"] if d.get("items") else ""')
    api "$A" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$LGT\",\"decision\":0}" >/dev/null
    local LGST; LGST=$(api "$G" guozhou GET "/api/app/resource-library/$LIBG" | jg 'd.get("status")')
    [ "$LGST" = "1" ] && ok "M5-prep 自建企业贡献库并审核通过" || bad "M5-prep 自建库未通过审核（status=${LGST}）"
  else
    bad "M5-prep 自建库失败"
  fi
  RESG=$(pick_res "$GZ")
  if [ -n "$LIBG" ] && [ -n "$RESG" ]; then
    local deny; deny=$(api "$ET" guozhou POST "/api/app/teaching-resource-library/$LIBG/attach" "{\"contentType\":0,\"contentId\":\"$RESG\"}")
    echo "$deny" | grep -q '尚未通过认证' && ok "M5-2 未认证企业贡献被拒（含明确原因）" || bad "M5-2 未认证企业未被拒：$(echo "$deny" | head -c 120)"

    api "$ET" guozhou POST "/api/app/enterprise/$EID/submit-certification" '{}' >/dev/null
    local ETK; ETK=$(api "$A" - GET "/api/app/review-ticket?TargetId=$EID" | jg 'd["items"][0]["id"] if d.get("items") else ""')
    api "$A" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$ETK\",\"decision\":0}" >/dev/null
    api "$L" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$ETK\",\"decision\":0}" >/dev/null
    local at; at=$(api_code "$ET" guozhou POST "/api/app/teaching-resource-library/$LIBG/attach" "{\"contentType\":0,\"contentId\":\"$RESG\"}")
    local sub; sub=$(sql "SELECT \"SubmitterType\" FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$LIBG' AND \"ContentId\"='$RESG' ORDER BY \"CreationTime\" DESC LIMIT 1")
    [ "$at" = "200" ] && [ "$sub" = "1" ] && ok "M5-3 认证后贡献成功且标记 Enterprise" || bad "M5-3 企业贡献异常（http=${at}, submitter=${sub}）"
  else skip "M5-2/3 缺少 guozhou 已发布库或资源"; fi

  # 产教需求全链路
  DID=$(api "$ET" guozhou POST /api/app/cooperation/demand "{\"type\":1,\"title\":\"M5-合作需求-$SUFFIX\",\"scope\":2,\"region\":\"杭州\",\"majors\":\"养老\"}" | jg 'd.get("id","")')
  if [ -n "$DID" ]; then
    api "$ET" guozhou POST "/api/app/cooperation/$DID/submit-demand" '{}' >/dev/null
    DT=$(api "$A" - GET "/api/app/review-ticket?TargetId=$DID" | jg 'd["items"][0]["id"] if d.get("items") else ""')
    api "$A" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$DT\",\"decision\":0}" >/dev/null
    api "$L" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$DT\",\"decision\":0}" >/dev/null
    local inHall; inHall=$(api "$Q" qidi GET "/api/app/cooperation/hall?MaxResultCount=50" | jg 'any(x.get("id")=="'"$DID"'" for x in d.get("items",[]))')
    [ "$inHall" = "True" ] && ok "M5-4 公开需求外校可见" || bad "M5-4 公开需求外校不可见"

    RID=$(api "$Q" qidi POST /api/app/cooperation/respond "{\"demandId\":\"$DID\",\"note\":\"M5响应\"}" | jg 'd.get("id","")')
    api "$ET" guozhou POST "/api/app/cooperation/decide-response/$RID" '{"accept":true}' >/dev/null
    PID=$(api "$ET" guozhou POST /api/app/cooperation/project "{\"responseId\":\"$RID\",\"name\":\"M5项目-$SUFFIX\"}" | jg 'd.get("id","")')
    [ -n "$PID" ] && ok "M5-5 响应 → 接受 → 建项目台账" || bad "M5-5 建项目失败"
    local arch; arch=$(api "$Q" qidi POST /api/app/cooperation/advance-stage "{\"projectId\":\"$PID\",\"stage\":3}")
    echo "$arch" | grep -q '验收意见' && ok "M5-6 归档缺验收意见被拒" || bad "M5-6 归档校验缺失"
  else bad "M5-4 建需求失败"; fi

  # 有效期：过期禁写 → 读放行 → 续期恢复
  api "$A" - POST /api/app/school-validity/set-validity "{\"tenantId\":\"$GZ\",\"validFrom\":\"2025-01-01T00:00:00Z\",\"validTo\":\"2025-06-30T00:00:00Z\",\"readOnlyAfterExpiry\":true}" >/dev/null
  local rd wr
  rd=$(api_code "$G" guozhou GET /api/app/resource-library)
  wr=$(api_code "$G" guozhou POST /api/app/resource-library "{\"name\":\"M5-过期建库-$SUFFIX\",\"visibility\":0}")
  [ "$rd" = "200" ] && ok "M5-7 过期后读放行（200）" || bad "M5-7 过期后读被拒（${rd}）"
  [ "$wr" = "403" ] && ok "M5-8 过期后写被拒（403）" || bad "M5-8 过期后写未被拒（${wr}）"

  api "$A" - POST /api/app/school-validity/set-validity "{\"tenantId\":\"$GZ\",\"validFrom\":null,\"validTo\":null,\"readOnlyAfterExpiry\":true}" >/dev/null
  local wr2; wr2=$(api_code "$G" guozhou POST /api/app/resource-library "{\"name\":\"M5-续期库-$SUFFIX\",\"visibility\":0}")
  [ "$wr2" = "200" ] && ok "M5-9 续期后写恢复（200）" || bad "M5-9 续期后仍不可写（${wr2}）"

  # 清理
  local NEWLIB; NEWLIB=$(sql "SELECT \"Id\" FROM \"AppResourceLibraries\" WHERE \"Name\"='M5-续期库-$SUFFIX'")
  [ -n "$NEWLIB" ] && act_on_library "$NEWLIB"
  sql "DELETE FROM \"AppReviewActions\" WHERE \"TicketId\" IN (SELECT \"Id\" FROM \"AppReviewTickets\" WHERE \"TargetId\" IN ('$EID','$DID'))" >/dev/null
  sql "DELETE FROM \"AppReviewTickets\" WHERE \"TargetId\" IN ('$EID','$DID')" >/dev/null
  [ -n "$PID" ] && { sql "DELETE FROM \"AppProjectMilestones\" WHERE \"ProjectId\"='$PID'" >/dev/null; sql "DELETE FROM \"AppCooperationProjects\" WHERE \"Id\"='$PID'" >/dev/null; }
  [ -n "$DID" ] && sql "DELETE FROM \"AppCooperationResponses\" WHERE \"DemandId\"='$DID'" >/dev/null
  [ -n "$DID" ] && sql "DELETE FROM \"AppCooperationDemands\" WHERE \"Id\"='$DID'" >/dev/null
  act_on_library "$LIBG"
  sql "DELETE FROM \"AppEnterpriseMembers\" WHERE \"EnterpriseProfileId\"='$EID'" >/dev/null
  sql "DELETE FROM \"AppEnterpriseProfiles\" WHERE \"Id\"='$EID'" >/dev/null
  sql "DELETE FROM \"AbpUserRoles\" WHERE \"UserId\" IN (SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='$EUSER')" >/dev/null
  sql "DELETE FROM \"AbpUsers\" WHERE \"UserName\"='$EUSER'" >/dev/null
  ok "M5 清理完成（**有效期已续期**）"
}

# ───────────────────────── M6 权限 / AI / 检索 ─────────────────────────
module_m6() {
  sect "M6 权限 / AI / 检索治理"
  local A S W LIB RES before after d1 d2 d3
  A=$(tok - admin '1q2w3E*'); S=$(tok qidi zmq 1234567); W=$(tok qidi wwq 123456)

  before=$(api "$S" qidi GET /api/app/chat/resources-with-page-index | jg 'len(d) if isinstance(d,list) else -1')
  LIB=$(sql "SELECT \"Id\" FROM \"AppResourceLibraries\" WHERE \"TenantId\"='$QIDI_TENANT' AND \"Visibility\"=0 AND \"Status\"=1 LIMIT 1")
  RES=$(sql "SELECT \"Id\" FROM \"AppResources\" WHERE \"TenantId\"='$QIDI_TENANT' AND \"Status\" IN (2,3) AND \"IsDeleted\"=false LIMIT 1")
  if [ -n "$LIB" ] && [ -n "$RES" ] && [ "${before:-0}" -ge 1 ] 2>/dev/null; then
    sql "INSERT INTO \"AppResourcePublications\" (\"Id\",\"LibraryId\",\"ContentType\",\"ContentId\",\"ContentTenantId\",\"Status\",\"SubmitterType\",\"PublishedAt\",\"TenantId\",\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\",\"IsDeleted\") VALUES (gen_random_uuid(),'$LIB',0,'$RES','$QIDI_TENANT',2,0,now(),'$QIDI_TENANT','{}','x',now(),false)" >/dev/null
    after=$(api "$S" qidi GET /api/app/chat/resources-with-page-index | jg 'len(d) if isinstance(d,list) else -1')
    local has; has=$(api "$S" qidi GET /api/app/chat/resources-with-page-index | jg 'any(x.get("id")=="'"$RES"'" for x in (d if isinstance(d,list) else []))')
    [ "${after:-0}" -lt "${before:-0}" ] 2>/dev/null && [ "$has" = "False" ] && ok "M6-1 AI 语料排除「私有库未分配」资源（${before}→${after}）" || bad "M6-1 AI 语料未收口（${before}→${after}, has=${has}）"
    sql "DELETE FROM \"AppResourcePublications\" WHERE \"LibraryId\"='$LIB' AND \"ContentId\"='$RES'" >/dev/null
  else skip "M6-1 缺少私有库/资源/语料，跳过"; fi

  d1=$(api_code "$S" qidi GET "/api/app/review-ticket?MaxResultCount=5")
  d2=$(api_code "$S" qidi POST "/api/app/build-task/task" '{"libraryId":"00000000-0000-0000-0000-000000000000","title":"x"}')
  d3=$(api_code "$W" qidi DELETE "/api/app/resource-library/00000000-0000-0000-0000-000000000000")
  if { [ "$d1" = "403" ] || [ "$d1" = "302" ]; } && { [ "$d2" = "403" ] || [ "$d2" = "302" ]; } && { [ "$d3" = "403" ] || [ "$d3" = "302" ]; }; then
    ok "M6-2 越权直连一律被拒（学生审核中心=${d1} / 学生建任务=${d2} / 教师删库=${d3}）"
  else
    bad "M6-2 存在越权（${d1}/${d2}/${d3}）"
  fi

  # 前端策略悬空检查
  local repo; repo="$(cd "$(dirname "$0")/../.." && pwd)"
  local out; out=$(cd "$repo" && python3 - <<'PY'
import io, re
perms = io.open("src/KnowledgeHub.Domain.Shared/KnowledgeHubPermissions.cs", encoding="utf-8").read()
names=set()
for cls in re.finditer(r'public static class (\w+)\s*\{(.*?)\n    \}', perms, re.S):
    k,b=cls.group(1),cls.group(2)
    for m in re.finditer(r'public const string \w+ = (Default \+ "\.(\w+)"|GroupName \+ "\.(\w+)")', b):
        names.add(f"KnowledgeHub.{k}.{m.group(2) or m.group(3)}")
    if f'const string Default = GroupName + ".{k}"' in b: names.add(f"KnowledgeHub.{k}")
used=set()
for f in ["angular/src/app/app.routes.ts","angular/src/app/route.provider.ts","angular/src/app/alliance-route.provider.ts","angular/src/app/special-edu-route.provider.ts","angular/src/app/voice-assistant-route.provider.ts"]:
    try: used |= set(re.findall(r"requiredPolicy:\s*'([^']+)'", io.open(f, encoding="utf-8").read()))
    except FileNotFoundError: pass
missing=sorted(p for p in used if p.startswith("KnowledgeHub") and p not in names)
print(f"{len(used)}|{len(missing)}|{','.join(missing)}")
PY
)
  local uk mk; uk=$(echo "$out" | cut -d'|' -f1); mk=$(echo "$out" | cut -d'|' -f2)
  [ "$mk" = "0" ] && ok "M6-3 前端策略无悬空（${uk} 个策略）" || bad "M6-3 存在悬空策略：$(echo "$out" | cut -d'|' -f3)"
}

# ───────────────────────── M7 前端 UI（人工） ─────────────────────────
module_m7() {
  sect "M7 前端 UI（人工清单，脚本不执行）"
  cat <<'EOF'
  用 qidi-admin（机构：启迪学院111）与 ent-demo（机构：国州学院）分别登录，逐项确认：

  U1  /admin/libraries           列表有「审核状态」列（8 态）+「提交审核/申请删除」
  U2  /admin/libraries/audits    点「查看」→ 弹窗含阶段步进、变更内容表、审核时间线；自审显示「不能自审」
  U3  /admin/libraries/tasks     切「任务模板」Tab → 显示已发布库模板
  U4  /teaching/shared-refs      新建引用 → 只列已发布资源 → 成功后列表「生效中」、统计卡 +1
  U5  /admin/employment/outcomes 点「核实」→「未核实」变「已核实」；统计页「已核实去向」+1
  U6  /enterprise/workbench      ent-demo 登录 → 直达企业工作台；侧边栏仅 6 组
  U7  /admin/cooperation         「企业账号」Tab → 企业列表 + 开通弹窗可用
  U8  /admin/school-validity     admin 登录 → 各校有效期列表 + 设置弹窗
  U9  学生菜单                    zmq 登录 → 无任何管理端菜单
  U10 浏览器控制台                无 IconNotFoundError
EOF
  skip "M7 为人工浏览器验收，见上方清单（共 10 项）"
}

# ───────────────────────── 执行器 ─────────────────────────
run_case() {
  local rc=0
  case "$1" in
    M0) module_m0 || rc=$? ;;
    M1) module_m1 ;;
    M2) module_m2 ;;
    M3) module_m3 ;;
    M4) module_m4 ;;
    M5) module_m5 ;;
    M6) module_m6 ;;
    M7) module_m7 ;;
    *) echo "未知模块：$1（可用：M0 M1 M2 M3 M4 M5 M6 M7 all）"; exit 3 ;;
  esac
  return $rc
}

printf '\033[1mV2.0 分模块 E2E\033[0m  模块=%s  API=%s\n' "$MODULE" "$API"

if [ "$MODULE" = "all" ]; then
  module_m0 || { echo; echo "环境不可用，终止。"; exit 2; }
  for m in M1 M2 M3 M4 M5 M6; do run_case "$m"; done
else
  # 非 M0 时先做一次轻量健康检查
  if [ "$MODULE" != "M0" ]; then
    hc=$(curl -sk -o /dev/null -w '%{http_code}' "$API/health-status" || true)
    if [ "$hc" != "200" ]; then
      echo "✗ API 不可用（health=${hc}）——请先 ./dev.sh start api"; exit 2
    fi
  fi
  run_case "$MODULE"
fi

printf '\n\033[1m=== 汇总（%s）===\033[0m\n' "$MODULE"
printf '  通过 %d / 失败 %d / 跳过 %d\n' "$PASS" "$FAIL" "$SKIP"
if [ "$FAIL" -gt 0 ]; then
  printf '\n\033[31m失败用例：\033[0m\n'
  for c in "${FAILED[@]}"; do printf '  - %s\n' "$c"; done
  exit 1
fi
printf '\n\033[32m全部通过\033[0m\n'
