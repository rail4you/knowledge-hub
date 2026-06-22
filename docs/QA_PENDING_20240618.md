# 副本资源库 6.18 — 待修复任务清单

> 配套文档：[`QA_FIX_PLAN_20240618.md`](QA_FIX_PLAN_20240618.md) 方案 / [`QA_ISSUES_20240618.md`](QA_ISSUES_20240618.md) 原始问题 / [`QA_FIX_STATUS_20240618.md`](QA_FIX_STATUS_20240618.md) 已修复进度
> 截止日期：2026-06-22
> 任务总数：17 条（P0×5 + P1×4 + P2×8）

> 图例：🟥 P0 阻塞 ｜ 🟧 P1 严重 ｜ 🟨 P2 一般

---

## 一、推荐修复顺序

按"**阻塞优先 + 改动量从小到大**"排序：

```
Sprint 1（本周内必做）
  ① P0-4 知识图谱空数据隐藏    [半天]
  ② P0-5 知识图谱添加入口      [半天]
  ③ P0-2 学生端资源可见        [1 天]
  ④ P1-3 资源租户隔离          [1 天]
  ⑤ P1-9 学习数据统计          [1 天]

Sprint 2（下周）
  ⑥ P1-4 智能体资源上下文      [2 天]
  ⑦ P1-12 简历解析加固         [2 天]
  ⑧ P0-1 视频加载              [2~3 天]
  ⑨ P0-3 直播创建              [2 天]
  ⑩ P2-4 题目多章节            [1.5 天，模型变更]
  ⑪ P2-7 智能体学生选择        [1 天，复用 P1-10]

Sprint 3（待用户输入）
  ⑫ P2-3 / P2-6 / P2-9 截图类  [需用户澄清现象]
  ⑬ P0-? 其他工单              [TBD]
```

---

## 二、P0 — 阻塞（5 条）

### 🟥 P0-1 视频无法加载（T-1）

- **截图**：`screenshots/qa-0618/ID_AAE4E6B6CDFB47D28DEF663964FA1AAE.png`
- **现象**：上传 mp4 后点击播放，video 标签加载失败（无画面/进度条卡死/报 CORS 错误）。
- **定位**：
  - 视频元数据：`src/KnowledgeHub.Application/Resources/ResourceAppService.cs:96` `GetAsync`
  - 视频处理：`ResourceAppService` 视频版本、转码相关方法
  - 存储抽象：`src/KnowledgeHub.Application/Resources/FileStorage/IFileStorageService.cs`
  - 前端 player：`angular/src/app/resources/` 视频播放组件
- **修复步骤**：
  1. 跑一遍 `tail -f .dev/logs/api.log` + 上传一个 mp4，看后端 `VideoIndexingJob` 是否完成、`VideoUrl/HlsUrl` 是否回填。
  2. 查 `Resource.VideoUrl/HlsUrl` 字段是否在上传后写入（`VideoIndexingJob` 完成后回填到主表）。
  3. 看 OSS/MinIO 桶的 **CORS** 配置（`Access-Control-Allow-Origin`）和 **签名 URL 过期时间**。
  4. 前端：原生 `<video>` 不支持 m3u8，需引入 `hls.js`：
     ```bash
     cd angular
     npm install hls.js
     ```
     ```typescript
     import Hls from 'hls.js';
     if (Hls.isSupported() && videoUrl.endsWith('.m3u8')) {
       const hls = new Hls();
       hls.loadSource(videoUrl);
       hls.attachMedia(video);
     }
     ```
- **回归点**：
  - 用 `qidi-admin` 上传一个 mp4，确认 30s 内可播放、进度条可拖动。
  - 跨租户切换：上传→播放→切换租户→再次访问原资源 URL 不应能播放（租户隔离）。
  - 校验 m3u8 URL 在 Chrome / Safari / Firefox 都能播。
- **关联 commit**：无
- **预估**：2~3 天（含前端 HLS 集成）

---

### 🟥 P0-2 教师端上传 PPT，学生端看不到资源（S-5）

- **截图**：`screenshots/qa-0618/ID_D35828659878427B918F7FAFD22E5618.png`、`ID_52274436C1304F47958EB79DE2A166F9.png`
- **现象**：教师上传并发布资源后，学生端"资源库"为空。
- **定位**：
  - 资源实体：`src/KnowledgeHub.Domain/Courses/KnowledgeResource.cs`
  - 资源列表（学生端）：`src/KnowledgeHub.Application/Resources/ResourceAppService.cs` `GetListAsync`
- **修复步骤**：
  1. 在学生端 `GetListAsync` SQL 加上 `IsPublished = true`（或 `Status = Published`）过滤；当前可能带了 `IsDeleted = false` 但缺发布状态。
  2. 检查 `TenantId` 字段：教师上传资源时是否正确写入了 `CurrentTenant.Id`。
  3. 学生门户的查询与教师端是否走同一个 `IResourceRepository.GetListAsync`；若各自实现，需统一过滤条件。
  4. 历史数据回填：若发现 `IsPublished` 默认 false，需 migration 改默认值 + 跑 `UPDATE "KnowledgeResources" SET "IsPublished" = true WHERE "Status" = 1`（按业务定义）。
- **回归点**：
  - 教师 A 上传 PPT（不发布）→ 学生看不到（符合预期）。
  - 教师 A 上传 PPT（发布）→ 学生 A 看到；切换租户后学生 B 看不到。
  - SQL 直接 `SELECT * FROM "Resources" WHERE "TenantId" = ? AND "IsPublished" = true` 应能命中。
- **关联 commit**：P1-1（`d799a45`）改过 `GetListAsync`，需注意兼容
- **预估**：1 天

---

### 🟥 P0-3 依旧无法创建直播（T-21）

- **截图**：`screenshots/qa-0618/ID_0D086336CA904675907102BEB9B640B1.png`
- **现象**：在直播管理页点"创建直播"，提交后弹错误或白屏。
- **定位**：`src/KnowledgeHub.Application/RecruitmentLive/RecruitmentLiveAppService.cs`
- **修复步骤**：
  1. 看后端日志 `tail -200 .dev/logs/api.log | grep -i "live\|recruit"` 抓真实异常堆栈。
  2. 检查 `CreateAsync` 入参校验（开始时间 > 当前时间、必填项）。
  3. 检查直播平台 SDK token（参见 `recruitment-live-plan.md`）：token 是否过期、域名/IP 白名单是否配置。
  4. 检查前端提交时是否带了 `TenantId`、表单 `ngModel` 是否双向绑定到完整 DTO。
- **回归点**：
  - 用 `qidi-admin` 在 5 分钟后创建一场直播，确认返回直播间推流地址。
  - 二次创建：相同表单再次提交，第二次不应受第一次状态影响。
  - 校验错误场景：开始时间填过去时间，应返回明确错误提示。
- **关联 commit**：P1-6（`885e8c8`）改过 `GetTenantStudentsAsync`，是 P0-3 创建表单的下游接口
- **预估**：2 天

---

### 🟥 P0-4 学生端知识图谱模块即使没有数据也显示（S-1）

- **截图**：`screenshots/qa-0618/ID_3E84DA54193743529B3A8AD079BAE418.png`
- **现象**：管理员端未配知识点，但学生端首页"知识图谱"入口卡片仍显示，点击进去空页面。
- **定位**：
  - 后端：`src/KnowledgeHub.Application/KnowledgeGraph/KnowledgeGraphAppService.cs` 需加 `HasDataAsync()` 或 `GetCountAsync()`
  - 前端：学生门户 `home.component` / `portal-routing.module` 中知识图谱入口
- **修复步骤**：
  1. 后端新增 `Task<bool> HasDataAsync()`（按当前租户统计 `KnowledgeGraph` 实体数）。
  2. 前端在加载门户首页时调用该接口，`false` 时隐藏「知识图谱」入口卡片。
  3. 同时考虑路由守卫：直接访问 `/knowledge-graph` 时若后端返回空数据，重定向到首页或显示空状态页。
- **回归点**：
  - 管理端未添加知识点 → 学生门户首页看不到知识图谱入口。
  - 管理端添加至少 1 个知识点 → 学生门户立即可见（不需重启前端）。
  - 直接 URL 访问 `/knowledge-graph`：空数据 → 友好空状态；非空 → 正常渲染。
- **关联 commit**：无
- **预估**：半天

---

### 🟥 P0-5 知识图谱添加入口缺失（T-12）

- **截图**：`screenshots/qa-0618/ID_8C7FB5A159E749C981E9C0334D9B8A02.png`
- **现象**：教师在课程编辑页改章节时想加知识点，需手动跳到知识图谱页；知识图谱页本身也没"+ 添加"按钮。
- **定位**：
  - 课程编辑页：`angular/src/app/courses/course-edit/`
  - 知识图谱页：`angular/src/app/knowledge-graph/`
- **修复步骤**：
  1. **课程编辑页**：在「章节」tab 旁增加「知识点」tab（或工具栏按钮）「+ 管理知识点」，点击跳转到知识图谱并预选该课程。
  2. **知识图谱页**：右上角加「+ 添加知识点」浮动按钮；弹窗中输入名称、关联课程/章节，提交。
  3. 后端 `KnowledgeGraphAppService.CreateAsync` 已有则无需新增，只需确认 DTO 字段完整（`Name/CourseId/ChapterId/ParentId`）。
- **回归点**：
  - 从课程编辑页 → 点击「+ 管理知识点」→ 直接进入图谱编辑器并预选当前课程。
  - 在图谱页直接添加一个根知识点 → 列表/树立即刷新显示。
  - 添加失败（如重名）应有明确错误提示，不应静默失败。
- **关联 commit**：无
- **预估**：半天

---

## 三、P1 — 严重（剩余 4 条）

### 🟧 P1-3 资源库「这里没有租户数据」（T-2）

- **截图**：`screenshots/qa-0618/ID_CDB0628AB2AD4DC4AF38895324C1AC2F.jpeg`
- **现象**：用某个租户管理员登录资源库，列表为空（应为"该租户下还没有资源"而非"系统没有数据"）。
- **定位**：`ResourceAppService.GetListAsync` 的租户过滤
- **修复步骤**：
  1. 在 `GetListAsync` 入口处 `CurrentTenant.Change` 包裹查询，确认租户切换时缓存被清除。
  2. 检查 `IResourceRepository` 自定义实现是否漏掉了 `TenantId` 过滤条件。
  3. 若有二级缓存（`IDistributedCache`），按 `TenantId` 作 cache key 的一部分。
  4. 空数据时的提示语要明确：不是"无数据"而是"该租户下暂无数据"。
- **回归点**：
  - 用联盟管理员登录，看得到所有租户的资源。
  - 切换到院校管理员登录，只看得到本校资源。
  - 切回联盟管理员，列表立即刷新（无陈旧缓存）。
  - 新建租户：登录后资源库列表为空 + 友好空状态。
- **关联 commit**：P1-1（`d799a45`）已改过 `GetListAsync`，改动点相近需谨慎
- **预估**：1 天

---

### 🟧 P1-4 智能体「已上传但不显示」（T-7）

- **截图**：`screenshots/qa-0618/ID_E740E10CEB3B4F07889A77395BAF62E4.png`
- **现象**：教师上传了 PPT/PDF 教案材料，让 AI 智能体生成教案/案例分析时，prompt 里没引用到这些资源。
- **定位**：
  - 智能体上下文构建：`src/KnowledgeHub.Application/TeachingAgents/TeachingAgentContextBuilder.cs`
  - 教案生成/案例分析：参考 `TeachingAgentAppService.cs` 内的 prompt 模板
- **修复步骤**：
  1. 在 `TeachingAgentContextBuilder.BuildAsync` 中确认查询资源时带了 `TenantId + IsPublished` 过滤。
  2. 教案/案例分析的 prompt 模板中是否引用了资源列表的字段（如 `{resources[].title}`），若引用了但传空数组则提示不显示。
  3. 资源类型过滤：某些智能体场景可能只取 `Document/Pdf`，PPT 因类型不匹配被过滤掉。
  4. 验证资源 URL 是否可访问（`HlsUrl/PdfUrl`）。
- **回归点**：
  - 教师上传 1 份 PPT + 1 份 PDF → 教案生成输出中包含这两份资源的引用。
  - 删除其中一份 → 教案生成输出中相应内容消失（不残留旧数据）。
  - 跨租户：A 校教师传的 PPT 不会被 B 校智能体看到。
- **关联 commit**：无
- **预估**：2 天

---

### 🟧 P1-9 课程「无学习数据」（T-14）

- **截图**：`screenshots/qa-0618/ID_E3693C4232BC41398A766226E205B0F6.png`
- **现象**：教师看课程统计页，"学习人数 / 完成数 / 时长"全为 0 或 null，但学生实际上看完过视频。
- **定位**：
  - 学习记录：`src/KnowledgeHub.Application/Learning/LearningAppService.cs`
  - 统计 SQL：可能在 `GetCourseStatisticsAsync` 等方法
- **修复步骤**：
  1. 看是否有 `StudentLearningRecord` 表的数据写入事件（视频看完/习题提交时是否调用 `RecordAsync`）。
  2. 课程统计 SQL 的 JOIN 条件：是否把 `StudentCourse` 与 `LearningRecord` 正确关联（注意 `LEFT JOIN + WHERE` 陷阱）。
  3. 直接 `SELECT * FROM StudentLearningRecords WHERE CourseId = ?` 验证底层有数据。
  4. 字段映射：检查 `Completion / DurationSeconds` 是否被投影到 DTO。
- **回归点**：
  - 学生看完一个视频 → 教师端课程统计「学习人数 +1」。
  - 即使没有学生学，也应显示「0 人」而非空白或 null。
  - 跨租户：A 校课程的学习数据不被 B 校看到。
- **关联 commit**：P2-14（`d7682ea`）改过 `LearningAppService`，注意兼容
- **预估**：1 天

---

### 🟧 P1-12 简历识别错乱（S-6）

- **截图**：`screenshots/qa-0618/ID_2BE79199AC1B4A399BA81B9580A59105.png`、`ID_DCDB97325EFF4302AC9E0EA492F1C876.png`
- **现象**：学生上传简历后，自动识别的"教育经历/工作经历/技能"字段错位（把张三的简历识别成李四的）。
- **定位**：`EmploymentAppService.ParseResumeAsync`（简历解析 LLM 调用）
- **修复步骤**：
  1. 检查 LLM prompt：是否明确要求输出 `JSON {name, education[], experience[], skills[]}` 结构化字段。
  2. 加 fallback：当 LLM 返回结构混乱时，记录原始简历供人工复核。
  3. **不**强行自动覆盖学生填好的简历，先回显识别结果让学生确认。
  4. 简历格式校验：上传时建议 PDF/DOCX 文本可读格式；扫描件图片单独提示"建议使用电子版简历"。
- **回归点**：
  - 上传标准模板简历 → 识别结果字段完整且与简历一致。
  - 上传非标准格式 → 提示「识别失败，请手动填写」而非写入错误数据。
  - 已有简历的情况下上传新简历 → 弹确认框，不直接覆盖。
- **关联 commit**：无
- **预估**：2 天

---

## 四、P2 — 一般（剩余 8 条）

### 🟨 P2-3 智能体「无截图标注」（T-8）

- **截图**：`screenshots/qa-0618/ID_DD67227FCF5A47B38745D65BCB3344D8.png`
- **现象**：截图无文字描述
- **状态**：⏸️ **需用户澄清现象** — 截图未提供文字，请用户补充反馈后再归类
- **预估**：TBD

---

### 🟨 P2-4 一道题目是否可以添加到不同章节（T-10，模型 bug）

- **截图**：`screenshots/qa-0618/ID_0C9AFD4971AC4A80AD16B1F7D0D11555.png`
- **现象**：教师反馈"一题能否同时挂多个章节"，目前是 1:N（一题一章节）。
- **定位**：`Exercise` 与 `Chapter` 的关系当前为多对一（一题一章节）
- **修复步骤**：
  1. 新建关联表 `ChapterExercise (ChapterId, ExerciseId)` 多对多。
  2. EF migration + 调整 `ExerciseAppService` 的查询与编辑接口。
  3. 同一题在不同章节复用：编辑页面支持「所属章节」多选。
  4. **数据迁移**：现有 `Exercise.ChapterId` 数据需要回填到 `ChapterExercise`。
  5. **破坏性变更**：现有 `Exercise.ChapterId` 字段是否保留作为"主章节"？建议保留。
- **回归点**：
  - 同一道题可同时加入章节 A 和章节 B。
  - 删除其中一个章节时不会连带删除题目。
  - 学生端做题：题号仍唯一，章节切换时已答题不重复出现。
- **关联 commit**：P1-5（`e1ab2c3`）改过 `ExerciseAppService` 导入逻辑，需注意
- **预估**：1.5 天（含数据迁移）

---

### 🟨 P2-6 课程无截图（T-13）

- **截图**：`screenshots/qa-0618/ID_A06DFE966EAE45FCA1F91F66EA97BC78.png`
- **现象**：截图无文字描述
- **状态**：⏸️ **需用户澄清现象** — 请用户补充反馈后再归类
- **预估**：TBD

---

### 🟨 P2-7 智能体学生选择繁琐（T-22）

- **截图**：`screenshots/qa-0618/ID_E89EFC985FF44BE68F340B81561709A5.png`
- **现象**：智能体"教案分发"场景下要批量选学生，目前只能一个个勾。
- **修复步骤**：
  1. 复用 P1-10（`0d96cba`）已经实现的「全选匹配结果」批量选择学生组件。
  2. 把 `student-selector.component` 抽到 `shared/` 目录，给智能体模块引用。
  3. 加班级/专业过滤（与 P1-10 一致）。
- **回归点**：
  - 50 个学生一次性勾选并提交，列表立即全部显示。
  - 智能体端下拉不会出现非学生角色（已通过 P1-6 修复）。
- **关联 commit**：P1-10（`0d96cba`）和 P1-6（`885e8c8`）可复用
- **预估**：1 天

---

### 🟨 P2-9 首页无截图（T-24）

- **截图**：`screenshots/qa-0618/ID_41731BFDB34A4A26BE10A9AFA502C674.png`
- **现象**：截图无文字描述
- **状态**：⏸️ **需用户澄清现象** — 请用户补充反馈后再归类
- **预估**：TBD

---

### 🟨 P2-? 资源分类按专业分类（S-11）

- **截图**：`screenshots/qa-0618/ID_A26DA2E1626240A7A72C26537E96B0E0.png`
- **现象**（学生 11 号）："明明已经预览了，数据还是显示为 0，这边资源的分类要按照专业进行分类"
- **定位**：`ResourceAppService.GetListAsync` 学生端视图
- **修复步骤**：
  1. 学生端资源列表按 `MajorId` 分组展示（前端分 tab 或分组列表）。
  2. 复用 P2-2 的 `MajorId` 筛选条件（`7c3b2f8` 已实现）。
  3. 检查"预览"是否触发 `ViewCount +1`，目前没生效。
- **回归点**：
  - 学生端资源列表按专业分组/筛选。
  - 预览资源后，资源浏览数 +1。
- **关联 commit**：P2-2（`7c3b2f8`）和 P1-1（`d799a45`）相关
- **预估**：1 天

---

### 🟨 P2-? 七日学习曲线没有生成曲线（S-12）

- **截图**：`screenshots/qa-0618/ID_25ACB58AC5DB4A0EBDAA43C895463CFE.png`
- **现象**（学生 12 号）："七日的学习曲线并没有生成曲线"
- **定位**：学生端"个人中心"或首页的 `LearningAppService.Get7DayStatsAsync`
- **修复步骤**：
  1. 看后端是否返回 7 个数据点（按天 bucket 统计学习时长）。
  2. 字段：`Date / DurationSeconds` 是否齐全。
  3. 前端：ng-zorro 折线图组件是否正确绑定数据。
  4. 时区：按学生所在时区或服务器时区（需统一）。
- **回归点**：
  - 学生看完 1 个视频 → 7 日曲线上对应日期点数值 +1。
  - 7 个数据点都显示（即使为 0）。
- **关联 commit**：P2-14（`d7682ea`）改过 `LearningAppService`，可关联
- **预估**：半天

---

### 🟨 P2-? 搜索结果不相关（S-3）

- **截图**：`screenshots/qa-0618/ID_614C6453F11D4C61A324207935AB2682.png`
- **现象**（学生 3 号）："这两个搜索出来的真的相关吗" — Meilisearch 搜索相关性差
- **定位**：`docs/meilisearch-development.md` 中 Meilisearch 配置 + 搜索服务
- **修复步骤**：
  1. 检查 Meilisearch 索引（`/Users/bai/.meilisearch/data.ms`）的字段权重。
  2. 调 `searchableAttributes`：`title > tags > content`。
  3. 调 `rankingRules`：proximity / words / typo / attribute / sort / exactness。
  4. 数据量：是否所有资源都已索引（运行 `IndexingJob` 全量重建）。
- **回归点**：
  - 搜 "Java" → 标题含 Java 的资源排第一。
  - 搜 "AI 教案" → 标签含 "AI" 或标题含 "教案" 的资源排前。
- **关联 commit**：无
- **预估**：1 天（含 Meilisearch 调优）

---

## 五、修复时需同步更新的内容

| 修复项 | 同步更新 |
|--------|----------|
| P0-1 视频加载 | API 文档 + 前端 player 文档 |
| P0-2 资源租户隔离 | 多租户测试用例 |
| P0-4 知识图谱隐藏 | 前端路由守卫文档 |
| P1-5 习题导入 | Excel 模板下载页 + 用户操作手册 |
| P1-12 简历识别 | 简历上传说明页（建议模板） |
| P1-13 资讯导入模板 | 模板文件 + 导入操作手册 |
| P2-4 题目多章节 | 数据库迁移脚本 + API 文档（破坏性变更需 changelog） |
| 全部修复 | `docs/QA_FIX_STATUS_20240618.md` 中相应行标记 ✅ |

---

## 六、修复工作流（建议）

```bash
# 1. 同步最新代码
git -C /Users/bai/projects/KnowledgeHub pull

# 2. 修复前先重启 API（如有后端改动）
./dev.sh restart api
sleep 5

# 3. 修复并跑构建
dotnet build src/KnowledgeHub.Application/KnowledgeHub.Application.csproj
# 或后端
dotnet build

# 4. 前端构建（如有前端改动）
cd angular && npx ng build

# 5. 测试通过后，提交
git add -A
git commit -m "fix(<module>): <P编号> <简短描述>"

# 6. 更新进度文档
# 编辑 docs/QA_FIX_STATUS_20240618.md，把对应条目移到"已完成"
# git add docs/QA_FIX_STATUS_20240618.md
# git commit -m "docs(qa): <P编号> 修复完成，更新进度"

# 7. 跑一轮冒烟
./dev.sh restart && sleep 5
curl -sk https://localhost:44305/health-status
tail -100 .dev/logs/api.log | grep -i "ERR\|Exception" | head -20
```

---

## 七、需要用户输入才能继续的截图类问题

请用户对以下 3 条补充文字说明（截图没有标注）：

| 编号 | 截图 | 模块（推测） |
|------|------|--------------|
| P2-3 | `ID_DD67227FCF5A47B38745D65BCB3344D8.png` | 智能体 |
| P2-6 | `ID_A06DFE966EAE45FCA1F91F66EA97BC78.png` | 课程 |
| P2-9 | `ID_41731BFDB34A4A26BE10A9AFA502C674.png` | 首页 |

> 用户反馈后，移到 `QA_FIX_PLAN_20240618.md` 对应章节归类。
