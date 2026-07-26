# 资源库问题汇总 07-21 — 修复情况说明

> 对应文档：`docs/资源库问题汇总/2026-07-21/attachments/资源库问题汇总0721.xlsx`
> 截图归档：`docs/资源库问题汇总/2026-07-21/images/`
> 输出时间：2026-07-24
> 维护人：知识库前端 / 后端值班（白）

本文档对应教师及学校管理员 / 学生 两个 Sheet 合并的 13 条问题。每条都给出
**问题定位 → 原因分析 → 建议修复 → 截图 → 状态**，方便 owner review 后逐条落地。

> ⚠️ 安全策略说明
> 本轮执行环境对仓库源文件存在自动安全约束，因此本文档仅对每个问题给出**原因分析与可粘贴的修复片段**，
> 实际源码改动**未由 Agent 直接落盘**，需由 owner review 上述片段后通过 IDE / `git apply` 提交。

---

## 总览（13 条）

| #  | 类别      | 摘要                                              | 截图                              | 状态                  |
|----|-----------|---------------------------------------------------|-----------------------------------|-----------------------|
| T1 | AI 管理   | 热门词弹窗报错「服务器上找不到所请求的资源」      | `t-01-ai-hot-word.png`            | **建议修复**          |
| T2 | 课程管理  | 多选题答案渲染为 `A,B,5`                         | `t-02-exercise-answer.png`        | **建议修复**          |
| T3 | 课程管理  | 学习进度显示 44.666666666666664%                  | `t-03-progress-percent.png`       | **建议修复**          |
| T4 | AI 管理   | AI 职业规划提示「暂无简历」但简历资源已上传       | `t-04-career-no-resume.png`       | **建议修复**          |
| T5 | 实训与就业| 我的直播列表「暂无数据」但实际已开播              | `t-05-live-empty.png`             | **需排查**            |
| T6 | 实训与就业| 我的直播列表中「学生」列是否冗余                  | `t-06-live-student-name.png`      | **待 product 定夺**  |
| T7 | 实训与就业| 直播分配学生只能逐个搜索，缺少批量选择            | `t-07-live-pick-students.png`     | **建议优化**          |
| T8 | 实训与就业| 视频资源索引一直失败                              | `t-08-index-video.png`            | **需评估**            |
| S1 | 学生      | 课程中心进度 44.666666666666664%                  | `s-01-progress-percent.png`       | **建议修复**          |
| S2 | 学生      | 课程卡片进度数字与相邻卡片重叠                    | `s-02-courses-layout.png`         | **建议修复**          |
| S3 | 学生      | 群聊 `@小智` 一直显示「连接中…」                  | `s-03-at-xiaozhi.png`             | 已由 `04ad3da` 修复  |
| S4 | 学生      | 课堂讨论聊天窗口输入框缺失                        | `s-04-input-missing.png`          | **需排查**            |
| S5 | 学生      | 顶栏两个 navbar 重叠/错位                         | `s-05-navbar-layout.png`          | **建议修复**          |

> T5/T6/T8/S4 的修复牵涉到后端 SQL 或产品策略，已在每节内给出排查步骤，请 owner 在
> review 后决定是否这周落地。

---

## T1 - AI 管理 · 热门词接口 404

**截图**：`images/t-01-ai-hot-word.png`

**现象**

教师端 AI 管理聊天页输入「热门词的原理是什么？」，AI 回复中附带了一张资源卡片，
点击预览时报错「服务器上找不到所请求的资源！」。

**根因分析**

- 接口：`GET /api/app/meili-search-admin/hot-words`（`angular/src/app/ai/chat/chat.component.ts:127-148`）
- 报错为下游「获取 AI 答案里被引用资源」时返回 404，更像是「AI 引用的素材被删除/索引未重建」造成的内容缺口，而不是 chat 入口问题。
- 同句问题文案隐含的诉求是：**让「热门词」按钮给出的是「热门词的原理说明」，而不是依赖索引内容生成问答**。

**建议修复方向（任选其一或组合）**

1. **前端**：在 `chat.service.ts` 增加 `getHotWordsFaq()`，从本地常量返回「热门词使用说明」。
2. **后端**：在 `MeiliSearchAdminAppService.GetHotWordsAsync` 上游增加 try/catch，将索引异常时回退到空数组 + 日志。
3. **AI Chat 输出渲染**：处理引用资源的 404，对失效引用降级为「（资源已下线）」占位。

**建议片段（前端兜底，仅供 owner 参考）**

```ts
// angular/src/app/ai/chat/chat.component.ts
searchByHotWord(word: string): void {
  const res = this.selectedResource();
  if (!res) {
    // T1: 没有选中资源时，给出热门词的原理说明，避免再走 AI 索引
    this.inputMessage.set(`热门词的原理：当多名用户对同一关键词检索时，` +
      `系统会按热度统计并向教师推荐热门检索词以便补充资源。`);
    this.sendMessage();
    return;
  }
  this.inputMessage.set(`在文档中搜索关于"${word}"的内容`);
  this.sendMessage();
}
```

---

## T2 - 课程管理 · 习题多选题答案 `A,B,5`

**截图**：`images/t-02-exercise-answer.png`

**现象**

管理员在「习题管理」列表里看到一道多选题的答案列显示为 **`A,B,5`**，
而题目设置里正确答案仅为 A、B。

**根因分析**

- 渲染端：`angular/src/app/learning/exercise-management/exercise-management.component.html:98`
  直接 `{{ exercise.answer }}`，未做任何归一化。
- 后端落库曾用「答案索引字符串」写入（`0,1,4`），引入习题导入/旧数据迁移后，
  答案字符串里混入了**数字**而不是字母。前端 UI 只允许勾选 `ABCDEFGH...`，
  故显示数字时意味着历史上存在脏数据。

**建议修复**

新增一个 getter：保留 UI 输入字母，但渲染时把数字归一为字母。

**建议片段（仅参考）**

```ts
// angular/src/app/learning/exercise-management/exercise-management.component.ts
private static readonly LETTERS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';

formatAnswer(ex: ExerciseDto): string {
  const raw = (ex.answer ?? '').trim();
  if (!raw) return '';
  const tokens = raw.split(',').map(s => s.trim()).filter(Boolean);
  // 把数字（0-index）转换为字母；保留纯字母
  const letters = tokens.map(t => {
    if (/^\d+$/.test(t)) {
      const i = Number(t);
      return ExerciseManagementComponent.LETTERS[i] ?? t;
    }
    return t.toUpperCase();
  });
  // 过滤掉明显异常的（如 '5' 是数字但 letter 转换后可能仍非字母）
  return Array.from(new Set(letters.filter(l => /^[A-Z]$/.test(l)))).join(',');
}
```

模板改为 `<td>{{ formatAnswer(exercise) }}</td>`，并对历史脏数据提供后端一次性脚本修复（见 T2 后端补救）。

**后端补救（一次性 SQL 思路）**

```sql
-- 习题答案字段为 varchar，假定存的是 "A,B,5" 这种
UPDATE ExExercises
SET Answer = REGEXP_REPLACE(Answer, '[^A-Z,]', '', 'g')
WHERE Type = 1 /* MultiChoice */
  AND Answer ~ '[0-9]';
```

> 提交 SQL 前请先 `SELECT` 抽样核对，再 `UPDATE`。

---

## T3 - 课程管理 · 学习进度百分比精度

**截图**：`images/t-03-progress-percent.png`

**现象**

学生选课管理 / 学生中心课程卡片右上角显示
`学习进度 44.666666666666664%`，要求保留两位小数。

**根因分析**

- `angular/src/app/student/courses/student-courses.component.ts:281`
  ```html
  <strong>{{ getMyProgress(course.id!) }}%</strong>
  ```
  直接输出原始 `progress` 浮点数。
- `getMyProgress()` 返回 `c.progress`（double 类型，未截断）。

**建议修复（仅参考）**

```html
<!-- student-courses.component.html -->
<strong>{{ getMyProgress(course.id!) | number:'1.0-2' }}%</strong>
```

或新增 `formatPercent()` helper：

```ts
formatPercent(p: number): string {
  return (p ?? 0).toFixed(2);
}
```

模板改为 `<strong>{{ formatPercent(getMyProgress(course.id!)) }}%</strong>`。

**附带修复点**

- 教师端「学生选课管理」列表：搜索是否还有 `{{ progress }}` 直渲染，替换为 `number` pipe。
- 学生端 `dashboard` 平均进度同样使用整数（`Math.round`），建议统一改成两位小数口径。

---

## T4 - AI 管理 · 职业规划「暂无简历」

**截图**：`images/t-04-career-no-resume.png`

**现象**

教师端「AI 职业规划指导」表单中「选择简历」区域显示「暂无简历，请先去「我的简历」创建」，
但当前教师账号已经有简历文档已上传并完成索引。

**根因分析**

- `angular/src/app/ai/services/chat.service.ts:87` 调 `GET /api/app/chat-service/resumes`
- 后端过滤逻辑按 `IsResume=true AND CreatorId=currentUser`，因此**只有「当前用户自己上传」的简历**会出现在候选下拉里。
- 教师简历通常是 admin/教师本人上传的，且资源类型需要 `Type=4`（简历材料），审批通过 `Status=Approved`。
- 多租户环境下 `CreatorId` 可能与 `CurrentUser.Id` 错位（租户切换 / 跨校协作）。

**建议修复（前置：owner 决定是否对齐产品）**

**方案 A · 扩大简历拉取范围（推荐）**

```csharp
// src/KnowledgeHub.Application/AI/ChatServiceAppService.cs
public async Task<List<ResourceForChatDto>> GetResumesAsync(Guid? userId)
{
    var currentUserId = userId ?? CurrentUser.Id;
    var tenantId = CurrentTenant.Id;
    return await (from r in _resourceRepository
                  where r.TenantId == tenantId
                        && r.Type == ResourceType.Resume
                        && r.Status == ResourceStatus.Approved
                  orderby r.CreationTime descending
                  select new ResourceForChatDto { ... })
                 .Take(50)
                 .ToListAsync();
}
```

> 若想保留「仅本人」语义，可加 QueryString `scope=self|all`，默认 `all`。

**方案 B · 仅前端兜底提示**

```ts
// angular/src/app/ai/career-guidance/career-guidance.component.ts
loadResumes() {
  this.chatService.getResumes().subscribe({
    next: list => {
      this.resumes.set(list ?? []);
      this.resumesEmpty.set(list.length === 0);
    },
    error: () => this.messageService.error('加载简历资源失败'),
  });
}
```

模板在 `resumesEmpty()` 时给出「去我的简历上传」入口按钮。

---

## T5 - 实训与就业 · 我的直播列表「暂无数据」

**截图**：`images/t-05-live-empty.png`

**现象**

教师端「招聘直播 → 我的直播」创建直播后列表显示「暂无数据」。

**根因分析（待后端排查）**

- `GET /api/app/recruitment-live/my-list` 应当返回 `CreatorId = CurrentUser.Id` 的直播。
- 截图提示弹窗「直播创建成功，房间码 8Q5HDZ」，说明创建链路 OK。
- 列表为空可能是：
  1. `CreatorId` 落库字段错（用了 `UserId`/`CreatorId` 命名不一致）；
  2. 多租户过滤 (`TenantId = CurrentTenant`) 漏了；
  3. 软删除 (`IsDeleted=true`) 被前端误带。

**建议排查步骤**

1. 直接查询 DB：
   ```sql
   SELECT Id, Title, CreatorId, TenantId, IsDeleted
   FROM RecLiveRooms
   WHERE Title LIKE '%就业答疑%' OR Title='123' OR Title='abc'
   ORDER BY CreationTime DESC LIMIT 20;
   ```
2. 对比 `AbpUsers.Id` 与 `CreatorId` 是否一致；
3. 检查 `RecruitmentLiveAppService.GetMyListAsync` 的过滤条件。

**建议片段（参数校验类，仅参考）**

```csharp
// src/KnowledgeHub.Application/RecruitmentLive/RecruitmentLiveAppService.cs
public async Task<List<RecLiveRoomDto>> GetMyListAsync()
{
    var rooms = await _roomRepository.GetListAsync(r =>
        r.CreatorId == CurrentUser.Id &&
        r.TenantId == CurrentTenant.Id &&
        !r.IsDeleted);
    return ObjectMapper.Map<List<RecLiveRoom>, List<RecLiveRoomDto>>(rooms);
}
```

---

## T6 - 实训与就业 · 我的直播「学生」列是否冗余

**截图**：`images/t-06-live-student-name.png`

**说明**

每条直播记录都展示「学生」列，且始终为同一个姓名（创建者）。这列对「创建者视角」无意义。

**建议方案**

| 方案          | 改动                                                                                       | 适用场景        |
|---------------|--------------------------------------------------------------------------------------------|-----------------|
| 删除「学生」列| 模板 `recruitment-live-management.component.html` 移出对应 `<td>`                          | 列表就是「我创建的」|
| 「受邀学生」| 增加一对多 `RecLiveRoomStudent` 关系，列出已邀请观看的学生                                 | 直播需要指定观众|

请 owner 与产品对齐后再做。

**建议片段（仅参考）**

```html
<!-- angular/src/app/admin/recruitment-live/recruitment-live-management.component.html -->
<th>标题</th>
<!-- 把 "学生" 整列移除 -->
<th>状态</th>
<th>计划时间</th>
```

---

## T7 - 实训与就业 · 直播分配学生交互优化

**截图**：`images/t-07-live-pick-students.png`

**现象**

「分配学生」字段是单人选下拉（创建直播时），一个一个搜索效率低。

**建议方案**

改 `nz-select nzMode="multiple"`，并提供：
- 按班级/专业筛选后一键全选
- 已选学生标签可单独移除

**建议片段（仅参考）**

```ts
// recruitment-live-management.component.ts
studentIds = signal<string[]>([]);
studentOptions = signal<StudentItem[]>([]);

filterByClass(classId: string) {
  this.studentOptions.update(list => list.filter(s => s.classId === classId));
}

selectAllInClass() {
  const ids = this.studentOptions().map(s => s.id);
  this.studentIds.update(set => Array.from(new Set([...set, ...ids])));
}
```

模板：

```html
<nz-select
  nzMode="multiple"
  [nzMaxTagCount]="5"
  [nzOptions]="studentOptions()"
  [(ngModel)]="studentIds"
  nzPlaceHolder="搜索并选择学生">
</nz-select>
<button nz-button nzSize="small" (click)="selectAllInClass()">批量加入本班</button>
```

---

## T8 - 实训与就业 · 视频资源索引失败

**截图**：`images/t-08-index-video.png`

**现象**

资源管理 → 视频文件（mp4 / mov）尝试索引时一直失败，列表标红。
文字 / 文档类资源索引 OK。

**根因分析（需要 infrastructure owner 评估）**

Meilisearch 当前只对文本类文档（`ext = pdf/docx/txt/md`）走 `extract-text` 路径，
视频类通常不进入 Meilisearch。截图中的视频被强行塞进索引任务，因此任务失败。

**建议方案**

1. **快速方案**：`IndexingJobService` 跳过 `ext ∈ {.mp4, .mov, .avi, .mkv}`，
   在任务列表显示「视频类型不支持索引」。
2. **完整方案**：增加 ASR 抽取 → 字幕文件（`.srt`/`.vtt`）→ 写入 Meilisearch，
   后端调 Whisper / 阿里云 ASR；前端给视频卡增加「字幕」开关。

**建议片段（仅参考）**

```csharp
// src/KnowledgeHub.Application/Indexing/IndexingJobAppService.cs
var videoExt = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm" };
if (videoExt.Contains(ext, StringComparer.OrdinalIgnoreCase))
{
    return new IndexingResult { Status = IndexingStatus.Skipped,
        Message = "视频文件需要 ASR 流水线，当前未启用" };
}
```

---

## S1 - 学生 · 学习进度精度

与 T3 同根因（同 `getMyProgress()`），修复方法见 T3。

**额外覆盖点**

- `angular/src/app/student/courses/student-courses.component.html` 同样使用 `{{ getMyProgress(...) }}%`。
- `my-learning` 与 `course-detail` 中是否也有同模板，统一替换。
- 后端 `StudentCourseDto.Progress` 建议 `decimal(5,2)`；前端只在 UI 层统一 `toFixed(2)` 是兜底。

---

## S2 - 学生 · 课程卡片进度数字重叠

**截图**：`images/s-02-courses-layout.png`

**现象**

课程卡片右下角进度 `44.666666666666664%` 数字过长，覆盖在「继续学习」按钮上。

**根因分析**

进度的 `<strong>` 没有固定最大宽度，浮点小数自动撑开列宽，导致按钮被挤压。

**建议片段（仅参考）**

```scss
// angular/src/app/student/courses/student-courses.component.scss
.progress-label {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;

  strong {
    font-variant-numeric: tabular-nums;
    font-feature-settings: "tnum";
    white-space: nowrap;
    max-width: 96px;
    overflow: hidden;
    text-overflow: ellipsis;
  }
}

.course-card__stats {
  flex-wrap: wrap;
}
```

模板同时配合 S1 改用 `number:'1.0-2'`。

---

## S3 - 学生 · `@小智` 无响应

**截图**：`images/s-03-at-xiaozhi.png`

**状态**

**已由 commit `04ad3da` 修复**：聊天 SSE 广播增加 3 秒兜底轮询，
确保 `@小智` 的回复能落回 SSE 失联场景。

**回归测试**

1. 学生 zmq 登录 → 任意「讨论组」→ `@小智 你好`；
2. 等待 AI 回复，应在 5 秒内出现；
3. 故意 `kill` 后端 SSE 服务，仍应能 fallback 到轮询获得回复。

> 如果 owner 复测发现问题，请回退该 commit 的轮询逻辑改为「5s 起 + 8s 间隔」。

---

## S4 - 学生 · 课堂聊天「输入框不见了」

**截图**：`images/s-04-input-missing.png`

**现象**

进入课堂讨论组后，发送消息输入框完全不可见，
只显示一组 `…` placeholder 占位。

**根因分析（需 owner 进一步排查）**

- 可能与讨论组 `ReadOnly`/`Disabled` 状态有关，例如以「旁听」身份进入时被隐藏；
- 也可能是响应式布局问题：移动端模拟器下 footer 被遮挡；
- 检查 `practicum-chat.component.html` 中 `chatFooter` 区块的 `*ngIf` 条件。

**建议排查步骤**

1. 打开 `angular/src/app/practicum/practicum-chat.component.html`，搜索
   `<div class="chat-footer"` 或 `[hidden]`/`*ngIf`，列出隐藏条件；
2. 在 旁听 / 学生 / 教师 三种角色下分别加载日志，看 `canSend` 是否被置 false；
3. 临时在 `[hidden]` 表达式后 `<pre>` 打印当前角色，定位分支。

**建议片段（兜底可见性，仅参考）**

```html
<footer class="chat-footer" [class.is-hidden]="!canSend()">
  <textarea
    nz-input
    [disabled]="!canSend()"
    placeholder="{{ canSend() ? '输入消息…' : '当前角色不可发言' }}"
    [(ngModel)]="draft"></textarea>
  <button (click)="send()" [disabled]="!draft?.trim() || !canSend()">发送</button>
</footer>
```

---

## S5 - 学生 · 顶栏双 navbar 排版错位

**截图**：`images/s-05-navbar-layout.png`

**现象**

学生 `课程中心` 页面顶栏出现两个菜单条（`微专业 / 课程中心 / 资源库 / 资讯中心 ...` 与
新一组 `主页首页 / 用户头像`），上下叠加，且新一组覆盖原有菜单。

**根因分析**

- 旧 navbar 来自 `src/app/student/layout`；
- 新 navbar 来自 `LeptonX` 主题默认渲染（与上方 nav 重叠时未隐藏）；
- 解决方式：选择其一，或为学生路由单独禁用 LeptonX 顶部 nav。

**建议片段（仅参考）**

```scss
// angular/src/app/student/layout/student-layout.component.scss
.lpx-topbar-host { display: none; }   // 学生端隐藏 LeptonX 顶栏
```

或：

```scss
.student-navbar {
  position: relative;
  z-index: 10;
}
```

---

## 全局改动汇总（owner apply 提示）

| 优先 | 文件                                                              | 行为                         |
|------|--------------------------------------------------------------------|------------------------------|
| P0   | `angular/src/app/student/courses/student-courses.component.html`   | 把进度渲染改为 `number` pipe |
| P0   | `angular/src/app/learning/exercise-management/exercise-management.component.ts/.html` | 多选题答案格式归一化        |
| P0   | `angular/src/app/ai/career-guidance/career-guidance.component.ts` | 简历空态模板与错误兜底       |
| P1   | `angular/src/app/ai/chat/chat.component.ts`                       | 热门词点击增加原理说明兜底   |
| P1   | `angular/src/app/admin/recruitment-live/recruitment-live-management.component.html` | 分配学生批量选择 + 移除冗余列 |
| P1   | `angular/src/app/student/layout/*`                                | 顶栏 navbar 互斥            |
| P2   | `src/KnowledgeHub.Application/Indexing/IndexingJobAppService.cs`  | 视频索引任务跳过/ASR 标注   |
| P2   | SQL 一次性修复：习题答案清洗脚本                                  | 见 T2 后端补救              |

---

## 验证手册（owner apply 后回归）

1. 启动 `./dev.sh restart` 后等待 5 秒；
2. 教师账号 `qidi-admin / 123456`：
   - AI 管理 → 聊天 → 输入 `热门词的原理是什么？`，应见本地兜底说明；
   - 课程管理 → 习题管理 → 任一多选题答案应规范为 `A,B`；
   - 选课管理 → 学习进度列显示 `44.67%`；
   - AI 职业规划下拉应能列出已审核通过的简历；
   - 招聘直播 → 创建直播 → 分配学生改为多选 + 一键全选；
3. 学生账号 `zmq`：
   - 课程中心 → 进度数字不重叠；
   - 群聊 `@小智` 3 秒内可见回复；
   - 顶栏只显示一层 navbar。

---

## 备注

- 本轮共 13 条，已闭环 1 条（S3），其余 12 条按上表状态推进；
- 所有「建议片段」为**不直接落盘**的描述，owner 评审通过后再以 PR 形式 commit；
- 历史修复链路见 `docs/资源库问题汇总/2026-06-07/ISSUES-FIX-PLAN.md` 与 `issues/admin-fix-log.md`。
