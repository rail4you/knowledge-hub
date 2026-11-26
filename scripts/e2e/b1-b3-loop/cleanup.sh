#!/usr/bin/env bash
# =============================================================================
# B1–B3 整体回归 · 清理
# -----------------------------------------------------------------------------
# 优先走 API 删除（业务逻辑会连带清理：视频删磁盘文件、小组任务删分组/提交、
# 练习删作答、签到删记录、讨论删回复），最后用 SQL 把课程相关行硬删干净
# ——这些是测试产物，留着只会污染列表（课程是软删，硬删更彻底）。
#
# 用法：
#   ./cleanup.sh                       # 用 .last-run 的课程
#   COURSE_TITLE='B1B3回归课程-xxx' ./cleanup.sh
#   PGHOST=... PGPORT=... ./cleanup.sh
# =============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

API_BASE="${API_BASE:-https://localhost:44305}"
TENANT_NAME="${TENANT_NAME:-qidi}"
TEACHER_USER="${TEACHER_USER:-wwq}"; TEACHER_PASS="${TEACHER_PASS:-123456}"
OAUTH_CLIENT="${OAUTH_CLIENT:-KnowledgeHub_App}"
OAUTH_SCOPE="${OAUTH_SCOPE:-offline_access KnowledgeHub}"
CURL_SSL_ARGS="${CURL_SSL_ARGS:--k}"

PGHOST="${PGHOST:-localhost}"; PGPORT="${PGPORT:-5433}"
PGUSER="${PGUSER:-postgres}"; PGPASSWORD="${PGPASSWORD:-postgres}"
PGDATABASE="${PGDATABASE:-KnowledgeHub}"
export PGHOST PGPORT PGUSER PGPASSWORD PGDATABASE

RUN_TAG="${RUN_TAG:-}"; COURSE_TITLE="${COURSE_TITLE:-}"
# .last-run 格式：tag|课程标题|courseId|videoId|examId（旧格式只有 4 段：tag|courseId|...）
if [ -f "$SCRIPT_DIR/.last-run" ]; then
  LAST="$(cat "$SCRIPT_DIR/.last-run")"
  FIELDS="$(awk -F'|' '{print NF}' <<< "$LAST")"
  [ -z "$RUN_TAG" ] && RUN_TAG="$(cut -d'|' -f1 <<< "$LAST")"
  if [ "${FIELDS:-0}" -ge 5 ]; then
    [ -z "$COURSE_TITLE" ] && COURSE_TITLE="$(cut -d'|' -f2 <<< "$LAST")"
  fi
fi
if [ -z "$COURSE_TITLE" ]; then
  # .last-run 里第 2 段在 run.sh 中是 courseId（不是标题）——按标题模式兜底
  if [ -n "$RUN_TAG" ]; then COURSE_TITLE="B1B3回归课程-${RUN_TAG}"; fi
fi
[ -n "$COURSE_TITLE" ] || { echo "用法: RUN_TAG=xxxx ./cleanup.sh 或 COURSE_TITLE='...' ./cleanup.sh" >&2; exit 2; }

if [ "${PSQL_IN_CONTAINER:-0}" = "1" ]; then
  PG_CONTAINER="${PG_CONTAINER:-postgres}"
  PSQL() { docker exec -i -e PGPASSWORD="$PGPASSWORD" "$PG_CONTAINER" psql -U "$PGUSER" -d "$PGDATABASE" "$@"; }
else
  PSQL() { psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" "$@"; }
fi

if [ -t 1 ]; then C_OK=$'\033[32m'; C_BAD=$'\033[31m'; C_WARN=$'\033[33m'; C_B=$'\033[1m'; C_OFF=$'\033[0m'
else C_OK=""; C_BAD=""; C_WARN=""; C_B=""; C_OFF=""; fi
step() { printf '\n%s▶ %s%s\n' "$C_B" "$*" "$C_OFF"; }
ok()   { printf '  %s✓%s %s\n' "$C_OK" "$C_OFF" "$*"; }
skip() { printf '  %s·%s %s\n' "$C_WARN" "$C_OFF" "$*"; }

echo "=============================================================================="
echo " B1–B3 整体回归 · 清理"
echo " API: $API_BASE   课程/标记: $COURSE_TITLE"
echo "=============================================================================="

# ── 定位课程 ─────────────────────────────────────────────────
COURSE_ID="$(PSQL -At -t -d "$PGDATABASE" -v title="$COURSE_TITLE" <<'SQL'
SELECT "Id" FROM "AppCourses" WHERE "Title" = :'title' ORDER BY "CreationTime" DESC LIMIT 1
SQL
)"
if [ -z "$COURSE_ID" ]; then
  ok "没有找到课程「${COURSE_TITLE}」，无需清理"
  exit 0
fi
ok "courseId = $COURSE_ID"

# ── 拿租户 + 老师令牌（API 删除要鉴权） ──────────────────────
TENANT_ID="$(curl -s $CURL_SSL_ARGS "$API_BASE/api/public/tenants" | python3 -c "
import json,sys
items=json.load(sys.stdin)
items=items if isinstance(items,list) else items.get('items',[])
for t in items:
    if t.get('name')==sys.argv[1] or t.get('tenancyName')==sys.argv[1]:
        print(t.get('id') or t.get('tenantId') or ''); break
" "$TENANT_NAME")"
TOKEN="$(curl -s $CURL_SSL_ARGS -X POST "$API_BASE/connect/token" \
  -H 'Content-Type: application/x-www-form-urlencoded' ${TENANT_ID:+-H "Abp-TenantId: $TENANT_ID"} \
  --data-urlencode 'grant_type=password' \
  --data-urlencode "client_id=$OAUTH_CLIENT" \
  --data-urlencode "scope=$OAUTH_SCOPE" \
  --data-urlencode "username=$TEACHER_USER" \
  --data-urlencode "password=$TEACHER_PASS" \
  | python3 -c "import json,sys;print(json.load(sys.stdin).get('access_token',''))")"

api_del() {
  local path="$1" label="$2"
  [ -n "$TOKEN" ] || { skip "${label}（没拿到令牌，跳过 API 删除）"; return 0; }
  local code
  code="$(curl -s $CURL_SSL_ARGS -o /dev/null -w '%{http_code}' -X DELETE "$API_BASE$path" \
    -H "Authorization: Bearer $TOKEN" ${TENANT_ID:+-H "Abp-TenantId: $TENANT_ID"})"
  case "$code" in
    2*|404) ok "${label}（HTTP ${code}）" ;;
    *) skip "$label 删除失败（HTTP ${code}），交给下面的 SQL 兜底" ;;
  esac
}

# ── 1. 走 API 删（业务连带清理） ─────────────────────────────
step "1 业务对象（API 删除，连带清理子表与磁盘文件）"
while IFS= read -r id; do [ -n "$id" ] && api_del "/api/app/course-video/$id" "视频 $id"; done < <(
  PSQL -At -t -d "$PGDATABASE" -v cid="$COURSE_ID" <<'SQL'
SELECT "Id" FROM "AppCourseVideos" WHERE "CourseId" = :'cid' AND NOT "IsDeleted"
SQL
)
while IFS= read -r id; do [ -n "$id" ] && api_del "/api/app/group-task/$id" "小组任务 $id"; done < <(
  PSQL -At -t -d "$PGDATABASE" -v cid="$COURSE_ID" <<'SQL'
SELECT "Id" FROM "AppGroupTasks" WHERE "CourseId" = :'cid' AND NOT "IsDeleted"
SQL
)
while IFS= read -r id; do [ -n "$id" ] && api_del "/api/app/classroom-practice/$id" "课堂练习 $id"; done < <(
  PSQL -At -t -d "$PGDATABASE" -v cid="$COURSE_ID" <<'SQL'
SELECT "Id" FROM "AppPracticeSessions" WHERE "CourseId" = :'cid' AND NOT "IsDeleted"
SQL
)
while IFS= read -r id; do [ -n "$id" ] && api_del "/api/app/check-in/$id" "签到 $id"; done < <(
  PSQL -At -t -d "$PGDATABASE" -v cid="$COURSE_ID" <<'SQL'
SELECT "Id" FROM "AppCheckInSessions" WHERE "CourseId" = :'cid' AND NOT "IsDeleted"
SQL
)
while IFS= read -r id; do [ -n "$id" ] && api_del "/api/app/discussion/$id" "讨论 $id"; done < <(
  PSQL -At -t -d "$PGDATABASE" -v cid="$COURSE_ID" <<'SQL'
SELECT "Id" FROM "AppDiscussions" WHERE "CourseId" = :'cid' AND NOT "IsDeleted"
SQL
)
while IFS= read -r id; do [ -n "$id" ] && api_del "/api/app/exam/$id" "试卷 $id"; done < <(
  PSQL -At -t -d "$PGDATABASE" -v cid="$COURSE_ID" <<'SQL'
SELECT "Id" FROM "AppExams" WHERE "CourseId" = :'cid' AND NOT "IsDeleted"
SQL
)

# ── 2. SQL 硬删课程图谱（测试产物，软删留着只会出现在列表里） ─
step "2 课程相关行（SQL 硬删）"
CLEAN_TAG="${RUN_TAG:-$COURSE_TITLE}"
PSQL -v ON_ERROR_STOP=1 -d "$PGDATABASE" -v cid="$COURSE_ID" -v tag="$CLEAN_TAG" <<'SQL' >/dev/null && ok "课程图谱 + 能力/岗位图谱（标记 ${CLEAN_TAG}）已硬删"
BEGIN;
DELETE FROM "AppVideoKnowledgeMarks" WHERE "CourseVideoId" IN (SELECT "Id" FROM "AppCourseVideos" WHERE "CourseId" = :'cid');
DELETE FROM "AppCourseVideos"        WHERE "CourseId" = :'cid';
DELETE FROM "AppGroupTaskSubmissions" WHERE "GroupTaskId" IN (SELECT "Id" FROM "AppGroupTasks" WHERE "CourseId" = :'cid');
DELETE FROM "AppStudentGroupMembers"  WHERE "GroupId" IN (SELECT g."Id" FROM "AppStudentGroups" g JOIN "AppGroupTasks" t ON t."Id"=g."GroupTaskId" WHERE t."CourseId" = :'cid');
DELETE FROM "AppStudentGroups"        WHERE "GroupTaskId" IN (SELECT "Id" FROM "AppGroupTasks" WHERE "CourseId" = :'cid');
DELETE FROM "AppGroupTasks"           WHERE "CourseId" = :'cid';
DELETE FROM "AppPracticeAttemptAnswers" WHERE "AttemptId" IN (SELECT a."Id" FROM "AppPracticeAttempts" a JOIN "AppPracticeSessions" s ON s."Id"=a."SessionId" WHERE s."CourseId" = :'cid');
DELETE FROM "AppPracticeAttempts"     WHERE "SessionId" IN (SELECT "Id" FROM "AppPracticeSessions" WHERE "CourseId" = :'cid');
DELETE FROM "AppPracticeSessionItems" WHERE "SessionId" IN (SELECT "Id" FROM "AppPracticeSessions" WHERE "CourseId" = :'cid');
DELETE FROM "AppPracticeSessions"     WHERE "CourseId" = :'cid';
DELETE FROM "AppCheckInRecords"       WHERE "SessionId" IN (SELECT "Id" FROM "AppCheckInSessions" WHERE "CourseId" = :'cid');
DELETE FROM "AppCheckInSessions"      WHERE "CourseId" = :'cid';
DELETE FROM "AppDiscussionReplies"    WHERE "DiscussionId" IN (SELECT "Id" FROM "AppDiscussions" WHERE "CourseId" = :'cid');
DELETE FROM "AppDiscussions"          WHERE "CourseId" = :'cid';
DELETE FROM "AppStudentActivityLogs"  WHERE "CourseId" = :'cid';
DELETE FROM "AppStudentRecommendations" WHERE "CourseId" = :'cid';
-- 能力体系（P2）：课程支撑 + 能力↔知识点（link 可能指向本课知识点）
DELETE FROM "AppCompetencyKnowledgeLinks"
  WHERE "KnowledgeResourceId" IN (SELECT "Id" FROM "AppKnowledgeResources" WHERE "CourseId" = :'cid');
DELETE FROM "AppCourseCompetencySupports" WHERE "CourseId" = :'cid';
-- 智学分析知识点（KP）与习题映射
DELETE FROM "AppExerciseKnowledgePoints"
  WHERE "ExerciseId" IN (SELECT "Id" FROM "AppExercises" WHERE "CourseId" = :'cid');
DELETE FROM "AppStudentKpMasteries"
  WHERE "KnowledgePointId" IN (SELECT "Id" FROM "AppKnowledgePoints" WHERE "CourseId" = :'cid');
DELETE FROM "AppKpPrerequisites"
  WHERE "FromKpId" IN (SELECT "Id" FROM "AppKnowledgePoints" WHERE "CourseId" = :'cid')
     OR "ToKpId" IN (SELECT "Id" FROM "AppKnowledgePoints" WHERE "CourseId" = :'cid');
DELETE FROM "AppKnowledgePoints" WHERE "CourseId" = :'cid';
DELETE FROM "AppStudentExams"         WHERE "ExamId" IN (SELECT "Id" FROM "AppExams" WHERE "CourseId" = :'cid');
DELETE FROM "AppExamExercises"        WHERE "ExamId" IN (SELECT "Id" FROM "AppExams" WHERE "CourseId" = :'cid');
DELETE FROM "AppExams"                WHERE "CourseId" = :'cid';
DELETE FROM "AppStudentExerciseRecords" WHERE "CourseId" = :'cid';
DELETE FROM "AppKnowledgeMastery"     WHERE "KnowledgeResourceId" IN (SELECT "Id" FROM "AppKnowledgeResources" WHERE "CourseId" = :'cid');
DELETE FROM "AppChapterExercises"     WHERE "ChapterId" IN (SELECT "Id" FROM "AppChapters" WHERE "CourseId" = :'cid');
DELETE FROM "AppChapterResources"     WHERE "ChapterId" IN (SELECT "Id" FROM "AppChapters" WHERE "CourseId" = :'cid');
DELETE FROM "AppCourseResources"      WHERE "CourseId" = :'cid';
DELETE FROM "AppExercises"            WHERE "CourseId" = :'cid';
DELETE FROM "AppKnowledgeResources"   WHERE "CourseId" = :'cid';
DELETE FROM "AppChapters"             WHERE "CourseId" = :'cid';
DELETE FROM "AppCourseInfos"          WHERE "CourseId" = :'cid';
DELETE FROM "AppCourseTextbooks"      WHERE "CourseId" = :'cid';
DELETE FROM "AppStudentCourses"       WHERE "CourseId" = :'cid';
DELETE FROM "AppLearningProgress"    WHERE "CourseId" = :'cid';
DELETE FROM "AppCourses"              WHERE "Id" = :'cid';
-- 能力体系与岗位图谱（非课程维度）：按 E2E 标记清理
DELETE FROM "AppJobTaskCompetencies"
  WHERE "CoreTaskId" IN (SELECT t."Id" FROM "AppJobCoreTasks" t JOIN "AppJobCompetencyGraphs" g ON g."Id"=t."GraphId" WHERE g."Title" LIKE '%' || :'tag' || '%');
DELETE FROM "AppJobCoreTasks"
  WHERE "GraphId" IN (SELECT "Id" FROM "AppJobCompetencyGraphs" WHERE "Title" LIKE '%' || :'tag' || '%');
DELETE FROM "AppJobCompetencyGraphs" WHERE "Title" LIKE '%' || :'tag' || '%';
DELETE FROM "AppCourseCompetencySupports"
  WHERE "CompetencyId" IN (SELECT "Id" FROM "AppCompetencies" WHERE "Code" LIKE 'E2E-%');
DELETE FROM "AppCompetencyKnowledgeLinks"
  WHERE "CompetencyId" IN (SELECT "Id" FROM "AppCompetencies" WHERE "Code" LIKE 'E2E-%');
DELETE FROM "AppCompetencies" WHERE "Code" LIKE 'E2E-%';
DELETE FROM "AppJobPostings" WHERE "Title" LIKE '%' || :'tag' || '%';
COMMIT;
SQL

# ── 3. 复核 ──────────────────────────────────────────────────
step "3 复核残留"
LEFT="$(PSQL -At -t -d "$PGDATABASE" -v cid="$COURSE_ID" <<'SQL'
SELECT
  (SELECT count(*) FROM "AppCourses" WHERE "Id" = :'cid')
+ (SELECT count(*) FROM "AppKnowledgeResources" WHERE "CourseId" = :'cid')
+ (SELECT count(*) FROM "AppExercises" WHERE "CourseId" = :'cid')
+ (SELECT count(*) FROM "AppCourseVideos" WHERE "CourseId" = :'cid')
+ (SELECT count(*) FROM "AppExams" WHERE "CourseId" = :'cid');
SQL
)"
if [ "${LEFT:-1}" = "0" ]; then
  printf '  %s✓%s 课程/知识点/习题/视频/试卷 均已清空\n' "$C_OK" "$C_OFF"
else
  printf '  %s!%s 仍有 %s 行残留（可能被其它引用占用，需人工确认）\n' "$C_WARN" "$C_OFF" "$LEFT"
fi
echo
printf '%s清理完成%s\n' "$C_OK" "$C_OFF"
