-- pgbench 读写脚本：对独立压测表 perf_kv 做 INSERT/UPDATE/SELECT/DELETE，
-- 用于制造 WAL、锁、vacuum 压力，同时避免影响业务表。
-- 先执行 setup.sql 初始化。
-- 运行：
--   pgbench -h localhost -p 5433 -U postgres -c 50 -j 8 -T 120 \
--     -f scripts/perf/pgbench/read-write.sql KnowledgeHub

\set k random(1, 100000)

INSERT INTO perf_kv (v) VALUES (md5(random()::text));

UPDATE perf_kv
SET v = md5(random()::text), ts = now()
WHERE k = :k;

SELECT v FROM perf_kv WHERE k = :k;

DELETE FROM perf_kv WHERE k = :k;
