#!/usr/bin/env bash
# ============================================================
# iketong 演示数据补充：项目台账的里程碑时间/状态
#   - 造出「已完成 / 进行中 / 已逾期」三种状态，便于验收状态机与表格提示
#   - 同时验证「截止时间不得早于上一个里程碑」的顺序约束
# 用法：bash scripts/test/iketong-v2-milestones.sh
# ============================================================
set -uo pipefail
API=https://www.iketong.com
PROJECT_NAME='演示-养老实训基地共建项目'

tok(){ local t=$1 u=$2 p=$3; local h=(); [ "$t" != "-" ] && h=(-H "__tenant: $t"); \
  curl -sk -X POST $API/connect/token -H 'Content-Type: application/x-www-form-urlencoded' "${h[@]}" \
    --data-urlencode 'grant_type=password' --data-urlencode 'client_id=KnowledgeHub_App' \
    --data-urlencode 'scope=KnowledgeHub offline_access' --data-urlencode "username=$u" --data-urlencode "password=$p" \
  | python3 -c 'import json,sys;print(json.load(sys.stdin).get("access_token",""))'; }
api(){ local t=$1 ten=$2 m=$3 p=$4 d=${5:-}; local h=(-H "Authorization: Bearer $t" -H 'Content-Type: application/json'); \
  [ "$ten" != "-" ] && h+=(-H "__tenant: $ten"); \
  if [ -n "$d" ]; then curl -sk -X "$m" "$API$p" "${h[@]}" -d "$d"; else curl -sk -X "$m" "$API$p" "${h[@]}"; fi; }

T=$(tok qidi qidi-admin 123456)
PID=$(api "$T" qidi GET "/api/app/cooperation/projects?MaxResultCount=50" \
  | python3 -c "import json,sys
d=json.load(sys.stdin)
print(next((i['id'] for i in d.get('items',[]) if i.get('name')=='$PROJECT_NAME'),''))")
[ -z "$PID" ] && { echo "未找到项目：$PROJECT_NAME"; exit 1; }
echo "项目 id=$PID"

day(){ python3 -c "import datetime;print((datetime.date.today()+datetime.timedelta(days=$1)).isoformat()+'T00:00:00Z')"; }
add(){ # <name> <dueAt|null>
  local body
  if [ "$2" = "null" ]; then
    body="{\"projectId\":\"$PID\",\"name\":\"$1\",\"dueAt\":null}"
  else
    body="{\"projectId\":\"$PID\",\"name\":\"$1\",\"dueAt\":\"$2\"}"
  fi
  api "$T" qidi POST /api/app/cooperation/milestone "$body" \
    | python3 -c "import json,sys
d=json.load(sys.stdin)
if d.get('id'): print('  ✓', d.get('sortOrder'), d.get('name'), 'due=', d.get('dueAt'), 'status=', d.get('statusName'))
else: print('  ✗', d.get('error',{}).get('message'))"
}

echo "=== 追加里程碑（顺序 2/3/4） ==="
add "中期检查（已逾期示例）" "$(day -7)"
add "结项验收（进行中示例）" "$(day 30)"
add "成果推广（未设截止）" "null"

echo "=== 顺序约束验证：把截止填到更早的时间，应被拒绝 ==="
add "违规节点（早于上一个截止）" "$(day -20)"

echo "=== 完成任务 1 个里程碑（造「已完成」状态） ==="
MID=$(api "$T" qidi GET "/api/app/cooperation/projects?MaxResultCount=50" \
  | python3 -c "import json,sys
d=json.load(sys.stdin)
p=next((i for i in d.get('items',[]) if i.get('name')=='$PROJECT_NAME'),None)
ms=(p or {}).get('milestones') or []
m=next((x for x in ms if not x.get('isCompleted')),None)
print((m or {}).get('id',''))")
if [ -n "$MID" ]; then
  api "$T" qidi POST /api/app/cooperation/complete-milestone "{\"milestoneId\":\"$MID\",\"note\":\"演示数据：标记完成\"}" \
    | python3 -c "import json,sys;d=json.load(sys.stdin);print('  ✓',d.get('name'),'->',d.get('statusName'))"
fi

echo "=== 台账数据核对 ==="
api "$T" qidi GET "/api/app/cooperation/projects?MaxResultCount=50" | python3 -c "
import json,sys
d=json.load(sys.stdin)
p=next((i for i in d.get('items',[]) if i.get('name')=='$PROJECT_NAME'),None)
if not p: print('未找到项目'); raise SystemExit
print('  创建时间:', p.get('creationTime'))
print('  项目截止:', p.get('endAt'))
print('  里程碑:', p.get('completedMilestoneCount'), '/', p.get('milestoneCount'), ' 逾期数:', p.get('overdueMilestoneCount'), ' 下个节点:', p.get('nextMilestoneDueAt'))
for m in p.get('milestones') or []:
    print('   -', m.get('sortOrder'), m.get('name'), '| due=', m.get('dueAt'), '|', m.get('statusName'), '| overdue=', m.get('isOverdue'))
"
