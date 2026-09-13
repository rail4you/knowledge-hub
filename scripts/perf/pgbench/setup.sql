-- 为 pgbench 读写场景创建独立压测表，避免污染业务表。
-- 运行：
--   psql -h localhost -p 5433 -U postgres -d KnowledgeHub -f scripts/perf/pgbench/setup.sql

DROP TABLE IF EXISTS perf_kv;

CREATE TABLE perf_kv (
  k  bigserial PRIMARY KEY,
  v  text NOT NULL,
  ts timestamptz NOT NULL DEFAULT now()
);

INSERT INTO perf_kv (v)
SELECT md5(g::text)
FROM generate_series(1, 100000) AS g;

ANALYZE perf_kv;
