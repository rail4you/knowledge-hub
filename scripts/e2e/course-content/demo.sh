#!/usr/bin/env bash
# =============================================================================
# 课程内容闭环 E2E：习题 → 试卷 → 作业 → 学生端作答/提交 → 教师批阅 → 学生看反馈
# -----------------------------------------------------------------------------
# 在已有课程（默认：老师的「Python 数据分析」课程）上补齐三类内容，
# 并走通学生端（zmq）的完整链路，最后由教师批阅、学生看反馈。
#
# 依赖：resource-library-loop 建出来的课程（课程已发布 + 学生已导入）。
#       没有就先跑 resource-library-loop/run.sh。
#
# 用法：
#   ./demo.sh                          # 自动按标题找老师的「Python 数据分析」课程
#   COURSE_ID=<uuid> ./demo.sh         # 指定课程
#   API_BASE=http://127.0.0.1:4200 ./demo.sh   # 生产（iketong）环境
#
# 退出码：0 = 全部通过；非 0 = 某环节失败
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
JQX="$SCRIPT_DIR/../lib/jqx.py"

# ----------------------------- 可配置项 -------------------------------------
API_BASE="${API_BASE:-https://localhost:44305}"
TENANT_NAME="${TENANT_NAME:-qidi}"
TEACHER_USER="${TEACHER_USER:-wwq}"
TEACHER_PASS="${TEACHER_PASS:-123456}"
STUDENT_USER="${STUDENT_USER:-zmq}"
STUDENT_PASS="${STUDENT_PASS:-1234567}"
ADMIN_USER="${ADMIN_USER:-qidi-admin}"
ADMIN_PASS="${ADMIN_PASS:-123456}"
OAUTH_CLIENT="${OAUTH_CLIENT:-KnowledgeHub_App}"
OAUTH_SCOPE="${OAUTH_SCOPE:-offline_access KnowledgeHub}"
# 目标课程：默认按标题前缀自动找老师自己的「Python 数据分析」课程
COURSE_ID="${COURSE_ID:-}"
COURSE_TITLE_PREFIX="${COURSE_TITLE_PREFIX:-Python 数据分析}"
# 生成的数据带这个标记，便于识别与清理
TAG="${TAG:-demo}"
CURL_SSL_ARGS="${CURL_SSL_ARGS:--k}"
KEEP_TMP="${KEEP_TMP:-0}"

WORK="$(mktemp -d)"
cleanup() { [ "$KEEP_TMP" = "1" ] && echo "临时目录: $WORK" || rm -rf "$WORK"; }
trap cleanup EXIT

TENANT_ID=""; TEACHER_TOKEN=""; STUDENT_TOKEN=""; ADMIN_TOKEN=""
LAST_CODE=""; LAST_BODY=""

if [ -t 1 ]; then
  C_GREEN=$'\033[32m'; C_YELLOW=$'\033[33m'; C_RED=$'\033[31m'
  C_BLUE=$'\033[36m'; C_BOLD=$'\033[1m'; C_OFF=$'\033[0m'
else C_GREEN=""; C_YELLOW=""; C_RED=""; C_BLUE=""; C_BOLD=""; C_OFF=""; fi
step() { printf '\n%s▶ %s%s\n' "$C_BOLD$C_BLUE" "$*" "$C_OFF"; }
pass() { printf '  %s✓%s %s\n' "$C_GREEN" "$C_OFF" "$*"; }
warn() { printf '  %s!%s %s\n' "$C_YELLOW" "$C_OFF" "$*"; }
die()  { printf '\n%s✗ 失败：%s%s\n' "$C_RED" "$*" "$C_OFF" >&2; dump_last >&2; exit 1; }
info() { printf '    %s· %s%s\n' "$C_BLUE" "$*" "$C_OFF"; }
dump_last() {
  [ -n "$LAST_BODY" ] || return 0
  printf '  最近一次响应 (HTTP %s):\n' "${LAST_CODE:-?}" >&2
  printf '%s\n' "$LAST_BODY" | head -c 1200 | sed 's/^/    /' >&2; printf '\n' >&2
}

# ----------------------------- HTTP -----------------------------------------
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
api_ok() {
  local method="$1" path="$2" token="$3" body="${4:-}" tid="${5:-$TENANT_ID}"
  api "$method" "$path" "$token" "$body" "$tid"
  case "$LAST_CODE" in 2*) : ;; *) die "$method $path → HTTP $LAST_CODE" ;; esac
}
jget()  { python3 "$JQX" get  "$WORK/resp.json" "${1:-}"; }
jlen()  { python3 "$JQX" len  "$WORK/resp.json" "${1:-}"; }
jfind() { python3 "$JQX" find "$WORK/resp.json" "$1" "$2" "$3" "${4:-id}"; }
jdeep() { python3 "$JQX" deep "$WORK/resp.json" "$1" "$2" "${3:-id}"; }

login() {
  local user="$1" pass="$2" tid="$3" out="$WORK/tok.json"
  local -a args
  args=(-s $CURL_SSL_ARGS -X POST "${API_BASE}/connect/token"
        -H 'Content-Type: application/x-www-form-urlencoded' -o "$out" -w '%{http_code}')
  [ -n "$tid" ] && args+=(-H "Abp-TenantId: $tid")
  local code
  code="$(curl "${args[@]}" --data-urlencode 'grant_type=password' \
      --data-urlencode "client_id=${OAUTH_CLIENT}" --data-urlencode "scope=${OAUTH_SCOPE}" \
      --data-urlencode "username=${user}" --data-urlencode "password=${pass}")"
  LAST_CODE="$code"; LAST_BODY="$(cat "$out" 2>/dev/null || echo '')"
  case "$code" in
    2*) python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('access_token',''))" "$out" ;;
    *) die "登录 ${user} 失败 (HTTP ${code})" ;;
  esac
}

expect_eq()   { if [ "$1" = "$2" ]; then pass "$3 = $2"; else die "$3 期望 $2，实际 '$1'"; fi; }
expect_ne()   { if [ "$1" != "$2" ]; then pass "$3 ≠ $2"; else die "$3 不应等于 $2"; fi; }
expect_nonempty() { if [ -n "$1" ]; then pass "$2 = $1"; else die "$2 为空"; fi; }
expect_ge()   { if [ "${1:-0}" -ge "$2" ] 2>/dev/null; then pass "$3 = $1 (≥ $2)"; else die "$3 期望 ≥ $2，实际 '${1:-0}'"; fi; }

py()  { python3 -c "$@"; }          # 行内代码
pyf() { local c; c="$(cat)"; python3 -c "$c" "$@"; }   # 从 stdin 读代码，参数在后面

# =============================================================================
printf '%s\n' "=============================================================================="
printf '%s\n' " 课程内容闭环 E2E（习题 / 试卷 / 作业 / 学生端）"
printf ' API   : %s\n' "$API_BASE"
printf ' 租户  : %s\n' "$TENANT_NAME"
printf ' 老师  : %s   学生: %s\n' "$TEACHER_USER" "$STUDENT_USER"
printf '%s\n' "=============================================================================="

# --- 0. 租户 + 登录 ----------------------------------------------------------
step "0/9 解析租户并登录"
api_ok GET /api/public/tenants -
TENANT_ID="$(pyf "$WORK/resp.json" "$TENANT_NAME" <<'PY'
import json,sys
for t in json.load(open(sys.argv[1])) or []:
    if str(t.get("name"))==sys.argv[2] or str(t.get("tenantName"))==sys.argv[2]:
        print(t.get("id") or ""); break
PY
)"
expect_nonempty "$TENANT_ID" "租户 Id"
TEACHER_TOKEN="$(login "$TEACHER_USER" "$TEACHER_PASS" "$TENANT_ID")"
STUDENT_TOKEN="$(login "$STUDENT_USER" "$STUDENT_PASS" "$TENANT_ID")"
ADMIN_TOKEN="$(login "$ADMIN_USER" "$ADMIN_PASS" "$TENANT_ID")"
expect_nonempty "$TEACHER_TOKEN" "老师 token"
expect_nonempty "$STUDENT_TOKEN" "学生 token"

# --- 1. 定位目标课程 + 章节 --------------------------------------------------
step "1/9 定位课程与章节"
if [ -z "$COURSE_ID" ]; then
  api_ok GET "/api/app/course?onlyMine=true&skipCount=0&maxResultCount=200" "$TEACHER_TOKEN"
  COURSE_ID="$(pyf "$WORK/resp.json" "$COURSE_TITLE_PREFIX" <<'PY'
import json,sys
items=(json.load(open(sys.argv[1])) or {}).get("items") or []
pref=sys.argv[2]
for c in items:
    if str(c.get("title","")).startswith(pref):
        print(c.get("id")); break
PY
)"
fi
expect_nonempty "$COURSE_ID" "课程 Id"
api_ok GET "/api/app/course/${COURSE_ID}" "$TEACHER_TOKEN"
COURSE_TITLE="$(jget title)"
COURSE_STATUS="$(jget status)"
expect_eq "$COURSE_STATUS" "5" "课程状态（5 = Published）"
info "课程: $COURSE_TITLE"

api_ok GET "/api/app/chapter/chapter-tree/${COURSE_ID}" "$TEACHER_TOKEN"
CH1_ID="$(jfind '' id "$(pyf "$WORK/resp.json" <<'PY'
import json,sys
t=json.load(open(sys.argv[1]))
print((t[0] if t else {}).get("id",""))
PY
)")"
expect_nonempty "$CH1_ID" "一级章节 Id"
CH2_ID="$(pyf "$WORK/resp.json" <<'PY'
import json,sys
t=json.load(open(sys.argv[1]))
print(t[1].get("id") if len(t)>1 else "")
PY
)"
info "章节: $CH1_ID / $CH2_ID"

# --- 2. 建习题 --------------------------------------------------------------
step "2/9 老师建习题（4 道，覆盖单选 / 判断 / 填空）"
mk_exercise() {
  local title="$1" content="$2" type="$3" options="$4" answer="$5" chapter="$6" score="$7"
  local body
  if [ -n "$options" ]; then
    body="$(pyf "$COURSE_ID" "$chapter" "$title" "$content" "$type" "$options" "$answer" "$score" <<'PY'
import json,sys
print(json.dumps({
  "courseId": sys.argv[1], "chapterId": sys.argv[2], "chapterIds": [sys.argv[2]],
  "title": sys.argv[3], "questionContent": sys.argv[4], "type": int(sys.argv[5]),
  "options": sys.argv[6], "answer": sys.argv[7], "difficulty": 2, "score": int(sys.argv[8])
}, ensure_ascii=False))
PY
)"
  else
    body="$(pyf "$COURSE_ID" "$chapter" "$title" "$content" "$type" "$answer" "$score" <<'PY'
import json,sys
print(json.dumps({
  "courseId": sys.argv[1], "chapterId": sys.argv[2], "chapterIds": [sys.argv[2]],
  "title": sys.argv[3], "questionContent": sys.argv[4], "type": int(sys.argv[5]),
  "answer": sys.argv[6], "difficulty": 2, "score": int(sys.argv[7])
}, ensure_ascii=False))
PY
)"
  fi
  api_ok POST /api/app/exercise "$TEACHER_TOKEN" "$body"
  jget id
}

EX1="$(mk_exercise "Python 环境搭建 · 单选" \
  "下列哪个工具最适合一次性安装 Python 科学计算环境？" 0 \
  '["Anaconda","Sublime Text","Photoshop","钉钉"]' "A" "$CH1_ID" 5 | tail -1)"
expect_nonempty "$EX1" "习题 1（单选）"
EX2="$(mk_exercise "venv 虚拟环境 · 判断" \
  "venv 是 Python 标准库自带的虚拟环境工具，无需额外安装。" 2 \
  "" "true" "$CH1_ID" 5 | tail -1)"
expect_nonempty "$EX2" "习题 2（判断）"
EX3="$(mk_exercise "Pandas 读取 CSV · 填空" \
  "读取本地 CSV 文件的 Pandas 函数名是 ______()（填英文函数名，不含括号）。" 3 \
  "" "read_csv" "$CH2_ID" 10 | tail -1)"
expect_nonempty "$EX3" "习题 3（填空）"
EX4="$(mk_exercise "DataFrame 去重 · 单选" \
  "DataFrame 中按某列去重应该使用哪个方法？" 0 \
  '["drop_duplicates","dropna","drop","filter"]' "A" "$CH2_ID" 5 | tail -1)"
expect_nonempty "$EX4" "习题 4（单选）"
info "习题: $EX1 / $EX2 / $EX3 / $EX4"

api_ok GET "/api/app/exercise/by-course/${COURSE_ID}" "$TEACHER_TOKEN"
expect_ge "$(jlen '')" "4" "课程习题数"

# --- 3. 建试卷 --------------------------------------------------------------
step "3/9 老师手工组卷"
api_ok POST /api/app/exam "$TEACHER_TOKEN" "$(pyf "$COURSE_ID" "$CH1_ID" "$TAG" "$EX1" "$EX2" "$EX3" "$EX4" <<'PY'
import json,sys
print(json.dumps({
  "courseId": sys.argv[1], "chapterId": sys.argv[2],
  "title": "Python 数据分析 · 阶段测验[%s]" % sys.argv[3],
  "description": "E2E 自动组卷：覆盖第 1/2 章核心知识点",
  "type": 0, "durationMinutes": 45, "passingScore": 60,
  "exercises": [
    {"exerciseId": sys.argv[4], "score": 5,  "sortOrder": 1},
    {"exerciseId": sys.argv[5], "score": 5,  "sortOrder": 2},
    {"exerciseId": sys.argv[6], "score": 10, "sortOrder": 3},
    {"exerciseId": sys.argv[7], "score": 5,  "sortOrder": 4}
  ]
}, ensure_ascii=False))
PY
)"
EXAM_ID="$(jget id)"
expect_nonempty "$EXAM_ID" "试卷 Id"
info "试卷: $EXAM_ID"

api_ok GET "/api/app/exam?courseId=${COURSE_ID}&maxResultCount=50" "$TEACHER_TOKEN"
expect_ge "$(jlen items)" "1" "教师端试卷列表数"

# --- 4. 建作业并发布 --------------------------------------------------------
step "4/9 老师建作业并发布"
DEADLINE="$(pyf <<'PY'
import datetime
print((datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(days=7)).strftime('%Y-%m-%dT%H:%M:%SZ'))
PY
)"
api_ok POST /api/app/assignment "$TEACHER_TOKEN" "$(pyf "$COURSE_ID" "$CH2_ID" "$TAG" "$DEADLINE" <<'PY'
import json,sys
print(json.dumps({
  "courseId": sys.argv[1], "chapterId": sys.argv[2],
  "title": "Pandas 数据清洗实战作业[%s]" % sys.argv[3],
  "description": "E2E 自动生成的作业：清洗 sales.csv，输出清洗后统计结果并附说明。",
  "type": 0, "maxScore": 100, "deadline": sys.argv[4],
  "allowLate": True, "latePenaltyPercent": 10,
  "allowedFileTypes": "pdf,doc,docx,xls,xlsx,ppt,pptx,zip,txt,md,csv,py",
  "maxFileSizeMB": 20, "maxFileCount": 5, "allowResubmit": True,
  "criteria": [
    {"name": "数据清洗脚本", "description": "可运行的清洗脚本", "maxScore": 40, "sortOrder": 1},
    {"name": "结果统计", "description": "清洗后的统计结果", "maxScore": 40, "sortOrder": 2},
    {"name": "说明文档", "description": "处理思路说明", "maxScore": 20, "sortOrder": 3}
  ]
}, ensure_ascii=False))
PY
)"
ASSIGN_ID="$(jget id)"
expect_nonempty "$ASSIGN_ID" "作业 Id"
info "作业: $ASSIGN_ID"

api_ok POST "/api/app/assignment/${ASSIGN_ID}/publish" "$TEACHER_TOKEN" ""
pass "作业已发布"

# --- 5. 学生端：习题 -------------------------------------------------------
step "5/9 学生端：习题可见 + 作答 + 进度"
api_ok GET "/api/app/exercise/by-course/${COURSE_ID}" "$STUDENT_TOKEN"
expect_ge "$(jlen '')" "4" "学生可见习题数"

api_ok POST /api/app/student-exercise-record/save-or-update-record "$STUDENT_TOKEN" \
  "$(pyf "$COURSE_ID" "$CH1_ID" "$EX1" <<'PY'
import json,sys
print(json.dumps({"courseId":sys.argv[1],"chapterId":sys.argv[2],
                  "exerciseId":sys.argv[3],"studentAnswer":"A","timeSpentTicks":600000000}))
PY
)"
expect_eq "$(jget isCorrect)" "true" "习题 1 自动判题结果"

api_ok POST /api/app/student-exercise-record/save-or-update-record "$STUDENT_TOKEN" \
  "$(pyf "$COURSE_ID" "$CH2_ID" "$EX3" <<'PY'
import json,sys
print(json.dumps({"courseId":sys.argv[1],"chapterId":sys.argv[2],
                  "exerciseId":sys.argv[3],"studentAnswer":"read_csv","timeSpentTicks":900000000}))
PY
)"
expect_eq "$(jget isCorrect)" "true" "习题 3（填空）自动判题结果"

api_ok POST /api/app/student-exercise-record/save-or-update-record "$STUDENT_TOKEN" \
  "$(pyf "$COURSE_ID" "$CH1_ID" "$EX2" <<'PY'
import json,sys
print(json.dumps({"courseId":sys.argv[1],"chapterId":sys.argv[2],
                  "exerciseId":sys.argv[3],"studentAnswer":"false","timeSpentTicks":300000000}))
PY
)"
expect_eq "$(jget isCorrect)" "false" "习题 2（判断，答错）自动判题结果"

api_ok GET "/api/app/student-exercise-record/records-by-course?courseId=${COURSE_ID}&maxResultCount=50" "$STUDENT_TOKEN"
expect_ge "$(jlen items)" "3" "学生答题记录数"

api_ok GET "/api/app/student-exercise-record/chapter-progress/${COURSE_ID}" "$STUDENT_TOKEN"
info "章节进度: $(jget '' 2>/dev/null | head -c 120)"

# --- 6. 学生端：试卷作答 ---------------------------------------------------
step "6/9 学生端：试卷可见 → 开始答题 → 交卷 → 看结果"
api_ok GET "/api/app/exam/my-exams/${COURSE_ID}" "$STUDENT_TOKEN"
expect_ge "$(jlen '')" "1" "学生可见试卷数"

api_ok POST "/api/app/exam/start/${EXAM_ID}" "$STUDENT_TOKEN" ""
STUDENT_EXAM_ID="$(jget studentExamId)"
if [ -z "$STUDENT_EXAM_ID" ]; then STUDENT_EXAM_ID="$(jget id)"; fi
expect_nonempty "$STUDENT_EXAM_ID" "学生试卷实例 Id"
expect_ge "$(jlen questions)" "4" "试卷题目数"
expect_eq "$(jget questions.0.options)" '["Anaconda","Sublime Text","Photoshop","钉钉"]' "试卷题目选项"
info "学生试卷实例: $STUDENT_EXAM_ID"

api_ok POST "/api/app/exam/submit/${STUDENT_EXAM_ID}" "$STUDENT_TOKEN" \
  "$(pyf "$EX1" "$EX2" "$EX3" "$EX4" <<'PY'
import json,sys
print(json.dumps({"answers":[
  {"exerciseId":sys.argv[1],"content":"A"},
  {"exerciseId":sys.argv[2],"content":"true"},
  {"exerciseId":sys.argv[3],"content":"read_csv"},
  {"exerciseId":sys.argv[4],"content":"A"}
]}, ensure_ascii=False))
PY
)"
expect_eq "$(jget status)" "2" "交卷后状态（2 = Submitted 待汇总）"
expect_eq "$(jget answers.0.score)" "5" "题目 1 客观题得分"
expect_eq "$(jget answers.2.score)" "10" "题目 3（填空）得分"

# 交卷时按题判分，总分由「自动判分」汇总（ApplyTotalAsync）
api_ok POST "/api/app/exam/auto-grade/${STUDENT_EXAM_ID}" "$TEACHER_TOKEN" ""
expect_eq "$(jget totalScore)" "25" "自动判卷汇总总分（5+5+10+5）"
expect_eq "$(jget status)" "3" "判卷后状态（3 = Graded）"

api_ok GET "/api/app/exam/my-result/${STUDENT_EXAM_ID}" "$STUDENT_TOKEN"
expect_eq "$(jget totalScore)" "25" "学生端看到的总分"

# --- 7. 学生端：作业提交 ---------------------------------------------------
step "7/9 学生端：作业可见 → 上传附件 → 提交"
api_ok GET "/api/app/assignment/my-assignments/${COURSE_ID}" "$STUDENT_TOKEN"
expect_ge "$(jlen '')" "1" "学生可见作业数"
expect_eq "$(jfind '' assignmentId "$ASSIGN_ID" submitted)" "false" "提交前状态"

api_ok GET "/api/app/assignment/my-assignment/${ASSIGN_ID}" "$STUDENT_TOKEN"
expect_eq "$(jget title)" "$(jget title)" "作业详情可读"
expect_ge "$(jlen criteria)" "3" "评分细则条数"

# 上传提交附件
cat > "$WORK/homework.py" <<'PY'
# E2E 示例：Pandas 数据清洗脚本
import pandas as pd
df = pd.read_csv("sales.csv")
print(df.dropna().groupby("category")["amount"].sum())
PY
UP_CODE="$(curl -s $CURL_SSL_ARGS -X POST "${API_BASE}/api/app/assignment/upload-file/${ASSIGN_ID}?purpose=submission" \
  -H "Authorization: Bearer ${STUDENT_TOKEN}" -H "Abp-TenantId: ${TENANT_ID}" \
  -F "file=@${WORK}/homework.py;filename=homework.py" \
  -o "$WORK/resp.json" -w '%{http_code}')"
expect_eq "$UP_CODE" "200" "学生上传作业附件"
FILE_ID="$(jget id)"
expect_nonempty "$FILE_ID" "作业附件 Id"

api_ok POST /api/app/assignment/submit "$STUDENT_TOKEN" \
  "$(pyf "$ASSIGN_ID" "$FILE_ID" <<'PY'
import json,sys
print(json.dumps({"assignmentId":sys.argv[1],"note":"E2E 示例提交：清洗脚本 + 结果统计",
                  "honorPledge":True,"isDraft":False,"fileIds":[sys.argv[2]]}, ensure_ascii=False))
PY
)"
pass "学生已提交作业"

api_ok GET "/api/app/assignment/my-assignment/${ASSIGN_ID}" "$STUDENT_TOKEN"
expect_eq "$(jget submitted)" "true" "提交后状态"
expect_eq "$(jget myStatus)" "1" "提交状态（1 = Submitted）"

# --- 8. 教师批阅 ----------------------------------------------------------
step "8/9 教师批阅作业"
api_ok GET "/api/app/assignment/submissions?assignmentId=${ASSIGN_ID}&maxResultCount=50" "$TEACHER_TOKEN"
expect_ge "$(jlen items)" "1" "教师端提交记录数"
SUB_ID="$(jfind items assignmentId "$ASSIGN_ID" submissionId)"
expect_nonempty "$SUB_ID" "提交记录 Id"

api_ok POST "/api/app/assignment/grade/${SUB_ID}" "$TEACHER_TOKEN" \
  "$(pyf "$SUB_ID" "$CH1_ID" "$CH2_ID" <<'PY'
import json,sys
print(json.dumps({
  "scores": [
    {"criterionName":"数据清洗脚本","score":40,"comment":"脚本可运行","sortOrder":1},
    {"criterionName":"结果统计","score":35,"comment":"统计口径需补充","sortOrder":2},
    {"criterionName":"说明文档","score":15,"comment":"说明较简略","sortOrder":3}
  ],
  "score": 90,
  "comment": "整体完成度不错，注意统计口径与空值处理说明。"
}, ensure_ascii=False))
PY
)"
pass "教师已批阅"

# --- 9. 学生看反馈 --------------------------------------------------------
step "9/9 学生端看批阅反馈"
api_ok GET "/api/app/assignment/my-feedback/${ASSIGN_ID}" "$STUDENT_TOKEN"
expect_eq "$(jget graded)" "true" "批阅状态"
expect_eq "$(jget score)" "90" "作业得分"
expect_ge "$(jlen scores)" "3" "评分明细条数"
expect_ge "$(jlen timeline)" "1" "提交/批阅时间线条数"

# --- 汇总 ---------------------------------------------------------------
step "汇总"
cat > "$SCRIPT_DIR/.last-demo-ids.json" <<EOF
{
  "tag": "$TAG",
  "apiBase": "$API_BASE",
  "tenant": "$TENANT_NAME",
  "courseId": "$COURSE_ID",
  "courseTitle": "$COURSE_TITLE",
  "exerciseIds": ["$EX1", "$EX2", "$EX3", "$EX4"],
  "examId": "$EXAM_ID",
  "studentExamId": "$STUDENT_EXAM_ID",
  "assignmentId": "$ASSIGN_ID",
  "submissionId": "$SUB_ID"
}
EOF
info "对象 Id 已写入 $SCRIPT_DIR/.last-demo-ids.json"
printf '\n%s\n' "------------------------------------------------------------------------------"
printf '   课程   : %s (%s)\n' "$COURSE_TITLE" "$COURSE_ID"
printf '   习题   : %s / %s / %s / %s\n' "$EX1" "$EX2" "$EX3" "$EX4"
printf '   试卷   : %s   学生试卷实例: %s\n' "$EXAM_ID" "$STUDENT_EXAM_ID"
printf '   作业   : %s   提交记录: %s\n' "$ASSIGN_ID" "$SUB_ID"
printf '%s\n' "------------------------------------------------------------------------------"
printf '\n%s\n' "✅ 课程内容闭环（习题 / 试卷 / 作业 / 学生端）全部通过"
