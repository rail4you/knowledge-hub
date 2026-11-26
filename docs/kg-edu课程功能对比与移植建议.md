# kg-edu → KnowledgeHub 课程功能差异与移植建议

> 调研对象：
> - `~/projects/kg-edu`（Elixir Phoenix + Ash + React/Vite/AntD，单校教学系统，代号 kg-edu）
> - `~/projects/KnowledgeHub`（ABP Framework .NET + Angular，多租户资源库/教学平台，代号 KH）
>
> 本文只覆盖**课程域**（课程 → 章节 → 知识点 → 材料 → 练习/考试/作业 → 学情）。按需求方口径：
> **知识图谱可视化不作为移植重点**，资源库系统不往图谱方向做。

---

## 0. 一句话结论

两个系统的课程骨架重合度约 **60%**（课程/章节/知识点树/习题/试卷/作业/微专业都有），
真正的差距不在"有没有"，而在 **kg-edu 把课程做成了完整的教学闭环，KH 把课程做成了资源与内容管理**。

具体说，kg-edu 多出来的是**教学过程环节**：

1. **课程信息的教学化表达**（教学目标 / 毕业要求 / 知识逻辑结构 / 教材）
2. **视频作为一等公民**（视频 + 时间轴知识点切片 + 转写分段）
3. **课堂互动四件套**（签到 / 讨论答疑 / 小组任务 / 课堂练习），全部走 token 免登录链接
4. **能力体系**（主能力 → 子能力 → 知识点 → 实验，含达成度）
5. **学习行为与画像**（活动日志 → 掌握度 → 推荐 → 个性化门户）
6. **AI 教研产出**（课程体系生成 / 能力图谱生成 / 课件 PPTX / 文件问答 / TTS）

**建议：移植 12 项，分 P0/P1/P2 三批；明确不移植 7 项**（KH 已有更强实现，或与平台定位冲突）。
移植前必须先解决 3 个架构障碍（下方第 4 节），其中「**KH 现在并存两套互不连通的知识点体系**」是最大的坑——
不先统一，移植进来的掌握度/推荐会变成第三套。

---

## 1. 现状全景

### 1.1 kg-edu 课程功能地图

| 域 | Ash Resource（`lib/kg_edu/`） | 前端页面（`src/pages/`） |
|---|---|---|
| 课程 | `courses/course.ex`(1220) `course_info.ex` `book.ex` `subject_category.ex` `course_assignment.ex` `course_enrollment.ex` `link.ex` | `teacher/course.tsx` `course-detail.tsx` `course-info.tsx` `book-info.tsx`；学生 `course-detail.tsx`(6 Tab) `overview.tsx` |
| 章节 | `courses/chapter.ex`(378，树 + `path` 物化路径 + `reorder` + `generate_knowledge_graph`) `chapter_graph.ex` | `teacher/chapter.tsx` |
| 知识点树 | `knowledge/resource.ex`(3656！`subject → unit → cell` 三级，含 `importance_level`/`sort_path`/`dimension`/`teaching_goal`/`tag`) | `teacher/knowledge-resource.tsx` `knowledge-relation.tsx`；学生 `knowledge.tsx` |
| 知识点关系 | `knowledge/relation.ex` `relation_type.ex` | `teacher/relation-type.tsx` |
| 习题 | `knowledge/exercise.ex`(587，含 `link_exercise_to_knowledge`/`move_up|down`/`generate_ai_exercise`/Excel 导入导出) `exercise_options.ex` `exercise/import_from_excel.ex` | `teacher/exercise.tsx` `ai-exercise.tsx` |
| 问题库 | `knowledge/question.ex` `question_connection.ex` | `teacher/question.tsx` |
| 试卷考试 | `knowledge/exam.ex`(611，`create_with_exercises`/`add_exercise`/`get_exam_analysis`) `exam_exercise.ex` `student_exam.ex`(同学生同时只能一场未完成) `student_exam_answer.ex` `exam_analysis.ex` `proctor_session.ex` `proctor_snapshot.ex` | `teacher/exam-management.tsx` `exam-grading.tsx` `exam-statistics.tsx` `exam-print.tsx` `face-check.tsx`；学生 `exam-courses/exam-list/exam-taking` |
| 练习 | `knowledge/practice_session.ex` `practice_attempt.ex` | 学生 `practice-session.tsx`；教师 `ai-practice.tsx` |
| 作业 | `knowledge/homework.ex`(537，挂知识点 + Excel 导入 + 提交日志) | `teacher/homework.tsx` |
| 实验 | `knowledge/experiment.ex`(561，`guide_file` + 关联知识点 + 关联能力) | `teacher/experiment-*.tsx`；学生 `experiment-courses` |
| 视频 | `courses/video.ex`(Mux: `upload_id`/`asset_id`/`playback_id`/`duration`/`thumbnail`) `course_video.ex` `video_knowledge_mark.ex`(秒级打点 → 知识点) `video_analysis.ex`(summary/segments/transcript/wordcloud) `video_analysis_pipeline.ex` | `teacher/video.tsx` `course-video.tsx` `video-uploader.tsx`；学生 `course-video.tsx`(2078 行) |
| 材料文件 | `courses/file.ex`(617) `file_example.ex` | `teacher/file.tsx` `knowledge-file.tsx` |
| 能力体系 | `knowledge/main_ability.ex` `sub_ability.ex` `sub_ability_knowledge_resource.ex` `ability_relation.ex` `knowledge_point_cognitive.ex`(认知目标) | `teacher/competency-management.tsx` `graph-competency.tsx` `knowedge-cognitive-goals` |
| 掌握度/推荐 | `knowledge/student_knowledge_mastery.ex`(712) `learning_analyzer.ex`(513) `recommendation_engine.ex`(728，薄弱/前置/行为/协同四路) `learning_recommendation.ex`(685，状态机) `student_profile.ex`(935) | 学生 `learning-recommendations.tsx`；教师 `learning-recommendations.tsx` `student-profile.tsx` |
| 行为日志/洞察 | `activity/activity_log.ex`(576) `insights/engine.ex`(636，自然语言问数 + 报告模板) `report_template.ex` | `teacher/activity-logs.tsx` `activity-summary.tsx`；`admin/data-insight.tsx` |
| 课堂互动 | `attendance/check_in_session.ex|record.ex|face_check_config.ex|face_check_log.ex` `courses/discussion.ex|discussion_reply.ex|discussion_session.ex` `group_task/group.ex|group_member.ex|task.ex|task_group.ex|task_submission.ex` | 教师 `check-in-management` `discussion-session` `group-management` `group-task` `course-evaluation`(实为答疑)；学生 `interaction-hub` + 免登录页 `check-in/:tenant/:token` |
| 微专业 | `major_analysis/micro_major*.ex`（课程/章节/视频/习题/资源/作业/报名/证书 8 张表） | `/micro-major/*` |
| 专业/岗位 | `major_analysis/major|job_position|major_competency|curriculum_design|job_competency_graph|job_core_task|job_task_ability|ability_knowledge_link` | `teacher/major-*.tsx` `job-*.tsx` |
| AI | `ai/assistant_*` `jido_agents/skills` + 生成服务 | `teacher/ai-exercise|ai-question-bank|ai-courseware|ai-practice|ai-file|ai-discussion|ai-command|ai-assistant|tts` |
| 门户 | `knowledge/smart_portal_config.ex`(教师级门户布局 + `student_ids`) | `/` `/courses` `/page/:slug` + `admin/portal-config` |
| 导入 | `xmind_parser.ex` `opml_parser.ex` `excel_import/parser.ex` `knowledge/import_from_llm.ex` | 各管理页内嵌 |

### 1.2 KnowledgeHub 课程功能地图（现状）

| 域 | 后端（`src/KnowledgeHub.*/`） | 前端（`angular/src/app/`） |
|---|---|---|
| 课程 | `Courses/Course.cs`（14 字段）`CourseAppService`（创建/更新/审核/发布/下架/详情/选课/退课）`CourseShareAppService` | `learning/course-list` `course-detail` `course-workbench` |
| 章节 | `Courses/Chapter.cs`（`ParentId` 树 + 协作溯源）`ChapterAppService`（Excel 导入/重排）`ChapterResourceAppService` | `learning/chapter-management` `chapter-resource` |
| 知识点树 | `Courses/KnowledgeResource.cs`（自引用 `ParentId` + `ImportanceLevel`/`Difficulty`/`Tags`/`Embedding`，**就是 kg-edu 的 subject/unit/cell 骨架**） | `learning/course-resource` 内使用 |
| 习题 | `Exams/Exercise.cs`（10 种题型 + `Score` + `KnowledgeResourceId`）`ExerciseAppService`（AI 生成/主观题批阅/Excel 导入/批量删） | `learning/exercise-management` `exercise` `chapter-exercise` |
| 试卷考试 | `Exams/Exam.cs` `ExamAppService`（规则组卷 + 总分模式 + 打乱 + 自动判分 + 主观题批改 + 统计） | `learning/exam-management`；学生 `exams/student-exam-take` |
| 作业 | `Assignments/`（作业 + 评分细则 + 附件 + 提交 + 得分明细 + 导出，**深度远超 kg-edu homework**） | `learning/assignment-management`；学生 `assignments/*` |
| 教案/案例 | `CourseLessonPlan` `CourseCase` + AI 多章节生成任务 | `learning/lesson-plan` `case-library` |
| 智学分析 | `LearningAnalytics/`（知识点 + 习题×知识点权重 + 先修边挖掘 + `MasteryCalculator`（Beta 先验 + 120 天半衰期）+ 纸卷 OCR/评分 + 报告 xlsx + `RecommendationEngine`/`Scorer`/`Evaluator`） | `learning-analytics/*` `learning/analysis-workbench` `analysis` `knowledge-points`；学生 `analysis` `practice` |
| 学习 | `Learning/LearningAppService`（进度/看板/掌握度）`StudentExerciseRecordAppService`（作答记录 + 自评 + 查看答案）`KnowledgeMastery`（简单计数：3 次且 ≥80% → 已掌握） | 学生 `course-learn`（章节学习 + 习题 + 自评） |
| 图谱 | `KnowledgeGraph/`（`KnowledgeNode`/`KnowledgeRelation`，**壳**：`GetRecommendedPathAsync` 抛 `NotImplementedException`，`BuildGraphAsync` 是 TODO） | `knowledge-graph/graph-viewer` |
| 资源/资源库 | `Resources/`（存储/OSS/转换/缩略图/Meili）+ `ResourceLibraries/`（共建/准入/协作策略/学习策略） | `resources/*` `learning/course-resource` |
| 微专业/专业 | `MicroMajors`（含证书模板/发放）`Majors`（仅 CRUD） | `micro-majors/*` |
| 实训 | `Practicums/`（项目/任务/材料/提交/指导/评分/导出/AI 引导/聊天） | `practicum/*` |
| 教学智能体 | `TeachingAgents/`（草稿/发布版本/课堂任务/课表/学生求助响应） | `teaching-agents` `learning/classroom-timetable` |
| 其他 | 课程思政 `CourseIdeology`、双高评估 `DoubleHigh`、就业 `Employment`、特教 `SpecialEducation`、白板 `Whiteboards`、AI 图片/视频生成 | 对应前端模块 |

---

## 2. 差异矩阵

### 2.1 kg-edu 有、KH 没有（移植候选，共 12 项）

| # | 能力 | kg-edu 实现位置 | 价值 | 难度 | 建议批次 |
|---|---|---|---|---|---|
| A1 | **课程教学信息**（背景/教学目标/课程定位/课程亮点/知识逻辑结构/毕业要求）+ 学生端 6 Tab 详情页 | `courses/course_info.ex`、学生 `course-detail.tsx` | 高（对外门户、评估材料直接可用） | 低 | **P0** |
| A2 | **教材**（书名/作者/出版社/封面/附件，课程 1:N） | `courses/book.ex` | 中 | 低 | **P0** |
| A3 | **课程视频 + 时间轴知识点切片**（秒级打点挂知识点、播放器内跳转）+ 视频 AI 分析（转写/分段/词云） | `courses/video.ex` `video_knowledge_mark.ex` `video_analysis*.ex` | 高（KH 现在**完全没有课程视频实体**，视频只能作为资源附件） | 高（要重做上传/转码/播放链路，kg-edu 用 Mux，KH 不可用） | **P1** |
| A4 | **课堂互动：签到**（token 免登录 + 签到记录 + 定位） | `attendance/check_in_session|record` | 中高（课堂真实需求） | 中 | **P1** |
| A5 | **课堂互动：讨论答疑**（课程/章节维度讨论 + 回复 + 评分 + 讨论会 token） | `courses/discussion*` `discussion_session` | 高（KH 只有白板/实训聊天，没有课程答疑区） | 中 | **P1** |
| A6 | **课堂互动：小组任务**（分组 + 任务 + token 提交 + 截止/发布） | `group_task/*` | 中 | 中 | **P1** |
| A7 | **课堂练习会话**（教师发起 → token 加入 → 逐题作答 → 即时统计） | `knowledge/practice_session.ex` | 中高 | 中 | **P1** |
| A8 | **学习行为日志 + 学习画像 + 数据洞察（NL 问数/报告模板）** | `activity/activity_log` `insights/engine` `student_profile` | 高（KH 的学情只到"知识点掌握"，没有行为维度） | 中高 | **P1** |
| A9 | **推荐闭环**（薄弱点/前置知识/行为/协同四路策略 + 推荐状态机 + 学生端推荐页） | `recommendation_engine.ex` `learning_recommendation.ex` | 高（KH 有引擎但没有"推荐列表 + 生命周期"落地给学生） | 中 | **P1** |
| A10 | **考试打印 / 成绩单打印** | `exam-print.tsx` + 后端组卷内容接口 | 中（很多学校要纸质考） | 低（Gotenberg 已在用，可转 PDF） | **P1** |
| A11 | **能力体系**（主能力→子能力→知识点→实验，能力达成度、认知目标） | `main_ability` `sub_ability` `ability_relation` `sub_ability_knowledge_resource` `knowledge_point_cognitive` | 中高（专业认证/双高评估要用；和 KH 的 `DoubleHigh` 天然互补） | 高 | **P2** |
| A12 | **AI 教研**：课程体系生成 / 能力图谱生成 / 课件 PPTX / 文件问答 / TTS / 指令模板 | `ai/*` `jido_agents/*` + `teacher/ai-courseware|ai-file|ai-command|tts` | 中（PPTX 与课程体系生成最值钱） | 高 | **P2** |
| A13 | 知识点导入：XMind / OPML / LLM | `xmind_parser.ex` `opml_parser.ex` `import_from_llm.ex` | 中低（KH 已有 Excel 导入） | 中 | P2 可选 |
| A14 | 教师级个性化门户（`smart_portal_config`：布局 + 指定学生） | `knowledge/smart_portal_config.ex` | 低（KH 已有租户级门户） | 中 | P2 可选 |
| A15 | 考试防作弊（`proctor_session`/`snapshot` + 人脸核验 `face_check_*`） | `knowledge/proctor_*.ex` `attendance/face_check_*` | 低（合规风险高、KH 无相机链路） | 高 | **不建议** |

### 2.2 两边都有，但深度不同（不需要移植，必要时小补）

| 能力 | kg-edu | KnowledgeHub | 结论 |
|---|---|---|---|
| 章节树 | 有 `path` 物化路径 + `reorder` + 生成图谱 | `ParentId` 树 + Excel 导入 + 重排 | KH 缺物化路径；**移植"知识点树"时需要补 `SortPath`**（见 4.1） |
| 习题 | 题型较少（atom 类型 + `options` map）、挂知识点、上下移、AI 生成、Excel | 10 种题型 + 权重 + 章节多对多 + AI 生成 + 主观题 AI 批阅 + Excel | **KH 更强，不移植** |
| 组卷 | 手工选 ± `create_with_exercises` | 题型规则 / 目标总分 / 打乱 / 多章节限定 | **KH 更强，不移植** |
| 判分/批阅 | 客观题判分 + 教师批改 | 自动判分 + `AutoGradeAsync` + 主观题 AI/人工 | **KH 更强** |
| 作业 | `title/content/score/answer` + 提交日志 | 评分细则/附件/迟交扣分/导出/重交 | **KH 远强，不移植** |
| 掌握度 | `student_knowledge_mastery`（计数 + 加权） | `MasteryCalculator`（Beta 先验 + 半衰期 + 证据量），但只接在试卷分析链路 | **算法 KH 更强，但没接课程学习链路** → 见 4.1 |
| 推荐 | 四路策略 + 推荐记录状态机 | `RecommendationEngine/Scorer/Evaluator` + 权重提供者，结果没有"推荐卡列表" | 引擎留 KH，**移植 kg-edu 的推荐落地形态**（A9） |
| 微专业 | 8 张表 + 独立课程/章节/视频副本 | 统一课程 + 证书模板/申请/核验/审批 | **KH 更强（且不复制课程），不移植** |
| 专业/岗位 | `job_competency_graph` 等 8 表，能力-知识点链 | `Majors` 只有 CRUD；岗位能力在 `Employment` | KH 偏弱，但属"能力体系"（A11）范畴，不单独移植 |
| 实验 | `experiment` + 指导书 + 知识点/能力关联 | `Practicums`（项目/任务/材料/提交/评分/AI 引导） | 语义不同：KH 是实训项目，kg-edu 是实验课。**如需"实验"语义，给 `PracticumProject` 补 `GuideResourceId` 即可，不新建模块** |
| AI 辅助 | 习题/课件/练习/文件问答/讨论/指令/插件/TTS | 习题/教案/案例/图片/视频/职规/智能体/白板助手 | **KH 覆盖大半，只补 A12 中"课程体系/能力图谱/PPTX"三项** |
| 门户 | 教师级 `smart_portal_config` | 租户级 `PortalAppService` + 课程/学校门户 | 不移植（A14 可选） |

### 2.3 KH 有、kg-edu 没有（保留，不引入 kg-edu 同类实现）

资源库/共建/准入审核、跨租户学校门户、多租户、就业与招聘直播、双高评估、特殊教育、智能白板协作、
教学智能体（版本化 + 课堂任务 + 课表）、AI 图片/视频生成、课程思政、教案/案例库、证书体系、实训项目、
前端缓存失效链路（`cacheInvalidationInterceptor`）。

---

## 3. 移植方案（分三批）

### P0 — 骨架补齐（约 8~12 人天）

> 目标：让 KH 的"课程"从"内容容器"变成"可对外呈现的教学课程"。

| 项 | 后端落点 | 前端落点 | 备注 |
|---|---|---|---|
| A1 课程教学信息 | 新增 `Domain/Courses/CourseInfo.cs`（6 文本字段，`CourseId` 唯一）；`CourseAppService.GetCourseInfoAsync/SaveCourseInfoAsync`；EF 迁移 `AddCourseInfo` | `learning/course-detail` 增「教学目标/课程结构/毕业要求」Tab（对齐 kg-edu 6 Tab） | 与 `DoubleHigh` 评估指标可互相取数 |
| A2 教材 | 新增 `Domain/Courses/CourseTextbook.cs`（标题/作者/出版社/封面/附件 `ResourceId`/是否公开） | 课程详情「教材信息」Tab + 教师端维护弹窗 | 附件直接复用 `Resource`，不要另建文件存储 |
| P0-3 知识点树可用化 | `Courses.KnowledgeResource` 已有，补 `SortPath`（物化路径）+ 类型字段（`NodeType`：单元/知识项）+ `ChapterId` 已有 | 复用现有 `shared/chapter-picker` 做知识点树选择器；课程工作台加「知识点管理」入口 | **必须做**，A8/A9/A11 都依赖它 |
| P0-4 掌握度打通 | 把 `Learning.KnowledgeMastery` 的计算改为调用 `LearningAnalytics.MasteryCalculator`（观测源：`StudentExerciseRecord`）；`LearningAppService.GetKnowledgeMasteryAsync` 输出 Mastery/Evidence/等级 | 学生 `course-learn` 知识点侧栏显示掌握度条形（已有 `MasteryBarComponent` 可复用） | **先打通再谈推荐**，见 4.1 |

### P1 — 教学环节补齐（约 25~35 人天）

| 项 | 后端 | 前端 |
|---|---|---|
| A3 课程视频 + 时间轴切片 | `Domain/Courses/CourseVideo.cs`（`ChapterId`/`KnowledgeResourceId`/`ResourceId`/`Duration`/`ThumbnailUrl`）+ `VideoKnowledgeMark.cs`（`PointSec`/`Label`/`KnowledgeResourceId`）；托管用现有 `Resources` 存储 + HLS/直链（**不要引 Mux**）；AI 分析可选（复用 `AI` 模块 + 转写服务） | 学生 `course-learn` 播放器 + 右侧知识点时间轴；教师端视频管理 + 打点控件 |
| A4 签到 | `Domain/Interactions/CheckInSession.cs`、`CheckInRecord.cs`（token/状态/起止/定位） | 教师 `check-in-management`；学生免登录页 `/student/check-in/:tenant/:token`（参考 kh 门户 guard + `__tenant` 处理，见 4.3）；人脸核验**不做** |
| A5 讨论答疑 | `Domain/Interactions/Discussion.cs`、`DiscussionReply.cs`（`CourseId`/`ChapterId?`/`KnowledgeResourceId?`/状态/回复数/查看数/评分） | 课程详情「讨论答疑」Tab + 教师答疑台（kg-edu 的 `course-evaluation` 就是这个） |
| A6 小组任务 | `Domain/Interactions/StudentGroup.cs`、`GroupMember.cs`、`GroupTask.cs`、`GroupTaskSubmission.cs`（token/截止/发布/评分） | 教师分组 + 任务发布；学生免登录 token 提交页 |
| A7 课堂练习会话 | 扩展 `LearningAnalytics/PracticeAppService`：`PracticeSession`（`CourseId`/`Token`/`Status`）+ `PracticeAttempt`；判定沿用现成客观题判定 | 教师发起 + 实时统计；学生 token 加入作答 |
| A8 行为日志 + 画像 | `Domain/Learning/StudentActivityLog.cs`（`UserId`/`ActionType`/`ResourceType`/`ResourceId`/`Metadata`/`CourseId`）+ 写入点（学习进度/作答/看视频/交作业）+ `StudentProfileService`（聚合：偏好类型/活跃时段/连续性/近期活跃度，算法可直接照搬 `recommendation_engine.ex` 的 `determine_preferred_learning_type`/`active_hours`/`consistency_score`） | 学生 `analysis` 增「学习行为」卡；教师端「学习画像」抽屉 |
| A9 推荐落地 | `LearningAnalytics/RecommendationEngine` 输出 → 新表 `StudentRecommendation`（类型/优先级/理由/状态机 pending→viewed→in_progress→completed→dismissed）；`RecommendationScorer` 复用 | 学生 `learning-recommendations` 页（kg-edu 可直接对照）+ 课程工作台入口 |
| A10 试卷/成绩打印 | `ExamAppService.ExportPaperPdfAsync`（HTML → Gotenberg，已在用） | 教师端「打印」按钮 + 打印样式页 |

### P2 — 增强（约 30~45 人天，按需）

| 项 | 说明 |
|---|---|
| A11 能力体系 | 与 `Majors`/`Employment` 的岗位能力合并成 `Competency` 模块：主能力/子能力/能力关系/能力-知识点/能力-实验 + 达成度（= 关联知识点掌握度加权）。**不要照搬 kg-edu 的三张表 + 两条链**，先定语义 |
| A12 AI 教研 | ① 课程体系生成（课程 → 章节 → 知识点 → 习题草稿一次性生成并落库，参考 kg-edu 的 prompt 链路 + KH 的 `AiGenerationJob` 任务模式）；② 能力图谱生成；③ 课件 PPTX（需引 `DocumentFormat.OpenXml` Presentation，KH 现只引了 ClosedXML）；④ 文件问答；⑤ TTS |
| A13 知识点导入 | XMind / OPML 解析 → `KnowledgeResource`（KH 已有 Excel 习题导入，可复用导入结果 DTO 模式） |
| A14 教师级门户 | 仅在"名师主页/课程主页个性化"确有需求时做 |
| — 图谱 | **不做**。`KnowledgeGraph` 模块的 `GetRecommendedPathAsync`（抛 `NotImplementedException`）与 `BuildGraphAsync`（TODO）建议在代码里标注「暂不实现」，避免被当成缺陷反复提。可视化沿用 KH 现有 `graph-viewer`；知识点关系维护放到知识点管理页（表格/树），不做 6 个图谱页面 |

### 明确不移植（7 项）

| 项 | 理由 |
|---|---|
| `knowledge/homework.ex` 作业 | KH `Assignments` 已远超（细则/迟交/重交/导出/未提交名单） |
| `knowledge/exam.ex` 组卷 | KH 规则组卷 + 目标总分 + 打乱 + 多章节更强 |
| `exercise/question/exercise_options` 习题模型 | KH 10 题型 + 权重 + 章节多对多更强 |
| `knowledge/question.ex` + `question_connection` 问题库 | 语义与习题重叠，kg-edu 内部也偏"遗留"（页面是"问题管理 + 关联"），价值不明 |
| `micro_major*` 8 张表 | KH 微专业已统一课程 + 证书申请/核验/审批，复制课程是倒退 |
| `proctor_*` + `face_check_*` 防作弊 | 合规与成本风险高，线上考试在 KH 不是主场景 |
| `email/`（邮件答疑）+ `research/`（虚拟教研室聊天）+ `ai/plugin_runner` | KH 有 AI 助教/白板协作/教学智能体覆盖，且邮件答疑在国内教学场景使用率低 |

---

## 4. 三个必须先解决的架构障碍

### 4.1 KH 并存两套知识点体系（最关键的坑）

| 体系 | 表 | 用途 | 掌握度 |
|---|---|---|---|
| ① 课程知识点树 | `Courses.KnowledgeResource`（自引用 `ParentId`） | 章节内容组织、`Exercise.KnowledgeResourceId` | `Learning.KnowledgeMastery`（计数：≥3 次且 ≥80%） |
| ② 智学分析知识点 | `LearningAnalytics.KnowledgePoint` + `ExerciseKnowledgePoint`（权重） | 纸卷 OCR 采集 → 知识点 → 掌握度 → 报告/推荐 | `StudentKpMastery`（`MasteryCalculator`：Beta + 半衰期） |

两者**代码里没有任何交叉引用**（`KnowledgePointExtractor`/`KnowledgePointAppService` 完全不碰 `KnowledgeResource`）。

后果：如果把 kg-edu 的掌握度/推荐/能力达成度直接移植过来，会出现**第三套**知识点 + 第三套掌握度，
学生端会出现三个互相矛盾的"我哪些知识点薄弱"。

**建议方案（最小改动）**：
1. 以 `Courses.KnowledgeResource` 为**唯一的知识点主数据**（它已有树、难度、重要性、embedding）。
2. 给 `LearningAnalytics.KnowledgePoint` 增加可空 `KnowledgeResourceId`，作为「试卷侧别名/映射」，
   由 `KnowledgePointExtractor` 落库时优先匹配同名/向量最近的 `KnowledgeResource`，人工在「知识点治理」页确认。
3. 掌握度只保留 `MasteryCalculator` 一套算法，`Learning.KnowledgeMastery` 改为它的**视图/物化缓存**
   （观测源：`StudentExerciseRecord` + `StudentAnswer`），`KnowledgeMastery.RecordPractice` 的
   "3 次 80%" 逻辑删除。
4. 推荐、能力达成度、学生端学情全部读 `MasteryCalculator` 结果，不再各算一套。

### 4.2 没有课程视频实体，且 Mux 不可用

kg-edu 视频全链路依赖 Mux（`upload_id`/`asset_id`/`playback_id`）。KH 侧应改为：
`Resource`（已有存储/OSS/转换/缩略图）+ 播放直链或 HLS 切片。**不要移植 Mux 相关字段**，
`CourseVideo` 只保留 `ResourceId`（+ 可选 `ExternalPlaybackUrl`）。转码/切片若需要，
用 `Resources/Conversion` 现有能力或后续接 ffmpeg，而不是引进第三方 SaaS。

### 4.3 token 免登录课堂互动 vs KH 的租户/门户约束

kg-edu 的签到/讨论/小组任务/练习都是 `/student/<feature>/:tenantSchema/:token` 这类**免登录链接**，
学生扫码进入。KH 侧要注意三件事：

1. **路由**：不要挂 `authGuard`，也别挂 `studentPortalGuard`（它的语义是"学生门户公开可浏览"，
   叠 `authGuard` 会互相抵消——项目里已有这个坑的注释）。
2. **租户解析**：URL 里的租户必须走 `CurrentTenant.Change(...)` 或 `__tenant` 头，
   且要防 `__tenant` **残留 cookie** 污染（项目已有 `SchoolDomainTenantResolveContributor` 与
   `ISchoolDomainContext` 的正确写法，照抄 `TenantListController` / `PortalAppService` 的判定）。
3. **审核**：这些接口都是匿名 POST，ABP 会拦 antiforgery → 必须 `[AllowAnonymous]` + `[IgnoreAntiforgeryToken]`
   （项目里有同类先例：`SearchAppService`）。

---

## 5. 建议的实施顺序

```
第 1 批（P0，1.5 周）
  4.1 知识点体系统一（先决策 + 落映射字段）
  → A1 课程教学信息 → A2 教材 → P0-3 知识点树补全 → P0-4 掌握度打通
第 2 批（P1 上半，2 周）
  A5 讨论答疑 → A4 签到 → A7 课堂练习（三者共用 token 免登录基础设施，一起做省一半）
第 3 批（P1 下半，2 周）
  A3 课程视频 + 时间轴切片 → A10 打印 → A6 小组任务
第 4 批（P1 尾 + P2 起，2~3 周）
  A8 行为日志/画像 → A9 推荐落地
  → A11 能力体系（与 DoubleHigh 打通）→ A12 AI 教研三项
```

理由：**token 免登录基础设施**（讨论/签到/练习/小组任务）是四件套的共同底座，必须一次建好；
「掌握度 → 行为日志 → 推荐 → 能力达成度」是数据依赖链，顺序不能颠倒；
课程视频独立且成本最高，放在基础设施稳定后。

---

## 6. 验收口径（怎么证明移植成功）

| 项 | 验收标准 |
|---|---|
| A1/A2 | 学生端课程详情 6 个 Tab 都有真实内容；门户课程页能展示教学目标/教材并下载附件 |
| P0-3/P0-4 | 同一套知识点树既能在章节管理里维护，又能驱动掌握度；`KnowledgePoint` 与 `KnowledgeResource` 映射可查、可人工确认 |
| A5/A4/A7 | 未登录扫码 → 落到正确租户 → 提交成功；同一 token 不被跨租户复用；教师在后台实时看到统计 |
| A3 | 视频打完点后，学生播放到该秒数能跳转知识点并直接练该知识点的题 |
| A8/A9 | 学生完成一次练习后：掌握度变化（Beta 算法）→ 生成 ≥1 条带理由的推荐 → 推荐可标记完成/忽略 |
| A10 | 试卷导出 PDF 与屏幕内容一致，中文字体不乱（Gotenberg 镜像需带中文字体） |
| A11 | 能力达成度 = 关联知识点掌握度加权，且与 `DoubleHigh` 指标取数口径一致 |

---

## 7. 一句话给决策者

> 移植 kg-edu 的课程功能，**不是搬表，而是补三段缺失的"教学过程"**：
> 课程的教学化表达（P0）、课堂内外的互动与练习（P1）、行为—掌握—推荐—能力的数据链（P1/P2）。
> 只要先把「KH 现有两套知识点合并成一套」这件事做掉，后面 12 项里 8 项都能复用 KH 已有实现，
> 真正需要新写的只有 **课程视频 + 时间轴切片**、**token 免登录互动四件套**、**行为日志/画像** 三块。
