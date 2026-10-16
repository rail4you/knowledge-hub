#!/usr/bin/env bash
# ============================================================================
# 从数据库重建 Meilisearch documents 索引（不重新解析源文件）
#
# 用途：Meilisearch 数据丢失 / 索引未构建（例如新服务器迁移了 Postgres 但没迁移
#       Meili 数据）时，直接用 KhPageContents 里已有的正文重建文本索引。
#       索引重建后 /ai/chat 的「热门词」、站内搜索会立即恢复。
#
# 运行位置：在部署服务器上执行（需要 docker + postgres/meilisearch 容器）。
#   ssh root@47.99.245.49 'bash -s' < scripts/reindex-meili-from-db.sh
#
# 幂等：可重复执行，每次先清空 documents 索引再写入。
# ============================================================================
set -euo pipefail

PG_CONTAINER="${PG_CONTAINER:-postgres}"
PGUSER="${PGUSER:-postgres}"
DB="${DB:-KnowledgeHub}"
MEILI_URL="${MEILI_URL:-http://127.0.0.1:7701}"
MEILI_KEY="${MEILI_KEY:-aSampleMasterKey}"
INDEX="${INDEX:-documents}"

TMP="$(mktemp)"
BACKUP_DIR="${BACKUP_DIR:-/root/kh-backup-$(date +%Y%m%d-%H%M%S)}"

psql_run() { docker exec -i "$PG_CONTAINER" psql -U "$PGUSER" -d "$DB" -v ON_ERROR_STOP=1 "$@"; }
trap 'rm -f "$TMP"' EXIT

echo "==> 1/4 备份 KhDocumentIndices 到 $BACKUP_DIR"
mkdir -p "$BACKUP_DIR"
docker exec "$PG_CONTAINER" pg_dump -U "$PGUSER" -d "$DB" -t '"KhDocumentIndices"' > "$BACKUP_DIR/KhDocumentIndices.sql"
echo "    备份完成: $(wc -l < "$BACKUP_DIR/KhDocumentIndices.sql") 行"

echo "==> 2/4 补齐缺失的 DocumentIndex 行 / 同步租户归属"
psql_run <<'SQL'
BEGIN;

INSERT INTO "KhDocumentIndices"
  ("Id","ResourceId","PageNumber","PageContent","PageTitle","EmbeddingVector","IndexingTaskId","IndexStatus","TenantId",
   "ExtraProperties","ConcurrencyStamp","CreationTime","CreatorId","LastModificationTime","LastModifierId","IsDeleted","DeleterId","DeletionTime")
SELECT gen_random_uuid(), pc."ResourceId", pc."PageNumber", pc."Content", NULL, NULL, NULL, 2, r."TenantId",
       '{}', gen_random_uuid()::text, now(), NULL, NULL, NULL, false, NULL, NULL
FROM "KhPageContents" pc
JOIN "AppResources" r ON r."Id" = pc."ResourceId"
LEFT JOIN "KhDocumentIndices" di
  ON di."ResourceId" = pc."ResourceId"
 AND di."PageNumber" = pc."PageNumber"
 AND di."IsDeleted" = false
WHERE di."Id" IS NULL;

UPDATE "KhDocumentIndices" di
SET "TenantId" = r."TenantId"
FROM "KhPageContents" pc
JOIN "AppResources" r ON r."Id" = pc."ResourceId"
WHERE di."ResourceId" = pc."ResourceId"
  AND di."PageNumber" = pc."PageNumber"
  AND di."IsDeleted" = false
  AND di."TenantId" IS DISTINCT FROM r."TenantId";

COMMIT;
SQL

echo "==> 3/4 从 KhPageContents 生成 Meili 文档 JSON"
psql_run -t -A <<'SQL' > "$TMP"
SELECT json_agg(doc ORDER BY (doc->>'uploadDate'), (doc->>'resourceName'), (doc->>'pageNumber')::int)
FROM (
  SELECT json_build_object(
    'id', di."Id"::text,
    'resourceId', r."Id"::text,
    'resourceName', coalesce(r."Name", ''),
    'resourceType', coalesce(r."ResourceType", 0),
    'categoryId', coalesce(r."CategoryId"::text, ''),
    'fileExtension', coalesce(r."FileExtension", ''),
    'keywords', coalesce(r."Keywords", ''),
    'description', coalesce(r."Description", ''),
    'pageNumber', pc."PageNumber",
    'pageContent', pc."Content",
    'pageTitle', coalesce(di."PageTitle", ''),
    'uploadDate', to_char(r."CreationTime", 'YYYY-MM-DD'),
    'tenantId', coalesce(r."TenantId"::text, ''),
    'status', r."Status",
    'relevanceScore', 1.0
  ) AS doc
  FROM "KhPageContents" pc
  JOIN "AppResources" r ON r."Id" = pc."ResourceId"
  JOIN "KhDocumentIndices" di
    ON di."ResourceId" = pc."ResourceId"
   AND di."PageNumber" = pc."PageNumber"
   AND di."IsDeleted" = false
) t;
SQL

DOC_COUNT="$(python3 -c "import json,sys;print(len(json.load(open('$TMP'))))" 2>/dev/null || echo '?')"
echo "    生成 $DOC_COUNT 篇文档，$(du -h "$TMP" | cut -f1)"

echo "==> 4/4 写入 Meilisearch 索引 [$INDEX]"
curl -sf -X DELETE "$MEILI_URL/indexes/$INDEX/documents" -H "Authorization: Bearer $MEILI_KEY" > /dev/null
RESP="$(curl -sf -X POST "$MEILI_URL/indexes/$INDEX/documents" \
  -H "Authorization: Bearer $MEILI_KEY" -H "Content-Type: application/json" \
  --data-binary "@$TMP")"
TASK_UID="$(printf '%s' "$RESP" | python3 -c "import sys,json;print(json.load(sys.stdin)['taskUid'])")"
echo "    taskUid=$TASK_UID，等待索引完成..."

for _ in $(seq 1 60); do
  T="$(curl -sf "$MEILI_URL/tasks/$TASK_UID" -H "Authorization: Bearer $MEILI_KEY")"
  STATUS="$(printf '%s' "$T" | python3 -c "import sys,json;print(json.load(sys.stdin)['status'])")"
  if [ "$STATUS" = "succeeded" ]; then break; fi
  if [ "$STATUS" = "failed" ]; then echo "    索引任务失败: $T" >&2; exit 1; fi
  sleep 1
done

curl -sf "$MEILI_URL/indexes/$INDEX/stats" -H "Authorization: Bearer $MEILI_KEY" \
  | python3 -c "import sys,json;d=json.load(sys.stdin);print('    完成：numberOfDocuments=%s' % d['numberOfDocuments'])"

echo "==> 索引重建完成。"
