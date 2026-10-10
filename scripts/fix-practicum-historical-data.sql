-- =============================================================================
-- 实训历史脏数据修复脚本
-- 背景：旧版 PracticumAppService.ReplaceTasksAsync 在每次保存项目时会“全删全建”任务，
--       导致同一个任务产生多份不同 GUID 的记录（旧的被软删除）。历史提交因此指向旧任务 ID，
--       造成：关闭重复提交仍可提交（BUG-005）、进度 200%（BUG-006）、提交任务列空标题。
--
-- 策略（与产品确认）：
--   1) 任务删除 = 归档隐藏（不物理删除）；
--   2) 不同版本保留为不同任务（不合并）；
--   3) 因此这里把“已被软删除但仍有提交历史”的任务恢复出来并标记为 IsArchived=true，
--      既能在历史/评分列表中解析出任务名，又不会重新出现在学生提交入口和进度分母中。
--
-- 执行顺序：先跑「步骤1 预览」，确认影响行，再跑「步骤2 执行」。
-- 数据库：PostgreSQL（KnowledgeHub）
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 步骤 1：预览 —— 将被恢复并归档的历史任务
-- ---------------------------------------------------------------------------
SELECT t."ProjectId",
       t."Id"   AS task_id,
       t."Title" AS task_title,
       count(s."Id") AS submission_count
FROM "AppPracticumTasks" t
JOIN "AppPracticumSubmissions" s ON s."TaskId" = t."Id"
WHERE t."IsDeleted" = true
GROUP BY t."ProjectId", t."Id", t."Title"
ORDER BY t."ProjectId", t."Title";

-- ---------------------------------------------------------------------------
-- 步骤 2：执行 —— 恢复并归档“有提交历史”的已删除任务
-- ---------------------------------------------------------------------------
UPDATE "AppPracticumTasks" t
SET "IsDeleted" = false,
    "IsArchived" = true,
    "DeletionTime" = NULL,
    "DeleterId" = NULL
WHERE t."IsDeleted" = true
  AND EXISTS (
      SELECT 1 FROM "AppPracticumSubmissions" s WHERE s."TaskId" = t."Id"
  );

-- ---------------------------------------------------------------------------
-- 步骤 3：兜底 —— 历史进度超过 100% 的报名记录强制收敛（应用侧也会在下次读取时重算）
-- ---------------------------------------------------------------------------
UPDATE "AppPracticumEnrollments"
SET "Progress" = 100
WHERE "Progress" > 100;

-- ---------------------------------------------------------------------------
-- 步骤 4：校验 —— 修复后，同一项目不应再有“同标题的多个启用任务”误导展示
-- ---------------------------------------------------------------------------
SELECT p."Title" AS project_title,
       t."Title" AS task_title,
       bool_or(t."IsArchived") AS any_archived,
       count(*) AS task_rows
FROM "AppPracticumTasks" t
JOIN "AppPracticumProjects" p ON p."Id" = t."ProjectId"
GROUP BY p."Title", t."Title"
HAVING count(*) > 1
ORDER BY p."Title", t."Title";
