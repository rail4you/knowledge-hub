# 智学分析（Learning Analytics）开发方案与进度

> 交付总结（功能/文件/实现摘要/待办）：`docs/LEARNING_ANALYTICS_SUMMARY.md`

> 目标：把「纸质试卷 → 成绩分析 → 学生薄弱点 → 教师教学诊断 → 课程联动衔接复习」整条链路落地到 KnowledgeHub。
> 原型：`智学分析 · 学生端原型.html` / `智学分析 · 教师端原型.html`
> 后端方案：`薄弱知识点分析 · .NET + ABP 后端方案.md`
> 状态：进行中（见文末进度表）

---

## 0. 范围与取舍

| 需求 | 本期做法 |
| --- | --- |
| ① 老师上传纸质试卷 → 分析成绩 → 学生薄弱点 → 推送学生/老师 | 上传 + **Mock OCR**（不接真实 OCR），生成「试卷题目 → 习题」映射并写入作答记录，统一算掌握度，生成报告与站内消息 |
| ② 由学生成绩反推教师教学成果 | 章节/知识点达成度 + 规则诊断（讲解不足 / 两极分化 / 达标）+ LLM 写建议 |
| ③ 课程联动（前置课程薄弱点复习包） | `CourseLink`（前置 → 后续课程关联）+ `KpPrerequisite`（跨课程知识点先修）+ 衔接复习包 |
| 真实数据对接 | 习题、课程、章节、学生作答记录全部走**应用内真实数据**；不另起一套题库 |
| OCR | **只做 Mock**：上传后模拟识别（学号/总分/每题得分），带置信度，支持人工校对 |
| 模型 | 复用应用内 `QwenClient`（`IChatClient`）；LLM 只负责「把事实写成文字」，数字全部代码算 |
| 协同过滤/矩阵分解 | 本期先用「规则 + 内容相似」实现推荐，`MasteryPredictor` 预留接口，ML.NET 后置 |

**三阶段交付**

1. **阶段 A（前端原型版）**：按原型风格做出学生端 / 教师端页面，用 Mock 服务层驱动，视觉与交互先定稿。
2. **阶段 B（后端真实化）**：领域模型 + EF + 迁移 + 应用服务 + Mock OCR，前端把 Mock 服务层替换为真实代理。
3. **阶段 C（智能与联动）**：LLM 报告、推送通知、课程联动复习包、后台任务与夜间挖掘。

---

## 1. 领域模型（新模块 `LearningAnalytics`）

命名空间：`KnowledgeHub.LearningAnalytics`，表前缀沿用 `KnowledgeHubConsts.DbTablePrefix`。

### 1.1 知识点与映射

| 实体 | 关键字段 | 说明 |
| --- | --- | --- |
| `KnowledgePoint` | `CourseId`、`Name`、`CanonicalId`、`SyllabusOrder`、`Embedding`(float[])、`Status` | 课程内知识点；归一化后 `CanonicalId` 指向标准点 |
| `ExerciseKnowledgePoint` | `ExerciseId`+`KnowledgePointId`(复合键)、`Weight`、`Source` | 习题↔知识点，权重和=1 |

### 1.2 纸质试卷

| 实体 | 关键字段 | 说明 |
| --- | --- | --- |
| `PaperBatch` | `CourseId`、`Title`、`ExamDate`、`Status`、`TotalScripts`、`AverageScore`、`PushedCount` | 一次上传批次（如「2023秋 期末考试」） |
| `PaperScript` | `BatchId`、`StudentId?`、`StudentNumber`、`StudentName`、`TotalScore`、`OcrConfidence`、`ConfirmStatus` | 单份试卷，OCR 结果需人工确认 |
| `PaperItemScore` | `ScriptId`、`QuestionNo`、`ExerciseId?`、`MatchStatus`、`Score`、`FullScore`、`KpIds`(json) | 每题得分，映射到应用内真实习题 |

### 1.3 作答与掌握度

`AnswerRecord` 统一「纸质试卷 / 课堂练习」作答；在线作答沿用现有 `StudentExerciseRecord`，计算时合并。

| 实体 | 关键字段 |
| --- | --- |
| `AnswerRecord` | `StudentId`、`CourseId`、`ExerciseId`、`Score`、`FullScore`、`Source`、`AnsweredAt`、`PaperScriptId?` |
| `StudentKpMastery` | `StudentId`+`KnowledgePointId`(复合键)、`Mastery`、`Evidence`、`UpdatedAt` |
| `MasteryThreshold` | `CourseId`、`KnowledgePointId?`、`WeakLine`(默认 .6)、`OkLine`(默认 .75) |

### 1.4 课程联动

| 实体 | 关键字段 |
| --- | --- |
| `KpPrerequisite` | `FromKpId`、`ToKpId`、`Strength`、`Source`(Manual/Mined/Llm)、`Status`(Proposed/Confirmed) |
| `CourseLink` | `PredecessorCourseId`、`SuccessorCourseId`、`Status` —— **只表达前后置关联，不含排课时间** |
| `ReviewPackage` | `StudentId`、`UpcomingCourseId`、`Strategy`、`Status`、`GeneratedAt` |
| `ReviewPackageItem` | `PackageId`、`KpId`、`SortOrder`、`Mastery`、`Level`、`IsCompleted` |

### 1.5 报告与推送

| 实体 | 关键字段 |
| --- | --- |
| `AnalysisReport` | `Scope`(Student/Class/Teaching)、`StudentId?`、`CourseId`、`PaperBatchId?`、`FactsJson`、`ContentJson`、`FactsHash`、`GeneratedAt`、`PushedAt` |
| `StudentNotification` | `StudentId`、`Type`、`Title`、`Content`、`CourseId?`、`IsRead`、`CreatedAt` |
| `TeachingInsight` | `CourseId`、`PaperBatchId?`、`ChapterId`、`AvgRate`、`StdDev`、`Diagnosis`、`Advice`、`SampleSize` |

---

## 2. 算法（代码算事实）

- **掌握度**：Beta 先验平滑 + 时间半衰期衰减。
  `a=A0+Σ w·ratio`，`b=B0+Σ w·(1−ratio)`，`w = weight · 0.5^(Δt/120d)`，`Mastery=a/(a+b)`，`Evidence=Σw`。
- **判定线**：知识点级 > 课程级 > 默认（`0.60 / 0.75`）。`Weak < 0.6 ≤ Fair < 0.75 ≤ Ok`。
- **教学诊断**（规则先行，LLM 只写建议）：
  - `Avg ≥ Ok` → 达标
  - `TopQuartile < Ok && Avg < Weak` → 讲解不足（TeachingGap）
  - `StdDev > 0.25` → 两极分化（Polarized）
  - 其余 `Avg ≥ Weak` → 一般
- **推荐（本期）**：内容相似（薄弱向量 × 习题知识点向量）× 难度适配（预测答对概率趋近 0.65）；协同路 `Wc/Wk/Wf` 可配置，无邻居数据时退化为纯内容。
- **衔接复习包**：沿 `Confirmed` 先修边向前追溯 1–2 跳 → 取薄弱/待巩固 → 拓扑排序（同层按影响面大、掌握度低优先）。

---

## 3. 应用服务（ABP 约定，自动生成 API）

> **实现说明**：为让前端只用**一个**接口前缀（`/api/app/learning-analytics`），本期把下表的职责全部收敛到
> `LearningAnalyticsAppService` 一个应用服务（内部拆成 `KnowledgePointIndexer` / `MasteryRecalculationService` /
> `ThresholdResolver` / `PaperOcrSimulator` / `RecommendationEngine` / `AnalysisReportWriter` 等支撑服务）。
> 下表保留职责划分，便于后续拆分。

| 应用服务 | 职责 | 主要端点 |
| --- | --- | --- |
| `KnowledgeAppService` | 知识点列表、待确认归并、合并/拆分 | `GET /knowledge/by-course/{courseId}`、`POST /knowledge/merge` |
| `ThresholdAppService` | 课程级/知识点级判定线 | `GET/PUT /threshold/by-course/{courseId}` |
| `PaperImportAppService` | 批次上传、Mock OCR、题目匹配、校对确认、写作答 | `POST /paper/batch`、`POST /paper/ocr/{batchId}`、`GET /paper/batch/{id}/scripts`、`POST /paper/confirm` |
| `WeaknessAppService` | 学生薄弱点、班级薄弱点 | `GET /weakness/my`、`GET /weakness/class/{batchId}` |
| `TeachingInsightAppService` | 章节诊断 | `GET /teaching-insight/{batchId}` |
| `PrerequisiteAppService` | 先修关系、课程联动配置 | `GET/POST /prerequisite/list`、`GET/POST /course-link` |
| `RecommendationAppService` | 推荐习题、衔接复习包 | `POST /recommendation/exercises`、`GET /recommendation/review-package/{courseId}` |
| `ReportAppService` | 报告生成/查看/推送 | `GET /report/my`、`POST /report/push` |
| `NotificationAppService` | 站内消息 | `GET /notification/my`、`POST /notification/read` |

**权限**：`KnowledgeHub.LearningAnalytics`（查看）、`.Manage`（上传/配置/推送）。
学生仅能读自己的报告；教师仅能看自己课程/班级。

---

## 4. 前端页面

| 端 | 路由 | 内容 |
| --- | --- | --- |
| 学生端 | `/student/analysis` | Tab①我的薄弱点（成绩卡 + 知识点掌握条 + 失分归因）②学习路径（课程链路 + 衔接复习进度）③消息（推送通知）④专项练习（来自课程习题库） |
| 教师端 | `/learning/analysis` | Tab①纸质试卷分析（上传、OCR 步骤、KPI、批次表）②班级薄弱点（掌握率条 + 重点关注学生 + 一键推送）③教学成果分析（KPI + 章节达成度 + 诊断建议）④课程联动设置（课程链路 + 前置知识点映射） |

- 风格：沿用原型的信息结构，套用应用**墨青绿主题**（`student/styles/_student-design.scss`）与 LeptonX 管理端样式。
- 菜单：学生端侧边栏新增「智学分析」；教师端 `::Menu:CourseManagement` 组新增「学情分析」。
- Mock 服务层：`learning-analytics/services/learning-analytics.service.ts` 采用「**API 优先 + 失败回退示例数据**」：后端不可用时自动用 `learning-analytics-mock.ts` 填充并显示「示例数据」标记，便于原型预览；后端就绪后无需改代码。

---

## 5. 后台任务与推送

**本期实现方式（同步 + 按需，已可用）**

| 时机 | 行为 |
| --- | --- |
| 校对确认单份试卷 | 立即写 `AnswerRecord` 并重算该学生该课程的掌握度 |
| 老师「一键推送」 | 生成 `AnalysisReport` + `StudentNotification`（LLM 文字限 8 人，其余规则文本） |
| 打开学生端智学分析 | 按需计算薄弱点、生成/刷新衔接复习包（幂等 upsert） |
| 打开教师端班级薄弱点 / 教学成果 | 实时聚合（证据加权），教学建议走 LLM，失败回退规则 |

**后续（需持久化队列 / 开课日期）**

| 任务 | 触发 |
| --- | --- |
| `MasteryRecalcJob` | 作答记录创建 / 试卷确认后（异步化，避免请求变慢） |
| `KpExtractJob` | 习题新增/修改（LLM 抽取知识点 + 归一化） |
| `PaperAnalysisJob` | 批次确认后批量切片计算与报告预生成 |
| `PreCourseReviewPushService` | 已实现：为「已配置前置关联 + 已选后续课程」的学生生成衔接复习包并推送（按学生+课程幂等）；夜间任务与教师端「立即推送」共用 |
| `NightlyModelWorker` | 每晚：重训掌握度预测、挖掘先修候选（Proposed） |

## 6. 风险

- Mock OCR 阶段：识别数字是模拟的，UI 已明示「示例数据 / 待接真实 OCR」，避免误以为真实成绩。
- 归一化错误会污染矩阵 → 灰区一律人工确认，保留拆分（本期先用章节规则初始化，LLM 抽取后置）。
- 相关不等于因果 → 挖掘出的先修边必须老师确认才生效。
- 教师诊断仅作教学反思参考，不用于考核。

---

## 7. 验收步骤（本机）

1. 启动服务：`./dev.sh start`（API + Angular）。
2. 教师端：登录教师账号 → 侧边栏「专业和课程管理 → 学情分析」→「纸质试卷分析」。
3. 选择一门**有选课学生且有习题**的课程 → 填写批次名称 → 选择文件或「直接按批次录入」→「开始识别」。
4. 步骤条跑完后，批次状态为「待校对」；点「去校对」→ 修正低置信度/未匹配题目的得分 →「确认并入库」。
5. 切到「班级薄弱点」查看知识点掌握率与重点关注学生 →「一键推送个人薄弱点报告」。
6. 切到「教学成果分析」查看章节达成度与诊断建议（已接 LLM，未配置模型时自动回退规则文本）。
7. 切到「课程联动设置」→「添加映射」会基于前置课程自动建议一条先修关系（状态待确认）。
8. 学生端：用被推送的学生账号登录 → 侧边栏「智学分析」→ 查看薄弱点 / 学习路径（需配置 `CourseLink`）/ 消息（会看到成绩分析推送）/ 专项练习。

> 注意：示例课程需先在「课程列表 / 章节管理 / 习题管理」建好章节与习题，并完成选课，否则 OCR 无可映射数据。

---

## 8. 接口清单（已实现，ABP 约定自动生成）

**知识点管理** 前缀 `/api/app/knowledge-point`（`KnowledgePointAppService`）：

| 方法 | 路由 | 说明 |
| --- | --- | --- |
| GET | `/` | 知识点分页列表（课程 + 筛选） |
| GET/PUT/DELETE | `/{id}` | 详情 / 编辑 / 删除 |
| POST | `/{id}/confirm` | 确认灰区知识点 |
| POST | `/merge` | 合并知识点 |
| POST | `/split` | 拆分知识点 |
| GET | `/exercise-links/{knowledgePointId}` | 习题关联列表 |
| POST/PUT | `/exercise-link` | 新增 / 更新权重（同一 URL 不同动词） |
| DELETE | `/exercise-link/{linkId}` | 移除关联 |
| GET | `/graph/{courseId}` | 知识点图谱（节点 + 先修/合并边 + 拓扑层级） |
| GET | `/merge-suggestions/{courseId}?scope=` | 合并建议（含相似度与方法；scope=course/tenant） |
| GET | `/vector-status?courseId=` | 向量引擎状态（pgvector / memory、维度、已入库数） |
| POST | `/rebuild-embeddings/{courseId}` | 重建语义向量 |

**智学分析** 前缀 `/api/app/learning-analytics`，前端 `learning-analytics.service.ts` 直接对齐：

| 方法 | 路由 | 说明 |
| --- | --- | --- |
| GET | `/student-dashboard` | 学生端整页聚合（薄弱点 / 路径 / 复习包 / 消息 / 专项练习） |
| POST | `/mark-notification-read/{notificationId}` | 消息已读 |
| GET | `/paper-overview` | 试卷批次概览 |
| POST | `/paper-batch` | 创建批次 |
| GET | `/paper-batch/{batchId}` | 单批次详情（含图片识别进度计数，前端轮询用） |
| POST | `/upload-paper-images/{batchId}` | 上传试卷图片（Base64 JSON；支持 `pageIndex` + `mergeGroup` 表达多页同一份试卷） |
| GET | `/paper-upload-images/{batchId}` | 图片识别状态列表（含失败原因、是否 Mock） |
| POST | `/run-paper-ocr/{batchId}` | 执行 OCR（Qwen-VL-OCR，未配置 Key 回退 Mock）并生成待校对试卷 |
| GET | `/paper-scripts/{batchId}` | 校对明细（题目↔真实习题↔知识点） |
| POST | `/confirm-paper-script/{scriptId}` | 校对确认 → 写 `AnswerRecord` → 重算掌握度 |
| POST | `/push-reports/{batchId}` | 生成报告 + 站内消息推送 |
| POST | `/export-docx-report/{batchId}?type=` | 导出 Word 文字报告（class 班级学情+教学诊断 / students 学生汇总） |
| POST | `/export-student-reports/{batchId}` | 导出学生薄弱点明细（Excel） |
| POST | `/archive-report/{batchId}?type=` | 生成并归档报告文件 |
| GET | `/report-files/{batchId}` | 归档文件列表 |
| POST | `/download-report-file/{fileId}` | 下载归档文件（累加下载次数） |
| DELETE | `/report-file/{fileId}` | 删除归档文件 |
| POST | `/evaluate-recommendation?courseId=&topK=` | 推荐效果离线评估（权重对比） |
| GET | `/student-report-preview/{batchId}?student=` | 预览推送给学生的报告内容 |
| GET | `/class-weakness/{batchId}` | 班级薄弱点 + 重点关注学生 |
| GET | `/teaching-insight/{batchId}` | 章节达成度 + 规则诊断 + LLM 建议 |
| GET | `/course-link/{courseId}` | 课程路径 + 前置知识点映射 |
| POST | `/save-prerequisite` | 新增/更新先修映射 |
| DELETE | `/prerequisite/{prerequisiteId}` | 删除映射（同组） |
| POST | `/ensure-course-knowledge-points/{courseId}` | 初始化课程知识点 |
| GET | `/knowledge-points/{courseId}` | 知识点列表 |
| GET | `/threshold/{courseId}?knowledgePoint=` | 判定线读取（课程级或知识点级生效值） |
| POST | `/set-threshold` | 配置判定线（带 `knowledgePointId` 即写知识点级覆盖） |
| POST | `/clear-threshold` | 清除知识点级覆盖，恢复课程级 |
| POST | `/extract-knowledge-points/{courseId}?overwrite=` | LLM 抽取习题知识点（入队） |
| POST | `/train-mastery-model` | 重训 ML.NET 掌握度模型 |
| POST | `/mine-prerequisites?courseId=` | 挖掘先修知识点候选（Proposed） |
| POST | `/save-course-link` | 保存课程前后关系 |
| DELETE | `/course-link/{courseLinkId}` | 删除课程前后关系 |
| POST | `/push-pre-course-reviews` | 立即为「已选后续课程」的学生生成并推送衔接复习包（幂等） |

---

## 9. 进度

### 阶段 A：前端原型版（UI 优先）

- [x] A1 数据模型 `models/learning-analytics.models.ts`
- [x] A2 服务层 `services/learning-analytics.service.ts`（API 优先，失败回退示例数据并显示「示例数据」标记）
- [x] A3 通用组件：`mastery-bar`（含教学目标标尺）、`analysis-kpi`
- [x] A4 学生端 `/student/analysis`（我的薄弱点 / 学习路径 / 消息 / 专项练习 4 Tab，含空态引导）
- [x] A5 教师端 `/learning/analysis`（纸质试卷分析 / 班级薄弱点 / 教学成果分析 / 课程联动设置 4 Tab，含校对面板）
- [x] A6 路由 + 菜单（学生端侧边栏「智学分析」；教师端「专业和课程管理」组「学情分析」）+ 本地化
- [x] A7 `ng build` 通过

### 阶段 B：后端真实化

- [x] B1 领域实体 15 张表（知识点 / 映射 / 先修 / 课程联动 / 判定线 / 掌握度 / 试卷批次·试卷·单题 / 作答记录 / 报告 / 消息 / 复习包 / 教学诊断）
- [x] B2 EF 配置 + 迁移 `20261019112904_AddLearningAnalytics`（已在本机 5433 执行成功）
- [x] B3 `MasteryCalculator`（Beta 平滑 + 120 天半衰期）+ `ThresholdResolver`（知识点 > 课程 > 默认 0.60/0.75）
- [x] B4 `KnowledgePointIndexer`：按叶子章节初始化知识点，习题按章节树（自身/祖先/子孙）映射，无法归属落到「课程综合」
- [x] B5 `PaperOcrSimulator`（Mock）：按真实选课学生 + 真实习题生成确定性得分，含低置信度/未匹配样本
- [x] B6 `MasteryRecalculationService`：合并「在线作答 + 纸质/课堂作答」，支持单学生与班级批量
- [x] B7 `GetClassWeaknessAsync`：证据加权聚合 + 重点关注学生 + 推送状态
- [x] B8 `GetTeachingInsightAsync`：按章节真实得分率、标准差、前 25%、规则诊断 + LLM 建议（失败回退规则）
- [x] B9 `RecommendationEngine`：专项练习（内容 + 难度适配 + 去重）与衔接复习包（先修边追溯 / 前置课程退化 / 拓扑排序）
- [x] B10 报告 + 站内消息：推送时生成 `AnalysisReport` 与 `StudentNotification`
- [x] B11 本地化菜单键 + 路由接入
- [x] B12 `dotnet build` 通过 + 迁移执行成功

### 阶段 C：联调与智能

- [x] C1 前端服务层对接真实接口（URL 与 ABP 约定一致，已在 `api-definition` 中逐一核对）
- [x] C2 端到端验证：登录（租户教师/学生 token）→ 建批次 → Mock OCR → 校对 → 掌握度重算 → 班级薄弱点 → 教学诊断（LLM 建议生效）→ 推送 → 学生端聚合
- [x] C3 LLM 报告文字（事实 JSON → 文字，校验知识点名必须来自事实，失败回退规则）
- [x] C4 站内消息推送 + 已读
- [x] C5 后台任务：Hangfire 队列（`PaperOcrProcessingJob` / `KpGenerationJob` / `MasteryModelTrainJob` / `PrerequisiteMiningJob`）+ 夜间 RecurringJob（`learning-analytics-nightly`，每天 03:00：重训模型 + 先修挖掘 + 可选开课前衔接推送）
- [x] C6 端到端验证记录与文档

### 阶段 D：真实 OCR + AI 任务 + ML.NET（进行中）

**D0 选型结论（Qwen OCR）**

| 项 | 结论 |
| --- | --- |
| 模型 | `qwen-vl-ocr`（稳定）/ `qwen-vl-ocr-2025-11-20` / `qwen3.5-ocr`（最新，支持 PDF） |
| 接口 | OpenAI 兼容 `POST {Qwen:BaseUrl}/chat/completions`，`messages[0].content=[{type:"image_url",image_url:{url:<dataURL或URL>},min_pixels,max_pixels},{type:"text",text:<prompt>}]` |
| 图片 | 支持 URL 或 `data:image/jpeg;base64,...`；建议 `min_pixels=3072`、`max_pixels=8388608` |
| 能力 | 通用 OCR / 文档解析 / 公式识别 / 表格解析 / 高精识别（带坐标）/ 关键信息抽取 |
| 限制 | 不支持自定义 System Message（指令写在 user message）；`max_tokens` 默认 4096；OpenAI 兼容通道下内置 task 需自己写 Prompt |
| 本方案用法 | **自定义 Prompt 直接要结构化 JSON**：`{studentNumber, studentName, totalScore, questions:[{no, score, fullScore, rawText}]}`，比 DashScope 专有 `ocr_options` 更可控，且不绑定地域/业务空间 |

**D1 真实 OCR 接入**
- [x] `IPaperOcrRecognizer` 抽象 + `QwenPaperOcrRecognizer`（OpenAI 兼容、base64、复用全局 Qwen 限流）+ `MockPaperOcrRecognizer`
- [x] 试卷图片落盘（`PaperUploadImage` + 本地存储），支持一次上传多张（一张 = 一份学生试卷）
- [x] `PaperIngestionService`：OCR 结果 → 匹配应用内真实习题（题号/分值/顺序三重启发式）→ 写 `PaperScript`/`PaperItemScore`，低置信度标 `Low/None` 待校对
- [x] OCR 走 Hangfire 队列（`PaperOcrProcessingJob`），前端轮询批次状态
- [x] 前端：真实文件上传（FileReader → base64）、OCR 进度轮询、失败重试

**D2 知识点抽取（LLM）**
- [x] `KnowledgePointExtractor`：习题题干 → 1~3 个知识点 + 权重，提示词带课程已有知识点名以减少同义不同词
- [x] 归一化：精确命中复用 → 包含/高相似标记 `PendingReview` → 否则新建
- [x] `KpGenerationJob`（按课程批量：章节 → 知识点、习题 → 知识点关联、重建语义索引），教师端「AI 全量生成知识点」一键触发

**D3 ML.NET 协同过滤**
- [x] `Microsoft.ML` 5.0.1 + `Microsoft.ML.Recommender` 0.23.1（netstandard2.0，兼容 net10）
- [x] `MasteryModelTrainer`：学生 × 知识点矩阵分解（rank 16），`MasteryPredictorHolder` 单例热替换
- [x] 数据不足 / 原生库不可用时回退 `BaselinePredictor`（全局均值 + 学生均值 + 知识点均值）
- [x] 推荐三路加权：内容 0.5 + 协同 0.3 + 难度适配 0.2（无邻居数据时退化为纯内容）
- [x] 夜间重训（Hangfire recurring job）+ 教师端手动「重训模型」

**D1 验证记录（本机实测）**

| 用例 | 结果 |
| --- | --- |
| 生成一张含「学号 zmq / 姓名 张梦倩 / 6 题手写得分」的试卷图（1000×1400 PNG，136KB） | `qwen-vl-ocr` 正确读出学号、姓名与每题得分 |
| 学生匹配 | OCR 的学号 → 匹配到应用内真实用户（`zmq`），试卷落到该学生 |
| 得分还原 | 实测 3/5、4/5、5/8、3/8、6/10、8/12，总分 29，与图片完全一致 |
| 题目→习题映射 | 章节树（自身/祖先/子孙）+ 题干片段 + 满分序列滑窗对齐；本测试课程题库分值为占位数据（大量 1 分）故标 `待复核`，真实题库可自动命中 |
| 校对→掌握度 | 确认后写 `AnswerRecord` → 班级薄弱点（3 个知识点有证据）→ 重点关注学生（张梦倩 29 分）→ 一键推送（1 条） |
| 教学诊断 | 章节得分率 + LLM 建议正常（good/gap 分类正确） |
| 知识点抽取 | 10 道习题 → 处理 6 道、新增 8 条映射，全部归一化复用已有章节知识点（无重复造词） |
| ML.NET | 样本不足（2 < 30）自动回退基线预测器，接口不报错 |

**踩坑与修复（已入库代码）**

1. `PaperOcrRecognizerResolver` 未实现 `ITransientDependency`，Hangfire 执行时 DI 解析失败 → 已注册。
2. 模型会把「（8分）」这类满分标注写进 `score`，甚至把满分/得分写反 → 解析层做了三件事：字符串数字宽容解析、从 `rawText` 的「得分：X分」「（Y分）」校正、`score > fullScore` 时自动交换。
3. 章节初始化产生的 Seed 映射会让「增量抽取」永远跳过 → 改为：增量模式跳过已有 **Llm** 映射的题；抽取前清掉该题的 Seed/Llm 映射（手工映射永不覆盖）。
4. 上传接口用 `PaperUploadImage` 新表，需先跑迁移；图片落盘到 `App_Data/paper-uploads`（已 gitignore）。

**D4 先修挖掘 + 课程前后关系**
- [x] `PrerequisiteMiner`：共同修读学生上，前置知识点掌握度 vs 后续知识点掌握度相关（N≥30、R≥0.3）→ `KpPrerequisite(Proposed)`
- [x] `SaveCourseLinkAsync`：老师配置课程前后关系（含提前周数），教师端可增删
- [x] 夜间任务可自动挖掘（默认关闭，需老师确认后生效）

### 阶段 E：PDF 试卷 / 判定线 / 衔接复习推送（已完成，排课字段已移除）

**E1 PDF 试卷支持（多页合并）**
- 前端：浏览器内用 `pdfjs-dist`（复用已有 `assets/pdfjs` worker）逐页渲染成 JPEG 后上传，
  避免服务端引入 PDF 渲染原生依赖；支持「每页一份试卷」与「多页合并为一份试卷」两种模式。
- 后端：`PaperUploadImage.PageIndex` + `MergeGroup`；`PaperOcrResultMerger` 合并多页结果
  （学生信息取首个非空、题号冲突加页码前缀、置信度取均值）。
- OCR 任务按合并分组处理：同一份多页试卷 → **一份 `PaperScript`**，所有页面共享同一 `ScriptId`。

**E2 判定线（教学目标）可视化配置**
- 教师端「班级薄弱点」页内直接改「薄弱线 / 达标线」（0~100%），走后端 `/threshold/{courseId}` + `/set-threshold`；
  默认课程级配置优先于系统默认（0.60 / 0.75），保存后自动刷新分析与诊断。

**E3 衔接复习推送（打通需求③）**
- `CourseLink` 只维护「前置课程 → 后续课程」关联，**不含开课日期 / 提前周数等排课信息**（按产品要求移除）。
- `PreCourseReviewPushService`：对「已配置前置关联 + 已选后续课程」的学生生成衔接复习包并推送一次
  （同一学生 + 课程幂等去重），由夜间 RecurringJob 与教师端「立即推送衔接复习包」共用。
- 复习包不再把「完全没作答证据」的整门课塞进来：实测数据优先，无数据时用 ML 预测取最薄弱 ≤5 个知识点。

**E 验证记录**

| 用例 | 结果 |
| --- | --- |
| 两页试卷（同 `mergeGroup`）上传 → OCR | 合并为 **1 份**试卷，学生信息取自第 1 页，两页题目正确拼接，两张图片都指向同一 `ScriptId` |
| 多页题的得分还原 | 3/5、4/5、5/8、3/8（合计 15）与图面一致；其中 3 题自动判定「已匹配」 |
| 判定线读写 | 默认 0.60/0.75 → 改为 0.65/0.80 → 读回一致；恢复默认后 `isDefault` 正确 |
| 课程前后关系 | 只保存前置/后续课程，接口返回字段仅 5 个（无排课字段） |
| 立即推送衔接复习包 | 首次推送 **2** 名学生（各 2/5 个知识点），第二次调用 **0**（幂等）；无排课字段也能正常推送 |
| 学生消息 | 收到「【课前提醒】医疗课程 即将开课」并带上最需巩固的知识点名称 |
| 数据迁移 | `RemoveCourseLinkSchedule` 删除 `LeadWeeks` / `PlannedStartDate` 两列（表结构已确认只剩关联字段） |

### 阶段 F：知识点管理（归一化闭环）+ 知识点关联（已完成）

**页面**：教师端「专业和课程管理 → 知识点管理」`/learning/knowledge-points`

| 能力 | 说明 |
| --- | --- |
| 列表 / 筛选 | 按课程查看，支持名称搜索、状态（全部 / 待确认 / 已确认）、来源（章节初始化 / AI 抽取 / 手工）过滤与分页 |
| 灰区确认 | `PendingReview` 一键确认为正式知识点（灰区行高亮、排前） |
| 编辑 | 改名、所属章节、大纲顺序、描述；改名后向量自动失效并提示重建 |
| 合并 | 习题关联、掌握度（按证据量加权）、先修关系全部迁移；同题关联权重相加后归一化；源知识点仅归档（`CanonicalId`）隐藏，可追溯 |
| 拆分 | 勾选部分习题拆到新建知识点（`Source=Manual`），并重新归一化同题权重 |
| 删除 | 同步清理习题关联 / 先修关系 / 掌握度（前端二次确认） |
| **习题关联** | 查看某知识点关联的习题（题干片段、章节、来源）、新增关联、改权重、移除；同一题多知识点权重自动归一化为 1 |
| 合并建议 | 相似度 ≥ 75% 的候选对（方向可反转、可忽略）；点击即合并 |
| 向量重建 | 批量为课程知识点生成语义向量 |

**向量归一化（`KnowledgeEmbeddingProvider`）**
- 优先级：项目既有 `EmbeddingService:BaseUrl`（与检索共用） → DashScope OpenAI 兼容 `/embeddings`（复用 Qwen 动态 Key 与全局限流） → 不可用则回退字符近似（Jaccard）。
- DashScope embeddings **单次批量上限 10**，已按 `LearningAnalytics:EmbeddingBatchSize`（默认 10）分片。
- LLM 抽取归一化升级为「语义优先」：余弦 ≥ 0.88 直接归并、0.75~0.88 新建但标 `PendingReview`、< 0.75 新建确认态；无向量时用字符近似同阈值兜底。
- 存储仍是 `KnowledgePoint.Embedding`（PostgreSQL `real[]`），万级以内进程内余弦足够；
  本机已确认 `pgvector 0.8.0` 可用，后续可直接加 `vector` 列 + 索引，无需改业务代码（`Compare()` 是唯一相似度入口）。

**F 验证记录（本机实测）**

| 用例 | 结果 |
| --- | --- |
| 重建向量 | `provider=qwen`，18/18 个知识点写入向量 |
| 合并建议 | 6 条，全部 `method=embedding`；如「平面构成的特点 ≈ 平面构成的基本概念」0.876、「线的形态 ≈ 点的形态」0.765 |
| 确认灰区 | `POST /{id}/confirm` → `status=0` |
| 合并 | 列表 18 → 17（源归档隐藏），返回迁移明细文案 |
| 习题关联 | 新增 0.6 → 归一化后 0.375；改 0.9 → 0.5902；移除后恢复原关联数 |
| 拆分 | 「平面构成的基本概念」拆出 2 道习题 → 新知识点 2 道，原件剩 4 道；测试数据已清理 |
| 删除 | 软删除（`IsDeleted=true`），列表不再出现 |

**踩坑与修复**

1. `IRepository<ExerciseKnowledgePoint>` 缺省键类型会被解析成分页仓储，`FindAsync(Guid)` 编译失败 → 统一写成 `IRepository<T, Guid>`。
2. 既有 `IEmbeddingService.GenerateBatchEmbeddingsAsync` 返回的是**扁平化**向量串（768×N），需按维度切片，已在适配层处理。
3. 合并建议的方法标记初版误用「是否需要补向量」的变量，全部显示为 `char`；修正为按实际分支返回 `embedding` / `char`。
4. DashScope embeddings 批量 > 10 直接 400（`batch size is invalid`）。

### 阶段 G：pgvector 落地（已完成）

**目标**：把知识点语义向量从「进程内余弦」升级为「数据库侧 ANN 检索」，支撑知识点量级上万后的跨课程归一化。

| 项 | 实现 |
| --- | --- |
| 迁移 | `AddPgvectorKnowledgePointEmbeddings`：尝试 `CREATE EXTENSION vector`（异常安全）→ 增加 `AppKnowledgePoints."EmbeddingVector" vector(1024)` → 从既有 `Embedding real[]` 回填（仅 1024 维）→ 建 HNSW 余弦索引 |
| 存储 | `KnowledgePointVectorStore`（`Application/LearningAnalytics`）：探测 pgvector 可用性与**列维度**（10 分钟缓存）→ 批量写入（`unnest(...)::vector`，200 条/批）→ ANN 检索（`<=> ` 余弦距离，`similarity = 1 - distance`）→ 状态 / 计数 / 清空 |
| 双写策略 | 保留既有 `Embedding real[]` 作为可移植副本，pgvector 列只做检索加速通道；写入时双写，因此**没有 pgvector 也能正常跑**（自动回退进程内余弦），迁移可安全回滚 |
| 归一化接入 | LLM 抽取（`KpGenerationJob`）新建知识点时同步写向量；全量生成末尾批量重建语义向量并写回 pgvector |
| 合并建议 | `GET /merge-suggestions/{courseId}?scope=course|tenant`：pgvector 可用时每个知识点做一次 ANN 检索（`scope=tenant` 即**跨课程**找同义知识点）；不可用时回退两两比较 |
| 可观测 | `GET /vector-status?courseId=` 返回 `engine / dimension / available / embeddedCount / knowledgePointCount`，前端工具栏展示「向量引擎：pgvector · 1024 维 · 已入库 n/N」徽标 |
| 安全阈值 | 向量维度与配置不一致时自动停用 pgvector 检索并给出原因（避免维度错配静默出错） |

**G 验证记录（本机实测）**

| 用例 | 结果 |
| --- | --- |
| 迁移执行 | `vector 0.8.0` 已安装；`EmbeddingVector` 列 `udt_name=vector`；HNSW 索引 `IX_AppKnowledgePoints_EmbeddingVector_Hnsw` 存在；**回填 18 条** |
| `vector-status` | `engine=pgvector, dimension=1024, available=true, embeddedCount=18/18` |
| 同课程建议（ANN） | 6 条，全部 `method=embedding`，最高「平面构成的特点 ≈ 平面构成的基本概念」0.876 |
| 跨课程建议（`scope=tenant`） | 正常返回（本机另一门课程无同义知识点，故无跨课程新增） |
| 回退路径 | 未探测到 pgvector / 维度不符 / 写入失败时自动降级为进程内余弦，业务不中断（日志仅告警） |
| 排课字段移除 | `AppCourseLinks` 表已无 `LeadWeeks` / `PlannedStartDate`；衔接推送不再依赖时间窗口 |

### 阶段 H：知识点图谱 + 知识点级判定线（已完成）

**H1 知识点图谱（先修关系 + 合并关系可视化）**

- 入口：知识点管理页右上角「列表 / 图谱」切换（同一个课程选择器，无需新增菜单）。
- 后端 `GET /api/app/knowledge-point/graph/{courseId}`：返回节点（含完整 `KnowledgePointDetail`，前端可直接复用编辑/合并/拆分/关联面板）、
  边（`prerequisite` 先修 / `merge` 已合并）、跨课程前置节点、待确认边数、已归档节点数；
  并按**最长路径**给每个节点算拓扑层级（`Level`，带环保护，脏数据不会死循环）。
- 前端 `app-knowledge-point-graph`：分层 SVG 布局（列=拓扑层级、行=层内按名称排序），
  三次贝塞尔连线；**实线=已生效先修、虚线=待确认先修、点线=已合并**；外部课程节点浅底虚线框；
  已归档（被合并）知识点单独放最末列；hover 高亮相邻边；支持缩放（40%~200%）与「显示待确认先修」开关；
  点击节点 → 打开该知识点的操作面板。
- 说明：不用 mind-elixir 等脑图库，是因为先修关系是 DAG（多入度），分层 SVG 语义更准，也避免再引入重量级依赖。

**H2 知识点级判定线（判定线真正生效到每个知识点）**

- `ThresholdResolver.ResolveAllAsync(courseId)` 一次查出「课程级默认 + 知识点级覆盖」，`CourseThresholds.Judge(kpId, mastery)` 按
  **知识点覆盖 > 课程级 > 系统默认（0.60 / 0.75）** 判定，避免逐知识点查库。
- 生效范围：学生端薄弱点、班级薄弱点、重点关注学生、专项练习推荐（weak 判定与文案里的目标线）全部改为按知识点判定线。
- `KnowledgeMasteryItemDto` 增加 `TargetLine` / `HasCustomThreshold`，前端掌握度条按知识点自己的目标线画标尺。
- `KnowledgePointDetailDto` 增加 `HasCustomThreshold` / `TargetLine` / `WeakLine`，列表里有覆盖的知识点显示「目标 x%」徽标。
- 接口：`GET /threshold/{courseId}?knowledgePoint=`（返回生效值 + `isOverride` + 课程级参考值）、`POST /set-threshold`（带 `knowledgePointId` 即写覆盖）、
  新增 `POST /clear-threshold`（清除覆盖，恢复课程级）。
- 界面：知识点管理页编辑面板内直接配置「薄弱线 / 达标线」，并显示「当前使用知识点级/课程级判定线」提示。

**H 验证记录（本机实测）**

| 用例 | 结果 |
| --- | --- |
| 图谱数据 | 18 个节点 / 1 条先修边；`externalNodeCount / proposedEdgeCount / mergedNodeCount` 均正确返回 |
| 拓扑分层 | 建边「点的概念 → 线的概念」后，前置层级 0 < 后续层级 1 ✅ |
| 合并边 | 合并「平面构成的特点 → 平面构成的基本概念」后 `mergedNodeCount=1`，图谱出现 `type=merge` 的边（验证后已撤销） |
| 知识点级判定线 | 设定 0.7/0.9 → `isOverride=true`；列表该行 `hasCustomThreshold=true, targetLine=0.9, weakLine=0.7` |
| 清除覆盖 | `clear-threshold` → 回到系统默认 0.6/0.75，`isOverride=false` |
| 路由回归 | 修复了「两个 Id 参数导致 `/threshold/{courseId}` 退化成 query string」的问题（第二个参数改名为 `knowledgePoint`） |

**踩坑与修复**

1. ABP 约定：方法里**只有一个**「以 Id 结尾」的参数时才会写进路径；`GetThresholdAsync(Guid courseId, Guid? knowledgePointId)` 有两个 →
   路由变成 `GET /threshold?courseId=&knowledgePointId=`，前端旧调用 404。改名为 `knowledgePoint` 后恢复 `GET /threshold/{courseId}?knowledgePoint=`。
2. 图谱分层必须做环保护（先修边来自数据挖掘，可能成环），否则最长路径迭代会退化；已限制迭代轮数并归一化层级。
3. 合并后的知识点默认隐藏在列表里，但图谱需要它作为「已归档」节点出现，因此图谱接口取**全部**知识点（含 `CanonicalId != null`）。

### 阶段 I：报告导出（Excel）+ 推送预览（已完成）

**导出能力（ClosedXML，统一排版：标题合并深色条、表头加粗冻结、自动列宽、百分比/正负号格式、薄弱项红黄绿着色）**

| 导出 | 接口 | 内容 |
| --- | --- | --- |
| 班级学情报告 | `POST /api/app/learning-analytics/export-class-report/{batchId}` | 概览 / 学生成绩（含排名、最薄弱知识点、推送状态）/ 知识点掌握率（含达标线、是否自定义）/ 重点关注学生 / 章节达成度 / 诊断与建议 |
| 学生薄弱点报告 | `POST /export-student-reports/{batchId}` | 报告摘要（复用推送时生成的真实摘要）+ 薄弱点明细（知识点/掌握率/等级/复习建议） |
| 教学诊断报告 | `POST /export-teaching-report/{batchId}` | 教学概览（得分率、较上届、未达标章节）/ 章节达成度 / 诊断与建议 |

- **导出不调用大模型**：新增 `GetTeachingInsightAsync(batchId, useAiAdvice)`，导出时传 `false`，
  优先复用已持久化的 `TeachingInsight.Advice`，缺失章节用 `RuleTeachingAdvice` 规则文案补齐；
  学生建议同理优先取 `AnalysisReport.ContentJson` 里已生成的「知识点 → 建议」。
- 文件名形如 `{课程}_{批次}_班级学情报告_{yyyyMMdd}.xlsx`，非法字符自动替换。
- 前端按钮：班级薄弱点页「导出班级报告（Excel）/ 导出学生薄弱点明细 / 预览学生收到的推送」；
  教学成果分析页「导出教学诊断报告（Excel）」。下载走 `responseType: 'blob'` + `URL.createObjectURL`。

**推送预览** `GET /student-report-preview/{batchId}?student=`
- 返回推送标题、正文（已推送取 `AnalysisReport.Summary`，未推送给实时预览文案）、薄弱点明细（掌握率/原因/建议）、`pushed` 状态；
  教师端可在重点关注学生之间切换预览，用来确认学生实际会收到什么。

**I 验证记录（本机实测）**

| 用例 | 结果 |
| --- | --- |
| 班级学情报告 | 200，`application/vnd.openxmlformats-...sheet`，13.5KB，包含 6 个 sheet（概览/学生成绩/知识点掌握率/重点关注学生/章节达成度/诊断与建议） |
| 学生薄弱点报告 | 200，8.8KB，2 个 sheet（报告摘要/薄弱点明细） |
| 教学诊断报告 | 200，9.8KB，3 个 sheet（教学概览/章节达成度/诊断与建议）；诊断建议为规则文案（「两极分化」「讲解不足」「达成度高」三种分支都出现） |
| 推送预览 | 返回已推送学生的真实 LLM 摘要 + 3 条薄弱点（掌握率/原因/建议），`pushed=true` |
| 数据来源 | 演示模拟器生成 2 份试卷 → 校对 → 推送 → 三种导出与预览全部正常 |

**踩坑**

1. `IReadOnlyList` 等集合类型在 DTO 中没问题，但 `IRemoteStreamContent` 返回需要在契约里 `using Volo.Abp.Content`。
2. ClosedXML 生成的 XML 使用 `x:` 命名空间前缀（`<x:si><x:t>`），用脚本校验 Excel 内容时正则要带前缀，否则会误判「空文件」。
3. 导出若直接复用页面接口会触发 LLM 建议生成（慢且耗 token），已通过 `useAiAdvice=false` 与「复用已持久化建议」规避。

### 阶段 J：Word 文字报告 + 报告留档 + 推荐离线评估（已完成）

**J1 报告改为 Word 文字版（简化）**

- 新增 `POST /export-docx-report/{batchId}?type=class|students`：
  - `class`：`{课程} · {批次}` 标题 → 元信息表 → **一、班级整体情况**（文字结论）→ **二、知识点掌握情况**（≤20 行表格）→ **三、学生成绩**（≤40 行表格）→ **四、重点关注学生**（逐人建议）→ **五、教学诊断与改进建议**（章节段落）→ **六、后续行动建议**。
  - `students`：学生薄弱点汇总（摘要 + 每人薄弱点与复习建议）。
- 生成器 `LearningAnalyticsDocxReportBuilder`（NPOI XWPF，微软雅黑，正文 11pt、标题加粗、首行缩进、表格列宽固定）。
- **精简掉两个 Excel 报告端点**（`export-class-report` / `export-teaching-report`），只保留 `export-student-reports`（Excel 明细）供二次分析——
  Word = 给人看的结论与建议，Excel = 可分析的数据明细。

**J2 报告留档（审计 + 历史对比）**

- 新表 `AppAnalysisReportFiles`：批次、课程、类型（班级Word / 学生汇总Word / 学生明细Excel）、文件名、大小、存储路径、**下载次数**、最后下载时间、**生成时关键指标快照**（平均分/份数/推送数/考试日期）。
- 文件落盘 `App_Data/analysis-reports/{batchId}/...`（`LearningAnalytics:ReportRoot` 可改；已 gitignore）。换 OSS 只需替换 `AnalysisReportFileStore`。
- 接口：`POST /archive-report/{batchId}?type=class|students|excel`、`GET /report-files/{batchId}`、`POST /download-report-file/{fileId}`（累加下载次数）、`DELETE /report-file/{fileId}`。
- 前端：班级薄弱点页新增「报告留档」卡片（3 个归档按钮 + 文件列表 + 下载/删除）。

**J3 推荐效果离线评估（给三路权重定标）**

- 打分逻辑抽成纯函数 `RecommendationScorer` + `RecommendationWeights`，权重可配置：
  `LearningAnalytics:RecWeights:{Content,Collab,Fit}`（默认 0.5 / 0.3 / 0.2），线上推荐与离线评估共用同一套打分。
- `RecommendationEvaluator`（留出法）：
  1. 合并 `AnswerRecord`（纸质/课堂）+ `StudentExerciseRecord`（在线）作答，按学生、时间排序；
  2. 留出每个学生最近 `EvalTestSize`（默认 3）条作测试集，其余作历史；
  3. 掌握度、相似学生、协同错误率**只用历史**计算（避免泄漏）；
  4. 候选池 = 全部习题 − 历史中已做对的题（保留测试题，否则永远命中不了错题）；
  5. 对 7 组权重分别算 **WeakHitRate@K**（错题命中率）与 **Precision@K**（推荐中确为错题的比例），按 WeakHitRate 优先给出建议权重。
- 接口 `POST /evaluate-recommendation?courseId=&topK=`；前端在「教学成果分析」页给出对比表（当前 / 建议标记 + 命中数），并提示通过配置应用建议权重。

**J 验证记录（本机实测）**

| 用例 | 结果 |
| --- | --- |
| Word 班级报告 | 200，5.8KB，73 个文本段落（标题/元信息/五个章节/表格），含教学诊断建议 |
| Word 学生汇总 | 200，4.9KB，25 个文本段落 |
| Excel 明细 | 200，8.6KB（保留） |
| 留档 | 归档 2 个文件（班级 Word / 学生 Excel），列表展示类型/大小/下载次数/生成时间 |
| 下载 | 200，`downloadCount` 由 0 → 1 |
| 删除 | 行 + 磁盘文件同时删除，列表剩 1 个 |
| 离线评估 | 2 名学生 47 条作答；1 名学生测试集含错题可用于评估（2/3 命中）；7 组权重结果：当前配置/纯协同/内容为主 = 0.667 命中率、纯内容 = 0、纯难度 = 0.333，结论「当前配置已接近最优」 |

**踩坑与修复**

1. 离线评估第一版把测试题从候选池剔除 → 错题永远不可能被推荐，指标恒为 0；改为「候选池 = 全部习题 − 历史已做对」，掌握度仍只用历史。
2. `IRepository<T>` 忘记写键类型时按复合键解析，`FindAsync(Guid)` 会编译失败（已统一为 `IRepository<T, Guid>`）。
3. NPOI 的 `SetColumnWidth` 需要 `ulong`（int 会报 CS1503）。
4. 测试集全对的学生无法贡献「错题命中」，需与「处理过的学生」分开统计，否则指标分母为 0 容易被误解。

### 阶段 K：归档报告走 OSS（已完成）

- `AnalysisReportFileStore` 升级为「**OSS 优先 + 本地回退**」：
  - 复用管理页维护的**租户级** OSS 配置（`IOssCredentialProvider`：AK/SK/Bucket/Endpoint/前缀，保存即生效）；
  - 对象键：`{UploadPath}/analysis-reports/{batchId}/{guid}.{ext}`，写入时带 `ContentType` / `ContentLength`；
  - 上传失败（网络/权限/未配置）自动回退本地磁盘并告警，功能不中断；
  - `StoredPath` 用前缀区分后端：`oss:{objectKey}` / `local:{relativePath}`，**历史数据无需迁移**（无前缀按本地处理）；
  - 下载走 `GetObject` 读流；删除同时清理 OSS 对象 / 本地文件；
  - DTO 增加 `Storage`（oss/local），前端归档列表显示存储徽标。
- 多实例部署：归档文件集中在 OSS，多个 API 实例共享；本地回退仅用于开发/未配置 OSS 的场景。

**K 验证记录（本机实测，OSS 已真实配置）**

| 用例 | 结果 |
| --- | --- |
| 归档上传 | 日志：`归档报告已上传 OSS：knowledgehub/analysis-reports/{batchId}/xxx.docx（5868 bytes）`；接口返回 `storage=oss`，本地 `App_Data/analysis-reports` 无文件 |
| 列表 | `storage=oss`，类型/大小正确 |
| 下载 | 200，5868 bytes（从 OSS `GetObject` 读取），`downloadCount` 0 → 1 |
| 删除 | 204，无失败日志（OSS `DeleteObject` 成功），列表清空 |

### 阶段 L：专项练习闭环 + 示例数据（已完成）

**L1 学生端专项练习（打通「推荐 → 练习 → 掌握度回写」）**

- 后端 `PracticeAppService`（`/api/app/practice`）：
  - `GET /set/{courseId}?knowledgePoint=&take=`：复用 `RecommendationEngine` 的薄弱知识点分组，
    返回题目（题干预选项/分值/难度/章节/知识点），带推荐理由；
  - `POST /submit`：逐题复用 `IStudentExerciseRecordAppService.SaveOrUpdateRecordAsync`（自动判分 + 同步课程完成度），
    随后 `MasteryRecalculationService.RecalculateAsync` 重算掌握度并返回涉及知识点的最新掌握率与逐题结果。
- 前端 `/student/practice`：逐题作答（单选/多选/判断/填空/主观题），即时判定 + 下一题，结束展示
  正确率、知识点掌握率（含目标线标尺）与逐题回顾；智学分析「专项练习」按钮改为跳转该页。
- 主观题（简答/论述/计算）`IsCorrect=null`（待老师批改），与原有在线练习口径一致。

**L2 qidi 租户示例数据（开箱可演示）**

- 新增 `LearningAnalyticsDemoDataSeeder`（Domain，数据播种阶段执行，仅 `LearningAnalytics:DemoTenantName`（默认 qidi）租户，幂等）：
  - 前置课程《计算机网络技术基础》4 章 12 题 + 后续课程《网络安全技术》3 章 6 题；
  - 两个可登录的示例学生（`demo_student` 张晨 / `demo_student2` 李思雨，密码 `1q2w3E*`）并完成选课；
  - 已确认的期末试卷批次（2 份试卷 + 单题得分 + 纸质作答记录）、在线作答记录各若干；
  - 课程前后关联、个人报告（规则文案）与站内消息（未读）。
  - 知识点与习题映射仍由 `KnowledgePointIndexer` 首次访问时按章节初始化（与真实使用一致）。
- 详见 `docs/LEARNING_ANALYTICS_DEMO.md`（数据清单、账号、端到端步骤、重置 SQL）。

**L 验证记录（实测）**

| 用例 | 结果 |
| --- | --- |
| 播种 | qidi 租户创建 2 门课程、4+3 章、18 题、2 名学生（租户正确）、1 个已确认批次（均分 44.5）、2 条消息 |
| 教师链路 | 批次/班级薄弱点（4 知识点 0.51~0.56）/教学诊断（4 章 0.60~0.73）/课程路径/知识点管理/图谱 全部正常 |
| 报告 | Word 导出 200（6.2KB）；归档 `storage=oss` |
| 学生链路 | 总览（33 分、排名 2/2、纸质来源、4 知识点）；路径含后续课程；复习包 4 项；1 条未读消息→标记已读后 0 |
| 专项练习 | 推荐「IP 地址与子网划分」3 题 → 逐题判定 → 掌握率 0.45 → **0.58**（回写生效） |

**踩坑与修复**

1. `IdentityUser.TenantId` 是 `protected set`，必须通过构造函数传租户；否则示例学生会被建成**宿主用户**导致登录后看不到租户数据。
2. `PracticeAppService.GetSetAsync` 的路由是 `GET /practice/set/{courseId}`（`courseId` 走路径），前端/脚本不要用 query 传。
3. 示例数据的章节名同时充当知识点名（`KnowledgePointIndexer` 用章节标题初始化），因此章节标题按「知识点粒度」命名，避免出现「第1章 …」这种不适合展示的知识点名。

### 阶段 M：推荐权重一键应用 + 试卷图片 OSS 直传（已完成）

**M1 推荐权重一键应用（热生效）**

- 新增 3 个 Setting：`KnowledgeHub.LearningAnalytics.RecWeight{Content,Collab,Fit}`（默认 0.5/0.3/0.2）。
- `RecommendationWeightProvider`：读取顺序 **Setting → appsettings → 默认**，写入走 `ISettingManager.SetGlobalAsync`
  （ABP 设置缓存自动失效，**保存即生效，无需重启**）；权重读取后自动归一化。
- `RecommendationEngine` / `RecommendationEvaluator` 统一改用该 Provider，线上推荐与离线评估口径一致。
- 接口：`GET /recommendation-weights`（含 `source=setting|config`）、`POST /apply-recommendation-weights`、
  `POST /apply-best-recommendation-weights?courseId=&topK=`（跑一次评估并应用最优权重，样本不足时友好报错）。
- 前端：教学成果分析页「推荐效果评估」卡片显示当前生效权重，并提供「应用建议权重（即时生效）」按钮。

**M2 试卷图片 OSS 直传 + OCR 按 URL 取图**

- 上传载荷新增 `url` 字段：图片先经 `/api/oss-upload/image` 上传 OSS，接口只收 URL，
  后端**校验 URL 必须属于本租户配置的 OSS Bucket / 自定义域名**（防任意 URL 被送入第三方模型），
  以 `url:{publicUrl}` 存入 `PaperUploadImage.StoredPath`，不落本地磁盘。
- OCR：`PaperOcrRequest` 新增 `ImageUrl`，有 URL 时直接作为 `image_url.url` 传给 Qwen-VL-OCR
  （请求体显著变小，整班批量上传不再需要几 MB 的 Base64）；本地上传仍走 Base64，两种来源可混用。
- 前端：上传时自动尝试 OSS 直传，失败（未配置/无权限/网络）**自动回退 Base64**，并在页面上提示
  「已启用 OSS 直传」；PDF 拆页后的每页图片同样走 OSS。

**M 验证记录（实测）**

| 用例 | 结果 |
| --- | --- |
| 权重读取 | 初始 `0.5/0.3/0.2`，`source=config` |
| 应用权重 | `POST /apply-recommendation-weights {0.6,0.2,0.2}` → 读回 `0.6/0.2/0.2`，`source=setting` |
| 离线评估 | 2 名学生可用、36 条作答；7 组权重对比：当前 0.500 → 纯内容 0.750（精度 0.300）|
| 应用建议权重 | `apply-best` → `内容 1 / 协同 0 / 难度 0` 写入 Setting 并读回成功；随后复位为 0.5/0.3/0.2 |
| OSS 图片直传 | `/api/oss-upload/image` 返回 `https://kg-storage.oss-cn-beijing.aliyuncs.com/knowledgehub/...png` |
| URL 上传 + OCR | 只传 `url`（无 Base64）→ 识别成功：学号 2023010101、6 题得分（5/4/5/5/5/6，总分 30），6 题全部匹配题库，学生自动匹配到「张晨」 |

**踩坑**

1. `ISettingManager` 在 `Volo.Abp.SettingManagement` 命名空间（不是 `Volo.Abp.Settings`）。
2. ABP 的 `SetGlobalAsync` 在「写入值等于设置项默认值」时会**删除该行**（回到默认），因此把权重复位成 0.5/0.3/0.2 后 `AbpSettings` 里查不到记录、`source` 变为 `config` —— 这是预期行为，不是写失败。
3. 示例数据的在线作答时间若都集中在播种瞬间，「最近 3 条」里可能没有错题导致离线评估无样本 → 播种时显式设置 `CompletedAt` 递增，保证测试集含错题。

### 阶段 N：PDF 试卷直传 + 教师视角收敛（已完成）

**N1 PDF 试卷直传（qwen3.5-ocr · Responses API）**

- 官方形态：`POST {Qwen:BaseUrl}/responses`
  `{ model: "qwen3.5-ocr", input:[{role:user, content:[{type:"input_file", file_url},{type:"input_text", text}]}] }`
  （PDF 仅 Responses API 支持，最多 50 页 / 100MB；Chat API 不支持）
- 实现：
  - 上传载荷支持 `contentType=application/pdf` + OSS `url`（PDF 必须走 OSS，不落本地）；
    `PaperUploadImage.StoredPath = url:{ossUrl}`，`ContentType=application/pdf`；
  - `PaperOcrRequest.PdfUrl` → `QwenPaperOcrRecognizer.RecognizePdfAsync` 走 `/responses`，
    输出兼容 `output_text` / `output[].content[].text` / `ocr_result` 三种形态；
  - PDF 用**逐行忠实转写**提示词（结构化交给本地 `ParseTranscript`），避免模型把题干塞进题号、丢分数；
  - 解析层加固：去 HTML/LaTeX 标签、按行抽「得分/满分」（括号内=满分、括号外=得分，写反自动交换）、
    题号归一化（只取开头 1~3 位数字，支持 `5(1)` 子题号）、`QuestionNo` 入库截断到 32；
  - 新增能力探测 `GET /ocr-capabilities`（`{image, pdfDirect, pdfModel, note}`），前端据此决定
    「整份 PDF 直传」还是「pdf.js 拆页」；OSS 不可用时自动回退拆页。
- **默认关闭**：`LearningAnalytics:PdfDirectEnabled=false`。实测 qwen3.5-ocr 对合成双页 PDF 的转写质量
  低于图片路径（题量偏少、分数可能错位），因此默认仍走「拆页 + 图片 OCR」；需要时改配置即可启用，
  且 PDF 直传结果统一按**低置信度（0.6）**落库并强制人工校对。

**N2 教师视角收敛：只看我教的课程**

- 后端：`PagedCourseRequestDto` 增加 `TeacherId` 并真正参与过滤（此前 DTO 有字段但未使用）。
- 前端：教师端「学情分析」「知识点管理」课程选择器旁新增「只看我教的课程」开关；
  过滤结果为空时自动回退全部课程并提示「你名下暂无课程，已显示全部」，避免教师看不到数据。
- 示例数据：播种时把示例课程挂到租户内第一个 Teacher 账号（`Course.TeacherId`），便于演示该过滤。

**N 验证记录（实测）**

| 用例 | 结果 |
| --- | --- |
| 能力探测 | `{image:true, pdfDirect:true, pdfModel:"qwen3.5-ocr"}` |
| PDF 直传识别 | PDF 传 OSS → 只传 URL → OCR 成功：学号 2023010101、学生匹配「张晨」、题号 1/2/3、得分 5/6 + 4/5 + 6/10、全部 matched、置信度 0.6 |
| 题号/分数解析 | 修复前出现题号「2.19」与分数全 0；修复后题号规整、分数按行抽取正确 |
| 教师过滤 | `?teacherId=<当前用户>` → 2 门（两门示例课程）；不带参数 → 7 门 |

**踩坑**

1. `PagedCourseRequestDto.TeacherId` 缺失（Angular 代理里有是因为旧版本残留），需后端补字段并加 `WhereIf`。
2. qwen3.5-ocr 在 Responses API 下会返回 HTML/LaTeX 包裹的转写，且可能只覆盖部分页/错位分数 → 必须去标签 + 低置信度 + 人工校对。
3. PDF 直传不要带 `ocr_options.task=document_parsing`（会强制 LaTeX 解析、忽略自定义提示词）。

### 剩余待办（主线）

**P1（推荐优先）**
1. **PDF 直传调优**（功能已就绪，默认关闭）：接入更清晰的扫描件后重新评估 `PdfDirectEnabled` 默认值，
   并在前端补充「本次识别来自 PDF 直传，请重点校对」的显式提示。
2. **学生端专项练习页**：现在「专项练习」跳到课程学习页，缺一个「按推荐题集合直接作答、结果回写掌握度」的独立练习页。

4. **教师视角收敛**：现在教师能看到本租户全部课程；需要「我的课程 / 我的班级」过滤与班级维度（目前以「课程 + 试卷批次」近似班级）。

**P2（完善度）**
5. **PDF 版报告**：HTML 模板 → 无头浏览器转 PDF（避开中文字体嵌入）；Word/Excel 已覆盖存档与分析需求。
6. **图谱交互增强**：节点拖拽微调、按章节泳道分组、导出 PNG。
7. **知识点级判定线批量维护**：目前逐个知识点配置，缺批量导入/导出（Excel）。
8. **跨课程归一化闭环**：「跨课程」合并建议已能给出，但缺「并入哪门课的标准知识点」的归属规则与确认流程。
9. **学习分析细粒度权限**：教师端目前挂在 `KnowledgeHub.Courses` 策略上，计划中的 `KnowledgeHub.LearningAnalytics(.Manage)` 未真正落地。
10. **线上推荐效果监控**：离线评估已有，缺「推荐 → 实际作答」的线上命中率看板与告警。

**已知技术债**
11. 掌握度重算仍为同步执行（确认试卷 / 校对时）；量大后应改为 Hangfire 异步 + 进度提示。
12. `AnalysisReport`（学生个人报告）未做「事实哈希缓存」，重复推送会重复调用模型。

### 变更记录

| 日期 | 变更 |
| --- | --- |
| 2026-07-13 | 建立方案与进度文档；完成阶段 A 前端原型版 |
| 2026-10-19 | 完成阶段 B 后端（实体/迁移/服务）与阶段 C 联调；Mock OCR 打通全链路；LLM 教学建议生效 |

<!-- Last updated: 2026-10-19 -->
