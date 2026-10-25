# Progress

## Status
In Progress

## Tasks
- [x] 全项目安全审计（两轮），详见 `issues/security-audit-2026.md`
- [x] 修复 P0：C-1 SSRF 代理、C-2 匿名 OSS 上传、C-3 匿名安装/弱许可证
- [x] 修复 P1：H-1/H-2/H-3/H-4/H-5/H-6/H-8~H-13
- [x] 修复部分 P2：M-1 PII 日志、M-2 Hangfire fail-closed、M-5 上传目录 XSS、M-6 租户统计鉴权、L-1 登出鉴权、L-2 Guid 校验
- [x] AI AppService 鉴权补齐（Chat/Learning/PracticumChat/MeiliSearchAdmin 类级；三个 AI 生成服务改 `[RemoteService(false)]` 隐藏 HTTP 暴露）
- [x] 前端路由 `permissionGuard`/`requiredPolicy` 补齐（H-7）
- [ ] 剩余：M-3 Swagger、M-4 密码策略、M-6b、`/admin/exercise` 无效策略
- [ ] 人工轮换历史泄露密钥（`.env`）

## Files Changed
- 安全文档：`issues/security-audit-2026.md`、`issues/README.md`
- 后端：
  - `src/KnowledgeHub.HttpApi/Controllers/HttpProxyController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/ImageProxyController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/ProxyHostGuard.cs`（新增）
  - `src/KnowledgeHub.HttpApi/Controllers/OssUploadController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/PracticumChatController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/AIController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/TenantListController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/ResourceFileController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/LogoutController.cs`
  - `src/KnowledgeHub.Application/Search/SummaryGenerationAppService.cs`
  - `src/KnowledgeHub.Application/Install/InstallAccessGuard.cs`（新增）
  - `src/KnowledgeHub.Application/Install/InstallAppService.cs`
  - `src/KnowledgeHub.Application/Edition/EditionAppService.cs`
  - `src/KnowledgeHub.Application/Courses/ChapterResourceAppService.cs`
  - `src/KnowledgeHub.Application/Search/SearchAppService.cs`
  - `src/KnowledgeHub.Application/Search/MeiliSearchAdminAppService.cs`
  - `src/KnowledgeHub.Application/AI/ChatAppService.cs`
  - `src/KnowledgeHub.Application/Learning/StudentExerciseRecordAppService.cs`
  - `src/KnowledgeHub.Application/Resources/FileStorage/LocalFileStorageService.cs`
  - `src/KnowledgeHub.Application.Contracts/Install/Dto/InstallDtos.cs`
  - `src/KnowledgeHub.Domain/Install/FixedLicenseValidator.cs`
  - `src/KnowledgeHub.HttpApi.Host/GrantAllPoliciesMiddleware.cs`
  - `src/KnowledgeHub.HttpApi.Host/KnowledgeHubHttpApiHostModule.cs`
  - `src/KnowledgeHub.HttpApi.Host/HangfireJobs/HangfireDashboardAuthorizationFilter.cs`
  - `src/KnowledgeHub.HttpApi.Host/appsettings.json`
- 前端：
  - `angular/src/app/install/install.component.ts`、`angular/src/app/install/models.ts`
  - `angular/src/app/practicum/practicum-chat.service.ts`
  - `angular/src/app/app.routes.ts`（路由策略补齐）
- 配置：`.gitignore`（修复规则失效）、`git rm --cached etc/docker/.env`

## Notes
- 后端 `dotnet build src/KnowledgeHub.HttpApi.Host` 通过（0 error）。
- 新增部署配置见 `issues/security-audit-2026.md` 文末「新增配置项」。
- `etc/docker/.env` 已从 git 索引移除但仍保留在本地磁盘；历史提交中的密钥必须轮换。

---

## 2026-10-19 · 智学分析（Learning Analytics）

已完成：纸质试卷（Mock OCR）→ 学生薄弱点 → 教师教学诊断 → 课程联动衔接复习 全链路。

- 方案与进度：`docs/LEARNING_ANALYTICS_PLAN.md`
- 后端：`src/KnowledgeHub.Domain/LearningAnalytics/`、`src/KnowledgeHub.Application/LearningAnalytics/`、`src/KnowledgeHub.Application.Contracts/LearningAnalytics/`
- EF：`src/KnowledgeHub.EntityFrameworkCore/LearningAnalytics/`，迁移 `20261019112904_AddLearningAnalytics`（已执行）
- 前端：`angular/src/app/learning-analytics/`（模型/服务/组件）、`angular/src/app/student/analysis/`（学生端）、`angular/src/app/learning/analysis/`（教师端）
- 路由：`/student/analysis`、`/learning/analysis`；菜单：学生端侧边栏「智学分析」、教师端「专业和课程管理 → 学情分析」
- 验证：`dotnet build` / `ng build` 通过；租户教师与学生 token 端到端跑通（建批次→Mock OCR→校对→掌握度→班级薄弱点→教学诊断 LLM→推送→学生端聚合）
- 待办：真实 OCR、知识点 LLM 抽取归一化、ML.NET 协同过滤、开课前定时推送（C5）

### 2026-10-19（续）· 智学分析 阶段 D：真实 OCR + AI 任务 + ML.NET

- 真实 OCR：接入 Qwen-VL-OCR（OpenAI 兼容 `/chat/completions`，base64 data URL，复用全局限流与租户级 Key）
  - `IPaperOcrRecognizer` + `QwenPaperOcrRecognizer` + `MockPaperOcrRecognizer`，未配置 Key 自动降级并标记 Mock
  - 图片上传落盘（`AppPaperUploadImages`）、Hangfire 异步识别（`PaperOcrProcessingJob`）、前端轮询进度
  - 解析层容错：字符串数字、`rawText` 得分/满分校正、满分-得分写反自动交换
  - 入库匹配：章节树 + 题干片段 + 满分序列滑窗对齐，低置信度标「待复核」，**校对确认后才写 AnswerRecord**
- AI 任务：`KpGenerationJob`（章节/习题 → 知识点全量生成 + 语义索引重建）、`MasteryModelTrainJob`、`PrerequisiteMiningJob`、
  夜间 `learning-analytics-nightly`（每天 03:00）；教师端「课程联动」新增 AI 任务工具条
- ML.NET：`Microsoft.ML` 5.0.1 + `Microsoft.ML.Recommender` 0.23.1，学生×知识点矩阵分解（rank 16），
  数据不足/原生库缺失回退 `BaselineMasteryPredictor`；推荐改为内容 0.5 + 协同 0.3 + 难度适配 0.2
- 课程联动：新增「课程前后关系」配置（`save-course-link` / `delete-course-link`），先修挖掘产出 `Proposed` 需确认
- 迁移：`AddPaperOcrAndKpExtraction`（已执行）
- 验证：真实图片 OCR 得分与图面完全一致（29 分）；校对→掌握度→班级薄弱点→推送→教学诊断全链路通过
- 待办：PDF 试卷解析（`qwen3.5-ocr` Responses API）、知识点向量归一化（pgvector）、推荐离线评估

### 2026-10-19（续 2）· 智学分析 阶段 E：PDF 试卷 / 判定线 / 开课前定时推送

- PDF 试卷：前端 pdfjs 逐页渲染上传（每页一份 / 多页合并两种模式），后端 `PageIndex` + `MergeGroup` +
  `PaperOcrResultMerger`，多页合并为一份 `PaperScript`（迁移 `AddPaperPageMergeAndCourseStartDate`）
- 判定线：教师端「班级薄弱点」内直接配置薄弱线 / 达标线（默认 0.60 / 0.75），保存后自动刷新分析
- 开课日期 + 定时推送：`CourseLink.PlannedStartDate`，只有进入「开课前 LeadWeeks 周」窗口才推送；
  新增 `PreCourseReviewPushService`（夜间任务 + 教师端「立即推送衔接复习包」共用，按学生 + 课程幂等）
- 衔接复习包瘦身：实测数据优先，无作答证据时用 ML 预测取最薄弱 ≤5 个知识点（此前会把整门课 18 个点都推给新学生）
- 验证：两页试卷合并为 1 份且得分还原正确；判定线读写一致；开课日期驱动 `inPushWindow`；首次推送 2 人、二次 0 条（幂等）
- 待办：知识点灰区确认 / 合并 / 拆分界面、知识点级判定线、报告导出（PDF/Excel）

### 2026-10-19（续 3）· 智学分析 阶段 F：知识点管理（归一化闭环）+ 习题关联

- 新页面：教师端「专业和课程管理 → 知识点管理」`/learning/knowledge-points`
  （列表筛选 / 灰区确认 / 编辑改名 / 合并 / 拆分 / 删除 / **习题关联（增删改权重，同题权重自动归一化）** / 合并建议 / 向量重建）
- 新后端：`KnowledgePointAppService`（路由 `/api/app/knowledge-point`）——合并会迁移习题关联、掌握度（证据加权）、
  先修关系并归一化权重；源知识点以 `CanonicalId` 归档隐藏，可追溯
- 向量归一化：`KnowledgeEmbeddingProvider`（既有 Embedding 服务 → DashScope `/embeddings` 回退 → 字符近似兜底），
  LLM 抽取改为语义优先（≥0.88 归并 / 0.75~0.88 灰区待确认）；DashScope embeddings 批量上限 10 已分片
- 实测：18/18 向量生成成功；合并建议 6 条均走 embedding（最高 0.876）；确认 / 合并 / 拆分 / 关联权重归一化全部通过
- 无需新迁移（复用既有 `AppKnowledgePoints.Embedding`）；本机 `pgvector 0.8.0` 可用，后续可平滑升级为数据库侧向量检索

### 2026-10-19（续 4）· 课程关联简化 + pgvector 落地

- 课程联动简化：按产品要求移除 `CourseLink.PlannedStartDate` / `LeadWeeks`（迁移 `RemoveCourseLinkSchedule`），
  只保留「前置课程 → 后续课程」关联；衔接复习推送不再依赖开课时间窗口（按学生+课程幂等）
- pgvector 落地：迁移 `AddPgvectorKnowledgePointEmbeddings`（扩展 + `EmbeddingVector vector(1024)` + HNSW 余弦索引 + 从 real[] 回填 18 条）；
  新增 `KnowledgePointVectorStore`（可用性/维度探测、批量写入、ANN 检索、状态统计、清空），保留 real[] 双写以便无扩展环境自动回退
- 归一化升级：合并建议支持 `scope=course|tenant`（跨课程走 pgvector ANN）；`GET /vector-status` 输出引擎状态，前端展示「向量引擎」徽标
- 实测：`engine=pgvector, dimension=1024, embeddedCount=18/18`；同课程 ANN 建议 6 条（最高 0.876）；排课字段已从表结构移除
- 待办：知识点图谱视图、知识点级判定线、推荐离线评估

### 2026-10-19（续 5）· 知识点图谱 + 知识点级判定线

- 知识点图谱：`GET /api/app/knowledge-point/graph/{courseId}`（节点/先修边/合并边/拓扑层级，含跨课程外部前置）
  + 前端 `app-knowledge-point-graph`（分层 SVG，实线=已生效先修、虚线=待确认、点线=已合并，缩放/开关/hover 高亮，点节点开操作面板）；
  知识点管理页新增「列表 / 图谱」切换
- 知识点级判定线：`ThresholdResolver.ResolveAllAsync` + `CourseThresholds`（知识点覆盖 > 课程级 > 默认），
  学生端薄弱点/班级薄弱点/重点关注学生/专项练习推荐全部按知识点判定线生效；
  新增 `POST /clear-threshold`；`GetThreshold` 支持 `knowledgePoint` 参数；列表与掌握度条展示各知识点目标线
- 踩坑：ABP 只有「单个 Id 参数」才写路径，两个 Id 参数会让 `/threshold/{courseId}` 退化成 query string（已修复）
- 实测：图谱 18 节点/分层正确/合并边正确；知识点级判定线 0.7/0.9 生效并可清除恢复默认；测试数据已还原
- 待办：图谱交互增强（拖拽/泳道/导出）、诊断报告导出

### 2026-10-19（续 6）· 报告导出（Excel）+ 推送预览

- 新增 3 个导出接口（ClosedXML，统一排版：标题条/冻结表头/自动列宽/百分比与正负号格式/薄弱项着色）：
  `export-class-report`（6 sheet）、`export-student-reports`（摘要+明细）、`export-teaching-report`（概览+达成度+诊断）
- 导出不调大模型：`GetTeachingInsightAsync(batchId, useAiAdvice)` 为 false 时复用已持久化建议、缺失用规则文案；
  学生建议优先取 `AnalysisReport.ContentJson` 里已生成的内容
- 新增 `GET /student-report-preview/{batchId}?student=` 推送预览（标题/正文/薄弱点/是否已推送），替换原占位按钮
- 前端：班级薄弱点页「导出班级报告 / 导出学生薄弱点明细 / 预览推送」，教学成果页「导出教学诊断报告」，下载走 blob
- 实测：三种导出均 200 且 sheet 结构正确（13.5KB / 8.8KB / 9.8KB）；预览返回真实已推送摘要；测试数据已清理
- 待办：PDF 版报告（HTML 模板转 PDF）、图谱交互增强、推荐离线评估

### 2026-10-19（续 7）· Word 报告 + 报告留档 + 推荐离线评估

- 报告简化：新增 `POST /export-docx-report/{batchId}?type=class|students`（NPOI XWPF 文字报告：结论+建议为主，表格为辅），
  移除两个 Excel 报告端点，保留 Excel 明细导出；后端 `LearningAnalyticsDocxReportBuilder`
- 报告留档：新表 `AppAnalysisReportFiles`（类型/大小/路径/下载次数/指标快照）+ 本地存储 `App_Data/analysis-reports`，
  接口 archive/list/download/delete，前端「报告留档」卡片（迁移 `AddAnalysisReportFiles`）
- 推荐离线评估：抽离 `RecommendationScorer` + 可配置权重（`LearningAnalytics:RecWeights:*`），
  `RecommendationEvaluator` 用留出法（最近 3 条作答做测试集）对比 7 组权重的 WeakHitRate@5 / Precision@5，
  接口 `POST /evaluate-recommendation`，前端展示对比表并提示应用建议权重
- 实测：Word 报告 5.8KB/73 段、学生汇总 4.9KB/25 段；归档 2 个文件、下载计数递增、删除同步清理磁盘；
  评估 47 条作答 → 当前配置 2/3 命中率，纯内容 0、纯难度 0.333
- 待办：PDF 报告（HTML 转 PDF）、图谱交互增强

### 2026-10-19（续 8）· 归档报告走 OSS

- `AnalysisReportFileStore` 改为「OSS 优先 + 本地回退」：复用租户级 OSS 配置，对象键
  `{UploadPath}/analysis-reports/{batchId}/{guid}.ext`，`StoredPath` 用 `oss:`/`local:` 前缀区分后端（历史数据免迁移）
- 下载走 OSS GetObject、删除同步清理 OSS 对象；DTO 增加 `storage`，前端归档列表显示 OSS/本地徽标
- 实测（OSS 已真实配置）：上传成功（日志含对象键）→ 列表 storage=oss → 下载 5868 bytes、下载次数 0→1 → 删除 204 且无失败日志

### 2026-10-20 · 专项练习闭环 + qidi 示例数据

- 专项练习：新增 `PracticeAppService`（`/api/app/practice`：`GET /set/{courseId}`、`POST /submit`）与前端 `/student/practice` 页面，
  逐题判定 + 掌握度回写；智学分析「专项练习」按钮改为跳转该页
- 示例数据：新增 `LearningAnalyticsDemoDataSeeder`（仅 qidi 租户、幂等）：2 门课程（前置/后续，4+3 章、18 题）、
  2 个可登录学生（demo_student/demo_student2，密码 1q2w3E*）、已确认试卷批次（2 份、均分 44.5、含纸质作答）、
  在线作答、课程关联、个人报告与未读消息
- 文档：新增 `docs/LEARNING_ANALYTICS_DEMO.md`（数据清单 / 账号 / 端到端步骤与实测结果 / 重置 SQL / 配置）
- 实测：教师 7 步 + 学生 6 步全通过；练习使「IP 地址与子网划分」掌握率 0.45 → 0.58
- 修复：IdentityUser 租户需构造函数传入（否则建成宿主用户）

### 2026-10-20（续）· 推荐权重一键应用 + 图片 OSS 直传

- 权重热生效：新增 3 个 Setting（`KnowledgeHub.LearningAnalytics.RecWeight*`）+ `RecommendationWeightProvider`，
  `RecommendationEngine`/`RecommendationEvaluator` 统一读取；新增 `GET /recommendation-weights`、
  `POST /apply-recommendation-weights`、`POST /apply-best-recommendation-weights`；前端「应用建议权重」一键写入并立即生效
- 图片 OSS 直传：上传载荷支持 `url`（校验必须属于本租户 OSS 域名），`PaperUploadImage.StoredPath=url:...`；
  `PaperOcrRequest.ImageUrl` 让 Qwen-VL-OCR 按 URL 取图，请求体大幅减小；前端自动尝试 OSS，失败回退 Base64
- 示例数据微调：在线作答显式设置 `CompletedAt` 递增，保证离线评估测试集含错题（评估可用 2 名学生）
- 实测：权重 0.5/0.3/0.2 → 0.6/0.2/0.2 读回一致；评估当前 0.500 → 纯内容 0.750；apply-best 写入并读回；
  OSS 图片 URL 直传 + OCR 识别成功（学号/6 题得分/全部匹配）

### 2026-10-20（续 2）· PDF 直传 + 教师「只看我教的课程」

- PDF 直传：`qwen3.5-ocr` Responses API（`input_file.file_url`）已接通，PDF 走 OSS URL；解析层加固
  （去 HTML 标签、按行抽得分/满分、题号归一化、QuestionNo 截断）；新增 `GET /ocr-capabilities`。
  因实测转写质量低于图片路径，`LearningAnalytics:PdfDirectEnabled` 默认 **false**（需时开启，结果强制人工校对）
- 教师视角：`PagedCourseRequestDto.TeacherId` 补字段并参与过滤；「学情分析」「知识点管理」新增「只看我教的课程」开关（空结果自动回退全部）；
  示例课程播种时挂到租户内第一个 Teacher 账号
- 实测：能力探测正常；PDF 直传识别学号/学生/3 题得分并全部匹配（置信度 0.6）；teacherId 过滤 2 门 vs 全部 7 门

### 2026-10-20（收尾）· 主线交付确认 + 总结文档

- 最终冒烟 14/14 PASS（教师课程过滤/OCR 能力/批次/知识点/班级薄弱点/教学诊断/课程关联/图谱/权重/Word 报告/学生总览/路径与复习包/推荐练习/练习集）
- 新增 `docs/LEARNING_ANALYTICS_SUMMARY.md`：需求对照、功能清单、数据模型、关键实现摘要、文件清单、接口清单、验证记录、配置项、待办优化
- 主线三条需求（纸质试卷→薄弱点→推送、教师教学诊断、课程联动）判定为已完成；剩余为 P1/P2 优化项

### 2026-10-23 · 多校资源库架构（学校 / 专业 / 共建资源库 / 社会学校）

对应需求三条：①租户=学校、租户下多专业、资源库按专业维度且可多校共建（主建学校+合作学校），
库内课程/资源/习题有专业维度限制；②学校为独立实体（简介/徽标），首页显示校徽与名称并可跳简介；
③平台唯一「社会学校」，学生可自由注册，课程与资源可一键共享到社会学校。

- 领域模型：新增 `School`（全局实体，与租户 1:1）、`ResourceLibrary`（主建学校的租户级实体）、
  `ResourceLibraryMember`（Owner=主建 / Partner=合作）、`CourseShare`；
  `Resource/Course/Exercise` 各加 `LibraryId`（`Exercise` 另加 `MajorId`）
- 枚举：`ResourceLibraryStatus`(Draft/Active/Archived)、`ResourceLibraryMemberRole`(Owner/Partner)
- EF 迁移 `20261023055341_AddSchoolLibraryPublicSchool`：4 张新表（AppSchools / AppResourceLibraries /
  AppResourceLibraryMembers / AppCourseShares）+ 3 张表补列
- 服务端：SchoolAppService、ResourceLibraryAppService、CourseShareAppService、PublicRegistrationAppService；
  资源侧补 `ShareToPublicSchoolAsync`；Portal 首页租户列表补 `LogoUrl`
- 专业维度强制校验：`ResourceLibraryAttachmentValidator` 在资源/课程挂库时校验主专业与库一致
- 公开注册：`IdentityUser.TenantId` 走构造函数传入（先 `CurrentTenant.Change`），自动授予 `Student` 角色
- 存量自愈：`SchoolProvisioningEnsureHostedService` 启动期为存量租户补建学校档案；
  新建租户由 `SchoolProvisioningHandler` 订阅 `EntityCreatedEventData<Tenant>` 即时建卡
- 权限：`Schools.*` / `ResourceLibraries.*` 已在 `RolePermissionSeeder` 补授（教师/学生可浏览共建库）
- 前端：新增 `/school/:id` 学校简介页、`/register` 社会学校注册页、`/admin/school` 学校设置、
  `/admin/libraries` 资源库管理；首页卡片渲染校徽 + 校名跳简介；资源共享弹窗加「共享到社会学校」
- 代理：`abp generate-proxy -t ng -u https://localhost:44305` 重新生成（新增 schools / resource-libraries /
  public-registrations / course-share 四个服务）；修回被生成器覆盖的手写 `UserService.changePassword`，
  并修正生成器对流式端点产出的非法 `any<T>` 泛型
- 实测：社会学校指定 / 公开注册 / 登录 / 建库 / 加合作学校 / 合作方跨校查库 /
  课程与资源一键共享到社会学校 / 社会学习者收到共享 / 首页校徽 / 学校简介页（真实浏览器）全部通过
- 文档：`docs/多校资源库架构设计.md`（模型 / 接口 / 权限矩阵 / 实测记录 / 已知边界）

### 2026-10-24 · 域名绑定多租户（学校专属域名）

需求：每所学校有自己的域名或子域名；通过该域名访问时系统只显示本租户的资源库内容，
登录账号也必须是本租户的账号；**平台域名（iketong.com / www.iketong.com）行为保持不变**。

- 领域模型：`School` 增加 `Domain`（学校自有完整域名）与 `Slug`（子域名前缀），各带唯一索引；
  迁移 `20261024031912_AddSchoolDomainBinding`；新增设置项 `KnowledgeHub.Domain.BaseDomains`
- 租户解析：自研 `SchoolDomainTenantResolveContributor`，注册到 `AbpTenantResolveOptions` 的 index 0，
  **域名优先于令牌**。不用 ABP 自带 `DomainTenantResolveContributor` 的原因：它只支持
  「固定后缀 + 前缀即租户名」，表达不了每校自己的独立域名；且 ABP 解析链令牌在前，
  会让「拿 A 校令牌访问 B 校域名」仍显示 A 校内容。匹配顺序：精确 `Domain` → `{slug}.{基础域名}` 匹配 `Slug`
- 缓存：原生 `IDistributedCache`（不用 ABP 类型化缓存 —— 它会按当前租户加键前缀，解析前后键不一致）；
  10 分钟 TTL，域名变更时主动失效
- 跨校账号守卫：新增 `SchoolDomainTenantGuardMiddleware`（置于 `UseMultiTenancy` 之后）。
  本项目管线是「令牌先校验、租户后解析」，跨校令牌不会被判为无效，只会查不到数据报 404；
  守卫改为显式 401 + 「当前登录账号不属于该学校」文案
- 接口：新增 `GET /api/public/current-tenant`（匿名，依据 AppliedResolvers 是否含 SchoolDomain 判断
  是否由域名解析）；收敛 `GET /api/public/tenants` —— 学校域名下只返回本校，避免把学校清单公开给访客
- 门户收窄：`PortalAppService` 的统计 / 热门资源 / 浏览 / 租户列表统一按 `CurrentTenant.Id` 收窄，
  学校域名只显示本校，平台域名维持全平台聚合
- 前端：新增 `SchoolDomainContextService` + 启动初始化器（用 HttpClient 直连相对路径 ——
  初始化器并行执行时 ABP 的 `environment.apis` 尚未就绪，RestService 解析 apiName 会抛错）；
  登录页学校域名下锁定机构、显示校名/校徽、隐藏系统管理员入口、他校账号登录追加归属提示；
  管理端 `/admin/school` 增加域名与子域名前缀配置
- issuer 动态化：OpenIddict 原先把 issuer 写死为平台地址，学校域名下 discovery 校验必然失败；
  改为按请求动态推导（`AuthServer:StaticIssuer=true` 可回退写死），每域签发本域令牌。
  前提：反代把原始协议透传到 API（宝塔 `X-Forwarded-Proto: https` + 容器 nginx 已固定 https）
- 本地开发：`proxy.conf.js` 统一所有反代 `changeOrigin: false` + `X-Forwarded-Proto: http`
  （Host 透传才能识别学校子域名；/api 与 /connect 协议头必须一致，否则登录后立刻 401 掉线）；
  `environment.ts` 改用 `window.location.origin`（原先硬编码 localhost:4200 会让 API 请求全打到平台域名）
- 代理修复脚本：新增 `angular/scripts/fix-generated-proxy.mjs`，固化 `abp generate-proxy` 的四处已知
  生成器问题（覆盖手写 changePassword、流式端点非法 `any<T>` 泛型、StringValues 未导出等）
- 实测（真实浏览器）：平台域名首页显示 2 所学校、机构下拉可选任意租户、host 管理员登录进工作台、
  会话保持正常；学校域名首页只显示本校、机构锁定为该校校名+校徽、本校账号登录成功进工作台、
  他校账号被 401 拒绝并给出「该账号不属于该学校」提示、匿名浏览正常
- 文档：`docs/域名绑定部署指引.md`（宝塔接入步骤 / 协议透传检查 / 验证清单 / 排障表）、
  `docs/域名绑定多租户方案.md` 追加实现对照与两个实施坑

### 2026-10-25 · 门户新原型改版（首页 / 租户资源库列表 / 详情）+ 资源库负责人字段

按 `new_design/易课通_全专业前端交互原型.html` 重做门户三个页面，并把「多校共建资源库」
（`ResourceLibrary` 实体）接到门户公开侧。

- **资源库负责人字段**：`ResourceLibrary` 新增 `Principal`（文本，≤128）——
  与 `OwnerTenantId`（主建学校）区分：后者是组织维度，这里是自然人责任人。
  迁移 `20261025124829_AddResourceLibraryPrincipal`；DTO 同步
  （`Create`/`Update`/`ResourceLibraryDto`）；后台「资源库管理」新增「建设负责人」输入项与列表列。
- **门户公开库接口**（`PortalAppService`，`[AllowAnonymous]`，跨租户）：
  - `GET /api/app/portal/public-library-list` → 已发布 + 公开展示的库列表
    （主建学校 / 参建学校 / 负责人 / 主建专业 / 课程·资源·微专业数）
  - `GET /api/app/portal/{id}/public-library` → 详情（参建单位、课程、资源、微专业、师资、学习人数）
  - 库内内容口径：显式挂库的课程/资源 ∪ 主建学校该专业维度下的内容
    （否则刚建库、尚未逐条挂载的库在门户上是空的）
- **前端三页重做**（新增共享样式层 `angular/src/styles/_proto-library.scss`，令牌取 `--kh-*`）：
  - 首页 `home/portal-home.component`：轮播（原型 SVG 示意图）+ 发现区（专业/关键字搜索 + 快捷入口）
    + 教学资源库 + 共建共享资源库（重点共建卡 + 推荐卡）+ 微专业（培养路径行）+ 实训入口 + 资源/资讯
  - 资源库列表 `home/library/library-list.component`（`/explore/library`）：
    「一库一专业，多校共建共享」+ 专业方向筛选 + 资源库卡片（主建学校 / 负责人 / N 校共建）
  - 资源库详情 `home/library/library-detail.component`（`/library/:id`）：
    库头横幅 + 建设介绍与统计 + 8 个原型栏目 + 建设目录侧栏 + 库级信息
  - 栏目与真实数据的映射（原型里没有独立数据源的栏目用已有数据填充）：
    课程资源→库内课程；实训资源→微专业培养路径；企业案例/标准规范→库内资源素材；
    师资发展→库内授课教师；学生发展→课程学习人数；建设动态→资源建设时间线；参建单位→库成员
  - 旧的租户制 `library-card` 组件随之无引用，已删除；`explore` 组件移除 library 分区
- **顺带修掉两个真实缺陷**（都会让整站白屏）：
  - `GrantAllPoliciesMiddleware` 与响应压缩冲突：浏览器带 `Accept-Encoding: gzip, deflate, br`
    时 `/api/abp/application-configuration` 返回空响应体 → 前端拿不到 app config，
    永远停在启动闪屏。改为该接口不参与压缩（`UseWhen`），并给中间件加解析失败降级
    （原样回写而不是抛 500）
  - `CustomAuthService.init()`：`refresh_token` 失效时 `super.init()` 抛错会让 APP_INITIALIZER 失败，
    整站卡在闪屏。改为捕获后清残留 token、按游客继续启动
- 实测（真实浏览器）：首页三张轮播 + 五门课程 + 三个共建库（负责人/参建学校均来自库）+
  两条微专业路径；资源库列表 3 个库（主建/负责人/N 校共建/参与建设）；详情页 8 个栏目
  逐个点开（课程 4 项、企业案例 2 项、参建单位主建校 + 参与校）

### 2026-10-25 · 资源库中心对齐原型「改版版」shared-center

复核 `new_design/source/work/` 时发现原型 `#shared-center` 有两个版本：
`sharedCenter`（旧）与 `redesignedShared`（改版版，最终采用）。
第一版按旧版做，结构与卡片都对不上，现已整体改成改版版。

- 页面结构（对应 redesignedShared + hubHero + hubFilter + hubCount）：
  频道头图（图标标签 + 标题 + 说明 + 主按钮 + 右侧「共建流程」栏）
  → 快捷入口条（功能模板 / 参建管理 / 共建任务 / 我的班级 / 发现资源库）
  → 筛选面板（关键词 + 搜索 + 重置 / 专业方向 + 筛选范围 + 提示）
  → 结果计数行 → 3 列资源库卡片 → 底部说明
- 卡片（对应 channel-card）：封面 = 专业方向标签（右上）+ 库简称 + 右下共建图标 SVG；
  正文 = 库名 + 「主建设单位：X / 负责人：Y」+ 底栏「N 所参建校 · M 份资源 / 进入 →」。
  关键细节：原型 `.cover` 基类带 `align-items:end`，竖排后封面内容是**右对齐**的，
  默认 stretch 会让标签贴左边 —— 已显式写 `align-items:flex-end`。
- 快捷入口条的落点：功能模板 → /admin/libraries?focus=create（新建资源库），
  参建管理 → /admin/libraries?focus=members（共建学校弹窗），共建任务 → /admin/libraries，
  我的班级 → /learning/student-enrollment。管理端新增 `?focus=` 入口参数处理。
- 共建共享资源库中心的「其他学员」在平台尚无独立页面，本轮用「发现资源库」占位。

### 2026-10-25 · 资源库中心卡片按原型逐项对齐（计算样式级）

上一轮只对齐了「结构」，本轮用 `agent-browser get styles` 把原型与本页的
**计算样式逐项对比**，找出仍然不一致的地方并修正。

原型最终生效的取值（来自 `new_design/source/work/` 的共享视觉系统段）：
- `body{font:16px/1.6}` + 后置 `body{line-height:1.65}` → 基准字号 16px / 行高 1.65
- `h1/h2/h3` 浏览器默认 bold（700）、`h3{font-size:18px;line-height:1.5}`
- `.btn` 不设 font-size（继承 16px）、`padding:10px 20px` → 按钮高 48.39px
- `.link` 是「品牌色 + 加粗」的纯文字链接（无边框无内边距），不是描边按钮
- `.channel-intro{padding:27px 40px}`、`.channel-aside{margin:23px 0}`
- `.channel-card .card-foot` 只有 `border-top + padding-top:14px`，间距由 p 的 margin-bottom 提供

本轮修正：
1. 基准字号/行高 15px/1.7 → **16px/1.65**；标题字重 600 → **700**，
   行高按原型（h1 1.25 / h2 1.4 / h3 1.5）。这一步同时把首页与详情页的
   文字块高度、标题基线一并拉齐到原型。
2. `.p-btn` 去掉 14px 字号、padding 改 10px 20px → 按钮尺寸与原型一致（156.25 × 48.39）
3. `.p-btn--link` 改为纯文字链接（原型 `.link`），另加 `.small` 变体（13px），
   首页各板块「全部 X →」改用它；详情页「查看课程 →」保持 16px
4. 头图 intro 内边距 36px → 27px、aside 外边距 30px → 23px、眉标下边距 14px
5. 卡片正文 p 的下边距交给 p 自己（16px），底栏不再额外加 margin-top

对齐后与原型的计算样式**完全一致**（卡片 424 × 362.578、封面 166px、
strong 25px/700/35px、caption 11px/letter-spacing 2px、正文 13px/23.4px、
底栏 12px/19.8px、头图 244.484px、按钮 156.25 × 48.39、入口条 55.09px）。

唯一保留的差异：品牌主色。原型最终共享视觉系统用 `--brand:#14766d`，
本项目 `_palette.scss` 明确保留 `#087f7a`（与既有首页 home-v2 一致），
其余令牌已与原型一致。改主色会波及全站（管理端 / 学生端 / ABP 主题），
属于产品级取舍，未在本轮改动。

### 2026-10-25 · 院校门户（原型 #campuses / #school）+ tab 补齐

原型里院校是独立的两页，本轮补上并接进 tab：

- **院校门户目录 `/campuses`**（新增 `home/campus/campus-directory.component`）
  对应原型 `#campuses`（campusDirectory）：频道头图「发现院校，走进专业学习空间」
  + 学校名称搜索 + 院校卡片网格（campus-mark 校徽/简称 + 校名 + 「进入院校门户 →」）。
  数据源 `GET /api/public/tenants`；列表里的「全局」伪租户（id 为空）与平台唯一
  「社会学校」不是院校，均已过滤。
- **院校独立门户 `/school/:id`**（`home/school-profile` 重做）
  对应原型 `#school`（campusPortal）：campus-hero（校徽 + 院校独立门户 + 校名 +
  「切换院校 →」）→ 院校栏目条 → campus-summary（教学资源库 / 主建·参建共建库 / 门户域名）
  → 本校教学资源库（course channel-card）→ 共建共享资源库 → 本校学习与服务
  → 联系方式 / 专业设置。本校课程用 `GET /api/app/portal/public-browse?tenantId=` 取。
- **tab 补齐**：门户顶栏与资源库中心的快捷入口条都加了「院校门户」；
  首页快捷入口加「找院校」。

共享样式层新增 campus-* 段（hero / mark / summary / tile / service / contact / major）。
