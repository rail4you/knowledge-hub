# 副本资源库 6.18 测试 — Bug 修复方案（按严重程度）

> 对应问题清单：[`QA_ISSUES_20240618.md`](QA_ISSUES_20240618.md)
> 测试账号：`qidi-admin / 123456`
> 整理日期：2026-06-21

本文档**仅聚焦 bug 修复**（不含新功能/模板建议/流程优化类需求，那些归入 P3 已删除或保留在原问题清单中）。
按 **P0 阻塞 → P1 严重 → P2 一般** 三级排序，每条 bug 给出：

- **现象**（用户反馈）
- **定位**（代码位置/接口）
- **修复步骤**
- **回归点**（如何验证已修好）

---

## 一、P0 — 阻塞（5 条，必须本周内修复）

> 影响主链路：登录后无法完成核心业务流程，必须立即修复。

### P0-1 视频无法加载（T-1）

- **截图**：`screenshots/qa-0618/ID_AAE4E6B6CDFB47D28DEF663964FA1AAE.png`
- **定位**：
  - 视频元数据：`src/KnowledgeHub.Application/Resources/ResourceAppService.cs:96` `GetAsync`
  - 视频处理：`src/KnowledgeHub.Application/Resources/ResourceAppService.cs`（视频版本、转码相关方法）
  - 存储抽象：`src/KnowledgeHub.Application/Resources/FileStorage/IFileStorageService.cs`
- **修复步骤**：
  1. 检查 `Resource.VideoUrl` / `Resource.HlsUrl` 字段是否在上传后写入（视频文件→ `VideoIndexingJob` 完成后回填）。
  2. 确认 `EnsureFileMetadata(dto)` 是否对视频类型生成了可播放 URL；查看 `ResourceDto` 是否含 `VideoUrl/HlsUrl`。
  3. 检查 OSS/MinIO 桶的 **CORS** 配置（`Access-Control-Allow-Origin`）和 **签名 URL 过期时间**。
  4. 前端 video player 是否支持 HLS：原生 `<video>` 不支持 m3u8，需引入 `hls.js`。
- **回归点**：
  - 用 `qidi-admin` 上传一个 mp4，确认 30s 内可播放、进度条可拖动。
  - 跨租户切换：上传→播放→切换租户→再次访问原资源 URL 不应能播放（租户隔离）。

---

### P0-2 教师端上传 PPT，学生端看不到资源（S-5）

- **截图**：`screenshots/qa-0618/ID_D35828659878427B918F7FAFD22E5618.png`、`ID_52274436C1304F47958EB79DE2A166F9.png`
- **定位**：
  - 资源实体：`src/KnowledgeHub.Domain/Courses/KnowledgeResource.cs`
  - 资源列表（学生端）：`src/KnowledgeHub.Application/Resources/ResourceAppService.cs` `GetListAsync`
- **修复步骤**：
  1. 在学生端 `GetListAsync` SQL 加上 `IsPublished = true`（或 `Status = Published`）过滤；当前可能带了 `IsDeleted = false` 但缺发布状态。
  2. 检查 `TenantId` 字段：教师上传资源时是否正确写入了 `CurrentTenant.Id`。
  3. 学生门户的查询与教师端是否走同一个 `IResourceRepository.GetListAsync`；若各自实现，需统一过滤条件。
- **回归点**：
  - 教师 A 上传 PPT（不发布）→ 学生看不到（符合预期）。
  - 教师 A 上传 PPT（发布）→ 学生 A 看到；切换租户后学生 B 看不到。
  - SQL 直接 `SELECT * FROM "Resources" WHERE "TenantId" = ? AND "IsPublished" = true` 应能命中。

---

### P0-3 依旧无法创建直播（T-21）

- **截图**：`screenshots/qa-0618/ID_0D086336CA904675907102BEB9B640B1.png`
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

---

### P0-4 学生端知识图谱模块即使没有数据也显示（S-1）

- **截图**：`screenshots/qa-0618/ID_3E84DA54193743529B3A8AD079BAE418.png`
- **定位**：
  - 后端：`src/KnowledgeHub.Application/KnowledgeGraph/KnowledgeGraphAppService.cs` 增加 `HasDataAsync()` 或 `GetCountAsync()`
  - 前端：学生门户 `home.component` / `portal-routing.module` 中知识图谱入口
- **修复步骤**：
  1. 后端新增 `Task<bool> HasDataAsync()`（按当前租户统计 `KnowledgeGraph` 实体数）。
  2. 前端在加载门户首页时调用该接口，`false` 时隐藏「知识图谱」入口卡片。
  3. 同时考虑路由守卫：直接访问 `/knowledge-graph` 时若后端返回空数据，重定向到首页或显示空状态页。
- **回归点**：
  - 管理端未添加知识点 → 学生门户首页看不到知识图谱入口。
  - 管理端添加至少 1 个知识点 → 学生门户立即可见（不需重启前端）。
  - 直接 URL 访问 `/knowledge-graph`：空数据 → 友好空状态；非空 → 正常渲染。

---

### P0-5 知识图谱添加入口缺失（T-12）

- **截图**：`screenshots/qa-0618/ID_8C7FB5A159E749C981E9C0334D9B8A02.png`
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

---

## 二、P1 — 严重（13 条，本周内全部修复）

> 数据错误/字段为空/权限错配，可能影响业务流程正确性。

### P1-1 资源列表「创建人」列都是空的（T-3）

- **截图**：`screenshots/qa-0618/ID_C2B790F05D9649808F6756570C316F15.png`
- **定位**：
  - 实体：`src/KnowledgeHub.Domain/Courses/KnowledgeResource.cs`（`CreatorId` 字段）
  - 列表：`src/KnowledgeHub.Application/Resources/ResourceAppService.cs` `GetListAsync`
  - DTO：`ResourceDto` 中的 `CreatorName`
- **修复步骤**：
  1. 创建时显式赋值 `CreatorId = CurrentUser.Id`（在 `CreateAsync` 中）。
  2. 数据库补字段（如缺）：新增 migration `Added_CreatorId_To_KnowledgeResource`。
  3. `GetListAsync` 投影补 `CreatorName`（JOIN `IdentityUsers` 或通过 `IUserLookupService`）。
  4. 历史数据回填脚本：`UPDATE "KnowledgeResources" SET "CreatorId" = ...`（按 TenantId 取最早一个 Admin）。
- **回归点**：
  - 新建资源 → 列表「创建人」显示上传者姓名。
  - 老数据迁移后，刷新列表，原有空字段也应显示「系统/未知」或最早 Admin。

---

### P1-2 院校管理员也是联盟管理员（T-4，权限/角色错配）

- **截图**：`screenshots/qa-0618/ID_89B42010A99848B7A2289AC28AE81134.png`
- **定位**：Identity 模块角色定义 + 数据种子（`IdentityRole` 表）
- **修复步骤**：
  1. 区分 `SchoolAdmin`（院校管理员）和 `AllianceAdmin`（联盟管理员）：检查 seed data，确认两个角色并存而非同一角色。
  2. 用户列表显示「所属角色」字段时，正确显示两个独立角色名。
  3. 在 UI 中给两个角色分配不同权限组（如院校管理员只看本校，联盟管理员看全部租户）。
- **回归点**：
  - 创建两个测试用户分别赋予 `SchoolAdmin` 和 `AllianceAdmin`，确认权限隔离生效。
  - 切换到 `SchoolAdmin` 登录，租户切换器只显示自己学校。

---

### P1-3 资源库「这里没有租户数据」（T-2）

- **截图**：`screenshots/qa-0618/ID_CDB0628AB2AD4DC4AF38895324C1AC2F.jpeg`
- **定位**：`ResourceAppService.GetListAsync` 的租户过滤
- **修复步骤**：
  1. 在 `GetListAsync` 入口处 `CurrentTenant.Change` 包裹查询，确认租户切换时缓存被清除。
  2. 检查 `IResourceRepository` 自定义实现是否漏掉了 `TenantId` 过滤条件。
  3. 若有二级缓存（`IDistributedCache`），按 `TenantId` 作 cache key 的一部分。
- **回归点**：
  - 用联盟管理员登录，看得到所有租户的资源。
  - 切换到院校管理员登录，只看得到本校资源。
  - 切回联盟管理员，列表立即刷新（无陈旧缓存）。

---

### P1-4 智能体「已上传但不显示」（T-7）

- **截图**：`screenshots/qa-0618/ID_E740E10CEB3B4F07889A77395BAF62E4.png`
- **定位**：
  - 智能体上下文构建：`src/KnowledgeHub.Application/TeachingAgents/TeachingAgentContextBuilder.cs`
  - 教案生成/案例分析：参考 `TeachingAgentAppService.cs` 内的 prompt 模板
- **修复步骤**：
  1. 在 `TeachingAgentContextBuilder.BuildAsync` 中确认查询资源时带了 `TenantId + IsPublished` 过滤。
  2. 教案/案例分析的 prompt 模板中是否引用了资源列表的字段（如 `{resources[].title}`），若引用了但传空数组则提示不显示。
  3. 资源类型过滤：某些智能体场景可能只取 `Document/Pdf`，PPT 因类型不匹配被过滤掉。
- **回归点**：
  - 教师上传 1 份 PPT + 1 份 PDF → 教案生成输出中包含这两份资源的引用。
  - 删除其中一份 → 教案生成输出中相应内容消失（不残留旧数据）。

---

### P1-5 模板导入习题，答案都堆在 A 选项（T-16）

- **截图**：`screenshots/qa-0618/ID_A955F849F3D44B5DA957524596338E39.png`
- **定位**：`src/KnowledgeHub.Application/Exams/ExerciseAppService.cs` 中导入方法（搜索 `Import`）
- **修复步骤**：
  1. 找到 Excel 解析代码（大概率 `NPOI` 或 `EPPlus`），按列循环读取。
  2. 当前 bug：把整行当文本拼接，或只读了 `A` 列。改为按列名读取 `OptionA/OptionB/OptionC/OptionD/Answer`。
  3. 在解析前增加模板校验：列名必须为标准列；不匹配时返回明确错误「请使用最新模板」。
- **回归点**：
  - 用模板正确导入 5 道选择题 → 4 个选项分别落在 A/B/C/D，答案正确。
  - 用错误列名的文件导入 → 返回明确错误，不静默写入错误数据。

---

### P1-6 学生端选择学生字段混入非学生角色（T-18）

- **截图**：`screenshots/qa-0618/ID_113062A21B5549DBB0C915A017116A59.png`
- **定位**：就业模块 `EmploymentAppService` 或前端 `student-selector.component`
- **修复步骤**：
  1. 后端用户下拉接口强制过滤 `role = 'Student'`（JOIN `IdentityUserRoles` + `IdentityRoles`）。
  2. 前端选择器改为异步分页搜索（`paged-result`），按班级/专业过滤。
- **回归点**：
  - 教师/管理员账号不应出现在学生下拉列表中。
  - 学生数量 > 50 时，下拉应支持搜索 + 分页加载。

---

### P1-7 就业列表「不显示学生名字」（T-19）

- **截图**：`screenshots/qa-0618/ID_088C8FC1811F49F89615FF538B2259EA.png`
- **定位**：`EmploymentAppService.GetListAsync` 投影
- **修复步骤**：
  1. EF 查询补 `.Include(x => x.Student)` 或 `.Join(...)`。
  2. 检查 DTO 映射中 `StudentName` 字段是否被赋值。
- **回归点**：
  - 列表「学生姓名」列正常显示。
  - 学生改名前后列表实时反映（不缓存旧名字）。

---

### P1-8 面试官字段为空且无设置入口（T-20）

- **截图**：`screenshots/qa-0618/ID_5288665DDD1D4D8F8CC0CF6A186F4B8B.png`
- **定位**：
  - 实体：`InterviewRecord`（在 `KnowledgeHub.Domain/Employment` 下）
  - DTO：`CreateUpdateInterviewRecordDto`
  - 前端：面试记录创建/编辑表单
- **修复步骤**：
  1. 确认实体已含 `InterviewerId` 字段；若缺则 migration 补字段。
  2. 表单加「面试官」下拉（关联教师/HR），可选。
  3. 后端创建时保存 `InterviewerId`，列表返回 `InterviewerName`。
- **回归点**：
  - 创建面试记录选择面试官 → 列表正确显示。
  - 不选面试官也可保存，列表显示「未指定」（不报错）。

---

### P1-9 课程「无学习数据」（T-14）

- **截图**：`screenshots/qa-0618/ID_E3693C4232BC41398A766226E205B0F6.png`
- **定位**：
  - 学习记录：`src/KnowledgeHub.Application/Learning/LearningAppService.cs`
  - 统计 SQL：可能在 `GetCourseStatisticsAsync` 等方法
- **修复步骤**：
  1. 看是否有 `StudentLearningRecord` 表的数据写入事件（视频看完/习题提交时是否调用 `RecordAsync`）。
  2. 课程统计 SQL 的 JOIN 条件：是否把 `StudentCourse` 与 `LearningRecord` 正确关联（注意 `LEFT JOIN + WHERE` 陷阱）。
  3. 直接 `SELECT * FROM StudentLearningRecords WHERE CourseId = ?` 验证底层有数据。
- **回归点**：
  - 学生看完一个视频 → 教师端课程统计「学习人数 +1」。
  - 即使没有学生学，也应显示「0 人」而非空白或 null。

---

### P1-10 学生多时仍需逐个选择（T-15）

- **截图**：`screenshots/qa-0618/ID_F522A59A11F94D6BA6E92D3DA038E28E.png`
- **定位**：课程管理「添加学生」入口 + `CourseAppService.AddStudentsAsync`
- **修复步骤**：
  1. 前端「添加学生」组件支持：① 全选/反选 ② 按班级筛选 ③ 按关键字搜索。
  2. 后端 `AddStudentsAsync` 接收 `Guid[] studentIds`（已是数组），单次提交即可。
- **回归点**：
  - 50 个学生一次性勾选并提交，列表立即全部显示。
  - 重复提交不应产生重复关联（UNIQUE INDEX 约束）。

---

### P1-11 学生「联系方式无法自主修改」（S-4）

- **截图**：`screenshots/qa-0618/ID_22761A5592824934A56D3FC4361A7072.png`
- **定位**：
  - 用户管理：`src/KnowledgeHub.Application/Users/UserAppService.cs`
  - 前端：学生端「个人资料」页面
- **修复步骤**：
  1. 后端新增/放行 `UpdateMyProfileAsync(Guid userId, UpdateMyProfileDto input)`，允许学生改 `PhoneNumber/Email`。
  2. 前端「个人资料」加「编辑联系方式」入口，仅暴露 `Phone/Email` 字段。
- **回归点**：
  - 学生改手机号 → 保存 → 重新登录后仍生效。
  - 邮箱若开启验证：发送验证码 → 验证通过 → 生效；否则直接保存。

---

### P1-12 简历识别错乱（S-6）

- **截图**：`screenshots/qa-0618/ID_2BE79199AC1B4A399BA81B9580A59105.png`、`ID_DCDB97325EFF4302AC9E0EA492F1C876.png`
- **定位**：`EmploymentAppService.ParseResumeAsync`（简历解析 LLM 调用）
- **修复步骤**：
  1. 检查 LLM prompt：是否明确要求输出 `JSON {name, education[], experience[], skills[]}` 结构化字段。
  2. 加 fallback：当 LLM 返回结构混乱时，记录原始简历供人工复核。
  3. **不**强行自动覆盖学生填好的简历，先回显识别结果让学生确认。
- **回归点**：
  - 上传标准模板简历 → 识别结果字段完整且与简历一致。
  - 上传非标准格式 → 提示「识别失败，请手动填写」而非写入错误数据。

---

### P1-13 资讯库缺少导入模板（T-9，部分 bug — 没有模板入口）

- **截图**：`screenshots/qa-0618/ID_377996A89D6144DE8ABC58A888D1B737.png`
- **定位**：
  - 现有：`src/KnowledgeHub.Application/News/NewsImportAppService.cs`
  - 前端：资讯管理页面是否提供「下载模板」按钮
- **修复步骤**：
  1. 在资讯管理页加「下载导入模板」按钮（提供固定格式 Excel）。
  2. 模板含必填列：`Title/Category/PublishDate/Content/Source`。
  3. `NewsImportAppService` 校验列名，不匹配返回明确错误。
- **回归点**：
  - 下载模板 → 填 3 条资讯 → 导入成功 → 列表显示。
  - 用错误列名文件 → 报错「请使用最新模板」。

---

## 三、P2 — 一般（15 条，下个 Sprint 修复）

> 体验/交互问题：列表过长、按钮点不动、样式不美观、不影响主流程。

### P2-1 资源上传 25MB 上限太小（T-5）

- **定位**：`FileStorageService.UploadAsync` 中的 `MaxFileSize` 常量
- **修复**：
  1. 调大到 200MB（或读配置：`appsettings.json` 加 `Resources:MaxFileSizeMB`）。
  2. 评估前端是否需要分片上传（超过 50MB 走 tus/uppy）。
- **回归**：上传 100MB 视频成功；上传 300MB 应在客户端预校验拦截。

---

### P2-2 资源列表无法按专业筛选（T-6）

- **定位**：`ResourceAppService.GetListAsync` 不接受 `MajorId` 参数
- **修复**：
  1. DTO 加 `MajorId?` 入参；查询条件补 `.Where(x => x.MajorId == input.MajorId)`。
  2. 前端筛选器加「专业」下拉（数据源：`Majors` 接口）。
- **回归**：选某专业后列表只剩该专业资源；清空筛选显示全部。

---

### P2-3 智能体「无截图标注」（T-8）

- **截图**：`screenshots/qa-0618/ID_DD67227FCF5A47B38745D65BCB3344D8.png`
- **修复**：用户截图无文字描述，需联系用户确认现象后归类。

---

### P2-4 一道题目是否可以添加到不同章节（T-10，模型 bug）

- **截图**：`screenshots/qa-0618/ID_0C9AFD4971AC4A80AD16B1F7D0D11555.png`
- **定位**：`Exercise` 与 `Chapter` 的关系当前为多对一（一题一章节）
- **修复**：
  1. 新建关联表 `ChapterExercise (ChapterId, ExerciseId)` 多对多。
  2. EF migration + 调整 `ExerciseAppService` 的查询与编辑接口。
  3. 同一题在不同章节复用：编辑页面支持「所属章节」多选。
- **回归**：同一道题可同时加入章节 A 和章节 B；删除其中一个章节时不会连带删除题目。

---

### P2-5 章节列过长遮挡习题（T-11）

- **截图**：`screenshots/qa-0618/ID_E0C6FD2A1198423082AB6B0250EA653C.png`
- **定位**：课程编辑页（章节 + 习题）布局
- **修复**：
  1. 章节改为可折叠手风琴，默认只展开当前选中章节。
  2. 习题区使用 sticky 头部。
  3. 或拆 tab：「章节」/「习题」分两个 tab。
- **回归**：章节折叠后习题区可见；展开多个章节时滚动正常。

---

### P2-6 课程无截图（T-13）

- **截图**：`screenshots/qa-0618/ID_A06DFE966EAE45FCA1F91F66EA97BC78.png`
- **修复**：截图无文字描述，需联系用户确认现象后归类。

---

### P2-7 智能体学生选择繁琐（T-22）

- **截图**：`screenshots/qa-0618/ID_E89EFC985FF44BE68F340B81561709A5.png`
- **修复**：与 P1-10 复用「批量选择学生」组件（按班级/专业过滤 + 全选）。

---

### P2-8 首页微专业显示 0 课程（T-23）

- **截图**：`screenshots/qa-0618/ID_6C9812FEC732407FA5D4FD25B62374D2.png`
- **定位**：`src/KnowledgeHub.Application/MicroMajors/MicroMajorAppService.cs` `GetAsync`/`GetListAsync`
- **修复**：
  1. 投影补 `Courses = x.CourseMicroMajors.Select(cm => cm.Course)`。
  2. 检查 `CourseMicroMajor` 关联表是否有数据；为空则正确显示「0 课程」而非 NRE。
- **回归**：管理端为某微专业关联 2 门课程 → 首页/列表正确显示「2 课程」。

---

### P2-9 首页无截图（T-24）

- **截图**：`screenshots/qa-0618/ID_41731BFDB34A4A26BE10A9AFA502C674.png`
- **修复**：截图无文字描述，需联系用户确认现象后归类。

---

### P2-10 首页「点不动」（T-26、T-27）

- **截图**：`ID_3715E03BFF3E4285BF8D5AB8414F772B.png`、`ID_55F3D4A46F9D4099BE8795D423A6BC12.png`
- **修复**：
  1. 前端 console 排查：是否 `RouterLink` 路径错误、`disabled` 状态、事件未绑定。
  2. 检查按钮外层是否被遮挡（z-index 或 fixed 元素覆盖）。
- **回归**：点击按钮有正常路由跳转或弹窗交互；console 无报错。

---

### P2-11 选择题选项混作一团（S-2）

- **截图**：`screenshots/qa-0618/ID_1571CABBF7064D989C5187B5F0154039.jpeg`
- **定位**：学生做题页（`exercise-detail.component`）
- **修复**：
  1. 选项加 `padding: 12px 16px`、`border-radius: 8px`、选项间 `margin-bottom: 12px`。
  2. 选项前加大写字母 `A./B./C./D.` + 圆形 radio。
  3. 长文本自动换行（`word-break: break-word`）。
- **回归**：4 个选项垂直排列无重叠；选中状态有明显视觉反馈。

---

### P2-12 资讯模块蓝色方框不美观（S-7）

- **截图**：`screenshots/qa-0618/ID_B94CB43DF9744ACCBCE56F5CBD3F415B.png`
- **修复**：资讯卡片改三段式：封面图 + 标题 + 摘要（2 行）+ 元信息（来源/日期）。

---

### P2-13 实训项目对未授权学生可见（S-8）

- **截图**：`screenshots/qa-0618/ID_60C7E2838EB648D1A40600CE09168EA5.png`、`ID_C91FC61BE2094BCEBC20815E05E8FA91.png`
- **定位**：`src/KnowledgeHub.Application/Practicums/PracticumAppService.cs` `GetMyPracticumsAsync`
- **修复**：
  1. 查询条件加 `.Where(x => x.Enrollments.Any(e => e.StudentId == CurrentUser.Id && e.Status == Approved))`。
  2. 移除无报名学生可见的逻辑。
- **回归**：未报名学生访问「我的实训」为空；已报名学生仅看自己的。

---

### P2-14 统计数据显示夸张/不对应（S-9）

- **截图**：`screenshots/qa-0618/ID_5B7A336228AD417C85013504D3BB01E1.png`
- **定位**：`LearningAppService.GetStatisticsAsync` 或前端 `dashboard.component`
- **修复**：
  1. 复核统计口径（PV/UV/学习时长）：是否同一用户重复计数。
  2. 前端加「数据更新时间」与「统计口径说明」提示。
- **回归**：同一学生多次观看同一视频只算 1 个 PV；数据每日 0 点刷新。

---

### P2-15 章节列表太长（S-13）

- **截图**：`screenshots/qa-0618/ID_C16D307E96D84DDC9157740BA584C1AC.png`
- **修复**：与 P2-5 复用折叠手风琴方案；只展开当前章节。

---

## 四、修复 Sprint 排期（推荐）

### Sprint 1（本周内）— 全部 P0 + P1
- **P0**：P0-1 视频加载、P0-2 学生端资源可见、P0-3 直播创建、P0-4 知识图谱模块隐藏、P0-5 添加入口前置
- **P1**：P1-1 ~ P1-13 全部
- **预计 18 个工单 / 跨 5 个模块**

### Sprint 2（下周）— 全部 P2
- **P2**：P2-1 ~ P2-15 全部
- 优先做被多次复用的样式/交互问题（P2-5、P2-11、P2-15 可抽公共组件）

### 后续 Sprint
- 剩余 P3 类需求（**不在本文档修复范围**）— 新建 `QA_ENHANCEMENT_20240618.md` 单独规划

---

## 五、修复时需同步更新的内容

| 修复项 | 同步更新 |
|--------|----------|
| P0-1 视频加载 | API 文档 + 前端 player 文档 |
| P0-2 资源租户隔离 | 多租户测试用例 |
| P1-5 习题导入 | Excel 模板下载页 + 用户操作手册 |
| P1-12 简历识别 | 简历上传说明页（建议模板） |
| P1-13 资讯导入模板 | 模板文件 + 导入操作手册 |
| P2-4 题目多章节 | 数据库迁移脚本 + API 文档（破坏性变更需 changelog） |
| 全部修复 | `docs/QA_ISSUES_20240618.md` 中相应行标记 ✅ |

---

## 六、回归测试清单

修复完每条 bug 后，需通过以下回归用例：

1. **租户隔离**：联盟管理员 vs 院校管理员看到的资源/学生/课程不交叉。
2. **权限**：学生操作越权接口应返回 403；教师/管理员操作一致。
3. **数据一致性**：上传/编辑/删除后列表与详情数据一致；前端刷新及时。
4. **样式**：回归 LeptonX 主题 + 移动端（响应式）显示。
5. **API 日志**：修复后跑相关接口，`.dev/logs/api.log` 无 5xx 异常。
6. **冒烟**：跑一遍 `dev.sh restart && curl -sk https://localhost:44305/health-status` 主链路。