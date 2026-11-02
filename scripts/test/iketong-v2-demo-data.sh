#!/usr/bin/env bash
# ============================================================
# iketong 演示数据：资源库管理主线（qidi 主建 ↔ dongyan 参建）
#
# 覆盖页面：
#   /admin/libraries/audits     审核中心（待审核 / 已驳回 / 已通过）
#   /admin/libraries/tasks      共建任务（已发布 + 进行中）
#   /teaching/build-tasks       我的共建任务（协作校教师视角）
#   /teaching/shared-refs       我的跨校引用（生效中 + 已撤销）
#   /admin/cooperation          产教合作（需求大厅 / 项目台账 / 企业账号）
#   /enterprise/workbench       企业工作台（资料 / 认证 / 需求 / 响应 / 项目）
#
# 用法：bash scripts/test/iketong-v2-demo-data.sh
# ============================================================
set -uo pipefail
API=https://www.iketong.com
QIDI=3a207891-a06c-8876-b34d-32a245adec63
DONG=3a23962d-6334-9807-4d20-54c30936c731

COBUILD=3a248d8a-340c-55f3-7845-b351efc3a869   # qidi · 东燕 校际共建教学资源库
Q_COURSE=3a222e32-d8aa-fcbf-c20c-d4785b374647 # qidi 循证体重管理方案设计
D_COURSE=3a239663-9298-64d0-837d-d0fce8dd4e61 # dongyan 新媒体写作
Q_RES=3a239b12-2450-e6af-3a5c-0c69981ea19b    # qidi 胸痹心痛病例分析
D_RES=3a244f61-6577-889d-004d-b53f918ed572    # dongyan 音画设计 5.2上-1

PASS=0; FAIL=0
ok(){ PASS=$((PASS+1)); printf '  \033[32m✓\033[0m %s\n' "$1"; }
bad(){ FAIL=$((FAIL+1)); printf '  \033[31m✗\033[0m %s -- %s\n' "$1" "${2:-}"; }
sect(){ printf '\n\033[1m=== %s ===\033[0m\n' "$1"; }

tok(){ local t=$1 u=$2 p=$3; local h=(); [ "$t" != "-" ] && h=(-H "__tenant: $t"); \
  curl -sk -X POST $API/connect/token -H 'Content-Type: application/x-www-form-urlencoded' "${h[@]}" \
    --data-urlencode 'grant_type=password' --data-urlencode 'client_id=KnowledgeHub_App' \
    --data-urlencode 'scope=KnowledgeHub offline_access' --data-urlencode "username=$u" --data-urlencode "password=$p" \
  | python3 -c 'import json,sys;print(json.load(sys.stdin).get("access_token",""))'; }

# api <token> <tenant|-> <method> <path> [json]  -> 打印 body
api(){ local t=$1 ten=$2 m=$3 p=$4 d=${5:-}; local h=(-H "Authorization: Bearer $t" -H 'Content-Type: application/json'); \
  [ "$ten" != "-" ] && h+=(-H "__tenant: $ten"); \
  if [ -n "$d" ]; then curl -sk -X "$m" "$API$p" "${h[@]}" -d "$d"; else curl -sk -X "$m" "$API$p" "${h[@]}"; fi; }
jget(){ python3 -c "import json,sys
try: d=json.load(sys.stdin)
except Exception: d={}
print($1)"; }

QIDI_T=$(tok qidi qidi-admin 123456)
DONG_A=$(tok dongyan dongyan-admin 123456)
DONG_T=$(tok dongyan dy-teacher 123456)
ENT_T=$(tok qidi qidi-ent 123456)
ADMIN_T=$(tok - admin '1q2w3E*')
LEAGUE_T=$(tok - league-admin 123456)
[ -n "$QIDI_T" ] && ok "令牌：qidi-admin / dongyan-admin / dy-teacher / qidi-ent / host admin / league-admin" || bad "令牌获取失败"

# 通用：对某 targetId 取在途审核单并审批
decide_by_target(){ # <token> <tenant|-> <targetId> <0通过|1驳回> [原因]
  local t=$1 ten=$2 tid=$3 dec=$4 reason=${5:-}
  local ticket
  ticket=$(api "$t" "$ten" GET "/api/app/review-ticket?TargetId=$tid" | jget 'next((i["id"] for i in d.get("items",[]) if i["status"]==0),"")')
  [ -z "$ticket" ] && { echo ""; return; }
  if [ "$dec" = "1" ]; then
    api "$ADMIN_T" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$ticket\",\"decision\":1,\"reason\":\"$reason\"}" >/dev/null
  else
    api "$ADMIN_T" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$ticket\",\"decision\":0}" >/dev/null
  fi
  echo "$ticket"
}

############################################################
sect "1) 审核中心：三种状态各造一条（qidi 建库）"
mk_lib(){ # <name> -> id
  api "$QIDI_T" qidi POST /api/app/resource-library "{\"name\":\"$1\",\"visibility\":0,\"description\":\"iketong 演示数据\"}" | jget 'd.get("id","")'
}
L_PENDING=$(mk_lib "演示-待院校审核库")
[ -n "$L_PENDING" ] && ok "待审核库已建（${L_PENDING}，状态应为待院校审核）" || bad "建库失败"

L_REJECT=$(mk_lib "演示-已驳回库")
R=$(decide_by_target "$QIDI_T" qidi "$L_REJECT" 1 "演示数据：名称需规范，请修改后重新提交")
[ -n "$L_REJECT" ] && ok "已驳回库已造（${L_REJECT}）" || bad "驳回库建库失败"

L_ACTIVE=$(mk_lib "演示-已发布库")
decide_by_target "$QIDI_T" qidi "$L_ACTIVE" 0 >/dev/null
ST=$(api "$QIDI_T" qidi GET "/api/app/resource-library/$L_ACTIVE" | jget 'd.get("status")')
[ "$ST" = "1" ] && ok "已发布库已造（${L_ACTIVE}，status=1）" || bad "已发布库状态异常" "status=$ST"

############################################################
sect "1.5) 协作校教师分配（共建库默认不自动分配，需主建校显式分配）"
DY_UID=$(api "$DONG_A" dongyan GET "/api/app/tenant-user?filter=dy-teacher" \
  | jget "next((i['id'] for i in d.get('items',[]) if i.get('userName')=='dy-teacher'),'')")
if [ -n "$DY_UID" ]; then
  api "$QIDI_T" qidi POST "/api/app/resource-library/$COBUILD/assign-teacher" "{\"userId\":\"$DY_UID\"}" >/dev/null \
    && ok "已把 dongyan 教师 dy-teacher 分配到共建库" || bad "分配教师失败"
else
  bad "未取到 dy-teacher 的 userId"
fi

############################################################
sect "2) 共建库内容准入：qidi / dongyan 各共享一条并审到已发布"
# 审批共建库里所有「待审」的内容准入单（两级：host 初审 → league 终审）
approve_pending_admissions(){
  local tickets t
  tickets=$(api "$ADMIN_T" - GET "/api/app/review-ticket?TargetType=1&LibraryId=$COBUILD&Status=0&MaxResultCount=50" \
    | jget "' '.join(i['id'] for i in d.get('items',[]))")
  for t in $tickets; do
    api "$ADMIN_T" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$t\",\"decision\":0}" >/dev/null
    # 共建库是两级：初审通过后同一单推进到「联盟终审」，仍需 pending 才能终审
    local still
    still=$(api "$ADMIN_T" - GET "/api/app/review-ticket?TargetType=1&LibraryId=$COBUILD&Status=0&MaxResultCount=50" \
      | jget "next((i['id'] for i in d.get('items',[]) if i['id']=='$t'),'')")
    [ -n "$still" ] && api "$LEAGUE_T" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$still\",\"decision\":0}" >/dev/null
  done
}

attach_and_approve(){ # <token> <tenant> <resourceId> <label>
  local resp st
  resp=$(api "$1" "$2" POST "/api/app/teaching-resource-library/$COBUILD/attach" "{\"contentType\":0,\"contentId\":\"$3\"}")
  case "$resp" in
    *'"error"'*) bad "$4 共享入共建库失败" "$(echo "$resp" | head -c 140)"; return;;
  esac
  # 注意：attach 返回的是内容 DTO（id=内容 id），准入审核单按「发布记录」建，
  # 所以审批要走「按库列出待审准入单」，不能拿返回 id 去查单据。
  approve_pending_admissions
  st=$(api "$QIDI_T" qidi GET "/api/app/teaching-resource-library/$COBUILD/contents?maxResultCount=50" \
       | jget "next((i.get('publicationStatus') for i in d.get('items',[]) if i['id']=='$3'),None)")
  [ "$st" = "2" ] && ok "$4 已发布到共建库（publicationStatus=2）" || bad "$4 未达已发布" "publicationStatus=$st"
}
attach_and_approve "$QIDI_T" qidi "$Q_RES" "qidi 资源「胸痹心痛病例分析」"
attach_and_approve "$DONG_T" dongyan "$D_RES" "dongyan 资源「音画设计 5.2上-1」"

############################################################
sect "3) 共建任务：qidi 建任务（协作校 dongyan）→ 发布 → 协作校提交 → 主责校验收"
TPL=$(api "$QIDI_T" qidi POST /api/app/build-task/task-template \
  "{\"libraryId\":\"$COBUILD\",\"code\":\"iketong-demo\",\"name\":\"演示交付清单\",\"items\":[{\"itemName\":\"课程标准\",\"contentType\":0,\"requiredCount\":1},{\"itemName\":\"章节课件\",\"contentType\":0,\"requiredCount\":2}]}" | jget 'd.get("id","")')
[ -n "$TPL" ] && ok "任务模板已建（${TPL}）" || bad "任务模板创建失败"

TASK=$(api "$QIDI_T" qidi POST /api/app/build-task/task \
  "{\"libraryId\":\"$COBUILD\",\"templateId\":\"$TPL\",\"title\":\"演示-校际共建任务（养老照护课程包）\",\"dueAt\":\"2026-12-31T00:00:00Z\",\"collaboratorSchoolIds\":[\"$DONG\"]}" | jget 'd.get("id","")')
[ -n "$TASK" ] && ok "任务已建（${TASK}）" || bad "任务创建失败"
api "$QIDI_T" qidi POST "/api/app/build-task/$TASK/publish-task" >/dev/null && ok "任务已发布"

DETAIL=$(api "$DONG_T" dongyan GET "/api/app/build-task/$TASK/task")
CAN_SUBMIT=$(echo "$DETAIL" | jget 'd.get("canSubmit")')
ITEM=$(echo "$DETAIL" | jget 'next((i["id"] for i in d.get("items",[])),"")')
[ "$CAN_SUBMIT" = "True" ] && ok "协作校（dongyan）教师视角 canSubmit=True" || bad "协作校 canSubmit 异常" "canSubmit=$CAN_SUBMIT"

DELIV=$(api "$DONG_T" dongyan POST /api/app/build-task/submit-delivery \
  "{\"taskId\":\"$TASK\",\"taskItemId\":\"$ITEM\",\"contentType\":0,\"contentId\":\"$D_RES\",\"comment\":\"dongyan 提交：音画设计课件\"}" | jget 'd.get("id","")')
[ -n "$DELIV" ] && ok "协作校已提交交付（${DELIV}）" || bad "提交交付失败"

api "$QIDI_T" qidi POST /api/app/build-task/review-delivery "{\"deliveryId\":\"$DELIV\",\"accept\":true,\"comment\":\"验收通过\"}" >/dev/null
PROG=$(api "$QIDI_T" qidi GET "/api/app/build-task/$TASK/task")
echo "$PROG" | python3 -c 'import json,sys;d=json.load(sys.stdin);print("     任务进度：已验收 %s/%s，状态=%s"%(d.get("acceptedTotal"),d.get("requiredTotal"),d.get("statusName")))'
[ "$(echo "$PROG" | jget 'd.get("acceptedTotal")')" = "1" ] && ok "主责校验收生效（1/N，任务进行中）" || bad "验收进度异常"

############################################################
sect "4) 跨校引用：qidi→引用共建库内容到本校课程；dongyan→引用到本校课程；再造一条已撤销"
mk_ref(){ # <token> <tenant> <courseId> <resourceId> <label>
  local id
  id=$(api "$1" "$2" POST /api/app/course-shared-ref "{\"courseId\":\"$3\",\"libraryId\":\"$COBUILD\",\"contentId\":\"$4\"}" | jget 'd.get("id","")')
  [ -n "$id" ] && { ok "$5 引用已建（${id}）"; echo "$id"; } || { bad "$5 引用失败" "$(api "$1" "$2" POST /api/app/course-shared-ref "{\"courseId\":\"$3\",\"libraryId\":\"$COBUILD\",\"contentId\":\"$4\"}" | head -c 160)"; echo ""; }
}
REF_Q=$(mk_ref "$QIDI_T" qidi "$Q_COURSE" "$D_RES" "qidi 课程引用 dongyan 资源")
REF_D=$(mk_ref "$DONG_A" dongyan "$D_COURSE" "$Q_RES" "dongyan 课程引用 qidi 资源（课程负责人）")
# 再造一条并撤销，供「已撤销」状态展示
REF_R=$(mk_ref "$QIDI_T" qidi "$Q_COURSE" "$Q_RES" "qidi 课程引用 qidi 资源（用于演示撤销）")
if [ -n "$REF_R" ]; then
  api "$QIDI_T" qidi POST "/api/app/course-shared-ref/$REF_R/revoke?reason=演示数据：更换更合适的资源" >/dev/null && ok "已撤销引用已造（${REF_R}）"
fi

############################################################
sect "5) 产教合作 / 企业工作台：企业认证（两级）→ 发需求（两级）→ 学校响应 → 企业接受 → 建项目"
EID=$(api "$ENT_T" qidi GET /api/app/enterprise/mine | jget 'd.get("id","")')
[ -n "$EID" ] && ok "企业主体：$(api "$ENT_T" qidi GET /api/app/enterprise/mine | jget 'd.get("name")')（${EID}）" || bad "企业主体缺失"

api "$ENT_T" qidi PUT "/api/app/enterprise/$EID" \
  "{\"name\":\"易课通康养科技（演示）\",\"unifiedSocialCreditCode\":\"91330000DEMO0002X\",\"industry\":\"养老服务\",\"contactName\":\"启迪企业\",\"contactPhone\":\"13800001111\",\"contactEmail\":\"ent@iketong.local\",\"summary\":\"iketong 演示企业：智慧康养实训与课程共建\"}" >/dev/null && ok "企业资料已补全"
api "$ENT_T" qidi POST "/api/app/enterprise/$EID/submit-certification" >/dev/null && ok "企业认证已提交"
ET=$(api "$ADMIN_T" - GET "/api/app/review-ticket?TargetId=$EID" | jget 'next((i["id"] for i in d.get("items",[]) if i["status"]==0),"")')
if [ -n "$ET" ]; then
  api "$ADMIN_T" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$ET\",\"decision\":0}" >/dev/null
  api "$LEAGUE_T" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$ET\",\"decision\":0}" >/dev/null
  CERT=$(api "$ENT_T" qidi GET /api/app/enterprise/mine | jget 'd.get("certificationStatus")')
  [ "$CERT" = "2" ] && ok "企业认证通过（已认证）" || bad "企业认证未通过" "status=$CERT"
fi

DID=$(api "$ENT_T" qidi POST /api/app/cooperation/demand \
  '{"type":1,"title":"演示-智慧养老实训基地共建","scope":2,"region":"浙江杭州","majors":"智慧健康养老服务与管理","background":"iketong 演示需求","cycle":"1 年","scale":"20 人"}' | jget 'd.get("id","")')
[ -n "$DID" ] && ok "合作需求已建（${DID}）" || bad "需求创建失败"
api "$ENT_T" qidi POST "/api/app/cooperation/$DID/submit-demand" >/dev/null && ok "需求已提交审核"
DT=$(api "$ADMIN_T" - GET "/api/app/review-ticket?TargetId=$DID" | jget 'next((i["id"] for i in d.get("items",[]) if i["status"]==0),"")')
if [ -n "$DT" ]; then
  api "$ADMIN_T" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$DT\",\"decision\":0}" >/dev/null
  api "$LEAGUE_T" - POST /api/app/review-ticket/decide "{\"ticketId\":\"$DT\",\"decision\":0}" >/dev/null
  DST=$(api "$ENT_T" qidi GET "/api/app/cooperation/$DID/demand" | jget 'd.get("status")')
  [ "$DST" = "2" ] && ok "需求已发布（进入需求大厅）" || bad "需求未发布" "status=$DST"
fi

RID=$(api "$DONG_A" dongyan POST /api/app/cooperation/respond "{\"demandId\":\"$DID\",\"note\":\"dongyan 有意向参与养老实训基地共建\"}" | jget 'd.get("id","")')
[ -n "$RID" ] && ok "dongyan 已响应需求（${RID}）" || bad "学校响应失败"
api "$ENT_T" qidi POST "/api/app/cooperation/decide-response/$RID" '{"accept":true}' >/dev/null && ok "企业已接受响应"
PID=$(api "$ENT_T" qidi POST /api/app/cooperation/project "{\"responseId\":\"$RID\",\"name\":\"演示-养老实训基地共建项目\"}" | jget 'd.get("id","")')
[ -n "$PID" ] && ok "项目台账已建（${PID}）" || bad "建项目失败"
if [ -n "$PID" ]; then
  api "$ENT_T" qidi POST /api/app/cooperation/milestone "{\"projectId\":\"$PID\",\"name\":\"课程包交付\",\"sortOrder\":1}" >/dev/null && ok "项目里程碑已加"
fi

############################################################
sect "汇总"
printf '  通过 %d / 失败 %d\n' "$PASS" "$FAIL"
cat <<EOF

  关键对象 id（便于页面核对）：
    待审核库      $L_PENDING
    已驳回库      $L_REJECT
    已发布库      $L_ACTIVE
    共建任务      $TASK
    跨校引用(qidi)$REF_Q
    跨校引用(dong)$REF_D
    已撤销引用    $REF_R
    企业          $EID
    合作需求      $DID
    学校响应      $RID
    合作项目      $PID
EOF
