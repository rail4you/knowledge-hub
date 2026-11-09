#!/usr/bin/env bash
# =============================================================================
# 教学资源库闭环 E2E · 数据库侧独立交叉验证
# -----------------------------------------------------------------------------
# 不依赖 E2E 脚本的断言，直接查库核对闭环是否真的落到位。
# 适合在「脚本说通过」之后，再做一次与代码无关的独立复核。
#
# 用法：
#   ./verify-db.sh                        # 本地（localhost:5433）
#   PGHOST=47.99.245.49 PGPORT=5433 ./verify-db.sh    # 远程
#   TENANT_NAME=qidi RUN_TAG=1109-074208 ./verify-db.sh  # 精确匹配某次运行
#
# 依赖：psql + 环境变量 PGPASSWORD（默认 postgres）
# =============================================================================
set -euo pipefail

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5433}"
PGUSER="${PGUSER:-postgres}"
PGPASSWORD="${PGPASSWORD:-postgres}"
PGDATABASE="${PGDATABASE:-KnowledgeHub}"
TENANT_NAME="${TENANT_NAME:-qidi}"
RUN_TAG="${RUN_TAG:-}"
LIB_PATTERN="${LIB_PATTERN:-数据分析资源库%}"

export PGHOST PGPORT PGUSER PGPASSWORD PGDATABASE

# 远程宿主可能没装 psql（只在 postgres 容器里），此时用 PSQL_IN_CONTAINER=1
if [ "${PSQL_IN_CONTAINER:-0}" = "1" ]; then
  PG_CONTAINER="${PG_CONTAINER:-postgres}"
  PSQL_RUN() { docker exec -e PGPASSWORD="$PGPASSWORD" "$PG_CONTAINER" \
                 psql -U "$PGUSER" -d "$PGDATABASE" "$@"; }
else
  PSQL_RUN() { psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" "$@"; }
fi

echo "=============================================================================="
echo " 教学资源库闭环 · 数据库交叉验证"
echo " DB: $PGUSER@$PGHOST:$PGPORT/$PGDATABASE  租户=$TENANT_NAME  标记=${RUN_TAG:-全部}"
echo "=============================================================================="

LIB_FILTER="$LIB_PATTERN"
COURSE_FILTER="Python 数据分析%"
[ -n "$RUN_TAG" ] && { LIB_FILTER="数据分析资源库-${RUN_TAG}"; COURSE_FILTER="Python 数据分析-${RUN_TAG}"; }

echo
echo "▶ ① 资源库（状态 1=已发布 / 可见性 0=私有）"
PSQL_RUN -v ON_ERROR_STOP=1 -c "
SELECT l.\"Name\" AS 资源库, l.\"Status\" AS 库状态, l.\"Visibility\" AS 可见性,
       l.\"IsPublic\" AS 公开展示, l.\"Principal\" AS 负责人, t.\"CreationTime\"
FROM \"AppResourceLibraries\" l
LEFT JOIN \"AbpTenants\" tn ON tn.\"Id\" = l.\"TenantId\"
LEFT JOIN \"AppResourceLibraryTeachers\" lt ON lt.\"LibraryId\" = l.\"Id\"
LEFT JOIN \"AbpUsers\" t ON t.\"Id\" = lt.\"UserId\"
WHERE l.\"Name\" LIKE '$LIB_FILTER' AND tn.\"Name\" = '$TENANT_NAME';"

echo "▶ ② 库 → 老师分配"
PSQL_RUN -v ON_ERROR_STOP=1 -c "
SELECT l.\"Name\" AS 资源库, u.\"UserName\" AS 老师, u.\"Name\" AS 姓名, lt.\"AssignedTime\"
FROM \"AppResourceLibraries\" l
JOIN \"AppResourceLibraryTeachers\" lt ON lt.\"LibraryId\" = l.\"Id\"
JOIN \"AbpUsers\" u ON u.\"Id\" = lt.\"UserId\"
WHERE l.\"Name\" LIKE '$LIB_FILTER';"

echo "▶ ③ 库 → 课程 → 库资源 → 准入（核心四链路）"
PSQL_RUN -v ON_ERROR_STOP=1 -c "
SELECT l.\"Name\" AS 资源库,
       c.\"Title\" AS 课程, c.\"Status\" AS 课程状态,
       r.\"Name\" AS 库资源, r.\"Status\" AS 资源审核态,
       p.\"Status\" AS 库准入态, r.\"SourceType\" AS 来源, r.\"SourceResourceId\" AS 源资源id
FROM \"AppResourceLibraries\" l
JOIN \"AppResourceLibraryCourses\" lc ON lc.\"LibraryId\" = l.\"Id\"
JOIN \"AppCourses\" c ON c.\"Id\" = lc.\"CourseId\"
LEFT JOIN \"AppResourceLibraryResources\" lr ON lr.\"LibraryId\" = l.\"Id\"
LEFT JOIN \"AppResources\" r ON r.\"Id\" = lr.\"ResourceId\"
LEFT JOIN \"AppResourcePublications\" p ON p.\"ContentId\" = r.\"Id\"
WHERE l.\"Name\" LIKE '$LIB_FILTER';"

echo "▶ ④ 课程资源（原始两份，须 SchoolApproved=2）"
PSQL_RUN -v ON_ERROR_STOP=1 -c "
SELECT c.\"Title\" AS 课程, r.\"Name\" AS 资源, r.\"Status\" AS 审核态,
       r.\"FileExtension\" AS 扩展名, r.\"FileSize\" AS 字节,
       (r.\"FilePath\" IS NOT NULL AND r.\"FilePath\" <> '') AS 有落盘路径
FROM \"AppCourses\" c
JOIN \"AppCourseResources\" cr ON cr.\"CourseId\" = c.\"Id\"
JOIN \"AppResources\" r ON r.\"Id\" = cr.\"ResourceId\"
WHERE c.\"Title\" LIKE '$COURSE_FILTER' AND c.\"IsDeleted\" = false;"

echo "▶ ⑤ 章节层级 + 章节资源 + 知识点资源"
PSQL_RUN -v ON_ERROR_STOP=1 -c "
SELECT ch.\"Title\" AS 章节, (ch.\"ParentId\" IS NULL) AS 顶级,
       (SELECT count(*) FROM \"AppChapterResources\" cr WHERE cr.\"ChapterId\"=ch.\"Id\") AS 章节资源数,
       (SELECT count(*) FROM \"AppKnowledgeResources\" kr WHERE kr.\"ChapterId\"=ch.\"Id\" AND kr.\"IsDeleted\"=false) AS 知识点数
FROM \"AppChapters\" ch
JOIN \"AppCourses\" c ON c.\"Id\" = ch.\"CourseId\"
WHERE c.\"Title\" LIKE '$COURSE_FILTER' AND c.\"IsDeleted\" = false
ORDER BY ch.\"SortOrder\", ch.\"Title\";"

echo "▶ ⑥ 学生选课 + 学习进度"
PSQL_RUN -v ON_ERROR_STOP=1 -c "
SELECT u.\"UserName\" AS 学生, c.\"Title\" AS 课程, sc.\"Status\" AS 选课态, sc.\"Progress\" AS 进度
FROM \"AppStudentCourses\" sc
JOIN \"AbpUsers\" u ON u.\"Id\" = sc.\"StudentId\"
JOIN \"AppCourses\" c ON c.\"Id\" = sc.\"CourseId\"
WHERE c.\"Title\" LIKE '$COURSE_FILTER' AND c.\"IsDeleted\" = false;"

PSQL_RUN -v ON_ERROR_STOP=1 -c "
SELECT c.\"Title\" AS 课程, p.\"ChapterId\" AS 章节id, p.\"ResourceId\" AS 资源id, p.\"Progress\" AS 进度
FROM \"AppLearningProgress\" p
JOIN \"AppCourses\" c ON c.\"Id\" = p.\"CourseId\"
WHERE c.\"Title\" LIKE '$COURSE_FILTER' AND c.\"IsDeleted\" = false;"

echo "▶ ⑦ 审核单时间线（提交人 ≠ 审核人 = 回避本人生效）"
PSQL_RUN -v ON_ERROR_STOP=1 -c "
SELECT tt.\"TargetType\" AS 目标类型, tt.\"ActionType\" AS 动作, tt.\"Stage\" AS 阶段,
       tt.\"Status\" AS 状态, su.\"UserName\" AS 提交人, au.\"UserName\" AS 审核人,
       ra.\"Operation\" AS 操作, ra.\"Reason\" AS 原因
FROM \"AppReviewTickets\" tt
JOIN \"AppResourceLibraries\" l ON l.\"Id\" = tt.\"LibraryId\"
LEFT JOIN \"AbpUsers\" su ON su.\"Id\" = tt.\"SubmittedBy\"
LEFT JOIN \"AppReviewActions\" ra ON ra.\"TicketId\" = tt.\"Id\"
LEFT JOIN \"AbpUsers\" au ON au.\"Id\" = ra.\"ActorId\"
WHERE l.\"Name\" LIKE '$LIB_FILTER'
ORDER BY tt.\"CreationTime\", ra.\"CreationTime\";"

echo "=============================================================================="
echo " 判定口径（与 E2E 脚本一致）"
echo "   库状态 1=已发布 / 可见性 0=私有 / 课程状态 5=已发布"
echo "   资源审核态 2=SchoolApproved / 库准入态 2=Published / 来源 1=来自课程"
echo "   审核单状态 1=已通过；提交人必须 ≠ 审核人"
echo "=============================================================================="
