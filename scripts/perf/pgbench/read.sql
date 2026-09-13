-- pgbench 只读脚本：模拟 API 的典型查询（主键点查 + 租户/状态过滤聚合）。
-- 运行：
--   pgbench -h localhost -p 5433 -U postgres -c 100 -j 8 -T 120 \
--     -f scripts/perf/pgbench/read.sql KnowledgeHub

\set rid random(1, 1000000)

-- 1) 主键点查（随机 uuid，强制走 PK 索引）
SELECT 1
FROM "AppResources"
WHERE "Id" = md5(:rid::text || clock_timestamp()::text)::uuid
LIMIT 1;

-- 2) 租户 + 状态过滤聚合（应命中 TenantId/Status 相关索引）
SELECT count(*)
FROM "AppResources"
WHERE "TenantId" = md5(:rid::text)::uuid
  AND "Status" = 1;

-- 3) 按创建时间排序取前 20（列表页常见模式）
SELECT "Id", "Name"
FROM "AppResources"
ORDER BY "CreationTime" DESC
LIMIT 20;
