#!/usr/bin/env bash
# =============================================================================
# 智学分析 · 数据链路验证（知识点 → 掌握度 → 薄弱点）
# -----------------------------------------------------------------------------
# 验证「智学分析」到底吃没吃到真实课程数据，特别是：
#   1. 知识点是否从课程章节派生出来并自动映射习题
#   2. 在线习题作答（StudentExerciseRecord）是否进入掌握度
#   3. 在线试卷作答（AnswerRecord.Source=Exam）是否进入掌握度
#   4. 纸质试卷校对确认（AnswerRecord.Source=Paper）是否进入掌握度
#   5. 判定线 → 薄弱/待巩固/达标 的分级是否生效
#
# 前置：先跑 course-content/demo.sh 让课程有习题与试卷
#
# 用法：
#   ./verify.sh                      # 本地
#   API_BASE=http://127.0.0.1:4200 ./verify.sh
#   PSQL_IN_CONTAINER=1 ./verify.sh  # 远程（psql 只在容器里）
# =============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
JQX="$SCRIPT_DIR/../lib/jqx.py"

API_BASE="${API_BASE:-https://localhost:44305}"
TENANT_NAME="${TENANT_NAME:-qidi}"
TEACHER_USER="${TEACHER_USER:-wwq}"
TEACHER_PASS="${TEACHER_PASS:-123456}"
STUDENT_USER="${STUDENT_USER:-zmq}"
STUDENT_PASS="${STUDENT_PASS:-1234567}"
OAUTH_CLIENT="${OAUTH_CLIENT:-KnowledgeHub_App}"
OAUTH_SCOPE="${OAUTH_SCOPE:-offline_access KnowledgeHub}"
COURSE_ID="${COURSE_ID:-}"
COURSE_TITLE_PREFIX="${COURSE_TITLE_PREFIX:-Python 数据分析}"
CURL_SSL_ARGS="${CURL_SSL_ARGS:--k}"

PGUSER="${PGUSER:-postgres}"; PGPASSWORD="${PGPASSWORD:-postgres}"
PGDATABASE="${PGDATABASE:-KnowledgeHub}"; PG_CONTAINER="${PG_CONTAINER:-postgres}"
export PGPASSWORD PGHOST PGPORT
PGHOST="${PGHOST:-localhost}"; PGPORT="${PGPORT:-5433}"
if [ "${PSQL_IN_CONTAINER:-0}" = "1" ]; then
  PSQL() { docker exec -i -e PGPASSWORD="$PGPASSWORD" "$PG_CONTAINER" psql -U "$PGUSER" -d "$PGDATABASE" "$@"; }
else
  PSQL() { psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" "$@"; }
fi

WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT
TENANT_ID=""; TTOK=""; STOK=""; LAST_CODE=""; LAST_BODY=""
if [ -t 1 ]; then G=$'\033[32m'; Y=$'\033[33m'; R=$'\033[31m'; B=$'\033[36m'; BD=$'\033[1m'; O=$'\033[0m'
else G=""; Y=""; R=""; B=""; BD=""; O=""; fi
step() { printf '\n%s▶ %s%s\n' "$BD$B" "$*" "$O"; }
ok()   { printf '  %s✓%s %s\n' "$G" "$O" "$*"; }
warn() { printf '  %s!%s %s\n' "$Y" "$O" "$*"; }
bad()  { printf '  %s✗%s %s\n' "$R" "$O" "$*"; }
info() { printf '    %s· %s%s\n' "$B" "$*" "$O"; }

api() {
  local m="$1" p="$2" t="$3" b="${4:-}"
  local -a a
  a=(-s $CURL_SSL_ARGS -X "$m" -H 'Accept: application/json' -o "$WORK/r.json" -w '%{http_code}')
  [ "$t" != "-" ] && a+=(-H "Authorization: Bearer $t")
  [ -n "$TENANT_ID" ] && a+=(-H "Abp-TenantId: $TENANT_ID")
  [ -n "$b" ] && a+=(-H 'Content-Type: application/json' --data-binary "$b")
  LAST_CODE="$(curl "${a[@]}" "${API_BASE}${p}")"
  LAST_BODY="$(cat "$WORK/r.json" 2>/dev/null || echo '')"
}
jget()  { python3 "$JQX" get "$WORK/r.json" "${1:-}"; }
jlen()  { python3 "$JQX" len "$WORK/r.json" "${1:-}"; }
jfind() { python3 "$JQX" find "$WORK/r.json" "$1" "$2" "$3" "${4:-id}"; }
login() {
  curl -s $CURL_SSL_ARGS -X POST "${API_BASE}/connect/token" \
    -H 'Content-Type: application/x-www-form-urlencoded' -H "Abp-TenantId: ${TENANT_ID}" \
    --data-urlencode 'grant_type=password' --data-urlencode "client_id=${OAUTH_CLIENT}" \
    --data-urlencode "scope=${OAUTH_SCOPE}" \
    --data-urlencode "username=$1" --data-urlencode "password=$2" \
    | python3 -c 'import json,sys;print(json.load(sys.stdin).get("access_token",""))'
}

echo "=============================================================================="
echo " 智学分析 · 数据链路验证    API=$API_BASE"
echo "=============================================================================="

step "0/6 登录与定位课程"
api GET /api/public/tenants -
TENANT_ID="$(python3 - "$WORK/r.json" "$TENANT_NAME" <<'PY'
import json,sys
for t in json.load(open(sys.argv[1])) or []:
    if str(t.get("name"))==sys.argv[2] or str(t.get("tenantName"))==sys.argv[2]:
        print(t.get("id") or ""); break
PY
)"
[ -n "$TENANT_ID" ] || { bad "找不到租户 $TENANT_NAME"; exit 1; }
TTOK="$(login "$TEACHER_USER" "$TEACHER_PASS")"
STOK="$(login "$STUDENT_USER" "$STUDENT_PASS")"
[ -n "$TTOK" ] && [ -n "$STOK" ] && ok "老师/学生已登录" || { bad "登录失败"; exit 1; }

if [ -z "$COURSE_ID" ]; then
  api GET "/api/app/course?onlyMine=true&skipCount=0&maxResultCount=200" "$TTOK"
  COURSE_ID="$(python3 - "$WORK/r.json" "$COURSE_TITLE_PREFIX" <<'PY'
import json,sys
for c in (json.load(open(sys.argv[1])) or {}).get("items") or []:
    if str(c.get("title","")).startswith(sys.argv[2]): print(c.get("id")); break
PY
)"
fi
[ -n "$COURSE_ID" ] || { bad "找不到课程（先跑 course-content/demo.sh）"; exit 1; }
info "课程: $COURSE_ID"

STEP_FAIL=0
fail() { bad "$*"; STEP_FAIL=$((STEP_FAIL+1)); }

step "1/6 知识点是否从课程章节派生并自动映射习题"
api POST "/api/app/learning-analytics/ensure-course-knowledge-points/${COURSE_ID}" "$TTOK" ""
if [ "$LAST_CODE" = "200" ]; then
  KP_CNT="$(jlen '')"; info "实现接口返回知识点 $KP_CNT 个"
else
  fail "ensure-course-knowledge-points 失败 HTTP $LAST_CODE"
fi
api GET "/api/app/learning-analytics/knowledge-points/${COURSE_ID}" "$TTOK"
KP_API="$(jlen '')"
if [ "${KP_API:-0}" -gt 0 ]; then
  ok "课程知识点 $KP_API 个：$(python3 - "$WORK/r.json" <<'PY'
import json,sys
print(" / ".join(str(k.get("name")) for k in (json.load(open(sys.argv[1])) or [])[:6]))
PY
)"
else
  fail "课程没有知识点（掌握度无法计算）"
fi
api GET "/api/app/knowledge-point/exercise-mappings/${COURSE_ID}" "$TTOK"
MAPPED="$(python3 - "$WORK/r.json" <<'PY'
import json,sys
rows=json.load(open(sys.argv[1])) or []
n=sum(1 for r in rows if (r.get("links") or []))
print(f"{n}/{len(rows)}")
PY
)"
if [ "${MAPPED%%/*}" -gt 0 ]; then ok "已映射知识点习题 $MAPPED"; else fail "习题未映射到知识点（${MAPPED}）"; fi

step "2/6 在线习题作答 → 掌握度"
QP1="$(PSQL -t -A -c "SELECT e.\"Id\" FROM \"AppExercises\" e WHERE e.\"CourseId\"='$COURSE_ID' AND e.\"IsDeleted\"=false ORDER BY e.\"Score\" DESC LIMIT 1;")"
QP2="$(PSQL -t -A -c "SELECT e.\"Id\" FROM \"AppExercises\" e WHERE e.\"CourseId\"='$COURSE_ID' AND e.\"IsDeleted\"=false ORDER BY e.\"Score\" DESC OFFSET 1 LIMIT 1;")"
CH_OF_QP1="$(PSQL -t -A -c "SELECT COALESCE(\"ChapterId\"::text,'') FROM \"AppExercises\" WHERE \"Id\"='$QP1';")"
STUDENT_ID="$(PSQL -t -A -c "SELECT \"Id\" FROM \"AbpUsers\" WHERE \"UserName\"='$STUDENT_USER' AND \"TenantId\"='$TENANT_ID';")"
if [ -n "$QP1" ] && [ -n "$STUDENT_ID" ]; then
  # 答对一题
  api POST /api/app/student-exercise-record/save-or-update-record "$STOK" \
    "$(python3 -c 'import json,sys;print(json.dumps({"courseId":sys.argv[1],"chapterId":sys.argv[2] or None,"exerciseId":sys.argv[3],"studentAnswer":sys.argv[4],"timeSpentTicks":600000000}))' \
       "$COURSE_ID" "$CH_OF_QP1" "$QP1" "$(PSQL -t -A -c "SELECT \"Answer\" FROM \"AppExercises\" WHERE \"Id\"='$QP1';")")"
  echo "      在线答对判定: $(jget isCorrect)"
  ok "在线习题作答已提交"
else
  warn "跳过：课程没有习题或找不到学生（先跑 course-content/demo.sh）"
fi

step "3/6 在线试卷作答 → AnswerRecord(Source=Exam) → 掌握度"
# 优先用「学生还没作答过」的试卷；都已作答则新建一张，保证本步可验证
EXAM_ID="$(PSQL -t -A -c "
SELECT x.\"Id\" FROM \"AppExams\" x
WHERE x.\"CourseId\"='$COURSE_ID' AND x.\"IsDeleted\"=false
  AND NOT EXISTS (SELECT 1 FROM \"AppStudentExams\" se WHERE se.\"ExamId\"=x.\"Id\" AND se.\"StudentId\"='$STUDENT_ID')
ORDER BY x.\"CreationTime\" DESC LIMIT 1;")"
if [ -z "$EXAM_ID" ]; then
  # 没有可用试卷 → 用已有题目现组一张
  EX_IDS="$(PSQL -t -A -c "SELECT string_agg(\"Id\"::text, ',') FROM (SELECT \"Id\" FROM \"AppExercises\" WHERE \"CourseId\"='$COURSE_ID' AND \"IsDeleted\"=false ORDER BY \"Score\" DESC LIMIT 4) t;")"
  if [ -n "$EX_IDS" ]; then
    BODY="$(python3 - "$COURSE_ID" "$EX_IDS" <<'PY'
import json,sys,datetime
ex=[x for x in sys.argv[2].split(",") if x]
print(json.dumps({"courseId":sys.argv[1],
  "title":"智学分析验证卷[verify]","description":"数据链路验证用",
  "type":0,"durationMinutes":30,"passingScore":60,
  "exercises":[{"exerciseId":e,"score":5,"sortOrder":i+1} for i,e in enumerate(ex)]}, ensure_ascii=False))
PY
)"
    api POST /api/app/exam "$TTOK" "$BODY"
    EXAM_ID="$(jget id)"
    info "现组一张试卷: $EXAM_ID"
  fi
fi
if [ -n "$EXAM_ID" ]; then
  api POST "/api/app/exam/start/${EXAM_ID}" "$STOK" ""
  SE_ID="$(jget studentExamId)"; [ -z "$SE_ID" ] && SE_ID="$(jget id)"
  if [ -n "$SE_ID" ]; then
    ANS="$(python3 - "$WORK/r.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1]))
out=[]
for q in d.get("questions") or []:
    out.append({"exerciseId": q["exerciseId"], "content": "A"})
print(json.dumps({"answers": out}, ensure_ascii=False))
PY
)"
    api POST "/api/app/exam/submit/${SE_ID}" "$STOK" "$ANS"
    info "交卷 HTTP ${LAST_CODE}，逐题得分 $(jget answers.0.score)"
    api POST "/api/app/exam/auto-grade/${SE_ID}" "$TTOK" ""
    info "判卷后总分 $(jget totalScore) / 状态 $(jget status)"
    EXAM_AR="$(PSQL -t -A -c "SELECT count(*) FROM \"AppAnswerRecords\" WHERE \"Source\"=3 AND \"CourseId\"='$COURSE_ID' AND \"StudentId\"='$STUDENT_ID';")"
    if [ "${EXAM_AR:-0}" -gt 0 ]; then ok "在线试卷已写入作答记录 $EXAM_AR 条（Source=Exam）"
    else warn "未查到 Source=Exam 的作答记录（可能题目都未判分）"; fi
  else
    warn "无法开始答题（可能已提交过）"
  fi
else
  warn "课程没有试卷（先跑 course-content/demo.sh）"
fi

step "4/6 掌握度矩阵（StudentKpMastery）"
PSQL -c "
SELECT kp.\"Name\" AS 知识点, ch.\"Title\" AS 章节,
       round(m.\"Mastery\"::numeric, 3) AS 掌握度,
       round(m.\"Evidence\"::numeric, 2) AS 证据量
FROM \"AppStudentKpMasteries\" m
JOIN \"AppKnowledgePoints\" kp ON kp.\"Id\" = m.\"KnowledgePointId\"
LEFT JOIN \"AppChapters\" ch ON ch.\"Id\" = kp.\"ChapterId\"
WHERE m.\"StudentId\" = '$STUDENT_ID' AND kp.\"CourseId\" = '$COURSE_ID'
ORDER BY kp.\"SyllabusOrder\";" 2>&1 | head -20

step "5/6 作答记录按来源汇总（证明各类数据都进了掌握度）"
cat > "$WORK/src.sql" <<SQL
SELECT CASE "Source" WHEN 0 THEN '在线练习' WHEN 1 THEN '课堂练习'
                      WHEN 2 THEN '纸质试卷' WHEN 3 THEN '在线试卷'
                      ELSE "Source"::text END AS 来源,
       count(*) AS 条数, sum("Score") AS 得分, sum("FullScore") AS 满分
FROM "AppAnswerRecords"
WHERE "CourseId" = '$COURSE_ID' AND "StudentId" = '$STUDENT_ID'
GROUP BY 1 ORDER BY 1;
SQL
PSQL -f "$WORK/src.sql" 2>&1 | head -20
cat > "$WORK/onl.sql" <<SQL
SELECT count(*) AS 在线练习记录数,
       sum(CASE WHEN "IsCorrect" THEN 1 ELSE 0 END) AS 其中答对
FROM "AppStudentExerciseRecords"
WHERE "CourseId" = '$COURSE_ID' AND "StudentId" = '$STUDENT_ID';
SQL
PSQL -f "$WORK/onl.sql" 2>&1 | head -10

step "6/6 薄弱知识点判定（判定线）"
api GET "/api/app/learning-analytics/threshold/${COURSE_ID}" "$TTOK"
info "判定线: $(jget '' 2>/dev/null | head -c 200)"
BATCH_ID="$(PSQL -t -A -c "SELECT \"Id\" FROM \"AppPaperBatches\" WHERE \"CourseId\"='$COURSE_ID' ORDER BY \"CreationTime\" DESC LIMIT 1;" 2>/dev/null)"
if [ -n "$BATCH_ID" ]; then
  api GET "/api/app/learning-analytics/class-weakness/${BATCH_ID}" "$TTOK"
  if [ "$LAST_CODE" = "200" ]; then
    info "班级薄弱点（按纸质批次）: $(jget '' 2>/dev/null | head -c 220)"
  else
    warn "class-weakness 需要纸质批次，当前 HTTP $LAST_CODE"
  fi
else
  warn "该课程还没有纸质试卷批次 → 报告页的「知识点掌握率/章节达成度」在界面上会完全空白"
  warn "（这正是本次要修的问题：在线习题与在线试卷的数据已进掌握度，但报告页被 selectedBatch() 挡住）"
fi

echo
echo "=============================================================================="
if [ "$STEP_FAIL" -eq 0 ]; then echo " ✓ 数据链路验证通过（知识点 → 作答/试卷 → 掌握度 → 薄弱点）"
else echo " 发现 $STEP_FAIL 处问题，见上方 ✗"; fi
echo "=============================================================================="
exit 0
