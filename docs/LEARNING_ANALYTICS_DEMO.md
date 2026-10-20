# 智学分析 · 示例数据与端到端验证

> 目标：在 **qidi 租户** 里预置一套完整示例数据，开箱即可走通「纸质试卷 → 薄弱点 → 教学诊断 → 课程联动 → 专项练习 → 掌握度回写 → 报告导出/留档」全链路。
>
> 相关文档：`docs/LEARNING_ANALYTICS_PLAN.md`（方案与进度）

---

## 1. 示例数据一览

播种器：`src/KnowledgeHub.Domain/LearningAnalytics/LearningAnalyticsDemoDataSeeder.cs`
执行时机：`dotnet run --project src/KnowledgeHub.DbMigrator`（或应用启动时的数据播种阶段），**仅 qidi 租户**执行，可幂等重跑（同名课程存在则跳过）。

| 类别 | 内容 |
| --- | --- |
| 前置课程 | **计算机网络技术基础**（Published / 2026春 / 4 学分 64 学时 / 推荐课程） |
| 章节（=知识点初始化来源） | `OSI 与 TCP/IP 分层模型`、`IP 地址与子网划分`、`TCP 与 UDP 协议`、`交换机与路由器组网` |
| 习题 | 12 道（单选 / 判断 / 填空 / 计算 / 简答，5~10 分，难度 1~3，含解析） |
| 后续课程 | **网络安全技术**（3 章 6 题：网络安全威胁与防护 / 对称与非对称加密 / 防火墙与入侵检测） |
| 课程联动 | 计算机网络技术基础 → 网络安全技术（已确认的 `CourseLink`） |
| 学生账号 | `demo_student`（张晨，学号 2023010101）、`demo_student2`（李思雨，学号 2023010102），密码均为 `1q2w3E*`，角色 Student |
| 选课 | 张晨：前置课程（学习中，35%）+ 后续课程（已选课）；李思雨：前置课程（学习中，45%） |
| 试卷批次 | `2026春 期末考试（示例）`：2 份试卷、已全部校对、班级均分 **44.5**、已推送 2 人 |
| 试卷明细 | 每题得分 + 匹配到真实习题（`Matched`），并按「确认后」写入纸质作答记录（`AnswerRecord`，Source=Paper） |
| 在线作答 | 张晨前 4 题在线记录（2 对 2 错），用于验证「在线 + 纸质」合并算掌握度 |
| 个人报告 | 2 条 `AnalysisReport`（规则文案，不调用模型）+ 2 条站内消息（`PaperWeakness`，未读） |
| 知识点 | 首次访问智学分析接口时由 `KnowledgePointIndexer` 按章节自动生成（4 个），并按章节树把 12 道题映射上去 |
| 归档 | 报告归档默认走 **OSS**（若已配置），否则本地 `App_Data/analysis-reports` |

**示例分数设计**：张晨偏弱（总分 33，4 个知识点全部低于 75%），李思雨中等（总分 56），便于呈现「薄弱点、重点关注学生、教学诊断、分层建议」。

---

## 2. 登录与入口

| 角色 | 账号 | 密码 | 租户 | 入口 |
| --- | --- | --- | --- | --- |
| 教师 | `qa-teacher` | `1q2w3E*` | qidi | 侧边栏「专业和课程管理 → 学情分析 / 知识点管理」 |
| 学生（张晨） | `demo_student` | `1q2w3E*` | qidi | 侧边栏「智学分析」→「专项练习」 |
| 学生（李思雨） | `demo_student2` | `1q2w3E*` | qidi | 同上 |

前端路由：

| 路由 | 页面 |
| --- | --- |
| `/learning/analysis` | 教师端：纸质试卷分析 / 班级薄弱点 / 教学成果分析 / 课程联动 |
| `/learning/knowledge-points` | 教师端：知识点管理（列表 / 图谱 / 合并 / 拆分 / 习题关联 / 判定线） |
| `/student/analysis` | 学生端：我的薄弱点 / 学习路径 / 消息 / 专项练习 |
| `/student/practice?courseId=&knowledgePointId=` | 学生端：专项练习作答页 |

---

## 3. 端到端验证步骤与结果

> 以下为本机实测（2026-10，PostgreSQL + 本机 OSS 已配置），API 走 `https://localhost:44305`，租户 `qidi`。

### 3.1 教师端（qa-teacher）

| 步骤 | 操作 | 实测结果 |
| --- | --- | --- |
| 1 | 打开「学情分析 → 纸质试卷分析」 | 批次「2026春 期末考试（示例）」：2 份、均分 44.5、待校对 0、已推送 2 |
| 2 | 打开「班级薄弱点」 | 4 个知识点掌握率 0.51~0.56（全部低于 75%）；重点关注：张晨 33、李思雨 56，均已推送 ✅ |
| 3 | 打开「教学成果分析」 | 4 个章节得分率 0.60~0.73，未达标 4；诊断建议按规则文案输出（导出时不调模型） |
| 4 | 打开「课程联动设置」 | 路径「计算机网络技术基础（当前课程）→ 网络安全技术（后续课程）」；关联列表 1 条 |
| 5 | 打开「知识点管理」 | 4 个知识点（章节初始化），习题数 3/4/3/2；可切「图谱」查看分层；可 AI 抽取细化、合并/拆分、配判定线 |
| 6 | 点击「导出报告（Word）」 | 200，约 6.2KB docx（班级学情 + 教学诊断 + 重点关注 + 后续建议） |
| 7 | 点击「生成班级报告并归档」 | 成功，`storage=oss`，文件名 `计算机网络技术基础_2026春 期末考试（示例）_班级学情报告_20261020.docx` |

### 3.2 学生端（demo_student / 张晨）

| 步骤 | 操作 | 实测结果 |
| --- | --- | --- |
| 1 | 打开「智学分析 → 我的薄弱点」 | 得分 33 / 班级均分 44.5，排名 2/2；来源「来自纸质试卷 · 老师已确认」；4 个知识点掌握率 0.50 / 0.45 / 0.52 / 0.51 |
| 2 | 「学习路径」 | 计算机网络技术基础（学习中）→ 网络安全技术（即将开课，列出需先补的前置知识点）；衔接复习包 4 项 |
| 3 | 「消息」 | 1 条未读「【成绩分析】2026春 期末考试（示例） 薄弱点报告已生成」；点击后未读数变 0 ✅ |
| 4 | 「专项练习」→ 开始 | 进入 `/student/practice`：推荐知识点「IP 地址与子网划分」，3~4 道题，带推荐理由 |
| 5 | 逐题作答 → 提交 | 每题即时判定（单选/判断/填空自动判分，主观题「待老师批改」）；答完展示正确率 + 知识点掌握率 |
| 6 | 返回智学分析 | 「IP 地址与子网划分」掌握率由 **0.45 → 0.58**（练习回写生效）✅ |

> 说明：判断题/单选/多选/填空由 `AutoGrade` 自动判分；简答、论述、计算等主观题 `IsCorrect=null`，需老师批改（与原有在线练习一致）。

---

## 4. 重新播种 / 重置示例数据

### 4.1 正常重跑（幂等）

```bash
dotnet run --project src/KnowledgeHub.DbMigrator
```
同名课程已存在时跳过，不会重复插入。

### 4.2 彻底重置

先删除示例课程与其关联数据，再重跑播种器。以下 SQL 在 `KnowledgeHub` 库执行（租户 `qidi`）：

```sql
BEGIN;
CREATE TEMP TABLE tmp_courses AS
  SELECT "Id" FROM "AppCourses"
   WHERE "TenantId" = (SELECT "Id" FROM "AbpTenants" WHERE "Name"='qidi')
     AND "Title" IN ('计算机网络技术基础','网络安全技术');
CREATE TEMP TABLE tmp_kps AS SELECT "Id" FROM "AppKnowledgePoints" WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
CREATE TEMP TABLE tmp_ex AS SELECT "Id" FROM "AppExercises" WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
CREATE TEMP TABLE tmp_batches AS SELECT "Id" FROM "AppPaperBatches" WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);

DELETE FROM "AppPaperItemScores"        WHERE "BatchId" IN (SELECT "Id" FROM tmp_batches);
DELETE FROM "AppPaperScripts"           WHERE "BatchId" IN (SELECT "Id" FROM tmp_batches);
DELETE FROM "AppPaperUploadImages"      WHERE "BatchId" IN (SELECT "Id" FROM tmp_batches);
DELETE FROM "AppPaperBatches"           WHERE "Id" IN (SELECT "Id" FROM tmp_batches);
DELETE FROM "AppAnswerRecords"          WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppStudentExerciseRecords" WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppStudentKpMasteries"     WHERE "KnowledgePointId" IN (SELECT "Id" FROM tmp_kps);
DELETE FROM "AppKpPrerequisites"        WHERE "FromKpId" IN (SELECT "Id" FROM tmp_kps) OR "ToKpId" IN (SELECT "Id" FROM tmp_kps);
DELETE FROM "AppReviewPackageItems"     WHERE "KnowledgePointId" IN (SELECT "Id" FROM tmp_kps);
DELETE FROM "AppReviewPackages"         WHERE "UpcomingCourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppAnalysisReports"        WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppStudentNotifications"   WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppTeachingInsights"       WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppAnalysisReportFiles"    WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppMasteryThresholds"      WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppCourseLinks"            WHERE "PredecessorCourseId" IN (SELECT "Id" FROM tmp_courses) OR "SuccessorCourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppExerciseKnowledgePoints" WHERE "ExerciseId" IN (SELECT "Id" FROM tmp_ex);
DELETE FROM "AppKnowledgePoints"        WHERE "Id" IN (SELECT "Id" FROM tmp_kps);
DELETE FROM "AppStudentCourses"         WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppExercises"              WHERE "Id" IN (SELECT "Id" FROM tmp_ex);
DELETE FROM "AppChapters"               WHERE "CourseId" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AppCourses"                WHERE "Id" IN (SELECT "Id" FROM tmp_courses);
DELETE FROM "AbpUserRoles"              WHERE "UserId" IN (SELECT "Id" FROM "AbpUsers" WHERE "UserName" IN ('demo_student','demo_student2'));
DELETE FROM "AbpUsers"                  WHERE "UserName" IN ('demo_student','demo_student2');
COMMIT;
```

归档文件同时清理：OSS 前缀 `{UploadPath}/analysis-reports/`；本地 `src/KnowledgeHub.HttpApi.Host/App_Data/analysis-reports/`。

### 4.3 换租户播种

`appsettings.json`：

```jsonc
"LearningAnalytics": { "DemoTenantName": "qidi" }
```
改成目标租户名后重跑 DbMigrator 即可（该租户需已存在）。

---

## 5. 关键配置（示例数据相关）

```jsonc
"LearningAnalytics": {
  "DemoTenantName": "qidi",          // 播种目标租户
  "OcrProvider": "qwen",             // qwen | mock（未配置 Key 自动降级 mock）
  "OcrModel": "qwen-vl-ocr",
  "ReportRoot": "",                   // 归档本地目录（OSS 未配置时使用）
  "RecWeights": { "Content": 0.5, "Collab": 0.3, "Fit": 0.2 },
  "NightlyTrainEnabled": true,
  "PrerequisiteMiningEnabled": true,
  "PreCourseReviewPushEnabled": false // 自动推送默认关闭，可用「立即推送衔接复习包」
}
```

- **OSS**：管理页「对象存储设置」维护 AK/SK/Bucket（租户级），保存即生效；未配置时报告归档自动落本地磁盘。
- **Qwen Key**：「AI 使用管理」维护，OCR / 知识点抽取 / 教学建议 / 向量共用同一 Key。

---

## 5.1 额外验证：推荐权重一键应用 / 图片 OSS 直传

| 用例 | 操作 | 结果 |
| --- | --- | --- |
| 权重应用 | 教学成果分析 →「推荐效果评估」→「应用建议权重」 | 当前 0.500 → 纯内容 0.750（精度 0.300），写入 Setting 并立即生效 |
| 权重读回 | `GET /api/app/learning-analytics/recommendation-weights` | `{content:1,collab:0,fit:0, source:"setting"}`（复位后 0.5/0.3/0.2） |
| 图片 OSS 直传 | 上传试卷图片（OSS 已配置） | 先传 `/api/oss-upload/image` 得到公网 URL，再只传 URL；页面提示「已启用 OSS 直传」 |
| URL 直给 OCR | 触发识别 | 学号 2023010101、6 题得分（5/4/5/5/5/6）、全部匹配题库，学生自动匹配「张晨」 |
| PDF 直传（需开启） | `LearningAnalytics:PdfDirectEnabled=true` 后上传 PDF | 整份 PDF 传 OSS → `qwen3.5-ocr` Responses API 解析；实测题号规整、得分按行抽取、全部匹配，**置信度 0.6（强制校对）** |
| 教师过滤 | 课程选择器勾选「只看我教的课程」 | 2 门示例课程（`?teacherId=`），未勾选 7 门；名下无课程时自动回退并提示 |

> OSS 未配置时：上传自动回退 Base64，功能不受影响（仅请求体较大）。

## 6. 本次改动清单（示例数据 + 练习闭环）

| 类型 | 文件 |
| --- | --- |
| 播种器 | `src/KnowledgeHub.Domain/LearningAnalytics/LearningAnalyticsDemoDataSeeder.cs`（新增） |
| 练习后端 | `src/KnowledgeHub.Application/LearningAnalytics/PracticeAppService.cs`（新增，`/api/app/practice`） |
| 练习契约 | `src/KnowledgeHub.Application.Contracts/LearningAnalytics/{IPracticeAppService.cs, Dtos/PracticeDtos.cs}`（新增） |
| 练习前端 | `angular/src/app/student/practice/*`（新增页面）、`learning-analytics/services/practice.service.ts`（新增） |
| 入口 | `student.routes.ts` 新增 `/student/practice`；智学分析「专项练习」按钮改为跳转练习页 |
| 文档 | 本文件、`docs/LEARNING_ANALYTICS_PLAN.md`（阶段 L） |

**验证记录**：`dotnet build KnowledgeHub.slnx` ✅、`ng build` ✅、播种日志 5 条、上述 13 个端到端步骤全部通过。

---

## 7. 当前仍待完成（P1 剩余）

1. **PDF 直传调优**：能力已接通但默认关闭（qwen3.5-ocr 对合成 PDF 的转写质量低于图片路径）；
   接入真实扫描件后重新评估是否默认开启，并在 UI 明确提示「PDF 直传需重点校对」。
2. **班级维度**：当前以「课程 + 试卷批次」近似班级，若要按「行政班/教学班」统计，需要引入班级实体与选课映射。

（P2：PDF 报告、图谱交互增强、判定线批量维护、跨课程合并归属、细粒度权限、线上推荐监控。）
