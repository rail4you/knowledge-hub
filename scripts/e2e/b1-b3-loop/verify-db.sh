#!/usr/bin/env bash
# =============================================================================
# B1–B3 整体回归 · 数据库侧独立交叉验证
# -----------------------------------------------------------------------------
# 不依赖 run.sh 的断言，直接查库核对 B1–B3 的关键落库事实（与 API 无关的复核）。
#
# 实现方式：**一条**带 CTE 的 SQL 输出 (检查项 | 实际值 | 期望值) 三元组，
# 由 bash 逐行比对。好处：
#   · 只连库一次，快；
#   · SQL 走 heredoc，标识符可以正常写 "Id"（不用在 bash 双引号里转义）；
#   · 期望值跟着 SQL 一起演进，看代码就知道在验什么。
#
# 用法：
#   ./verify-db.sh                      # 用 .last-run 里最近一次的标记
#   RUN_TAG=1126-143320 ./verify-db.sh
#   PGHOST=47.99.245.49 PGPORT=5433 ./verify-db.sh
# 依赖：psql（远程宿主没装时用 PSQL_IN_CONTAINER=1）
# =============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

PGHOST="${PGHOST:-localhost}"; PGPORT="${PGPORT:-5433}"
PGUSER="${PGUSER:-postgres}"; PGPASSWORD="${PGPASSWORD:-postgres}"
PGDATABASE="${PGDATABASE:-KnowledgeHub}"
export PGHOST PGPORT PGUSER PGPASSWORD PGDATABASE

RUN_TAG="${RUN_TAG:-}"
if [ -z "$RUN_TAG" ] && [ -f "$SCRIPT_DIR/.last-run" ]; then
  RUN_TAG="$(cut -d'|' -f1 "$SCRIPT_DIR/.last-run")"
fi
[ -n "$RUN_TAG" ] || { echo "用法: RUN_TAG=xxxx ./verify-db.sh（或先跑 KEEP=1 ./run.sh）" >&2; exit 2; }
COURSE_FILTER="B1B3回归课程-${RUN_TAG}"

if [ "${PSQL_IN_CONTAINER:-0}" = "1" ]; then
  PG_CONTAINER="${PG_CONTAINER:-postgres}"
  PSQL() { docker exec -i -e PGPASSWORD="$PGPASSWORD" "$PG_CONTAINER" psql -U "$PGUSER" -d "$PGDATABASE" "$@"; }
else
  PSQL() { psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" "$@"; }
fi

if [ -t 1 ]; then C_OK=$'\033[32m'; C_BAD=$'\033[31m'; C_WARN=$'\033[33m'; C_B=$'\033[1m'; C_OFF=$'\033[0m'
else C_OK=""; C_BAD=""; C_WARN=""; C_B=""; C_OFF=""; fi

echo "=============================================================================="
echo " B1–B3 整体回归 · 数据库交叉验证"
echo " DB: $PGUSER@$PGHOST:$PGPORT/$PGDATABASE"
echo " 课程: $COURSE_FILTER"
echo "=============================================================================="

OUT="$(PSQL -v ON_ERROR_STOP=1 -At -t -F'|' -v title="$COURSE_FILTER" -v tag="$RUN_TAG" -d "$PGDATABASE" <<'SQL'
WITH c AS (
  SELECT "Id" FROM "AppCourses"
  WHERE "Title" = :'title' AND NOT "IsDeleted"
  ORDER BY "CreationTime" DESC LIMIT 1
), v AS (
  SELECT "Id" FROM "AppCourseVideos" WHERE "CourseId" = (SELECT "Id" FROM c) AND NOT "IsDeleted"
  ORDER BY "CreationTime" DESC LIMIT 1
), p AS (
  SELECT "Id" FROM "AppPracticeSessions" WHERE "CourseId" = (SELECT "Id" FROM c) AND NOT "IsDeleted"
  ORDER BY "CreationTime" DESC LIMIT 1
), g AS (
  SELECT "Id" FROM "AppGroupTasks" WHERE "CourseId" = (SELECT "Id" FROM c) AND NOT "IsDeleted"
  ORDER BY "CreationTime" DESC LIMIT 1
)
SELECT '课程存在', (SELECT count(*)::text FROM c), '1'
UNION ALL SELECT '课程状态(5=Published)', (SELECT "Status"::text FROM "AppCourses" WHERE "Id"=(SELECT "Id" FROM c)), '5'
UNION ALL SELECT '教学信息行数', (SELECT count(*)::text FROM "AppCourseInfos" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted"), '1'
UNION ALL SELECT '主教材行数', (SELECT count(*)::text FROM "AppCourseTextbooks" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" AND "IsPrimary"), '1'
-- B1 知识点树
-- 只统计 B1-4 建的那三个节点（P2-3 的 xlsx 导入会往同一门课加更多节点）
UNION ALL SELECT '知识点:B1建的单元数', (SELECT count(*)::text FROM "AppKnowledgeResources" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" AND "NodeType"=0 AND "Name"='回归单元'), '1'
UNION ALL SELECT '知识点:B1建的知识项数', (SELECT count(*)::text FROM "AppKnowledgeResources" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" AND "NodeType"=1 AND "Name" LIKE '回归知识项%'), '2'
UNION ALL SELECT '知识点:SortPath为空的节点', (SELECT count(*)::text FROM "AppKnowledgeResources" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" AND "SortPath" IS NULL), '0'
UNION ALL SELECT '知识点:B1节点带层级路径数', (SELECT count(*)::text FROM "AppKnowledgeResources" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" AND "SortPath" LIKE '%.%' AND ("Name"='回归单元' OR "Name" LIKE '回归知识项%')), '2'
-- B1 掌握度
UNION ALL SELECT '掌握度:有证据的行数(≥1)', (SELECT count(*)::text FROM "AppKnowledgeMastery" m JOIN "AppKnowledgeResources" k ON k."Id"=m."KnowledgeResourceId" WHERE k."CourseId"=(SELECT "Id" FROM c) AND m."Evidence">0), 'ge:1'
UNION ALL SELECT '习题:绑定知识点的题数', (SELECT count(*)::text FROM "AppExercises" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" AND "KnowledgeResourceId" IS NOT NULL), '1'
-- B2 签到
UNION ALL SELECT '签到:场次数', (SELECT count(*)::text FROM "AppCheckInSessions" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted"), '1'
UNION ALL SELECT '签到:记录数', (SELECT count(*)::text FROM "AppCheckInRecords" r JOIN "AppCheckInSessions" s ON s."Id"=r."SessionId" WHERE s."CourseId"=(SELECT "Id" FROM c) AND NOT r."IsDeleted"), '1'
UNION ALL SELECT '签到:重复签到组数', (SELECT count(*)::text FROM (SELECT r."SessionId", r."StudentId" FROM "AppCheckInRecords" r JOIN "AppCheckInSessions" s ON s."Id"=r."SessionId" WHERE s."CourseId"=(SELECT "Id" FROM c) AND NOT r."IsDeleted" GROUP BY 1,2 HAVING count(*)>1) t), '0'
-- B2 讨论
UNION ALL SELECT '讨论:主题数', (SELECT count(*)::text FROM "AppDiscussions" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted"), '1'
UNION ALL SELECT '讨论:回复数', (SELECT count(*)::text FROM "AppDiscussionReplies" r JOIN "AppDiscussions" d ON d."Id"=r."DiscussionId" WHERE d."CourseId"=(SELECT "Id" FROM c) AND NOT r."IsDeleted"), '1'
UNION ALL SELECT '讨论:被采纳回复数', (SELECT count(*)::text FROM "AppDiscussionReplies" r JOIN "AppDiscussions" d ON d."Id"=r."DiscussionId" WHERE d."CourseId"=(SELECT "Id" FROM c) AND NOT r."IsDeleted" AND r."IsAccepted"), '1'
UNION ALL SELECT '讨论:已解决主题数', (SELECT count(*)::text FROM "AppDiscussions" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" AND "IsResolved"), '1'
-- B2 课堂练习
UNION ALL SELECT '练习:快照题数=场次题数', (SELECT CASE WHEN (SELECT count(*) FROM "AppPracticeSessionItems" WHERE "SessionId"=(SELECT "Id" FROM p)) = (SELECT "QuestionCount" FROM "AppPracticeSessions" WHERE "Id"=(SELECT "Id" FROM p)) THEN 'yes' ELSE 'no' END), 'yes'
UNION ALL SELECT '练习:提交份数', (SELECT count(*)::text FROM "AppPracticeAttempts" WHERE "SessionId"=(SELECT "Id" FROM p) AND NOT "IsDeleted"), '1'
UNION ALL SELECT '练习:同一学生多份提交', (SELECT count(*)::text FROM (SELECT "StudentId" FROM "AppPracticeAttempts" WHERE "SessionId"=(SELECT "Id" FROM p) AND NOT "IsDeleted" GROUP BY 1 HAVING count(*)>1) t), '0'
UNION ALL SELECT '练习:作答明细条数=题数', (SELECT CASE WHEN (SELECT count(*) FROM "AppPracticeAttemptAnswers" a JOIN "AppPracticeAttempts" t2 ON t2."Id"=a."AttemptId" WHERE t2."SessionId"=(SELECT "Id" FROM p)) = (SELECT "QuestionCount" FROM "AppPracticeSessions" WHERE "Id"=(SELECT "Id" FROM p)) THEN 'yes' ELSE 'no' END), 'yes'
-- B2 小组任务
UNION ALL SELECT '小组:分组数(≥1)', (SELECT count(*)::text FROM "AppStudentGroups" WHERE "GroupTaskId"=(SELECT "Id" FROM g)), 'ge:1'
UNION ALL SELECT '小组:提交份数', (SELECT count(*)::text FROM "AppGroupTaskSubmissions" WHERE "GroupTaskId"=(SELECT "Id" FROM g) AND NOT "IsDeleted"), '1'
UNION ALL SELECT '小组:同组多份提交', (SELECT count(*)::text FROM (SELECT "GroupId" FROM "AppGroupTaskSubmissions" WHERE "GroupTaskId"=(SELECT "Id" FROM g) AND NOT "IsDeleted" GROUP BY 1 HAVING count(*)>1) t), '0'
UNION ALL SELECT '小组:已评分提交数', (SELECT count(*)::text FROM "AppGroupTaskSubmissions" WHERE "GroupTaskId"=(SELECT "Id" FROM g) AND NOT "IsDeleted" AND "Score" IS NOT NULL), '1'
-- B3 视频
UNION ALL SELECT '视频:关联资源', (SELECT count(*)::text FROM "AppCourseVideos" WHERE "Id"=(SELECT "Id" FROM v) AND "ResourceId" IS NOT NULL), '1'
UNION ALL SELECT '视频:时长已回填', (SELECT count(*)::text FROM "AppCourseVideos" WHERE "Id"=(SELECT "Id" FROM v) AND "DurationSeconds">0), '1'
UNION ALL SELECT '视频:同秒重复标记', (SELECT count(*)::text FROM (SELECT "PointSec" FROM "AppVideoKnowledgeMarks" WHERE "CourseVideoId"=(SELECT "Id" FROM v) GROUP BY 1 HAVING count(*)>1) t), '0'
UNION ALL SELECT '视频:标记数', (SELECT count(*)::text FROM "AppVideoKnowledgeMarks" WHERE "CourseVideoId"=(SELECT "Id" FROM v)), '1'
-- B3 试卷
UNION ALL SELECT '试卷:数量', (SELECT count(*)::text FROM "AppExams" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted"), '1'
UNION ALL SELECT '试卷:题目数(≥1)', (SELECT count(*)::text FROM "AppExamExercises" e JOIN "AppExams" x ON x."Id"=e."ExamId" WHERE x."CourseId"=(SELECT "Id" FROM c)), 'ge:1'
-- P2-1 能力体系
UNION ALL SELECT '能力:本课支撑数', (SELECT count(*)::text FROM "AppCourseCompetencySupports" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted"), '1'
UNION ALL SELECT '能力:关联知识点数', (SELECT count(*)::text FROM "AppCompetencyKnowledgeLinks" l JOIN "AppCourseCompetencySupports" s ON s."CompetencyId"=l."CompetencyId" WHERE s."CourseId"=(SELECT "Id" FROM c) AND NOT s."IsDeleted"), '1'
UNION ALL SELECT '能力:支撑强度', (SELECT ("Weight"*10)::int::text FROM "AppCourseCompetencySupports" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" LIMIT 1), '8'
-- 达成度的数据前提：被能力引用的知识点必须有掌握度证据
UNION ALL SELECT '能力:证据知识点数(≥1)', (SELECT count(*)::text FROM "AppKnowledgeMastery" m
   JOIN "AppCompetencyKnowledgeLinks" l ON l."KnowledgeResourceId"=m."KnowledgeResourceId"
   JOIN "AppCourseCompetencySupports" s ON s."CompetencyId"=l."CompetencyId"
   WHERE s."CourseId"=(SELECT "Id" FROM c) AND m."Evidence">0), 'ge:1'
-- P2-3 xlsx 导入（模板示例行：3 个单元 + 3 个知识项，且 SortPath 齐全）
UNION ALL SELECT '导入:模板导入的知识项数', (SELECT count(*)::text FROM "AppKnowledgeResources" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" AND "NodeType"=1 AND "Name" IN ('人工材料','天然材料','触觉肌理')), '3'
UNION ALL SELECT '导入:导入节点SortPath为空数', (SELECT count(*)::text FROM "AppKnowledgeResources" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" AND "Name" IN ('人工材料','天然材料','触觉肌理') AND "SortPath" IS NULL), '0'
-- P2-4 岗位图谱
UNION ALL SELECT '岗位:图谱数(本标记)', (SELECT count(*)::text FROM "AppJobCompetencyGraphs" WHERE "Title" LIKE '%' || :'tag' || '%' AND NOT "IsDeleted"), '1'
UNION ALL SELECT '岗位:核心任务数', (SELECT count(*)::text FROM "AppJobCoreTasks" t JOIN "AppJobCompetencyGraphs" g ON g."Id"=t."GraphId" WHERE g."Title" LIKE '%' || :'tag' || '%' AND NOT t."IsDeleted"), '1'
UNION ALL SELECT '岗位:任务能力数', (SELECT count(*)::text FROM "AppJobTaskCompetencies" l JOIN "AppJobCoreTasks" t ON t."Id"=l."CoreTaskId" JOIN "AppJobCompetencyGraphs" g ON g."Id"=t."GraphId" WHERE g."Title" LIKE '%' || :'tag' || '%'), '1'
UNION ALL SELECT '岗位:图谱已挂招聘岗位', (SELECT count(*)::text FROM "AppJobCompetencyGraphs" WHERE "Title" LIKE '%' || :'tag' || '%' AND NOT "IsDeleted" AND "JobPostingId" IS NOT NULL), '1'
UNION ALL SELECT '岗位:能力有课程支撑(≥1)', (SELECT count(*)::text FROM "AppCourseCompetencySupports" s
   JOIN "AppJobTaskCompetencies" l ON l."CompetencyId"=s."CompetencyId"
   JOIN "AppJobCoreTasks" t ON t."Id"=l."CoreTaskId"
   JOIN "AppJobCompetencyGraphs" g ON g."Id"=t."GraphId"
   WHERE g."Title" LIKE '%' || :'tag' || '%' AND NOT s."IsDeleted"), 'ge:1'
UNION ALL SELECT '岗位:要求等级', (SELECT l."RequiredLevel"::text FROM "AppJobTaskCompetencies" l JOIN "AppJobCoreTasks" t ON t."Id"=l."CoreTaskId" JOIN "AppJobCompetencyGraphs" g ON g."Id"=t."GraphId" WHERE g."Title" LIKE '%' || :'tag' || '%' LIMIT 1), '2'
-- P2-2 知识点映射表结构可用（本课无 KP 时 count=0）
UNION ALL SELECT '映射:KP总数(本课)', (SELECT count(*)::text FROM "AppKnowledgePoints" WHERE "CourseId"=(SELECT "Id" FROM c)), '0'
-- P2-5 画像 / P2-6 推荐
UNION ALL SELECT '画像:行为日志条数(≥1)', (SELECT count(*)::text FROM "AppStudentActivityLogs" WHERE "CourseId"=(SELECT "Id" FROM c)), 'ge:1'
UNION ALL SELECT '推荐:本课推荐条数(≥1)', (SELECT count(*)::text FROM "AppStudentRecommendations" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted"), 'ge:1'
UNION ALL SELECT '推荐:已完成条数(≥1)', (SELECT count(*)::text FROM "AppStudentRecommendations" WHERE "CourseId"=(SELECT "Id" FROM c) AND NOT "IsDeleted" AND "Status"=3), 'ge:1'
-- 学生行为日志
UNION ALL SELECT '行为日志:条数(≥3)', (SELECT count(*)::text FROM "AppStudentActivityLogs" l JOIN "AppCourses" cc ON cc."Id"=l."CourseId" WHERE cc."Title"=(SELECT "Title" FROM "AppCourses" WHERE "Id"=(SELECT "Id" FROM c))), 'ge:3'
SQL
)"

if [ -z "$OUT" ]; then
  printf '  %s✗ 查询没有返回任何行（课程不存在？）%s\n' "$C_BAD" "$C_OFF"
  exit 1
fi

FAILED=0; TOTAL=0
while IFS='|' read -r label actual expected; do
  [ -n "${label:-}" ] || continue
  TOTAL=$((TOTAL+1))
  case "$expected" in
    ge:*)
      need="${expected#ge:}"
      if [ "${actual:-0}" -ge "$need" ] 2>/dev/null; then
        printf '  %s✓%s %s = %s (≥ %s)\n' "$C_OK" "$C_OFF" "$label" "$actual" "$need"
      else
        printf '  %s✗%s %s 期望 ≥ %s，实际 %s\n' "$C_BAD" "$C_OFF" "$label" "$need" "${actual:-空}"
        FAILED=$((FAILED+1))
      fi ;;
    *)
      if [ "$actual" = "$expected" ]; then
        printf '  %s✓%s %s = %s\n' "$C_OK" "$C_OFF" "$label" "$actual"
      else
        printf '  %s✗%s %s 期望 %s，实际 %s\n' "$C_BAD" "$C_OFF" "$label" "$expected" "${actual:-空}"
        FAILED=$((FAILED+1))
      fi ;;
  esac
done <<< "$OUT"

# 掌握度样例（不参与判定，打印出来便于肉眼核对是否符合 Beta 先验直觉）
SAMPLE="$(PSQL -At -t -F'|' -d "$PGDATABASE" -v title="$COURSE_FILTER" <<'SQL'
SELECT round(m."Mastery"::numeric,3), round(m."Evidence"::numeric,2), m."PracticeCount", m."Level"
FROM "AppKnowledgeMastery" m
JOIN "AppKnowledgeResources" k ON k."Id" = m."KnowledgeResourceId"
JOIN "AppCourses" c ON c."Id" = k."CourseId"
WHERE c."Title" = :'title' AND m."Evidence" > 0
ORDER BY m."Evidence" DESC LIMIT 1
SQL
)"
[ -n "$SAMPLE" ] && printf '\n  %s·%s 掌握度样例（mastery|evidence|练习次数|level）= %s\n' "$C_WARN" "$C_OFF" "$SAMPLE"

echo
echo "=============================================================================="
if [ "$FAILED" -eq 0 ]; then
  printf ' %s数据库交叉验证全部通过%s（%d 项）\n' "$C_OK" "$C_OFF" "$TOTAL"
else
  printf ' %s数据库交叉验证失败 %d/%d 项%s\n' "$C_BAD" "$FAILED" "$TOTAL" "$C_OFF"
  exit 1
fi
