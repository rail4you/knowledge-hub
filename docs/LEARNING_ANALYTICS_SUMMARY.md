# 智学分析（Learning Analytics）· 交付总结

> 结论：**三条主线需求已全部完成并可端到端演示**（qidi 租户已预置示例数据，2026-10-20 最终冒烟 14/14 通过）。
> 本文汇总：完成的功能、涉及文件、实现摘要、验证记录、配置与待办优化。
>
> 相关文档：
> - 方案与分阶段进度：`docs/LEARNING_ANALYTICS_PLAN.md`
> - 示例数据与操作步骤：`docs/LEARNING_ANALYTICS_DEMO.md`

---

## 1. 需求完成情况

| 原始需求 | 状态 | 交付要点 |
| --- | --- | --- |
| ① 老师上传纸质试卷 → 分析成绩 → 学生薄弱点 → 推送学生/老师 | ✅ 完成 | Qwen-VL-OCR 真实识别 + 多页 PDF 合并 + 人工校对闸门；统一作答记录与掌握度计算；站内消息推送；学生端 4 Tab 分析页 |
| ② 由学生成绩反推教师教学成果（哪部分教学不到位） | ✅ 完成 | 章节得分率/标准差/前 25% → 规则诊断（讲解不足 / 两极分化 / 达标）+ LLM 建议；Word/Excel 报告与归档 |
| ③ 课程联动：学 A 前先补前置课程 B 的薄弱点，形成学习流程 | ✅ 完成 | `CourseLink`（仅前后置关联，不含排课）+ `KpPrerequisite` 先修图 + 衔接复习包（拓扑排序）+ 学习路径 + 一键推送 |
| 附加（过程中明确） | ✅ 完成 | 知识点管理（灰区确认/合并/拆分/习题关联）+ 向量归一化（pgvector）+ 专项练习闭环 + 推荐离线评估与权重热生效 + 报告 OSS 留档 |

**主线判定**：可以认为「纸质试卷驱动的学情分析闭环」已完成——从上传识别、校对入库、掌握度计算、班级与教学诊断，到学生端薄弱点/路径/练习、教师端报告导出与归档，全部有真实数据与接口支撑。

---

## 2. 功能清单

### 2.1 教师端

| 页面/入口 | 能力 |
| --- | --- |
| `/learning/analysis` · 纸质试卷分析 | 上传整班试卷（图片/多页 PDF）、Mock OCR 演示、OCR 步骤条与图片级状态、失败重试、校对面板（逐题改分、一键确认全部）、批次列表与统计 |
| `/learning/analysis` · 班级薄弱点 | 知识点掌握率（带教学目标标尺）、重点关注学生、一键推送个人报告、导出 Word/Excel、报告留档（OSS/本地） |
| `/learning/analysis` · 教学成果分析 | KPI（得分率/较上届/未达标章节）、章节达成度、诊断建议、推送预览、**推荐效果离线评估 + 应用建议权重** |
| `/learning/analysis` · 课程联动 | 课程前后关系配置、前置知识点映射、AI 任务（抽知识点/挖先修/重训模型/立即推送复习包）、图谱入口 |
| `/learning/knowledge-points` | 知识点列表与筛选、灰区确认、编辑、**合并**、**拆分**、**习题关联（权重归一化）**、合并建议（同课程/跨课程 pgvector）、重建向量、知识点级判定线、**图谱视图**、只看我教的课程 |

### 2.2 学生端

| 页面/入口 | 能力 |
| --- | --- |
| `/student/analysis` | 我的薄弱点（成绩/排名/知识点/失分归因）、学习路径（前置→后续 + 衔接复习进度）、消息（已读/未读）、专项练习推荐 |
| `/student/practice` | 逐题作答（单选/多选/判断/填空/主观）、即时判定、结束页正确率 + 掌握率变化 + 逐题回顾 |
| 侧边栏 | 新增「智学分析」入口（学生端） |

### 2.3 后台能力（Hangfire + 夜间任务）

| 任务 | 触发 | 说明 |
| --- | --- | --- |
| `PaperOcrProcessingJob` | 上传后/重试 | 图片或 PDF 识别 → 习题匹配 → 待校对试卷（多页合并） |
| `KpExtractionJob` | 教师点击/AI 任务 | LLM 抽知识点 + 语义归一化（≥0.88 归并 / 0.75~0.88 灰区） |
| `MasteryModelTrainJob` | 教师点击/夜间 | ML.NET 矩阵分解训练，数据不足回退基线 |
| `PrerequisiteMiningJob` | 教师点击/夜间 | 共同修读学生的前后知识点相关性挖掘（Proposed，需确认） |
| `LearningAnalyticsNightlyJob` | 每天 03:00 | 逐租户重训模型 + 挖掘先修 + 可选开课前衔接推送 |

---

## 3. 数据模型

15 张业务表（`App` 前缀）+ 1 张归档表，全部实现 `IMultiTenant`：

| 分组 | 实体 |
| --- | --- |
| 知识点 | `KnowledgePoint`、`ExerciseKnowledgePoint`（复合唯一）、`KpPrerequisite`、`MasteryThreshold` |
| 掌握度 | `StudentKpMastery`（学生×知识点）、`AnswerRecord`（纸质/课堂）、复用 `StudentExerciseRecord`（在线） |
| 试卷 | `PaperBatch`、`PaperScript`、`PaperItemScore`、`PaperUploadImage` |
| 课程联动 | `CourseLink`、`ReviewPackage`、`ReviewPackageItem` |
| 报告/推送 | `AnalysisReport`、`AnalysisReportFile`（归档）、`StudentNotification` |
| 教学诊断 | `TeachingInsight` |

迁移序列：`AddLearningAnalytics` → `AddPaperOcrAndKpExtraction` → `AddPaperPageMergeAndCourseStartDate` → `RemoveCourseLinkSchedule` → `AddPgvectorKnowledgePointEmbeddings` → `AddAnalysisReportFiles`。

---

## 4. 关键实现摘要

**掌握度计算**（`MasteryCalculator` + `MasteryRecalculationService`）
Beta 先验（A0=B0=2）平滑 + 120 天半衰期时间衰减；观测权重 = 知识点权重 × clamp(满分/10, 0.2, 3)。
在线作答（`StudentExerciseRecord`）与纸质/课堂作答（`AnswerRecord`）统一进同一套观测，避免「一题定生死」与旧成绩长期主导。

**判定线**（`ThresholdResolver` → `CourseThresholds`）
优先级 **知识点覆盖 > 课程级 > 系统默认 0.60/0.75**，批量解析避免逐点查库；学生端薄弱点、班级薄弱点、重点关注学生、专项练习推荐全部按各自判定线判定。

**习题 → 知识点映射**（`KnowledgePointIndexer`）
按叶子章节初始化知识点（同名去重）；习题按章节树「自身 → 祖先 → 子孙」解析，无法归属落到「课程综合」；LLM 抽取结果与 Seed 映射互不覆盖（手工映射最高优先）。

**OCR**（`IPaperOcrRecognizer`）
- `QwenPaperOcrRecognizer`：OpenAI 兼容 `/chat/completions` + `qwen-vl-ocr`（图片，支持 Base64 / OSS URL）；
  解析层三重加固：字符串数字宽容解析、从「得分：X分」「（Y分）」校正、得分/满分写反自动交换。
- `RecognizePdfAsync`：`qwen3.5-ocr` + `/responses` + `input_file.file_url`（整份 PDF，≤50 页），
  用「逐行转写」提示词 + 本地 `ParseTranscript`（去 HTML 标签、题号归一化、按行抽分）。默认关闭。
- `MockPaperOcrRecognizer`：未配置 Key 时降级，结果标记 mock。
- 多页合并：`PaperOcrResultMerger` + `PaperUploadImage.MergeGroup/PageIndex` → 一份 `PaperScript`。

**入学闸门**：OCR 结果只落 `PaperScript/PaperItemScore`；**校对确认后**才写 `AnswerRecord`，因此不会污染掌握度矩阵。

**推荐**（`RecommendationEngine` + `RecommendationScorer`）
三路打分：内容（薄弱向量 × 习题知识点，0.5）、协同（相似学生错误率，0.3）、难度适配（预测答对概率趋近 0.65，0.2）；
权重可由设置项热生效；同题权重归一化到 1；推荐理由可解释（「XX 掌握率 44%（低于目标 75%）」）。

**协同过滤**（ML.NET）
学生×知识点矩阵分解（rank 16）补全未作答知识点与相似学生；样本不足/原生库缺失回退 `BaselineMasteryPredictor`（全局均值+学生偏移+知识点偏移）。

**课程联动**
`CourseLink`（仅前置→后续）+ `KpPrerequisite`（跨课程先修，需确认）；
衔接复习包：沿确认先修边向前追溯 1~2 跳 → 取最薄弱 → 影响面/掌握度优先 + 拓扑排序；无作答证据时用模型预测取 ≤5 个，避免把整门课塞给学生。

**知识点归一化**
精准同名 → 复用；互相包含（≥2 字）→ 复用；语义余弦 ≥0.88 → 归并；0.75~0.88 → 新建但 `PendingReview`；否则确认态。
向量提供方：既有 `EmbeddingService` → Qwen `/embeddings`（批量 10）→ 字符 Jaccard 兜底；落库 `real[]` + pgvector `vector(1024)` 双写。

**pgvector**
`EmbeddingVector vector(1024)` + HNSW 余弦索引（异常安全迁移，无扩展自动降级为进程内余弦）；
`KnowledgePointVectorStore` 负责探测/写入/ANN 检索/清空；合并建议支持 `scope=course|tenant`（跨课程走 ANN）。

**报告**
Word（`LearningAnalyticsDocxReportBuilder`，文字为主：结论、知识点、学生成绩、重点关注、诊断、建议）+ Excel 明细（学生薄弱点）。
导出**不调用模型**：`GetTeachingInsightAsync(useAiAdvice:false)` 复用已持久化建议、缺失用规则文案。
归档 `AnalysisReportFileStore`：**OSS 优先 + 本地回退**，`StoredPath` 用 `oss:`/`local:` 前缀（历史数据免迁移），记录下载次数与指标快照。

**推荐离线评估**（`RecommendationEvaluator`）
留出法：每学生最近 3 条作答作测试集、掌握度只用历史；候选池 = 全部习题 − 历史已做对；
对 7 组权重算 WeakHitRate@K / Precision@K，给出建议权重并可一键应用。

**示例数据**（`LearningAnalyticsDemoDataSeeder`）
仅 `LearningAnalytics:DemoTenantName`（默认 qidi）租户、幂等：2 门课程（4+3 章 / 18 题）、2 个可登录学生、
已确认试卷批次（2 份/均分 44.5/24 条单题得分）、在线作答、课程关联、个人报告与未读消息、课程挂到租户内 Teacher 账号。

---

## 5. 文件清单

### 5.1 后端 · 新增

**Domain（`src/KnowledgeHub.Domain/LearningAnalytics/`）**
`KnowledgePoint.cs`、`ExerciseKnowledgePoint.cs`、`KpPrerequisite.cs`、`CourseLink.cs`、`MasteryThreshold.cs`、`StudentKpMastery.cs`、`AnswerRecord.cs`、`PaperBatch.cs`、`PaperScript.cs`、`PaperItemScore.cs`、`PaperUploadImage.cs`、`AnalysisReport.cs`、`AnalysisReportFile.cs`、`StudentNotification.cs`、`ReviewPackage.cs`、`TeachingInsight.cs`、`MasteryCalculator.cs`、`LearningAnalyticsDemoDataSeeder.cs`

**Domain.Shared** `LearningAnalytics/Enums/LearningAnalyticsEnums.cs`

**Application.Contracts（`.../LearningAnalytics/`）**
`ILearningAnalyticsAppService.cs`、`IKnowledgePointAppService.cs`、`IPracticeAppService.cs`、`ILearningAnalyticsJobQueue.cs`、
`Dtos/LearningAnalyticsDtos.cs`、`Dtos/KnowledgePointDtos.cs`、`Dtos/PracticeDtos.cs`

**Application（`src/KnowledgeHub.Application/LearningAnalytics/`）**
`LearningAnalyticsAppService.cs`、`KnowledgePointAppService.cs`、`PracticeAppService.cs`、
`KnowledgePointIndexer.cs`、`KnowledgePointExtractor.cs`、`KnowledgePointVectorStore.cs`、`KnowledgeEmbeddingProvider.cs`、
`MasteryRecalculationService.cs`、`ThresholdResolver.cs`、`PaperIngestionService.cs`、`PaperBatchStatsService.cs`、
`PaperOcrSimulator.cs`、`PaperOcrProcessingJob.cs`、`KpExtractionJob.cs`、`LearningAnalyticsJobs.cs`、
`RecommendationEngine.cs`、`RecommendationScorer.cs`、`RecommendationEvaluator.cs`、`RecommendationWeightProvider.cs`、
`PrerequisiteMiner.cs`、`AnalysisReportWriter.cs`、
`Ocr/PaperOcrModels.cs`、`Ocr/QwenPaperOcrRecognizer.cs`、`Ocr/MockPaperOcrRecognizer.cs`、`Ocr/PaperImageStore.cs`、`Ocr/PaperOcrResultMerger.cs`、
`Ml/MasteryPredictor.cs`、`Ml/MfMasteryPredictor.cs`、`Ml/MasteryModelTrainer.cs`、
`Reports/LearningAnalyticsDocxReportBuilder.cs`、`Reports/LearningAnalyticsExcelReportBuilder.cs`、`Reports/AnalysisReportFileStore.cs`

**EntityFrameworkCore** `LearningAnalytics/LearningAnalyticsDbModelCreatingExtensions.cs` + 6 个迁移

**HttpApi.Host** `HangfireJobs/HangfireLearningAnalyticsJobQueue.cs`

### 5.2 前端 · 新增

`angular/src/app/learning-analytics/`
`models/learning-analytics.models.ts`、`services/learning-analytics.service.ts`、`services/learning-analytics-mock.ts`、
`services/knowledge-point.service.ts`、`services/practice.service.ts`、
`components/mastery-bar/*`、`components/analysis-kpi/*`

`angular/src/app/learning/analysis/*`（教师端学情分析 4 Tab）
`angular/src/app/learning/knowledge-points/*`（知识点管理 + `knowledge-point-graph.component.*`）
`angular/src/app/student/analysis/*`（学生端智学分析 4 Tab）
`angular/src/app/student/practice/*`（专项练习）

### 5.3 既有文件改动

| 文件 | 改动 |
| --- | --- |
| `src/KnowledgeHub.Domain.Shared/KnowledgeHubPermissions.cs` | 未改（教师端复用 `KnowledgeHub.Courses` 策略） |
| `src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/{zh-Hans,en}.json` | 新增 `Menu:LearningAnalysis`、`Menu:KnowledgePointManagement` |
| `src/KnowledgeHub.Domain/Settings/KnowledgeHubSettings.cs` + `...SettingDefinitionProvider.cs` | 新增推荐权重 3 个设置项 |
| `src/KnowledgeHub.EntityFrameworkCore/.../KnowledgeHubDbContext.cs` | 注册 16 个 DbSet + `ConfigureLearningAnalytics()` |
| `src/KnowledgeHub.HttpApi.Host/KnowledgeHubHttpApiHostModule.cs` | 注册 Hangfire 队列 + 夜间 RecurringJob |
| `src/KnowledgeHub.HttpApi.Host/appsettings.json` | 新增 `LearningAnalytics` 配置节、`Qwen:OcrModel` |
| `src/KnowledgeHub.Application/KnowledgeHub.Application.csproj` | 新增 `Microsoft.ML`、`Microsoft.ML.Recommender` |
| `src/KnowledgeHub.Application.Contracts/Courses/Dtos/CourseDtos.cs` | `PagedCourseRequestDto.TeacherId`（新增字段） |
| `src/KnowledgeHub.Application/Courses/CourseAppService.cs` | `GetListAsync` 支持按 `TeacherId` 过滤 |
| `angular/src/app/app.routes.ts` | `/learning/analysis`、`/learning/knowledge-points` |
| `angular/src/app/route.provider.ts` | 菜单「学情分析」「知识点管理」 |
| `angular/src/app/student/student.routes.ts` | `/student/analysis`、`/student/practice` |
| `angular/src/app/student/layout/student-layout.component.ts` | 学生端侧边栏「智学分析」 |
| `.gitignore` | 试卷图片与归档报告本地目录 |
| `progress.md` | 8 次开发记录 |

### 5.4 文档

`docs/LEARNING_ANALYTICS_PLAN.md`（方案 + 阶段 A~N 进度 + 踩坑）、`docs/LEARNING_ANALYTICS_DEMO.md`（示例数据与操作）、本文件。

---

## 6. 接口清单（前缀 `/api/app`）

| 分组 | 端点 |
| --- | --- |
| 智学分析 `learning-analytics` | `GET /student-dashboard`、`GET /paper-overview`、`GET /paper-batch/{id}`、`POST /upload-paper-images/{id}`、`GET /paper-upload-images/{id}`、`GET /ocr-capabilities`、`POST /run-paper-ocr/{id}`、`GET /paper-scripts/{id}`、`POST /confirm-paper-script/{id}`、`POST /push-reports/{id}`、`GET /class-weakness/{id}`、`GET /teaching-insight/{id}`、`GET /course-link/{courseId}`、`POST /save-course-link`、`DELETE /course-link/{id}`、`POST /save-prerequisite`、`DELETE /prerequisite/{id}`、`POST /extract-knowledge-points/{courseId}`、`POST /train-mastery-model`、`POST /mine-prerequisites`、`POST /push-pre-course-reviews`、`GET/POST /threshold...`、`POST /clear-threshold`、`GET /student-report-preview/{id}`、`POST /export-docx-report/{id}`、`POST /export-student-reports/{id}`、`POST /archive-report/{id}`、`GET /report-files/{id}`、`POST /download-report-file/{id}`、`DELETE /report-file/{id}`、`POST /evaluate-recommendation`、`GET /recommendation-weights`、`POST /apply-recommendation-weights`、`POST /apply-best-recommendation-weights` |
| 知识点 `knowledge-point` | `GET /`、`GET/PUT/DELETE /{id}`、`POST /{id}/confirm`、`POST /merge`、`POST /split`、`GET /exercise-links/{id}`、`POST/PUT /exercise-link`、`DELETE /exercise-link/{id}`、`GET /graph/{courseId}`、`GET /merge-suggestions/{courseId}`、`POST /rebuild-embeddings/{courseId}`、`GET /vector-status` |
| 专项练习 `practice` | `GET /set/{courseId}`、`POST /submit` |

---

## 7. 验证记录

**最终冒烟（2026-10-20，qidi 租户，14/14 PASS）**

| 检查项 | 结果 |
| --- | --- |
| 教师「我的课程」过滤 | 2 门（vs 全部 7 门） |
| OCR 能力探测 | image=true，pdfDirect=false（默认关闭） |
| 试卷批次 | 1 批次 / 2 份 / 均分 44.5 |
| 知识点（章节初始化） | 4 个 |
| 班级薄弱点 | 4 知识点 / 重点关注 2 人 |
| 教学诊断 | 4 章，未达标 4 |
| 课程前后关联 | 路径 2 步 |
| 知识点图谱 | 4 节点 |
| 推荐权重读取 | 内容 0.5 / 协同 0.3 / 难度 0.2（config） |
| Word 报告导出 | 6193 bytes，含「班级学情分析与教学诊断报告」「教学诊断与改进建议」 |
| 学生端总览 | 33 分 / 排名 2/2 / 薄弱 4 个 |
| 学习路径与复习包 | 2 步路径 + 4 项复习包 |
| 推荐专项练习 | 5 组 |
| 专项练习题集 | 「IP 地址与子网划分」3 题 |

**过程性关键验证（详见 PLAN 各阶段）**：真实试卷图片 OCR（学号/6 题得分/总分 29 与图面一致）、多页 PDF 合并为 1 份、校对→掌握度→班级薄弱点→推送→教学诊断、知识点抽取与语义归一化（18/18 向量）、合并提示 0.876、pgvector 状态 18/18、权重 0.5→0.6 热生效、离线评估（0.500 → 纯内容 0.750）、OSS 图片 URL 直传 + OCR、PDF 直传（置信度 0.6 强制校对）。

---

## 8. 配置项（`appsettings.json` → `LearningAnalytics`）

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `OcrProvider` | `qwen` | `qwen` / `mock`（未配置 Key 自动降级 mock） |
| `OcrModel` / `PdfOcrModel` | `qwen-vl-ocr` / `qwen3.5-ocr` | 图片 / PDF 模型 |
| `PdfDirectEnabled` | `false` | PDF 直传开关（质量低于图片路径，默认关） |
| `OcrMinPixels` / `OcrMaxPixels` / `OcrTimeoutSeconds` | 3072 / 8388608 / 120 | 图片规格与超时 |
| `MaxImageBytes` / `UploadRoot` / `ReportRoot` | 10MB / 空 / 空 | 上传与归档落盘 |
| `KpExtractionEnabled` / `KpExtractionModel` / `KpExtractionMaxExercises` | true / `qwen-plus` / 200 | 知识点抽取 |
| `KpAutoMergeThreshold` / `KpReviewThreshold` | 0.88 / 0.75 | 语义归并阈值 |
| `EmbeddingModel` / `EmbeddingDimension` / `EmbeddingBatchSize` | `text-embedding-v4` / 1024 / 10 | 向量生成（DashScope 批量上限 10） |
| `VectorDimension` | 1024 | pgvector 列维度（须与向量一致） |
| `MatrixFactorizationEnabled` / `MfRank` / `MfMinSamples` | true / 16 / 30 | ML.NET |
| `NightlyTrainEnabled` / `PrerequisiteMiningEnabled` / `MiningMinSamples` / `MiningMinCorrelation` | true / true / 30 / 0.3 | 夜间任务 |
| `PreCourseReviewPushEnabled` / `PreCourseReviewMaxPerRun` | false / 200 | 开课前自动推送（默认手动） |
| `RecWeights:{Content,Collab,Fit}` | 0.5 / 0.3 / 0.2 | 推荐权重（可被 Setting 覆盖） |
| `EvalMinAnswers` / `EvalTestSize` / `EvalMaxStudents` / `EvalWeakRatio` | 8 / 3 / 200 / 0.6 | 离线评估 |
| `DemoTenantName` | `qidi` | 示例数据播种租户 |

设置项（可在 ABP 设置页/接口写入，保存即生效）：`KnowledgeHub.LearningAnalytics.RecWeight{Content,Collab,Fit}`。

---

## 9. 待办与优化建议

### P1（建议优先）

1. **PDF 直传调优**：功能已通（`PdfDirectEnabled` 默认 false）。需用真实扫描件复测，评估后决定是否默认开启；
   前端补「本次识别来自 PDF 直传，请重点校对」显式提示。
2. **班级维度**：当前以「课程 + 试卷批次」近似班级。若要按行政班/教学班统计（班级排名、班级对比），
   需引入 `Class`/`Enrollment` 实体与课程-班级映射。
3. **掌握度重算异步化**：现在确认试卷/提交练习时同步重算；整班确认或大批量练习后应改 Hangfire 异步 + 进度提示。

### P2（完善度）

4. **PDF 版报告**（当前 Word/Excel）：建议「HTML 模板 → 无头浏览器转 PDF」，避开中文字体嵌入问题。
5. **图谱交互增强**：节点拖拽微调、按章节泳道分组、导出 PNG（现为只读分层布局）。
6. **知识点级判定线批量维护**：目前逐个知识点配置，缺 Excel 导入/导出。
7. **跨课程归一化闭环**：已能给出跨课程合并建议，缺「并入哪门课的标准知识点」的归属规则与确认流程。
8. **细粒度权限**：教师端目前挂在 `KnowledgeHub.Courses` 策略，计划中的
   `KnowledgeHub.LearningAnalytics(.Manage)` 未落地（学生端隔离已通过 `[Authorize]` + 当前用户过滤）。
9. **线上推荐效果监控**：离线评估已有，缺「推荐 → 实际作答」的线上命中率看板与告警。
10. **报告留档 OSS 生命周期**：归档文件已可 OSS 存储，但缺自动清理策略（按批次/保留期）与配额统计。

### 技术债

11. **学生个人报告缓存**：`AnalysisReport` 未做「事实哈希缓存」，重复推送会重复调用模型（当前推送限 8 人走 LLM）。
12. **示例数据与真实数据隔离**：示例课程/学生与真实数据同库（同一租户），生产环境需关闭播种器
    （`LearningAnalytics:DemoTenantName` 指向不存在的租户，或直接移除 seeder 注册）。
13. **前端 Mock 回退**：`learning-analytics.service.ts` 在接口不可用时回退示例数据并展示标记，
    生产环境建议改为显式报错（避免误以为是真实数据）。
14. **OCR 供应商可插拔**：接口已抽象（`IPaperOcrRecognizer`），但只实现 Qwen 与 Mock；
    若要接其他 OCR（如阿里云文档智能），需新增实现并在 `PaperOcrRecognizerResolver` 注册。

---

## 10. 使用指引（本地）

```bash
# 1) 启动（API + Angular）
./dev.sh start

# 2) 首次或重置示例数据（qidi 租户）
dotnet run --project src/KnowledgeHub.DbMigrator

# 3) 账号（租户 qidi）
#   教师：qa-teacher / 1q2w3E*
#   学生：demo_student（张晨）/ 1q2w3E*、demo_student2（李思雨）/ 1q2w3E*

# 4) 教师：专业和课程管理 → 学情分析 / 知识点管理
#    学生：智学分析 → 专项练习
```

数据重置 SQL 见 `docs/LEARNING_ANALYTICS_DEMO.md` 第 4.2 节。

<!-- 生成时间：2026-10-20 -->
