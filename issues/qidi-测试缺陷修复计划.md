# 易课通 qidi 测试缺陷修复计划

来源：`易课通_qidi_完整测试报告_20261010.md`
制定日期：2026-10-10
状态：**已按审核决定实施完成**（详见 `issues/qidi-测试缺陷修复总结.md`）

> 审核决定（已落地）：
> 1. 任务删除 = 归档隐藏；
> 2. 允许重复提交时自动清空旧分、回到待评阅，旧分留作历史；
> 3. 不同版本保留为不同任务；
> 4. 完成后锁定并推广到其他模块（课程已完成禁止退课；微专业/就业已有防重复逻辑）。
>
> 实施结果：BUG-001~006 全部修复，另有课程退课锁定与历史数据修复脚本；测试证据见总结文档。

---

## 一、根因结论（已在生产环境验证）

> 下面每条都不是猜测，均在生产数据库 / 生产浏览器控制台 / 生产 API 上取证。

### RC-1　BUG-001 课程学分、学时保存后为空 —— 前端模板表达式运行时抛错

**证据：**

- 生产浏览器控制台（chunk-KHT4UA5T.js = `course-list` 懒加载块）在输入学分时抛出：
  `TypeError: l.Number is not a function`
- 编译产物把模板里的 `Number($event)` 编译成了 `ctx.Number($event)`；组件没有 `Number` 属性，处理函数抛错，`formData.credits = ...` 这句**永远不会执行**。
- 因此 `buildPayload()` 里 `credits` 是 `undefined`，`JSON.stringify` 会直接丢弃该字段，请求体里没有 `credits` → 后端 `input.Credits == null` → 数据库写 null。
- 反证 1：直接用 API `POST /api/app/course` 带 `credits:3, semesterHours:24`，返回并落库正确（临时探测课程已硬删除，无残留）。
- 反证 2：用 `GET /api/app/course/{id}` 取已有的 8 学分课程，返回 `credits:8, semesterHours:48` 正常。
- 反证 3：qidi 全部 5 条相关课程中，`AppCourses.Credits` 仅 1 条有值（且非近期 UI 会话创建），其余全为 null。

**根因：** `angular/src/app/learning/course-list/course-list.component.html` 第 231 / 246 行：
```
(ngModelChange)="formData.credits = $event ? Number($event) : undefined"
(ngModelChange)="formData.semesterHours = $event ? Number($event) : undefined"
```
Angular 模板不允许直接使用未挂到组件上的全局 `Number`。全仓仅此 2 处使用 `Number(`，属孤立引用错误。

**修复：** 改为不使用全局标识符的一元加号：
```
(ngModelChange)="formData.credits = $event === '' || $event === null ? undefined : +$event"
```
同时补测：空值、0、负数、小数、超大值的后端 `ValidateProjectInputAsync` 类似校验。

---

### RC-2　BUG-005 / BUG-006 / OBS-09 / OBS-10 —— 实训任务被“删后重建”，任务 ID 每次保存都变

**证据（生产数据库）：**

- `AppPracticumTasks` 中同一个 QA 项目、同一个任务标题 `QA_20261009_文本成果提交` 有 **两条记录**：
  - `3a243052-...`（`IsDeleted=t`，删除时间 2026-10-10 01:20:01）
  - `3a243592-...`（`IsDeleted=f`，新建）
- 提交记录中：
  - V1/V2 的 `TaskId = 3a243052-...`（指向已删除任务）
  - V3 的 `TaskId = 3a243592-...`（指向新任务）
- 项目 `AllowResubmission = false`（数据库确认 `f`），但 V3 仍然写入成功。

**因果链：**

1. 教师改“允许重复提交”并保存 → `PracticumAppService.UpdateAsync` → `ReplaceTasksAsync` 把旧任务**全部软删除**、用**新 GUID** 重建同标题任务。
2. `CreateSubmissionAsync` 的重复提交判断是 `AnyAsync(x => x.EnrollmentId == e.Id && x.TaskId == task.Id)`。新任务 ID 下查不到旧提交 → 判定“首次提交” → **放行 V3**（BUG-005）。
3. `RefreshEnrollmentProgressAsync` 用 `submissions.Select(x => x.TaskId).Distinct().Count()` 作分子，`tasks.Count` 作分母。**旧任务 ID + 新任务 ID 各算 1** → 2/1 = **200%**（BUG-006）。
4. `MapSubmissionDtosAsync` 的 `taskMap` 只查未删除任务，旧任务标题取不到 → 提交任务列出现空标题 → “、、”（报告中的重复分隔符）。
5. `VersionNo` 按“同任务历史条数 + 1”计算。V3 落在新任务下 → 版本号重新从 1 开始 → 详情显示“版本：V1”（OBS-09）。
6. `enrollment.FinalScore = 85` 与旧任务下的 V2 绑定，但列表展示的是最新 V3 正文 → “新成果沿用旧成绩”（OBS-10）。

**根因：** `src/KnowledgeHub.Application/Practicums/PracticumAppService.cs`
- `ReplaceTasksAsync` / `ReplaceMaterialsAsync` 为“全删全建”。
- `CreateUpdatePracticumTaskDto` 没有 `Id` 字段，前端 `originalTasks` 也丢弃了任务 ID，导致服务端无法做 upsert。

**影响面（不止实训项目页）：** 任何一次项目保存（改标题、开关、时间）都会重建全部任务，进而：
- 破坏既有提交与任务的关联；
- 让重复提交限制失效；
- 让进度可超过 100%；
- 让评分与成果版本脱钩。

---

### RC-3　BUG-002 完成率分母不一致 —— 教师端口径未校验章节是否属于本课程

**证据（生产数据库，课程“测试课程” `3a2086cc-...`）：**

- 习题 `单选题 1` 的 `ChapterId = 3a2086cd-4f8a-...`，但该章节在 `AppChapters` 中**已不存在**（0 行）。
- 该课程实际章节 5 个，其中一个章节挂 1 道题；其余 4 道题 `ChapterId` 为空。

**因果链：**

- 教师端 `BuildExerciseChapterMap(allExercises, links)` 只判断 `e.ChapterId.HasValue`，**不校验该章节是否属于本课程/是否存在** → 把“单选题 1”也算进分母 → `TotalExercises = 2`。
- 章节明细只遍历本课程章节 → 该题不落入任何章节 → 章节题数求和 = 1。
- 学生端 `LearningAppService.GetExerciseProgressMapAsync` 要求 `chapterCourseId == exercise.CourseId` → 排除该题 → 学生看到 1/1。
- 于是同一学生：学生端 1/1、教师学生行 1/2、教师章节明细 1/1、课程概览总题数 2。

**根因：** `src/KnowledgeHub.Application/Learning/StudentExerciseRecordAppService.cs`
- `BuildExerciseChapterMap` 缺少“章节归属本课程”的校验。该方法被
  `GetLearningStatisticsAsync`、`GetCourseLearningOverviewAsync`、`GetStudentLearningDetailAsync`
  三处共用，所以三处都会偏。

---

### RC-4　BUG-003 收藏计数不即时刷新 —— 只处理了“收藏页取消收藏”分支

**根因：** `angular/src/app/student/resources/student-resources.component.ts` 的 `toggleCollection`：
- 仅在 `showFavorites() && !nextValue`（在收藏分类里取消收藏）时更新 `favoritesCount`；
- 在普通列表里**新增收藏**时完全不动 `favoritesCount`，只有刷新后重新请求才更新。

**修复：** 成功回调里对 `nextValue` 的两种方向都做 `favoritesCount ± 1`（并处理失败回滚）。

---

### RC-5　BUG-004 搜索无结果误报“机构未发布资源” —— 空态文案是写死的

**根因：** `angular/src/app/student/resources/student-resources.component.html` 第 227–233 行，
`resources().length === 0` 时固定显示：
```
该租户下暂无资源
该院校/机构暂未发布任何资源，请尝试更换关键词或选择其他分类
```
没有区分“搜索/筛选无命中”“加载失败”“租户真的没资源”三种状态。

**修复：** 依据 `filterText()`、当前分类、`loadFailed()` 分三种文案。同类文案在 `student-search` / `resources/resource.ts` 里也要一并排查。

---

## 二、修复批次

### 批次 0（先做，0.5 天）—— 止血 + 可回归

| # | 内容 | 文件 |
|---|------|------|
| 0.1 | 修 `Number()` → `+$event`（RC-1，P2） | `course-list.component.html` |
| 0.2 | 收藏计数双向同步（RC-4，P2） | `student-resources.component.ts` |
| 0.3 | 资源空态三分支文案（RC-5，P3） | `student-resources.component.html` / `.ts` |

这三项互相独立、改动小、可立即部署并用报告里的回归点验收。

### 批次 1（核心，2–3 天）—— 实训任务 ID 稳定化（RC-2，P1）

**后端：**

1. `CreateUpdatePracticumTaskDto` 增加 `Guid? Id`；`CreateUpdatePracticumMaterialDto` 已有 `TaskId`，补 `Guid? Id`。
2. `ReplaceTasksAsync` → `SyncTasksAsync`：
   - 按 `Id` 匹配已有任务（含已软删除的，可用 `IRepository` 的 `Disable` / 直接查 IgnoreFilter）→ **更新**，不换 ID；
   - 无 `Id` 的新任务 → 插入；
   - 输入里不存在的任务 → 删除前**先检查是否有提交**：
     - 有提交 → 拒绝删除并抛 `UserFriendlyException("该任务已有学生提交，不能删除")`（或改为归档隐藏，二选一，见待确认项 Q1）。
3. `ReplaceMaterialsAsync` → `SyncMaterialsAsync`（同理 upsert；`TaskId` 必须重映射到保留下来的任务 ID）。
4. `UpdateAsync` / `CreateAsync` 调用新方法。
5. `RefreshEnrollmentProgressAsync` 加固（RC-2 的 BUG-006 兜底）：
   - 分子 = `submissions.Where(s => taskIds.Contains(s.TaskId)).Select(s => s.TaskId).Distinct().Count()`；
   - `Progress = Math.Clamp(..., 0, 100)`；
   - 达到 100% 时置 `Status = Completed`、写 `CompletedAt`（当前只在“评分”时才写）。
6. `CreateSubmissionAsync` 规则加固（见批次 2）。

**前端：**

7. `practicum-management.component.ts`：
   - `applyDetailToForm` / `originalTasks` 保留 `id`；
   - `saveForm` 回传 `tasks[].id`；
   - 弹窗任务区显示已有提交提示，禁用删除按钮。
8. `practicum-tasks.component.html`：提交任务列用 `row.submissions.map(s => s.taskTitle).filter(Boolean).join('、')`，避免空标题产生“、、”。

**验收：** 保存项目 3 次（只改开关/标题），任务 ID 不变；关闭重复提交后第二次提交被服务端拒绝；同一任务提交 3 次进度仍为 100%。

### 批次 2（业务规则，1–2 天）—— 完成后锁定不可重新进入

> 就是你在需求里提到的“完成/关闭后不允许重新进入”。建议按“服务端强校验 + 前端隐藏入口”双层实现。

**A. 成果提交（学生）**

在 `CreateSubmissionAsync` 增加前置校验（按顺序）：

1. 项目必须 `Published`，否则拒绝；
2. `StartTime` 未到 / `EndTime` 已过 → 拒绝（`EndTime` 已有，补 `StartTime`）；
3. **`enrollment.Status == Completed`（或 `FinalScore` 已存在）→ 拒绝**，提示“该实训已完成，如需重新提交请联系教师重新开放”；
4. `AllowResubmission == false` 且同任务已有提交 → 拒绝（保留原逻辑，修好任务 ID 后即生效）；
5. 新增开关（建议）：教师“重新开放评阅 / 允许重新提交”时，显式把 `enrollment.Status` 置回 `Submitted`、清空 `FinalScore/FinalComment`（或保留历史版本，见 Q2）。

**B. 报名（学生）**

`EnrollAsync` 增加：`existing.Status == Completed` → 拒绝“已完成的项目不能重新报名”；`ProjectStatus == Archived` → 拒绝。

**C. 进度状态机**

`RefreshEnrollmentProgressAsync` 不再把已 `Completed` 的 enrollment 降级为 `InProgress`（避免重复计算导致状态回退）。

**D. 前端**

`student-practicum-detail.component.html`：
- `enrollment.status === Completed`（或 `Reviewed`）时不再渲染“提交任务”按钮，改为展示“已完成 / 已评阅 + 分数 + 评价”；
- 若被服务端拒绝，用后端返回的 `UserFriendlyException.Message` 做错误提示（现在前端写死“提交失败”，会吞掉真实原因）。

**E. 成绩与版本绑定（OBS-09 / OBS-10）**

- 学生端/教师端展示分数时，明确“成绩对应哪一版成果”（`submission.VersionNo` + `ReviewedAt`）；
- 教师列表中“最新提交”和“当前成绩来源版本”分列展示；
- 新增提交后，`enrollment.Status` 回到 `Submitted` 并标记“待重新评阅”（若项目允许重复提交）。

### 批次 3（口径统一，1 天）—— 学习统计分母（RC-3，P1）

1. `BuildExerciseChapterMap` 增加“章节必须属于本课程”的校验：把 `courseChapterIds` 传进去，`e.ChapterId` 不在集合内则跳过；`links` 已在调用方按课程章节过滤，保持。
2. 三处调用（`GetLearningStatisticsAsync` / `GetCourseLearningOverviewAsync` / `GetStudentLearningDetailAsync`）统一口径。
3. 与学生端 `LearningAppService.GetExerciseProgressMapAsync` 对齐，并在注释里写明“分母 = 本课程章节内去重习题”。
4. 同时给 `GetChapterProgressAsync`（学生端章节进度）做一次交叉核对，避免第四种口径。

**验收：** 同一课程、同一学生、同一有效题目集合下，学生端 / 教师学生行 / 教师章节明细 / 课程概览四处一致；补测多章节、删题、复用关联、无题课程。

### 批次 4（数据修复，0.5–1 天）—— 清理已产生的脏数据

只针对受影响的项目/课程做一次性修复，建议脚本 + dry-run。至少包含：

1. **实训重复任务**：
   - 找出同 `ProjectId` 下“已软删除任务”与其后续新建的同标题任务；
   - 把引用旧任务的 `AppPracticumSubmissions.TaskId` 重指向保留任务（或恢复旧任务，二选一，见 Q3）；
   - 重算受影响 `AppPracticumEnrollments` 的 `Progress / Status / CompletedAt / FinalScore`；
   - 硬删除多余任务行。
   - qidi 的 QA 项目（V1/V2/V3）作为首个修复样本，先比对本报告的数据再扩大范围。
2. **课程学分/学时**：本次只修“以后能存”，历史 null 值需业务确认是否补录（不建议猜测填充）。
3. **学习统计**：批次 3 上线后自动修正展示，无需改数据；但若后续有报表/导出落库，需要重算。

### 批次 5（规则确认，产品/教学团队）—— 非代码

- Q1 任务删除策略：有提交的任务是“禁止删除”还是“归档隐藏”？
- Q2 重复提交开启时的成绩策略：新成果是否自动清空旧分、回到“待评阅”？旧分数是否保留为历史？
- Q3 历史 V1/V2/V3：合并到同一任务并保留多版本，还是保留为不同任务？
- OBS-01 微专业发证规则、OBS-02 空课程发布校验、OBS-03 教师权限范围、OBS-04 学生自助选课、OBS-05 记住上次机构。
- 这些不影响批次 0–4 的开发，但会影响批次 2 的分支设计。

---

## 三、需要改动的文件清单

**后端（.NET）**

| 文件 | 改动 |
|------|------|
| `src/KnowledgeHub.Application.Contracts/Practicums/Dtos/PracticumDtos.cs` | `CreateUpdatePracticumTaskDto.Id`、`CreateUpdatePracticumMaterialDto.Id` |
| `src/KnowledgeHub.Application/Practicums/PracticumAppService.cs` | `SyncTasksAsync`/`SyncMaterialsAsync`、进度口径与 clamp、提交/报名解锁规则、状态机 |
| `src/KnowledgeHub.Application/Learning/StudentExerciseRecordAppService.cs` | `BuildExerciseChapterMap` 课程校验 + 3 处调用 |

**前端（Angular）**

| 文件 | 改动 |
|------|------|
| `angular/src/app/learning/course-list/course-list.component.html` | `Number()` → `+$event` |
| `angular/src/app/student/resources/student-resources.component.ts` | 收藏计数双向同步 |
| `angular/src/app/student/resources/student-resources.component.html` | 空态三分支文案 |
| `angular/src/app/admin/practicum/practicum-management.component.ts` | 任务/资料保留 ID、回传 ID |
| `angular/src/app/admin/practicum/practicum-tasks.component.html` | 提交任务列去空标题分隔符 |
| `angular/src/app/student/practicums/student-practicum-detail.component.{ts,html}` | 完成后隐藏提交入口、透传后端错误信息 |
| `angular/src/app/admin/practicum/practicum-management.component.html` | 任务删除限制提示 |

**代理（只由生成，不手改）**

- `angular/src/app/proxy/practicums/**`：DTO 变更后执行 `cd angular && abp generate-proxy -t ng`。

**迁移**

- 批次 1–3 不新增数据库列，理论上**无需 EF 迁移**；批次 4 用脚本修复数据。
- 若批次 2 选择新增“重新开放”字段，则需迁移（建议直接复用 `enrollment.Status`，避免加列）。

---

## 四、回归清单（同版本、三角色、记录版本号）

| 检查点 | 验收标准 | 关联 |
|--------|----------|------|
| 课程字段 | 新建/编辑 2 学分 16 学时，发布、重登、跨角色回读一致；补测空/0/小数/边界 | BUG-001 |
| 实训任务 ID | 连续保存项目 3 次，任务 ID 不变 | BUG-005/006 |
| 重复提交关闭 | 首次允许、二次被服务端拒绝；刷新、重登仍拒绝 | BUG-005 |
| 重复提交开启 | 多版本可追溯；进度永不超 100% | BUG-006 |
| 完成后锁定 | 已评阅/已完成学生无提交入口；直连 API 也被拒 | 新规则 |
| 报名重入 | 已完成项目不能再次报名 | 新规则 |
| 成绩版本 | 分数/评价与成果版本对应；新提交后状态为待评阅 | OBS-09/10 |
| 学习统计 | 学生端 / 教师学生行 / 章节明细 / 课程概览四处口径一致 | BUG-002 |
| 收藏同步 | 收藏/取消后状态、列表、计数即时一致，失败回滚 | BUG-003 |
| 资源空态 | 无命中 / 租户真空 / 加载失败三种文案分别正确 | BUG-004 |
| 数据修复 | 先比对保留样本，再扩大范围 | 批次 4 |

---

## 五、建议实施顺序与工期

1. **批次 0**（0.5 天）→ 可单独发布，先让 BUG-001/003/004 验收。
2. **批次 1 + 2**（3–5 天）→ 一起发布，因为任务 ID 稳定化是“完成后锁定”和进度正确的前提。
3. **批次 3**（1 天）→ 可独立发布。
4. **批次 4**（0.5–1 天）→ 跟在批次 1/2 之后，用同一版本验证。
5. **批次 5** 与开发并行推进，影响批次 2 的可选分支。

总计约 **5–7 个工作日**（不含批次 5 的产品决策时间）。

---

## 六、对报告结论的修正/补充

- BUG-001 不是“后端没写”，而是**前端根本没发字段**；后端映射与持久化已验证正常。
- BUG-005 与 BUG-006 是**同一个根因**（任务删后重建），修一处即同时解决，且必须一起回归。
- 报告里“测试用例 2 全站显示 17 条公开课程”等计数差异，本轮未发现额外缺陷，不做处理。
- `OBS-07`（跨日旧页报错）与 `OBS-08`（DOCX 翻页指示）本轮未取证，建议单独立项，不并入本次修复。
