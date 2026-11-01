#!/usr/bin/env bash
# iketong 环境：V2 基础种子数据（资源库模板）
# 资源库模板是宿主级数据（AppResourceLibraryTemplates 无 TenantId 列），全平台共用一条。
# 用法：ssh root@47.99.245.49 'bash -s' < scripts/test/iketong-v2-seed.sh
set -e
ADMIN_ID=00000000-0000-0000-0000-000000000001   # host admin

docker exec -i postgres psql -U postgres -d KnowledgeHub -v ON_ERROR_STOP=1 <<SQL
INSERT INTO "AppResourceLibraryTemplates"
  ("Id","Code","Name","Version","Description","ConfigJson","Status",
   "ExtraProperties","ConcurrencyStamp","CreationTime","CreatorId","LastModificationTime","LastModifierId","IsDeleted")
SELECT gen_random_uuid(), 'professional_resource_base', '通用专业教学资源库模板', 1, 'V2默认模板',
       '{"modules":["course","practice"],"reviewStages":["SCHOOL","LIBRARY"]}', 1,
       '{}', md5(random()::text), now(), '$ADMIN_ID', now(), '$ADMIN_ID', false
WHERE NOT EXISTS (
  SELECT 1 FROM "AppResourceLibraryTemplates" WHERE "Code"='professional_resource_base');
SQL

docker exec -i postgres psql -U postgres -d KnowledgeHub -t -A -c \
  "SELECT \"Id\",\"Code\",\"Name\",\"Version\",\"Status\" FROM \"AppResourceLibraryTemplates\";"
