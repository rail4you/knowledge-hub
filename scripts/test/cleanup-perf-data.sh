#!/usr/bin/env bash
# 清理 seed-perf-data.sh 造的测试数据，恢复开发库。
#
# 用法：
#   CONFIRM=yes scripts/test/cleanup-perf-data.sh
set -euo pipefail

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5433}"
DB_USER="${DB_USER:-postgres}"
DB_PASSWORD="${DB_PASSWORD:-postgres}"
DB_NAME="${DB_NAME:-KnowledgeHub}"

if [ "${CONFIRM:-}" != "yes" ]; then
  echo "将删除 $DB_NAME 中名称以 perf- 开头的资源/课程/租户，并删除 perf_kv 表。"
  echo "确认请设置 CONFIRM=yes 重新执行。"
  exit 1
fi

export PGPASSWORD="$DB_PASSWORD"
PSQL=(psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 -q)

"${PSQL[@]}" <<'SQL'
-- 先删课程（避免遗留），再删资源/租户
DELETE FROM "AppCourses"   WHERE "Title" LIKE 'perf-course-%';
DELETE FROM "AppResources" WHERE "Name"  LIKE 'perf-res-%';
DELETE FROM "AbpTenants"   WHERE "Name"  LIKE 'perf-tenant-%';
DROP TABLE IF EXISTS perf_kv;
SQL

echo "-> 清理完成："
"${PSQL[@]}" -c 'SELECT
  (SELECT count(*) FROM "AbpTenants")   AS tenants,
  (SELECT count(*) FROM "AppResources") AS resources,
  (SELECT count(*) FROM "AppCourses")   AS courses;'
