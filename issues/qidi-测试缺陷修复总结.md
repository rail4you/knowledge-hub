# 易课通 qidi 测试缺陷修复总结（基于测试）

对应报告：`易课通_qidi_完整测试报告_20261010.md`
修复计划：`issues/qidi-测试缺陷修复计划.md`
实施日期：2026-10-10
代码状态：已实现并本地验证（`develop` 工作区）

---

## 1. 验证环境

| 项目 | 值 |
|------|----|
| API | https://localhost:44305（本地，已应用新迁移 `20261010021632_AddPracticumTaskIsArchived`） |
| Angular | http://localhost:4200（Angular 21 dev，AOT） |
| 数据库 | PostgreSQL localhost:5433 / KnowledgeHub |
| 登录 | host `admin / 1q2w3E*`；新建测试学生 `qa_student_fix / Test123!`（Student 角色） |
| 生产取证 | www.iketong.com（BUG-001 控制台报错、BUG-002/005/006 数据库实证，见第 3 节） |

> 说明：为生成 Angular 代理，本次临时启动了本地 API；代理生成完成后应用已重新构建并保持运行。

---

## 2. 四条业务策略的落地方式

| 策略（审核决定） | 落地实现 | 代码位置 |
|------------------|----------|----------|
| 1. 任务删除 = **归档隐藏** | 项目保存时不再物理/软删除任务：未回传的任务置 `IsArchived=true`；任务列表、进度分母、学生提交入口均排除归档任务；历史提交仍可解析任务名 | `PracticumTask.IsArchived`；`PracticumAppService.SyncTasksAsync` / `GetTaskDtosAsync` / `RefreshEnrollmentProgressAsync` |
| 2. 允许重复提交时：**自动清空旧分、回到待评阅，旧分留历史** | 新提交成功后清空 `enrollment.FinalScore/FinalComment/CompletedAt`，状态回到 `Submitted`（待评阅）；旧提交记录保留自身 `Score/TeacherFeedback/ReviewedAt` | `PracticumAppService.CreateSubmissionAsync` |
| 3. **不同版本保留为不同任务** | 采用按 `Id` upsert + 归档，不合并、不重建；历史任务 ID 与其提交一一保留；数据修复脚本把“已软删除但有提交”的任务恢复并归档 | `SyncTasksAsync`；`scripts/fix-practicum-historical-data.sql` |
| 4. **完成后锁定，推广到其他模块，不要重复完成** | 实训：关闭重复提交后已提交/已完成一律拒绝；项目 Completed 后隐藏提交入口；完成后不能重新报名。课程：已完成不允许退课。微专业/就业经核查已有防重复逻辑 | `PracticumAppService.CreateSubmissionAsync` / `EnrollAsync`；`CourseAppService.DropAsync`；`student-practicum-detail.component.*` |

---

## 3. 缺陷 → 根因 → 修复 → 验证

### BUG-001 课程学分、学时保存后为空（P2）

- **根因**：`course-list.component.html` 模板里 `Number($event)` 被 Angular 编译为 `ctx.Number($event)`，组件无 `Number` 属性 → 抛 `TypeError: l.Number is not a function` → `ngModelChange` 未执行 → 请求体不带 `credits/semesterHours` → 数据库写 null。
- **生产取证**：生产浏览器控制台 `TypeError: l.Number is not a function`；`AppCourses` 中 UI 创建的课程 `Credits` 均为 null；直接 `POST` 带 `credits` 可正常落库。
- **修复**：`(ngModelChange)="formData.credits = $event === '' || $event === null || $event === undefined ? undefined : +$event"`（学时同理）。
- **修复文件**：`angular/src/app/learning/course-list/course-list.component.html`

### BUG-002 学习完成率分母不一致（P1）

- **根因**：教师端 `BuildExerciseChapterMap` 只判断 `Exercise.ChapterId` 是否有值，不校验章节是否属于本课程；当章节已被删除（习题仍保留旧 ChapterId）时分母被撑大，而章节明细/学生端会排除该题。
- **生产取证**：课程 `测试课程` 的习题「单选题 1」指向的章节 `3a2086cd-4f8a-...` 在 `AppChapters` 中已不存在；教师总题数 2、章节明细 1、学生 1/1。
- **修复**：`BuildExerciseChapterMap` 增加 `chapterCourseMap` 参数，仅当“章节所属课程 == 习题所属课程”时计入；4 处调用统一传 `chapters.ToDictionary(c => c.Id, c => c.CourseId)`。
- **修复文件**：`src/KnowledgeHub.Application/Learning/StudentExerciseRecordAppService.cs`

### BUG-003 收藏后计数不即时更新（P2）

- **根因**：`toggleCollection` 只在“我的收藏”分类下取消收藏时更新 `favoritesCount`；普通列表新增收藏时不更新。
- **修复**：成功回调对 `nextValue` 的双向统一 `favoritesCount ± 1`。
- **修复文件**：`angular/src/app/student/resources/student-resources.component.ts`

### BUG-004 搜索无结果误提示机构未发布资源（P3）

- **根因**：空态文案写死为“该租户下暂无资源/该院校机构暂未发布任何资源”。
- **修复**：新增 `emptyState()` 计算属性，区分 **加载失败 / 搜索筛选无命中 / 收藏为空 / 租户真空** 四种文案。
- **修复文件**：`.../student-resources.component.ts`、`.../student-resources.component.html`

### BUG-005 关闭重复提交后仍可新增成果（P1）

- **根因**：`UpdateAsync` 的 `ReplaceTasksAsync` 每次保存都软删除旧任务、用新 GUID 重建；`CreateSubmissionAsync` 的重复提交判断按 `TaskId` 比对，新任务下查不到旧提交 → 放行。
- **生产取证**：同一 QA 项目同标题任务两条（一条 `IsDeleted=t`）；V1/V2 指向旧任务、V3 指向新任务；项目 `AllowResubmission=f`。
- **修复**：`SyncTasksAsync` 按 `Id` upsert，未回传的归档隐藏（不删）；前端在任务保存/编辑与资料保存时回传 `task.id`；重复提交判断基于稳定的任务 ID；并新增“项目 Completed 后拒绝提交”。
- **修复文件**：`PracticumAppService.cs`、`PracticumDtos.cs`、`practicum-management.component.ts`、`practicum-tasks.component.ts`、`practicum.service.ts`（手写 DTO）

### BUG-006 同一任务多次提交进度 200%（P2）

- **根因**：`RefreshEnrollmentProgressAsync` 用 `submissions.Select(TaskId).Distinct().Count()` 作分子，`tasks.Count` 作分母；任务重建后新旧 TaskId 各算 1 → 2/1。
- **修复**：分子只统计“当前启用（未归档）任务集合内”的去重任务数；`Progress = Clamp(…, 0, 100)`；达到 100% 且已评阅时写 `CompletedAt`。
- **修复文件**：`PracticumAppService.cs`
- **配套**：`practicum-tasks.component.html/ts` 提交任务列按任务名去重、过滤空标题，消除“、、”。

### 关联 OBS-09 / OBS-10 成绩版本与成果版本脱钩

- 版本号 `VersionNo` 现在按稳定任务 ID 递增；允许重复提交时清空旧分并回到待评阅，旧分保留在历史提交记录中；前端在详情/列表按任务 ID 取任务分值。

---

## 4. 测试用例明细（数据 / 预期 / 实际）

> 以下用例均为本次在本地环境实际执行的脚本/浏览器操作。`scripts` 中的 curl 脚本可复现。

### TC-001　BUG-001 学分学时保存（UI 实机）

| 项 | 内容 |
|----|------|
| 测试数据 | 新建课程 `FIXTEST_UI_学分学时`，学分 `2`、学时 `16` |
| 操作 | 以 host admin 在 `/learning/course-list` 打开「新建课程」，填名称、学分、学时，点「保存」 |
| 预期 | 保存成功，数据库写入 `Credits=2, SemesterHours=16` |
| 实际 | `SELECT Title,Credits,SemesterHours` → `FIXTEST_UI_学分学时 \| 2 \| 16`；控制台 0 error（修复前为 `l.Number is not a function`） |
| 结论 | **通过** |

### TC-002　BUG-005 关闭重复提交后拒绝二次提交（API）

| 项 | 内容 |
|----|------|
| 测试数据 | 项目 `FIXTEST_实训_闭环`(`3a2435cd-28d0-...`)；任务 `FIXTEST_任务A`；学生 `qa_student_fix` |
| 操作 | ① 建项目(允许重复提交=true, 1 任务) → ② 学生报名 → ③ 提交 V1 → ④ 教师改项目仅关闭“允许重复提交”并保存 → ⑤ 学生再提交 V2 |
| 预期 | ④ 保存后任务 ID 不变；⑤ 服务端拒绝并给出原因 |
| 实际 | ④ `taskCount=1, taskIds=['3a2435cd-28d2-...'], isArchived=[false]`（ID 不变）；⑤ `403` + `当前项目不允许重复提交，已完成或已提交的成果不能再次提交。` |
| 结论 | **通过** |

### TC-003　BUG-006 进度不再超过 100%（API）

| 项 | 内容 |
|----|------|
| 测试数据 | TC-002 的报名记录 |
| 操作 | 读取 `enrollment-list` |
| 预期 | 进度 = 100（1 个任务 / 已提交），不是 200% |
| 实际 | `progress=100, status=2` |
| 结论 | **通过** |

### TC-004　策略1 任务归档隐藏（API）

| 项 | 内容 |
|----|------|
| 操作 | 项目保存时不回传任务（`tasks: []`） |
| 预期 | 任务被归档隐藏，不物理删除；学生不能再提交 |
| 实际 | `taskCount=0, tasks=0`；学生提交旧任务返回 `403 该实训任务已归档，无法提交成果。` |
| 结论 | **通过** |

### TC-005　策略2 允许重复提交清空旧分、保留历史（API）

| 项 | 内容 |
|----|------|
| 测试数据 | 项目 `FIXTEST_重提交_闭环`；提交 V1 → 教师给 85 分 |
| 操作 | 教师评分后，学生再次提交 V2（允许重复提交开启） |
| 预期 | 评分后 `status=已完成, finalScore=85`；V2 提交后 `finalScore` 清空、状态回到待评阅；V1 仍保留 85 分 |
| 实际 | 评分后 `progress=100, status=4, finalScore=85`；提交 V2 后 `progress=100, status=2, finalScore=None, completedAt=None`；历史 `v1 content=FIXTEST_A_V1 score=85 status=2`，`v2 content=FIXTEST_A_V2 score=None status=0` |
| 结论 | **通过** |

### TC-006　策略3 提交任务名解析（API）

| 项 | 内容 |
|----|------|
| 操作 | 提交记录指向已归档任务时读取 `submission-list` |
| 预期 | `taskTitle` 可解析，不为空 |
| 实际 | `taskId=3a2435cd-28d2-... taskTitle='FIXTEST_任务A'` |
| 结论 | **通过**（前端按任务名去重拼接，无“、、”） |

### TC-007　BUG-002 完成率口径统一（API + 构造悬挂章节）

| 项 | 内容 |
|----|------|
| 测试数据 | 课程 `FIXTEST_课程_口径`；章节 `FIXTEST章节`；习题 E1 挂该章节、习题 E2 挂一个**不存在的章节**；学生选课并对 E1 作答 |
| 操作 | 分别调用 学生端 `learning/my-exercise-progress`、教师端 `course-learning-overview`、`learning-statistics`、`student-learning-detail` |
| 预期 | 四处分母都为 1（不计悬挂章节的 E2），已完成为 1 |
| 实际 | 学生 `1/1`；overview `totalExercises=1`，章节 `[('FIXTEST章节',1,1)]`；统计行 `QA 1/1 100`；明细 `totalCount=1 completed=1 chapters=[('FIXTEST章节',1,1)]` |
| 结论 | **通过**（修复前 overview/统计/明细分母会为 2） |

### TC-008　BUG-003 收藏计数即时同步（UI 实机）

| 项 | 内容 |
|----|------|
| 测试数据 | 学生 `qa_student_fix` 资源库（7 条已审核资源），收藏初始 0 |
| 操作 | 点击资源卡右上角收藏按钮（`.cover-favorite`） |
| 预期 | 按钮变“取消收藏”，收藏计数 0→1；取消后 1→0 |
| 实际 | 收藏后 `收藏=1`、`aria-label=取消收藏`；再点取消后 `收藏=0`、`aria-label=收藏` |
| 结论 | **通过** |

### TC-009　BUG-004 空态文案区分（UI 实机）

| 项 | 内容 |
|----|------|
| 操作 | 资源库搜索框输入 `QA_不存在_20261009` 并点「搜索」 |
| 预期 | 显示“未找到匹配的资源”，而不是“该租户下暂无资源” |
| 实际 | 空态显示 `未找到匹配的资源 / 没有符合当前关键词或筛选条件的资源，请调整关键词或清除筛选后重试` |
| 结论 | **通过** |

### TC-010　策略4 完成后锁定退课（API）

| 项 | 内容 |
|----|------|
| 测试数据 | 课程 `FIXTEST_课程_退课锁定`；学生已选课，`Status=Completed(2), Progress=100` |
| 操作 | 学生调用退课 |
| 预期 | 已完成课程拒绝退课；未完成可正常退课 |
| 实际 | 已完成 → `403 课程已完成，不能退课。如需重学请联系教师。`；改为 `Enrolled/0` 后 → `204` |
| 结论 | **通过** |

### TC-011　历史进度 >100% 自愈（API）

| 项 | 内容 |
|----|------|
| 测试数据 | 把报名进度手动改为 `250` |
| 操作 | 学生读取 `my-enrollments` |
| 预期 | 读取时重算并收敛为 `100` |
| 实际 | API 返回 `progress=100`，数据库同步为 `100` |
| 结论 | **通过** |

---

## 5. 数据修复脚本

新增 `scripts/fix-practicum-historical-data.sql`，用于清理旧版本“删后重建”产生的历史任务：

1. **预览**：列出“已软删除但仍有提交记录”的任务及提交数。
2. **执行**：把这些任务恢复并置 `IsArchived=true`（保留为独立任务，符合策略 3），清理删除标记。
3. **兜底**：把历史 `Progress>100` 的报名记录收敛为 100。
4. **校验**：按项目+任务标题聚合，确认同标题的任务行及其归档状态。

本地执行结果：命中并恢复 1 条历史任务（`测试任务`），其余软删除且无提交的任务保持不可见。

---

## 6. 代码变更清单

**后端**

| 文件 | 变更 |
|------|------|
| `src/KnowledgeHub.Domain/Practicums/PracticumTask.cs` | 新增 `IsArchived` |
| `src/KnowledgeHub.Application.Contracts/Practicums/Dtos/PracticumDtos.cs` | `CreateUpdatePracticumTaskDto.Id`、`PracticumTaskDto.IsArchived` |
| `src/KnowledgeHub.Application/Practicums/PracticumAppService.cs` | `SyncTasksAsync`（upsert+归档）、进度口径与 clamp、提交/报名锁定、重复提交清分留史、历史任务名解析 |
| `src/KnowledgeHub.Application/Learning/StudentExerciseRecordAppService.cs` | `BuildExerciseChapterMap` 课程归属校验，4 处调用对齐 |
| `src/KnowledgeHub.Application/Courses/CourseAppService.cs` | 已完成课程禁止退课 |
| `src/KnowledgeHub.EntityFrameworkCore/Migrations/20261010021632_AddPracticumTaskIsArchived.*` | 新增迁移 |

**前端**

| 文件 | 变更 |
|------|------|
| `angular/src/app/learning/course-list/course-list.component.html` | `Number()` → `+$event`（BUG-001） |
| `angular/src/app/student/resources/student-resources.component.ts/.html` | 收藏计数双向同步；四态空态文案（BUG-003/004） |
| `angular/src/app/admin/practicum/practicum-management.component.ts` | 保存项目/资料时回传任务 ID |
| `angular/src/app/admin/practicum/practicum-tasks.component.ts/.html` | 编辑任务回传 ID；提交任务列去重去空 |
| `angular/src/app/student/practicums/student-practicum-detail.component.ts/.html/.scss` | 完成后锁定提交入口；透传后端错误原因；加载已提交任务 |
| `angular/src/app/practicum/practicum.service.ts`（手写 DTO） | 补 `id` / `isArchived` |
| `angular/src/app/proxy/practicums/dtos/models.ts` | 由 `abp generate-proxy` 生成 |

**脚本**

| 文件 | 用途 |
|------|------|
| `scripts/fix-practicum-historical-data.sql` | 历史实训任务/进度修复 |

---

## 7. 部署与回归注意事项

1. **先迁移**：应用 `AddPracticumTaskIsArchived`（`dotnet run --project src/KnowledgeHub.DbMigrator` 或 `dotnet ef database update`），否则实训相关接口会因缺列报错。
2. **再执行数据修复脚本**（生产）：先跑预览 SELECT，确认后再执行 UPDATE。
3. **Angular 代理**：本次已重新生成；若后续再改 Contracts DTO，请重跑 `abp generate-proxy -t ng`（当前 ABP CLI 会把 `microsoft/asp-net-core/http/models.ts` 与 `teaching-agents/agent-run.service.ts` 生成坏，已手动还原，注意回归时不要覆盖）。
4. **回归重点**：BUG-001/002/003/004 用例 + 实训 TC-002~006 + TC-010/011，三角色在同一版本上执行。

---

## 8. 已知边界与后续事项

- 历史数据里“任务被重建、而最新提交仍留在旧任务上”的项目：按策略 3 旧任务保留为归档任务，其提交不计入当前启用任务的进度；如需把这类项目的历史提交计入当前进度，需要额外的数据映射决策。
- `OBS-01/02/03/04/05/07/08` 未在本次范围内处理（沿用报告中的待确认状态）。
- 微专业、就业投递经代码核查已具备防重复完成/重复投递逻辑，本次未新增改动；如需强制“完成后锁定”的统一文案与入口策略，可在下一批次补齐。
- 前端 `student-resources` 存在一个与本任务无关的既有 403（`resource-share/shared-to-me`）控制台报错，未处理。
