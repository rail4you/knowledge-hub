#!/usr/bin/env bash
# =============================================================================
# B1–B3 整体回归 E2E（课程教学改造 · 三批一起跑）
# -----------------------------------------------------------------------------
# B1 课程教学骨架
#   1. 老师建课 → 发布
#   2. 课程教学信息（教学目标/毕业要求/知识逻辑结构）保存 → 回读一致
#   3. 教材（主教材）登记 → 回读 isPrimary
#   4. 知识点树：单元 → 2 个知识项 → 树序/物化路径 → 上移 → 路径重算
#   5. 习题绑定知识项 → 学生选课 → 作答 → 知识点掌握度出现证据
# B2 课堂互动（独立栏目）
#   6. 签到：发起 → 匿名解析 → 学生签到 → 重复签到幂等 → 名单
#   7. 讨论答疑：学生提问 → 老师回复 → 采纳（已解决）
#   8. 课堂练习：快照抽题 → 学生答题（错→覆盖为对）→ 老师实时统计
#   9. 小组任务：随机分组+发布 → 学生提交 → 老师评分
# B3 内容与打印
#  10. 课程视频：上传 → Range 流式播放(206) → 秒级打点（同秒覆盖）→ 时长回填
#  11. 打印：建卷 → 试卷 PDF + 成绩单 PDF（校验 %PDF 魔数）
#
# 本地 / 远程通用；所有连接信息走环境变量。
#
# 用法：
#   ./run.sh                       # 本地默认
#   KEEP=1 ./run.sh                # 保留数据（不自动清理，便于人工核对）
#   SKIP_VIDEO=1 ./run.sh          # 不测视频上传（无 ffmpeg 时）
#   API_BASE=http://1.2.3.4:44354 TENANT_NAME=qidi ./run.sh
#
# 退出码：0 = 全通过；非 0 = 首个失败点（打印响应体）
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LIB_DIR="$SCRIPT_DIR/../lib"
JQX="$LIB_DIR/jqx.py"

API_BASE="${API_BASE:-https://localhost:44305}"
TENANT_NAME="${TENANT_NAME:-qidi}"
ADMIN_USER="${ADMIN_USER:-admin}";      ADMIN_PASS="${ADMIN_PASS:-1q2w3E*}"
TEACHER_USER="${TEACHER_USER:-wwq}";    TEACHER_PASS="${TEACHER_PASS:-123456}"
STUDENT_USER="${STUDENT_USER:-zmq}";    STUDENT_PASS="${STUDENT_PASS:-1234567}"
OAUTH_CLIENT="${OAUTH_CLIENT:-KnowledgeHub_App}"
OAUTH_SCOPE="${OAUTH_SCOPE:-offline_access KnowledgeHub}"
CURL_SSL_ARGS="${CURL_SSL_ARGS:--k}"

RUN_TAG="${RUN_TAG:-$(date +%m%d-%H%M%S)}"
COURSE_TITLE="${COURSE_TITLE:-B1B3回归课程-${RUN_TAG}}"
SKIP_VIDEO="${SKIP_VIDEO:-0}"
KEEP="${KEEP:-0}"

WORK="$(mktemp -d)"
trap 'if [ "$KEEP" = "1" ]; then echo "临时目录保留: $WORK"; else rm -rf "$WORK"; fi' EXIT

TENANT_ID=""; ADMIN_TOKEN=""; TEACHER_TOKEN=""; STUDENT_TOKEN=""
LAST_CODE=""; LAST_BODY=""

# 本次运行产物（清理用）
COURSE_ID=""; CHAPTER_ID=""; UNIT_ID=""; ITEM1_ID=""; ITEM2_ID=""; EXERCISE_ID=""; EXAM_ID=""
VIDEO_ID=""

if [ -t 1 ]; then
  C_RED=$'\033[31m'; C_GREEN=$'\033[32m'; C_YELLOW=$'\033[33m'
  C_BLUE=$'\033[36m'; C_BOLD=$'\033[1m'; C_OFF=$'\033[0m'
else C_RED=""; C_GREEN=""; C_YELLOW=""; C_BLUE=""; C_BOLD=""; C_OFF=""; fi

step() { printf '\n%s▶ %s%s\n' "$C_BOLD$C_BLUE" "$*" "$C_OFF"; }
pass() { printf '  %s✓%s %s\n' "$C_GREEN" "$C_OFF" "$*"; }
info() { printf '    %s· %s%s\n' "$C_BLUE" "$*" "$C_OFF"; }
warn() { printf '  %s!%s %s\n' "$C_YELLOW" "$C_OFF" "$*"; }
die()  { printf '\n%s✗ 失败：%s%s\n' "$C_RED" "$*" "$C_OFF" >&2; dump_last >&2; exit 1; }

dump_last() {
  [ -n "$LAST_BODY" ] || return 0
  printf '  最近一次响应 (HTTP %s):\n' "${LAST_CODE:-?}" >&2
  printf '%s\n' "$LAST_BODY" | head -c 1200 | sed 's/^/    /' >&2
  printf '\n' >&2
}

# ── HTTP ─────────────────────────────────────────────────────
api() {
  local method="$1" path="$2" token="$3" body="${4:-}" tid="${5:-$TENANT_ID}"
  local -a args=(-s $CURL_SSL_ARGS -X "$method" -H 'Accept: application/json'
                 -o "$WORK/resp.json" -w '%{http_code}')
  [ "$token" != "-" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$tid" ] && args+=(-H "Abp-TenantId: $tid")
  # POST/PUT 一律带 JSON Content-Type：无 body 时 ABP 也要求有 Content-Type，
  # 否则「发布/关闭」这类无参动作会直接 415 Unsupported Media Type。
  case "$method" in
    POST|PUT|PATCH)
      args+=(-H 'Content-Type: application/json' --data-binary "${body:-{\}}") ;;
    *)
      [ -n "$body" ] && args+=(-H 'Content-Type: application/json' --data-binary "$body") ;;
  esac
  LAST_CODE="$(curl "${args[@]}" "${API_BASE}${path}" || echo 000)"
  LAST_BODY="$(cat "$WORK/resp.json" 2>/dev/null || echo '')"
}

api_ok() {
  api "$@"
  case "$LAST_CODE" in 2*) : ;; *) die "$1 $2 → HTTP $LAST_CODE" ;; esac
}

# 二进制 / multipart：不受 api() 的 JSON 约束
api_file() {
  local out="$1"; shift
  LAST_CODE="$(curl -s $CURL_SSL_ARGS -o "$out" -w '%{http_code}' "$@" || echo 000)"
  LAST_BODY="$(head -c 200 "$out" 2>/dev/null || echo '')"
}

jget()   { python3 "$JQX" get   "$WORK/resp.json" "${1:-}"; }
jlen()   { python3 "$JQX" len   "$WORK/resp.json" "${1:-}"; }
jdeep()  { python3 "$JQX" deep  "$WORK/resp.json" "$1" "$2" "${3:-id}"; }

login() {
  local user="$1" pass="$2" tid="${3:-}"
  local -a args=(-s $CURL_SSL_ARGS -X POST "${API_BASE}/connect/token"
    -H 'Content-Type: application/x-www-form-urlencoded' -o "$WORK/token.json" -w '%{http_code}')
  [ -n "$tid" ] && args+=(-H "Abp-TenantId: $tid")
  local code
  code="$(curl "${args[@]}" \
    --data-urlencode 'grant_type=password' \
    --data-urlencode "client_id=$OAUTH_CLIENT" \
    --data-urlencode "scope=$OAUTH_SCOPE" \
    --data-urlencode "username=$user" \
    --data-urlencode "password=$pass")"
  LAST_CODE="$code"; LAST_BODY="$(cat "$WORK/token.json" 2>/dev/null || echo '')"
  case "$code" in
    2*) python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('access_token',''))" "$WORK/token.json" ;;
    *) die "登录 $user 失败 (HTTP $code)" ;;
  esac
}

expect_eq()       { if [ "$1" = "$2" ]; then pass "$3 = $2"; else die "$3 期望 $2，实际 '$1'"; fi; }
expect_ne()       { if [ "$1" != "$2" ]; then pass "$3 ≠ $2（实际 $1）"; else die "$3 不应等于 $2"; fi; }
expect_nonempty() { if [ -n "$1" ]; then pass "$2 = $1"; else die "$2 为空"; fi; }
expect_ge()       { if [ "${1:-0}" -ge "$2" ] 2>/dev/null; then pass "$3 = $1 (≥ $2)"; else die "$3 期望 ≥ $2，实际 '${1:-0}'"; fi; }
# 只校验"非空"但**不回显内容**（令牌等敏感值不要打到日志里）
expect_present()  { if [ -n "$1" ]; then pass "$2"; else die "$2（值为空）"; fi; }
expect_file_magic() {
  local file="$1" magic="$2" label="$3"
  if [ -s "$file" ] && head -c 4 "$file" | grep -q "$magic"; then
    pass "${label}（$(wc -c < "$file" | tr -d ' ') 字节）"
  else
    die "$label 内容不是预期格式（前 60 字节：$(head -c 60 "$file" | tr -d '\0' )）"
  fi
}

# =============================================================================
printf '%s\n' "=============================================================================="
printf ' B1–B3 整体回归 E2E\n'
printf ' API     : %s\n' "$API_BASE"
printf ' 租户    : %s\n' "$TENANT_NAME"
printf ' 账号    : %s(宿主) / %s(老师) / %s(学生)\n' "$ADMIN_USER" "$TEACHER_USER" "$STUDENT_USER"
printf ' 课程    : %s\n' "$COURSE_TITLE"
printf ' 标记    : %s\n' "$RUN_TAG"
printf '%s\n' "=============================================================================="

# ── 0. 环境 ──────────────────────────────────────────────────
step "0 环境：租户 + 登录"
api_ok GET /api/public/tenants -
TENANT_ID="$(python3 - "$WORK/resp.json" "$TENANT_NAME" <<'PY'
import json,sys
data=json.load(open(sys.argv[1]))
items=data if isinstance(data,list) else data.get('items',[])
for t in items:
    if t.get('name')==sys.argv[2] or t.get('tenancyName')==sys.argv[2]:
        print(t.get('id') or t.get('tenantId') or ''); break
PY
)"
expect_nonempty "$TENANT_ID" "tenantId"
ADMIN_TOKEN="$(login "$ADMIN_USER" "$ADMIN_PASS" "")"
TEACHER_TOKEN="$(login "$TEACHER_USER" "$TEACHER_PASS" "$TENANT_ID")"
STUDENT_TOKEN="$(login "$STUDENT_USER" "$STUDENT_PASS" "$TENANT_ID")"
expect_present "$TEACHER_TOKEN" "老师令牌已获取"
expect_present "$STUDENT_TOKEN" "学生令牌已获取"

# ══════════════════════════════════════════════════════════════
#  B1
# ══════════════════════════════════════════════════════════════
step "B1-1 老师建课 → 发布"
api_ok POST /api/app/course "$TEACHER_TOKEN" "{
  \"title\": \"$COURSE_TITLE\",
  \"description\": \"B1–B3 整体回归用课程（可删）\",
  \"semester\": \"2026-2027-1\",
  \"credits\": 2,
  \"semesterHours\": 32,
  \"difficulty\": 2,
  \"isRecommended\": false,
  \"majorIds\": [],
  \"libraryIds\": []
}"
COURSE_ID="$(jget id)"
expect_nonempty "$COURSE_ID" "courseId"
api_ok POST "/api/app/course/$COURSE_ID/publish" "$TEACHER_TOKEN"
STATUS="$(jget status)"
expect_eq "$STATUS" "5" "课程状态（5=Published）"

step "B1-2 课程教学信息（教学目标 / 毕业要求 / 知识逻辑结构）"
api_ok POST /api/app/course-info/save "$TEACHER_TOKEN" "{
  \"courseId\": \"$COURSE_ID\",
  \"objectives\": \"掌握回归测试的基本方法\\n能独立完成一次闭环验证\",
  \"target\": \"教师/管理员\",
  \"highlights\": \"一次跑通 B1–B3\",
  \"background\": \"改造后需要端到端回归\",
  \"structure\": \"1. 骨架 2. 互动 3. 内容与打印\",
  \"graduationRequirements\": \"毕业要求 5.2\"
}"
expect_eq "$(jget hasContent)" "true" "courseInfo.hasContent"
api_ok GET "/api/app/course-info/by-course/$COURSE_ID" "$TEACHER_TOKEN"
expect_eq "$(jget hasContent)" "true" "回读 hasContent"
OBJ="$(jget objectives)"
case "$OBJ" in *"能独立完成一次闭环验证"*) pass "多行文本换行保留" ;; *) die "objectives 内容不一致: $OBJ" ;; esac

step "B1-3 教材（主教材）"
api_ok POST /api/app/course-textbook "$TEACHER_TOKEN" "{
  \"courseId\": \"$COURSE_ID\",
  \"title\": \"回归测试教材（第 1 版）\",
  \"author\": \"E2E\",
  \"publisher\": \"自动化出版社\",
  \"edition\": \"第 1 版\",
  \"isbn\": \"978-7-000-00000-0\",
  \"isPrimary\": true,
  \"sortOrder\": 0
}"
TEXTBOOK_ID="$(jget id)"
expect_nonempty "$TEXTBOOK_ID" "textbookId"
expect_eq "$(jget isPrimary)" "true" "textbook.isPrimary"
api_ok GET "/api/app/course-textbook/by-course/$COURSE_ID" "$TEACHER_TOKEN"
expect_ge "$(jlen)" 1 "教材数量"

step "B1-4 知识点树（单元 → 知识项）+ 物化路径"
mk_node() {
  local name="$1" node_type="$2" parent="$3"
  api_ok POST /api/app/knowledge-resource "$TEACHER_TOKEN" "{
    \"courseId\": \"$COURSE_ID\",
    \"name\": \"$name\",
    \"description\": \"$name 的说明\",
    \"nodeType\": $node_type,
    \"difficulty\": 2,
    \"sortOrder\": 0,
    \"parentId\": $parent
  }"
  jget id
}
UNIT_ID="$(mk_node "回归单元" 0 null)"
ITEM1_ID="$(mk_node "回归知识项 A" 1 "\"$UNIT_ID\"")"
ITEM2_ID="$(mk_node "回归知识项 B" 1 "\"$UNIT_ID\"")"
expect_nonempty "$UNIT_ID" "unitId"; expect_nonempty "$ITEM1_ID" "item1"; expect_nonempty "$ITEM2_ID" "item2"

api_ok GET "/api/app/knowledge-resource/tree/$COURSE_ID" "$TEACHER_TOKEN"
expect_eq "$(jlen)" "1" "顶层节点数（1 个单元）"
UNIT_NODE_TYPE="$(jdeep id "$UNIT_ID" nodeType)"
expect_eq "$UNIT_NODE_TYPE" "0" "单元 nodeType（0=Unit）"
CHILD_COUNT="$(python3 - "$WORK/resp.json" "$UNIT_ID" <<'PY'
import json,sys
rows=json.load(open(sys.argv[1])); target=sys.argv[2]
for r in rows:
    if r.get('id')==target:
        print(len(r.get('children') or [])); break
else: print(0)
PY
)"
expect_eq "$CHILD_COUNT" "2" "单元下子节点数"

api_ok POST "/api/app/knowledge-resource/regenerate-sort-paths/$COURSE_ID" "$TEACHER_TOKEN"
CHANGED="$(python3 -c "import json;print(json.load(open('$WORK/resp.json')))" 2>/dev/null || echo 0)"
expect_ge "$CHANGED" 0 "重算路径条数"
api_ok GET "/api/app/knowledge-resource/tree/$COURSE_ID" "$TEACHER_TOKEN"
PATH1="$(jdeep id "$ITEM1_ID" sortPath)"
expect_nonempty "$PATH1" "item1.sortPath（创建即维护，无需手动重算）"

# 上移：item2 → index 0
api_ok POST "/api/app/knowledge-resource/$ITEM2_ID/move" "$TEACHER_TOKEN" "{\"parentId\": \"$UNIT_ID\", \"index\": 0}"
api_ok GET "/api/app/knowledge-resource/tree/$COURSE_ID" "$TEACHER_TOKEN"
FIRST_CHILD="$(python3 - "$WORK/resp.json" "$UNIT_ID" <<'PY'
import json,sys
rows=json.load(open(sys.argv[1])); target=sys.argv[2]
for r in rows:
    if r.get('id')==target:
        kids=r.get('children') or []
        print(kids[0]['id'] if kids else ''); break
PY
)"
expect_eq "$FIRST_CHILD" "$ITEM2_ID" "上移后第一个子节点"

step "B1-5 习题绑定知识项 → 学生作答 → 掌握度出证据"
api_ok POST /api/app/exercise "$TEACHER_TOKEN" "{
  \"courseId\": \"$COURSE_ID\",
  \"title\": \"回归练习题（单选）\",
  \"questionContent\": \"1+1 等于几？\",
  \"type\": 0,
  \"options\": \"[\\\"2\\\",\\\"3\\\",\\\"4\\\",\\\"5\\\"]\",
  \"answer\": \"A\",
  \"questionAnalysis\": \"基础题\",
  \"difficulty\": 1,
  \"score\": 5,
  \"knowledgeResourceId\": \"$ITEM1_ID\"
}"
EXERCISE_ID="$(jget id)"
expect_eq "$(jget knowledgeResourceId)" "$ITEM1_ID" "习题 knowledgeResourceId"

# ABP 的配置里当前用户是 currentUser（不是 user）
STUDENT_ID="$(curl -s $CURL_SSL_ARGS "$API_BASE/api/abp/application-configuration" -H "Authorization: Bearer $STUDENT_TOKEN" -H "Abp-TenantId: $TENANT_ID" | python3 -c "import json,sys;print(json.load(sys.stdin).get('currentUser',{}).get('id',''))")"
expect_nonempty "$STUDENT_ID" "studentId"

api_ok POST /api/app/student-course/enroll-student "$TEACHER_TOKEN" "{\"courseId\": \"$COURSE_ID\", \"studentId\": \"$STUDENT_ID\"}"
info "老师把学生导入课程"

api_ok POST /api/app/student-exercise-record/save-or-update-record "$STUDENT_TOKEN" "{
  \"courseId\": \"$COURSE_ID\",
  \"exerciseId\": \"$EXERCISE_ID\",
  \"studentAnswer\": \"A\",
  \"timeSpentTicks\": 50000000
}"
expect_eq "$(jget isCorrect)" "true" "客观题自动判对"

api_ok GET "/api/app/learning/knowledge-mastery/$COURSE_ID" "$STUDENT_TOKEN"
EVID="$(python3 - "$WORK/resp.json" "$ITEM1_ID" <<'PY'
import json,sys
rows=json.load(open(sys.argv[1])); target=sys.argv[2]
for r in rows:
    if r.get('knowledgeResourceId')==target:
        # 注意：Python 的 bool 打印是 True/False，bash 里要和 "true" 对齐
        has_ev = 'true' if r.get('hasEvidence') else 'false'
        print(f"{r.get('mastery')}|{r.get('evidence')}|{r.get('practiceCount')}|{has_ev}")
        break
else: print("|||")
PY
)"
MASTERY="${EVID%%|*}"; REST="${EVID#*|}"; EVIDENCE="${REST%%|*}"; REST="${REST#*|}"; PRACTICE="${REST%%|*}"; HAS_EV="${REST##*|}"
expect_eq "$HAS_EV" "true" "掌握度有证据"
expect_ge "$PRACTICE" 1 "练习次数"
MASTERY_PCT="$(python3 -c "print(round(float('$MASTERY' or 0)*100))" 2>/dev/null || echo 0)"
info "掌握度=${MASTERY_PCT}% 证据=${EVIDENCE} 练习=${PRACTICE} 次（Beta 先验，不会一题拉满）"

# ══════════════════════════════════════════════════════════════
#  B2
# ══════════════════════════════════════════════════════════════
step "B2-1 签到：发起 → 匿名解析 → 签到 → 幂等 → 名单"
api_ok POST /api/app/check-in "$TEACHER_TOKEN" "{\"courseId\": \"$COURSE_ID\", \"title\": \"回归签到\", \"durationMinutes\": 30}"
CHECKIN_ID="$(jget id)"; CHECKIN_TOKEN="$(jget token)"
expect_nonempty "$CHECKIN_TOKEN" "签到 token"
expect_ge "$(jget expectedCount)" 1 "应签到人数（选课人数）"

api_ok POST /api/app/classroom-token/resolve "-" "{\"token\": \"$CHECKIN_TOKEN\", \"kind\": 0}"
expect_eq "$(jget isOpen)" "true" "匿名解析 isOpen"
# token 泄露时最怕被枚举出个人信息：这里显式排除"名单/成绩/题目"类字段
LEAK="$(python3 -c "
import json
# title/description 是场次自己的文案，允许；要排除的是「别人的名单 / 成绩 / 题目内容」
forbidden={'checkedIn','absent','members','students','submitters','notSubmitted','replies','answers','groups','submissions'}
d=json.load(open('$WORK/resp.json'))
bad=[k for k in d if k in forbidden]
print(','.join(bad))")"
expect_eq "$LEAK" "" "匿名解析不含名单/成绩/题目字段（只回会话元信息）"

api_ok POST /api/app/check-in/do-check-in "$STUDENT_TOKEN" "{\"token\": \"$CHECKIN_TOKEN\"}"
expect_eq "$(jget isNew)" "true" "首次签到 isNew"
api_ok POST /api/app/check-in/do-check-in "$STUDENT_TOKEN" "{\"token\": \"$CHECKIN_TOKEN\"}"
expect_eq "$(jget isNew)" "false" "重复签到 isNew（幂等）"

api_ok GET "/api/app/check-in/roster/$CHECKIN_ID" "$TEACHER_TOKEN"
CI_IN="$(jlen checkedIn)"; CI_ABSENT="$(jlen absent)"
expect_ge "$CI_IN" 1 "已签到人数"
info "已签到=$CI_IN / 未签到=$CI_ABSENT"

step "B2-2 讨论答疑：提问 → 回复 → 采纳"
api_ok POST /api/app/discussion "$STUDENT_TOKEN" "{
  \"courseId\": \"$COURSE_ID\",
  \"title\": \"回归提问：为什么需要 Beta 先验？\",
  \"content\": \"我直接算出 0/1 就判薄弱，感觉不合理\"
}"
DISCUSSION_ID="$(jget id)"
expect_nonempty "$DISCUSSION_ID" "discussionId"
expect_eq "$(jget isMine)" "true" "isMine"

api_ok POST /api/app/discussion/reply "$TEACHER_TOKEN" "{
  \"discussionId\": \"$DISCUSSION_ID\",
  \"content\": \"少量证据时用先验平滑，避免一道题定生死\"
}"
REPLY_ID="$(jget id)"
expect_nonempty "$REPLY_ID" "replyId"

api_ok POST "/api/app/discussion/$DISCUSSION_ID/accept-reply/$REPLY_ID" "$TEACHER_TOKEN"
expect_eq "$(jget isResolved)" "true" "采纳后已解决"
api_ok GET "/api/app/discussion/$DISCUSSION_ID" "$STUDENT_TOKEN"
ACCEPTED="$(python3 -c "
import json;d=json.load(open('$WORK/resp.json'))
print(sum(1 for r in (d.get('replies') or []) if r.get('isAccepted')))")"
expect_eq "$ACCEPTED" "1" "最佳答案数量"

step "B2-3 课堂练习：抽题 → 提交(错) → 覆盖(对) → 统计"
api_ok POST /api/app/classroom-practice "$TEACHER_TOKEN" "{\"courseId\": \"$COURSE_ID\", \"title\": \"回归课堂练习\", \"questionCount\": 3}"
PRACTICE_ID="$(jget id)"; PRACTICE_TOKEN="$(jget token)"
PRACTICE_Q="$(jget questionCount)"
expect_ge "$PRACTICE_Q" 1 "抽到题数（快照）"
info "抽题数=${PRACTICE_Q}（池里只有客观题，所以可能少于 3）"

api_ok GET "/api/app/classroom-practice/paper?token=$PRACTICE_TOKEN" "$STUDENT_TOKEN"
PAPER_N="$(jlen items)"
expect_eq "$PAPER_N" "$PRACTICE_Q" "题目数与快照一致"
WRONG_BODY="$(python3 - "$WORK/resp.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1]))
print(json.dumps({"token":"__TOKEN__","answers":[{"exerciseId":i["exerciseId"],"answer":"Z"} for i in d["items"]]}))
PY
)"
WRONG_BODY="${WRONG_BODY//__TOKEN__/$PRACTICE_TOKEN}"
api_ok POST /api/app/classroom-practice/submit "$STUDENT_TOKEN" "$WRONG_BODY"
WRONG_CORRECT="$(jget correctCount)"
info "全填 Z：答对 $WRONG_CORRECT / $(jget totalCount)"

CORRECT_BODY="$(python3 - "$WORK/resp.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1]))
print(json.dumps({"token":"__TOKEN__","answers":[{"exerciseId":a["exerciseId"],"answer":a["correctAnswer"]} for a in d["answers"]]}))
PY
)"
CORRECT_BODY="${CORRECT_BODY//__TOKEN__/$PRACTICE_TOKEN}"
api_ok POST /api/app/classroom-practice/submit "$STUDENT_TOKEN" "$CORRECT_BODY"
RIGHT_CORRECT="$(jget correctCount)"; RIGHT_TOTAL="$(jget totalCount)"
expect_eq "$RIGHT_CORRECT" "$RIGHT_TOTAL" "按正确答案覆盖提交后全对"

api_ok GET "/api/app/classroom-practice/$PRACTICE_ID/stats" "$TEACHER_TOKEN"
expect_ge "$(jget submittedCount)" 1 "已提交人数"
RATE="$(jget averageAccuracy)"
info "全班平均正确率 = ${RATE}（覆盖式提交只留最后一份）"

step "B2-4 小组任务：随机分组 → 发布 → 提交 → 评分"
api_ok POST /api/app/group-task "$TEACHER_TOKEN" "{
  \"courseId\": \"$COURSE_ID\",
  \"title\": \"回归小组任务\",
  \"description\": \"每组一份提交\",
  \"maxScore\": 100,
  \"groupMode\": 1,
  \"publishImmediately\": true
}"
GROUP_TASK_ID="$(jget id)"; GROUP_TASK_TOKEN="$(jget token)"
expect_eq "$(jget status)" "1" "任务已发布"
expect_eq "$(jget groupCount)" "1" "随机分组组数（1 名学生 → 1 组）"

api_ok GET "/api/app/group-task/my-task?token=$GROUP_TASK_TOKEN" "$STUDENT_TOKEN"
expect_ge "$(jlen groups)" 1 "学生能看到分组"

api_ok POST /api/app/group-task/submit "$STUDENT_TOKEN" "{\"token\": \"$GROUP_TASK_TOKEN\", \"content\": \"回归提交内容\"}"
SUBMISSION_ID="$(jget id)"
expect_nonempty "$SUBMISSION_ID" "submissionId"
expect_nonempty "$(jget groupName)" "提交归属组名"

api_ok POST /api/app/group-task/grade "$TEACHER_TOKEN" "{\"submissionId\": \"$SUBMISSION_ID\", \"score\": 88, \"feedback\": \"结构清晰\"}"
expect_eq "$(jget score)" "88" "评分"

# ══════════════════════════════════════════════════════════════
#  P2-1 能力体系（能力 ← 课程支撑 ← 知识点 ← 掌握度 → 达成度）
# ══════════════════════════════════════════════════════════════
step "P2-1 能力体系：建能力 → 课程支撑 → 关联知识点 → 达成度"
api_ok POST /api/app/competency "$TEACHER_TOKEN" "{
  \"name\": \"能够完成回归验证\",
  \"code\": \"E2E-${RUN_TAG}\",
  \"level\": 2,
  \"sortOrder\": 0,
  \"isEnabled\": true
}"
COMPETENCY_ID="$(jget id)"
expect_nonempty "$COMPETENCY_ID" "competencyId"

api_ok POST /api/app/competency/set-course-support "$TEACHER_TOKEN" "{
  \"courseId\": \"$COURSE_ID\",
  \"items\": [{\"competencyId\": \"$COMPETENCY_ID\", \"weight\": 0.8}]
}"
expect_ge "$(jlen)" 1 "课程支撑能力数"
expect_eq "$(python3 -c "
import json;d=json.load(open('$WORK/resp.json'));print(d[0]['weight'])" 2>/dev/null)" "0.8" "支撑强度"

api_ok POST /api/app/competency/link-knowledge "$TEACHER_TOKEN" "{
  \"competencyId\": \"$COMPETENCY_ID\",
  \"knowledgeResourceId\": \"$ITEM1_ID\",
  \"weight\": 1
}"
expect_nonempty "$(jget id)" "能力-知识点关联 id"

# B1-5 已经让学生答对过 1 题（该题绑在 ITEM1 上），所以达成度应当 > 0
api_ok GET "/api/app/competency/achievement/$COURSE_ID" "$TEACHER_TOKEN"
ACH="$(python3 -c "
import json;d=json.load(open('$WORK/resp.json'))
it=(d.get('items') or [{}])[0]
print(str(round((it.get('achievement') or 0)*100))+'|'+str(d.get('studentCount'))+'|'+str(it.get('knowledgeCount')))")"
ACH_PCT="${ACH%%|*}"; REST="${ACH#*|}"; ACH_STUDENTS="${REST%%|*}"; ACH_KL="${REST##*|}"
expect_ge "$ACH_PCT" 1 "能力达成度（%，应 > 0，因为已有作答证据）"
expect_ge "$ACH_STUDENTS" 1 "参与统计的学生数"
expect_eq "$ACH_KL" "1" "关联知识点数"
info "能力达成度=${ACH_PCT}%（只统计有证据的知识点，无证据不按 0 分拉低）"

api_ok GET "/api/app/competency/student-achievement?courseId=$COURSE_ID&studentId=$STUDENT_ID" "$TEACHER_TOKEN"
expect_ge "$(python3 -c "
import json;d=json.load(open('$WORK/resp.json'));print(str(round((d.get('overallAchievement') or 0)*100)))")" 1 "学生能力达成度（%）"

step "P2-2 知识点映射接口（KP → 课程知识点树）"
# 本课没有「智学分析知识点」（未走试卷采集），所以期望 0 条，但接口必须可用且自洽
api_ok GET "/api/app/knowledge-point/resource-mapping/$COURSE_ID" "$TEACHER_TOKEN"
MAP_TOTAL="$(jget totalCount)"; MAP_MAPPED="$(jget mappedCount)"
expect_ge "$MAP_TOTAL" 0 "映射条目数"
expect_eq "$MAP_MAPPED" "0" "已映射数（无 KP 时为 0）"
api_ok POST "/api/app/knowledge-point/auto-map-resources/$COURSE_ID" "$TEACHER_TOKEN"
expect_ge "$(jget scannedCount)" 0 "自动映射扫描数"

step "P2-3 知识点 xlsx 导入（模板 → 导入 → 幂等重导）"
# 用「下载的模板本身」当输入：模板里自带 2 行示例，正好验证解析 + 层级 + 幂等
api_file "$WORK/kp-template.xlsx" "$API_BASE/api/app/knowledge-resource/import-template" \
  -H "Authorization: Bearer $TEACHER_TOKEN" -H "Abp-TenantId: $TENANT_ID"
expect_eq "$LAST_CODE" "200" "模板下载"
expect_file_magic "$WORK/kp-template.xlsx" "PK" "模板是合法 xlsx（zip 魔数 PK）"

api_file "$WORK/import1.json" -X POST \
  "$API_BASE/api/app/knowledge-resource/import-from-excel/$COURSE_ID" \
  -H "Authorization: Bearer $TEACHER_TOKEN" -H "Abp-Tenant-Id: $TENANT_ID" \
  -F "file=@$WORK/kp-template.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
case "$LAST_CODE" in 2*) : ;; *) die "知识点导入 → HTTP $LAST_CODE" ;; esac
cp "$WORK/import1.json" "$WORK/resp.json"
IMP_UNITS="$(jget createdUnits)"; IMP_ITEMS="$(jget createdItems)"
expect_ge "$IMP_UNITS" 1 "导入创建单元数"
expect_ge "$IMP_ITEMS" 1 "导入创建知识项数"
expect_ge "$(jget sortPathsRegenerated)" 1 "导入后重算路径条数"
info "首次导入：单元 ${IMP_UNITS} / 知识项 ${IMP_ITEMS}"

# 幂等：同一份文件再导一次，不应新增节点
api_file "$WORK/import2.json" -X POST \
  "$API_BASE/api/app/knowledge-resource/import-from-excel/$COURSE_ID" \
  -H "Authorization: Bearer $TEACHER_TOKEN" -H "Abp-Tenant-Id: $TENANT_ID" \
  -F "file=@$WORK/kp-template.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
case "$LAST_CODE" in 2*) : ;; *) die "重复导入 → HTTP $LAST_CODE" ;; esac
cp "$WORK/import2.json" "$WORK/resp.json"
expect_eq "$(jget createdTotal)" "0" "重复导入新增数（应为 0）"
expect_ge "$(jget reusedCount)" 1 "重复导入复用数"

step "P2-4 岗位图谱（以就业模块的真实岗位为锚 → 任务 → 能力 → 课程）"
# ① 先建一个真实招聘岗位（图谱不再手填岗位名，而是挂在岗位库里的岗位上）
api_ok POST /api/app/employment/job "$TEACHER_TOKEN" "{
  \"companyName\": \"E2E科技有限公司\",
  \"industry\": \"软件和信息技术服务业\",
  \"title\": \"E2E岗位-${RUN_TAG}\",
  \"description\": \"回归用岗位\",
  \"salaryRange\": \"10-15K\",
  \"location\": \"杭州\",
  \"skillTags\": \"Java,MySQL\",
  \"recruitmentCount\": 1
}"
JOB_POSTING_ID="$(jget id)"
expect_nonempty "$JOB_POSTING_ID" "jobPostingId"

# ② 从该岗位创建图谱（幂等：再调一次返回同一个）
api_ok POST "/api/app/job-competency-graph/from-job/$JOB_POSTING_ID" "$TEACHER_TOKEN"
JOB_GRAPH_ID="$(jget id)"
expect_nonempty "$JOB_GRAPH_ID" "jobGraphId"
expect_eq "$(jget jobPostingId)" "$JOB_POSTING_ID" "图谱挂到了岗位"
expect_eq "$(python3 -c "
import json;d=json.load(open('$WORK/resp.json'));print((d.get('job') or {}).get('companyName'))" 2>/dev/null)" "E2E科技有限公司" "岗位公司信息已连通"

api_ok POST "/api/app/job-competency-graph/from-job/$JOB_POSTING_ID" "$TEACHER_TOKEN"
expect_eq "$(jget id)" "$JOB_GRAPH_ID" "重复从岗位创建为幂等"


api_ok POST /api/app/job-competency-graph/task "$TEACHER_TOKEN" "{
  \"graphId\": \"$JOB_GRAPH_ID\",
  \"title\": \"完成一次闭环回归\",
  \"weight\": 0.6,
  \"sortOrder\": 0
}"
JOB_TASK_ID="$(jget id)"
expect_nonempty "$JOB_TASK_ID" "核心任务 id"

# 复用 P2-1 建的能力项（它已关联 ITEM1，所以可测率应为 100%）
api_ok POST /api/app/job-competency-graph/link-competency "$TEACHER_TOKEN" "{
  \"coreTaskId\": \"$JOB_TASK_ID\",
  \"competencyId\": \"$COMPETENCY_ID\",
  \"weight\": 0.9,
  \"requiredLevel\": 2
}"
expect_eq "$(jget requiredLevel)" "2" "要求等级"
expect_ge "$(jget knowledgeCount)" 1 "能力的知识点数（复用能力体系的关联）"

api_ok GET "/api/app/job-competency-graph/$JOB_GRAPH_ID/coverage?courseId=$COURSE_ID" "$TEACHER_TOKEN"
COV="$(python3 -c "
import json;d=json.load(open('$WORK/resp.json'))
print(str(d.get('taskCount'))+'|'+str(d.get('competencyCount'))+'|'+str(d.get('measurableCompetencyCount')))")"
expect_eq "$COV" "1|1|1" "覆盖度（任务|能力|可测能力）"

# 「岗位 → 课程」：P2-1 已把本课程设为该能力的支撑，详情里应能看到这门课
api_ok GET "/api/app/job-competency-graph/$JOB_GRAPH_ID/detail?courseId=$COURSE_ID" "$TEACHER_TOKEN"
expect_ge "$(jget courseCount)" 1 "支撑本岗位能力的课程数"
COURSE_HIT="$(python3 -c "
import json
d=json.load(open('$WORK/resp.json'))
hits=[c['courseTitle'] for c in (d.get('courses') or []) if c['courseId']=='$COURSE_ID']
print(len(hits))")"
expect_eq "$COURSE_HIT" "1" "岗位 → 课程 连通（回归课程出现在支撑课程里）"
COMP_COURSE="$(python3 -c "
import json
d=json.load(open('$WORK/resp.json'))
n=0
for t in d.get('tasks') or []:
    for c in t.get('competencies') or []:
        n += sum(1 for x in (c.get('courses') or []) if x['courseId']=='$COURSE_ID')
print(n)")"
expect_ge "$COMP_COURSE" 1 "能力维度也标出了支撑课程"
ACH="$(python3 -c "
import json;d=json.load(open('$WORK/resp.json'))
v=d.get('overallAchievement'); print('' if v is None else str(round(v*100)))")"
expect_ge "$ACH" 1 "岗位能力整体达成度（%）"

step "P2-5 学习画像 + 数据洞察 + 报告导出"
api_ok GET "/api/app/learning-profile/course-profile/$COURSE_ID?days=30" "$TEACHER_TOKEN"
PROFILE_STUDENTS="$(jget studentCount)"
expect_ge "$PROFILE_STUDENTS" 1 "课程画像：学生数"
expect_ge "$(jget activeStudentCount)" 1 "课程画像：活跃学生数（前面已有作答行为）"

api_ok GET "/api/app/learning-profile/course-insight/$COURSE_ID?days=30" "$TEACHER_TOKEN"
expect_eq "$(jlen metrics)" "4" "数据洞察指标卡数量"
expect_ge "$(jlen findings)" 1 "自动结论条数"

api_ok POST "/api/app/learning-profile/build-report/$COURSE_ID?template=monthly&days=30" "$TEACHER_TOKEN"
expect_ge "$(jlen sections)" 3 "报告小节数"

api_file "$WORK/profile-report.xlsx" -X POST \
  "$API_BASE/api/app/learning-profile/export-report/$COURSE_ID?template=monthly&days=30" \
  -H "Authorization: Bearer $TEACHER_TOKEN" -H "Abp-Tenant-Id: $TENANT_ID"
expect_eq "$LAST_CODE" "200" "报告导出 xlsx"
expect_file_magic "$WORK/profile-report.xlsx" "PK" "报告是合法 xlsx（zip 魔数 PK）"

step "P2-6 学习推荐（生成 → 学生完成 → 教师采纳率）"
api_ok POST "/api/app/student-recommendation/refresh-mine/$COURSE_ID" "$STUDENT_TOKEN"
expect_ge "$(jget createdCount)" 1 "学生端生成推荐条数"

api_ok GET "/api/app/student-recommendation/my?courseId=$COURSE_ID" "$STUDENT_TOKEN"
REC_COUNT="$(jlen)"
expect_ge "$REC_COUNT" 1 "我的推荐条数"
REC_ID="$(jget 0.id)"
expect_nonempty "$REC_ID" "第一条推荐 id"

api_ok POST "/api/app/student-recommendation/$REC_ID/mark?status=3" "$STUDENT_TOKEN"
expect_eq "$(jget statusLabel)" "已完成" "学生标记完成"

api_ok GET "/api/app/student-recommendation/course-overview/$COURSE_ID" "$TEACHER_TOKEN"
expect_ge "$(jget completedCount)" 1 "课程已完成推荐数"
ADOPT="$(python3 -c "
import json;d=json.load(open('$WORK/resp.json'));print(str(round((d.get('adoptRate') or 0)*100)))")"
expect_ge "$ADOPT" 1 "采纳率（%）"

# ══════════════════════════════════════════════════════════════
#  B3
# ══════════════════════════════════════════════════════════════
if [ "$SKIP_VIDEO" = "1" ]; then
  warn "SKIP_VIDEO=1：跳过视频上传与流式播放"
else
  step "B3-1 课程视频：上传 → Range 播放 → 打点 → 时长回填"
  command -v ffmpeg >/dev/null 2>&1 || die "需要 ffmpeg 生成测试视频（或 SKIP_VIDEO=1）"
  ffmpeg -y -f lavfi -i "testsrc=size=320x240:rate=10:duration=4" \
    -pix_fmt yuv420p -movflags +faststart "$WORK/test.mp4" >/dev/null 2>&1 || die "ffmpeg 生成测试视频失败"

  api_file "$WORK/upload.json" -X POST \
    "$API_BASE/api/app/course-video/upload?courseId=$COURSE_ID&title=%E5%9B%9E%E5%BD%92%E8%A7%86%E9%A2%91&sortOrder=0" \
    -H "Authorization: Bearer $TEACHER_TOKEN" -H "Abp-TenantId: $TENANT_ID" \
    -F "file=@$WORK/test.mp4;type=video/mp4"
  case "$LAST_CODE" in 2*) : ;; *) die "上传视频 → HTTP $LAST_CODE" ;; esac
  cp "$WORK/upload.json" "$WORK/resp.json"
  VIDEO_ID="$(jget id)"
  expect_nonempty "$VIDEO_ID" "videoId"
  expect_eq "$(jget playUrl)" "/api/course-video/$VIDEO_ID/stream" "playUrl"

  # 普通播放（学生）
  api_file "$WORK/v1.bin" "$API_BASE/api/course-video/$VIDEO_ID/stream" \
    -H "Authorization: Bearer $STUDENT_TOKEN" -H "Abp-TenantId: $TENANT_ID"
  expect_eq "$LAST_CODE" "200" "学生播放"
  expect_ge "$(wc -c < "$WORK/v1.bin" | tr -d ' ')" 1000 "视频字节数"

  # Range 播放（应 206）
  LAST_CODE="$(curl -s $CURL_SSL_ARGS -o "$WORK/v2.bin" -D "$WORK/v2.hdr" -w '%{http_code}' \
    -H "Range: bytes=0-1023" -H "Authorization: Bearer $STUDENT_TOKEN" -H "Abp-TenantId: $TENANT_ID" \
    "$API_BASE/api/course-video/$VIDEO_ID/stream")"
  expect_eq "$LAST_CODE" "206" "Range 请求返回 206"
  if grep -qi 'accept-ranges: bytes' "$WORK/v2.hdr"; then pass "响应含 accept-ranges: bytes"; else die "缺少 accept-ranges 头"; fi

  # 匿名 → 401
  LAST_CODE="$(curl -s $CURL_SSL_ARGS -o /dev/null -w '%{http_code}' "$API_BASE/api/course-video/$VIDEO_ID/stream")"
  expect_eq "$LAST_CODE" "401" "匿名访问被拒"

  api_ok POST /api/app/course-video/save-mark "$TEACHER_TOKEN" "{\"courseVideoId\": \"$VIDEO_ID\", \"pointSec\": 2, \"label\": \"第 2 秒：开场\", \"knowledgeResourceId\": \"$ITEM1_ID\"}"
  API_MARK_ID="$(jget id)"
  expect_eq "$(jget timeText)" "00:02" "标记时间文案"
  api_ok POST /api/app/course-video/save-mark "$TEACHER_TOKEN" "{\"courseVideoId\": \"$VIDEO_ID\", \"pointSec\": 2, \"label\": \"第 2 秒：开场（改）\"}"
  MARK_ID2="$(jget id)"
  expect_eq "$MARK_ID2" "$API_MARK_ID" "同秒打点为更新（id 不变）"

  api_ok GET "/api/app/course-video/marks/$VIDEO_ID" "$TEACHER_TOKEN"
  expect_eq "$(jlen)" "1" "同秒只留一个标记"

  api_ok PUT "/api/app/course-video/$VIDEO_ID/duration?durationSeconds=4" "$TEACHER_TOKEN"
  expect_eq "$(jget durationSeconds)" "4" "时长回填"
fi

step "B3-2 打印：建卷 → 试卷 PDF → 成绩单 PDF"
[ -n "$EXERCISE_ID" ] || die "缺少练习题，无法建卷"
api_ok POST /api/app/exam "$TEACHER_TOKEN" "{
  \"courseId\": \"$COURSE_ID\",
  \"title\": \"回归试卷-$RUN_TAG\",
  \"description\": \"打印验证\",
  \"type\": 0,
  \"durationMinutes\": 30,
  \"passingScore\": 60,
  \"exercises\": [{\"exerciseId\": \"$EXERCISE_ID\", \"score\": 5}]
}"
EXAM_ID="$(jget id)"
expect_nonempty "$EXAM_ID" "examId"

api_file "$WORK/paper.pdf" -X POST "$API_BASE/api/app/exam/export-paper-pdf/$EXAM_ID" \
  -H "Authorization: Bearer $TEACHER_TOKEN" -H "Abp-TenantId: $TENANT_ID"
expect_eq "$LAST_CODE" "200" "试卷 PDF 生成"
expect_file_magic "$WORK/paper.pdf" "%PDF" "试卷是合法 PDF"

api_file "$WORK/scores.pdf" -X POST "$API_BASE/api/app/exam/export-scores-pdf/$EXAM_ID" \
  -H "Authorization: Bearer $TEACHER_TOKEN" -H "Abp-TenantId: $TENANT_ID"
expect_eq "$LAST_CODE" "200" "成绩单 PDF 生成"
expect_file_magic "$WORK/scores.pdf" "%PDF" "成绩单是合法 PDF"

# ══════════════════════════════════════════════════════════════
step "汇总"
cat <<EOF
  courseId      = $COURSE_ID
  unit/item1/2  = $UNIT_ID / $ITEM1_ID / $ITEM2_ID
  exerciseId    = $EXERCISE_ID
  checkIn/disc  = $CHECKIN_ID / $DISCUSSION_ID
  practice/group= $PRACTICE_ID / $GROUP_TASK_ID
  videoId       = ${VIDEO_ID:-（跳过）}
  competencyId  = ${COMPETENCY_ID:-（未创建）}
  jobGraphId    = ${JOB_GRAPH_ID:-（未创建）}
  examId        = $EXAM_ID
  RUN_TAG       = $RUN_TAG
EOF

# 无论 KEEP 与否都写 .last-run：否则默认跑完就再也定位不到这批产物了
printf '%s\n' "$RUN_TAG|$COURSE_TITLE|$COURSE_ID|$VIDEO_ID|$EXAM_ID" > "$SCRIPT_DIR/.last-run"
printf '  已写入 %s/.last-run（cleanup.sh 会读它）\n' "$SCRIPT_DIR"

if [ "${AUTO_CLEANUP:-0}" = "1" ]; then
  warn "AUTO_CLEANUP=1：开始自动清理本次产物"
  "$SCRIPT_DIR/cleanup.sh" || warn "自动清理未完全成功，可手工重试 ./cleanup.sh"
elif [ "$KEEP" = "1" ]; then
  warn "KEEP=1：数据保留（可用 ./verify-db.sh 交叉复核，再 ./cleanup.sh 清理）"
fi

printf '\n%s✓ B1–B3 整体回归全部通过%s\n' "$C_GREEN$C_BOLD" "$C_OFF"
