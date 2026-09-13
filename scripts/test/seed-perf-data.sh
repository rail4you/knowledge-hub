#!/usr/bin/env bash
# 性能测试数据准备：以「克隆现有行 + 改写主键/名称/租户」的方式批量造数。
# 采用 jsonb 序列化/反序列化，避免硬编码表结构（表结构随迁移变化仍可用）。
#
# 用法：
#   CONFIRM=yes scripts/test/seed-perf-data.sh
#
# 环境变量：
#   DB_HOST=localhost DB_PORT=5433 DB_USER=postgres DB_PASSWORD=postgres DB_NAME=KnowledgeHub
#   COUNT=5000     每个实体克隆的行数（资源/课程）
#   TENANTS=3      额外创建的租户数（随机分配给克隆行）
#
# 前置条件：库中已存在至少 1 个租户、1 条 Resource、1 条 Course 作为模板。
set -euo pipefail

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5433}"
DB_USER="${DB_USER:-postgres}"
DB_PASSWORD="${DB_PASSWORD:-postgres}"
DB_NAME="${DB_NAME:-KnowledgeHub}"
COUNT="${COUNT:-5000}"
TENANTS="${TENANTS:-3}"

[[ "$COUNT" =~ ^[0-9]+$ ]] || { echo "COUNT 必须为整数"; exit 1; }
[[ "$TENANTS" =~ ^[0-9]+$ ]] || { echo "TENANTS 必须为整数"; exit 1; }

if [ "${CONFIRM:-}" != "yes" ]; then
  echo "该脚本会向 $DB_HOST:$DB_PORT/$DB_NAME 写入性能测试数据。"
  echo "确认请设置 CONFIRM=yes 重新执行。"
  exit 1
fi

export PGPASSWORD="$DB_PASSWORD"
PSQL=(psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 -q)

echo "-> 检查模板数据 ..."
"${PSQL[@]}" -tAc 'SELECT count(*) FROM "AbpTenants"' | grep -qv '^0$' \
  || { echo "库中没有租户，请先完成安装/迁移。"; exit 1; }
"${PSQL[@]}" -tAc 'SELECT count(*) FROM "AppResources"' | grep -qv '^0$' \
  || { echo "库中没有 Resource 模板，请先在系统中创建至少 1 条资源。"; exit 1; }

sql_file="$(mktemp)"
trap 'rm -f "$sql_file"' EXIT

cat > "$sql_file" <<SQL
-- 1) 额外租户（克隆首个租户）
DO \$\$
DECLARE j jsonb; i int; newid text;
BEGIN
  SELECT to_jsonb(t) INTO j FROM "AbpTenants" t ORDER BY "CreationTime" LIMIT 1;
  FOR i IN 1..$TENANTS LOOP
    newid := gen_random_uuid()::text;
    j := jsonb_set(j, '{Id}', to_jsonb(newid));
    j := jsonb_set(j, '{Name}', to_jsonb('perf-tenant-' || i));
    BEGIN
      INSERT INTO "AbpTenants" SELECT * FROM jsonb_populate_record(NULL::"AbpTenants", j);
    EXCEPTION WHEN unique_violation THEN
      NULL;
    END;
  END LOOP;
END \$\$;

-- 2) 资源
DO \$\$
DECLARE j jsonb; i int; newid text; tids uuid[]; tid uuid;
BEGIN
  SELECT array_agg("Id") INTO tids FROM "AbpTenants";
  SELECT to_jsonb(r) INTO j FROM "AppResources" r ORDER BY "CreationTime" LIMIT 1;
  FOR i IN 1..$COUNT LOOP
    newid := gen_random_uuid()::text;
    tid := tids[1 + floor(random() * array_length(tids, 1))::int];
    j := jsonb_set(j, '{Id}', to_jsonb(newid));
    j := jsonb_set(j, '{Name}', to_jsonb('perf-res-' || i));
    j := jsonb_set(j, '{TenantId}', to_jsonb(tid::text));
    INSERT INTO "AppResources" SELECT * FROM jsonb_populate_record(NULL::"AppResources", j);
  END LOOP;
END \$\$;

-- 3) 课程（若存在模板）
DO \$\$
DECLARE j jsonb; i int; newid text; tids uuid[];
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "AppCourses") THEN
    RAISE NOTICE 'No course template, skip.';
    RETURN;
  END IF;
  SELECT array_agg("Id") INTO tids FROM "AbpTenants";
  SELECT to_jsonb(c) INTO j FROM "AppCourses" c ORDER BY "CreationTime" LIMIT 1;
  FOR i IN 1..$COUNT LOOP
    newid := gen_random_uuid()::text;
    j := jsonb_set(j, '{Id}', to_jsonb(newid));
    -- 注意：AppCourses 的名称列是 Title（不是 Name）
    j := jsonb_set(j, '{Title}', to_jsonb('perf-course-' || i));
    j := jsonb_set(j, '{TenantId}', to_jsonb(tids[1 + floor(random() * array_length(tids, 1))::int]::text));
    INSERT INTO "AppCourses" SELECT * FROM jsonb_populate_record(NULL::"AppCourses", j);
  END LOOP;
END \$\$;

ANALYZE "AppResources";
ANALYZE "AppCourses";
SQL

echo "-> 造数中（tenants=${TENANTS}, count=${COUNT}）..."
"${PSQL[@]}" -f "$sql_file"

echo "-> 完成。统计："
"${PSQL[@]}" -c 'SELECT
  (SELECT count(*) FROM "AbpTenants")   AS tenants,
  (SELECT count(*) FROM "AppResources")    AS resources,
  (SELECT count(*) FROM "AppCourses")      AS courses;'
