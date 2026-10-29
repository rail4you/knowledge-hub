-- ═══════════════════════════════════════════════════════════════
-- 教学班种子数据（开发环境实例）
--
-- 用途：
--   1. 给「用户管理 → 学生归班」提供可选的教学班；
--   2. 给「教学智能体任务 → 选择学生」的班级 / 专业筛选提供真实数据。
--
-- 说明：
--   · 班级是租户级数据，本脚本只造「启迪学院111」租户的班级；
--   · 学生与班级的对应关系存在 AppTeachingClassMembers（关系的事实来源）；
--   · AbpUsers."ClassName" 与 ExtraProperties."MajorId" 是冗余字段，
--     由后端在归班时同步回写，这里一并补齐，保持与关系表一致；
--   · 可重复执行（ON CONFLICT DO NOTHING）。
-- ═══════════════════════════════════════════════════════════════

\set TENANT_ID '''3a2034d1-6c43-2219-8149-45182659c849'''

-- ── 教学班 ────────────────────────────────────────────────────
INSERT INTO "AppTeachingClasses"
  ("Id", "TenantId", "Name", "MajorId", "GradeYear", "Description", "Status",
   "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
VALUES
  ('11111111-aaaa-4111-8111-000000000001', :TENANT_ID, '2026 级机电一班',
   '11111111-1111-4111-8111-000000000001', 2026, '智能制造技术专业 2026 级 1 班', 0,
   '{}', 'seed-2026-jd-1', now(), false),
  ('11111111-aaaa-4111-8111-000000000002', :TENANT_ID, '2026 级市场营销一班',
   '3a21eb5b-4398-43f1-4bd1-222d678314dd', 2026, '智能市场营销专业 2026 级 1 班', 0,
   '{}', 'seed-2026-sc-1', now(), false),
  ('11111111-aaaa-4111-8111-000000000003', :TENANT_ID, '2026 级数字创意一班',
   '11111111-1111-4111-8111-000000000002', 2026, '数字创意设计专业 2026 级 1 班', 0,
   '{}', 'seed-2026-sz-1', now(), false),
  ('11111111-aaaa-4111-8111-000000000004', :TENANT_ID, '2025 级护理一班',
   '3a221503-56af-521c-ba68-1703467b9f46', 2025, '医疗专业 2025 级 1 班', 0,
   '{}', 'seed-2025-hl-1', now(), false)
ON CONFLICT ("Id") DO UPDATE SET
  "Name" = EXCLUDED."Name",
  "MajorId" = EXCLUDED."MajorId",
  "GradeYear" = EXCLUDED."GradeYear",
  "Description" = EXCLUDED."Description";

-- ── 班级成员（学生 ↔ 班级） ────────────────────────────────────
INSERT INTO "AppTeachingClassMembers"
  ("Id", "TenantId", "ClassId", "StudentId", "StudentName", "StudentNumber",
   "JoinedAt", "Source", "CreationTime", "IsDeleted")
VALUES
  ('22222222-bbbb-4111-8111-000000000001', :TENANT_ID, '11111111-aaaa-4111-8111-000000000001',
   '3a2468d9-ccd0-886c-2d2b-beb4a1f048a6', '张晨', 'demo_student', now(), 0, now(), false),
  ('22222222-bbbb-4111-8111-000000000002', :TENANT_ID, '11111111-aaaa-4111-8111-000000000002',
   '3a2468d9-cd4c-ec84-c0cd-e6c189616dd2', '李思雨', 'demo_student2', now(), 0, now(), false),
  ('22222222-bbbb-4111-8111-000000000003', :TENANT_ID, '11111111-aaaa-4111-8111-000000000003',
   '2792815f-00e8-443d-bc4a-dc9a9a089360', 'QA学生(qidi)', 'qa_qidi_stu', now(), 0, now(), false),
  ('22222222-bbbb-4111-8111-000000000004', :TENANT_ID, '11111111-aaaa-4111-8111-000000000004',
   '3a20dcc5-e5b6-1f13-be69-f80f622e3718', '张梦倩', 'zmq', now(), 0, now(), false)
ON CONFLICT ("Id") DO NOTHING;

-- ── 用户档案冗余字段（与关系表保持一致） ───────────────────────
UPDATE "AbpUsers" SET
  "ClassName" = '2026 级机电一班',
  "Grade" = '2026',
  "ExtraProperties" = ("ExtraProperties"::jsonb || jsonb_build_object('MajorId', '11111111-1111-4111-8111-000000000001'))::text
WHERE "Id" = '3a2468d9-ccd0-886c-2d2b-beb4a1f048a6';

UPDATE "AbpUsers" SET
  "ClassName" = '2026 级市场营销一班',
  "Grade" = '2026',
  "ExtraProperties" = ("ExtraProperties"::jsonb || jsonb_build_object('MajorId', '3a21eb5b-4398-43f1-4bd1-222d678314dd'))::text
WHERE "Id" = '3a2468d9-cd4c-ec84-c0cd-e6c189616dd2';

UPDATE "AbpUsers" SET
  "ClassName" = '2026 级数字创意一班',
  "Grade" = '2026',
  "ExtraProperties" = ("ExtraProperties"::jsonb || jsonb_build_object('MajorId', '11111111-1111-4111-8111-000000000002'))::text
WHERE "Id" = '2792815f-00e8-443d-bc4a-dc9a9a089360';

UPDATE "AbpUsers" SET
  "ClassName" = '2025 级护理一班',
  "Grade" = '2025',
  "ExtraProperties" = ("ExtraProperties"::jsonb || jsonb_build_object('MajorId', '3a221503-56af-521c-ba68-1703467b9f46'))::text
WHERE "Id" = '3a20dcc5-e5b6-1f13-be69-f80f622e3718';
