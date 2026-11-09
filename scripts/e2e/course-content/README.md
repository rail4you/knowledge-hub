# 课程内容闭环 E2E（习题 / 试卷 / 作业 / 学生端）

在**已有课程**上补齐三类教学内容，并走通学生端的完整链路，最后由教师批阅、学生看反馈。

```
scripts/e2e/
├── lib/                    # 共享工具（jqx.py JSON 查询、mkpdf.py 生成测试 PDF）
├── resource-library-loop/  # 资源库闭环（建库 → 建课 → 传资源 → 章节 → 互转 → 准入 → 学生学习）
└── course-content/         # 本目录：习题 / 试卷 / 作业 / 学生端
    ├── demo.sh             # 9 步闭环
    └── cleanup.sh          # 清理生成的习题 / 试卷 / 作业
```

---

## 1. 前置条件

先跑通 `resource-library-loop/run.sh`（它会建好课程、导入学生 zmq）。
本脚本默认自动找老师（wwq）名下标题以 `Python 数据分析` 开头的课程。

## 2. 用法

```bash
cd scripts/e2e/course-content

# 本地
./demo.sh

# iketong 生产
API_BASE=http://127.0.0.1:4200 \
OAUTH_SCOPE="offline_access openid profile email phone KnowledgeHub" \
TAG=prod-demo ./demo.sh

# 指定课程
COURSE_ID=<uuid> ./demo.sh

# 清理（demo 是追加式的，重复跑会堆积）
TAG=prod-demo ./cleanup.sh
```

> 生产环境在服务器上执行（宿主到容器），或 `API_BASE=https://www.iketong.com` 从外网直连。

## 3. 覆盖的 9 步

| # | 谁 | 动作 | 接口 |
|---|---|---|---|
| 0 | — | 解析租户 + 三账号登录 | `/api/public/tenants`、`/connect/token` |
| 1 | 老师 | 定位课程与章节 | `GET /api/app/course`、`/chapter/chapter-tree/{id}` |
| 2 | 老师 | 建 4 道习题（单选 ×2 / 判断 / 填空） | `POST /api/app/exercise` |
| 3 | 老师 | 手工组卷（4 题，45 分钟） | `POST /api/app/exam` |
| 4 | 老师 | 建作业（3 条评分细则）并发布 | `POST /api/app/assignment` → `/{id}/publish` |
| 5 | 学生 | 看习题 → 作答 → 自动判题 → 章节进度 | `exercise/by-course`、`student-exercise-record/save-or-update-record`、`chapter-progress` |
| 6 | 学生 | 看试卷 → 开考 → 交卷 → 看结果 | `exam/my-exams`、`/start`、`/submit`、`/my-result` |
| 7 | 学生 | 看作业 → 上传附件 → 提交 | `assignment/my-assignments`、`upload-file`、`/submit` |
| 8 | 老师 | 看提交记录 → 批阅 | `assignment/submissions`、`/grade/{submissionId}` |
| 9 | 学生 | 看成绩与反馈 | `assignment/my-feedback` |

---

## 4. 回归时必知的规则（都在这套脚本里踩过）

1. **习题「选项」是 JSON 字符串**
   `options` 存的是 `'["A项","B项","C项","D项"]'` 这样的 JSON 数组字符串，不是换行文本。
   只有单选/多选需要；判断、填空传 `null`。

2. **客观题答案格式**
   - 单选 / 多选：`A`、`B`…（字母；后端 `NormalizeChoiceToken` 会把 `1`→`A`、`A.`→`A`）
   - 判断：`true` / `false`（也兼容 `对` / `错` / `正确` / `错误`）
   - 填空：和标准答案 `trim()` + 忽略大小写全等比较

3. **交卷 ≠ 出总分**
   `POST /exam/submit/{studentExamId}` 只逐题判客观题（`answers[].score` 有值、
   `status = 2 Submitted`），**`totalScore` 仍是 null**。
   总分由 `POST /exam/auto-grade/{studentExamId}` 汇总（`status = 3 Graded`）。
   → 学生端要看到总分，教师/系统必须触发一次自动判分。

4. **`upload-file` 是「加分项」，不是唯一路径**
   作业附件走 `POST /assignment/upload-file/{assignmentId}?purpose=submission`（multipart，
   字段名 `file`），返回 `AssignmentFileDto`，再把 `id` 放进 `submit` 的 `fileIds`。

5. **作业评分细则字段是 `maxScore` 不是 `score`**
   `criteria[]` 用 `{ name, maxScore, sortOrder, description }`；
   批阅入参 `GradeAssignmentSubmissionDto` 用 `{ scores[], score, comment }`（总分 key 是 `score`）。

6. **提交记录列表的主键叫 `submissionId`**（不是 `id`），别拿 `id` 去找批阅接口。

7. **已有作答记录的试卷不能删**
   `DELETE /exam/{id}` 会返回 403「该试卷已有学生作答记录，不能删除」。
   `cleanup.sh` 只删得掉没被作答过的卷；本地开发环境要彻底清需直接清库
   （`AppStudentAnswers` → `AppStudentExams` → `AppExamExercises` → `AppExams`）。

8. **学生端「我的作业」默认选第一门课**
   `student/assignments` 取 `learning/my-courses` 的第一门，不是当前正在学的课。
   用深链 `?courseId=<id>` 可直达指定课程的作业列表。

---

## 5. 生产验证结果（iketong / www.iketong.com）

以 `TAG=prod-demo` 在 Python 课程（`3a24cfce-4d73-36a2-e0fe-ca55f77bbda5`）上跑通：

| 项 | 结果 |
|---|---|
| 习题 | 4 道（单选 2 / 判断 1 / 填空 1），分值 5+5+10+5 |
| 试卷 | 「Python 数据分析 · 阶段测验」4 题、45 分钟；学生作答状态 3(Graded)、25/25 分、通过 |
| 作业 | 「Pandas 数据清洗实战作业」100 分、3 条评分细则；学生已提交 → 教师批阅 90 分 |
| 学生端 | 学习页「习题 3/4、进度 75%」；作业页「已批阅 1、90/100、查看反馈」；试卷页正确拦截重复作答 |
