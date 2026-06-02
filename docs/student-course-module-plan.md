# 学生端课程模块 - 开发计划评估

> 参考：[ICVE 课程中心](https://zyk.icve.com.cn) · [ICVE 课程详情](https://zyk.icve.com.cn/courseDetailed)
> 设计风格：复用现有学生端 ds 设计系统（与资源/资讯/收藏/任务保持一致）

---

## 一、需求拆解

### 1.1 业务背景
- **管理端**：教师/管理员可以创建课程、维护章节、绑定资源与习题、向学生分配选课
- **学生端**：登录学生进入"课程中心"，可以浏览已选课/全部课程、查看详情、进入学习
- **学习闭环**：学生选课 → 学习章节 → 观看资源 → 提交习题 → 记录学习数据 → 形成统计

### 1.2 用户旅程
```
[学生登录] → [课程中心] → [浏览/搜索/筛选] → [课程详情]
  ├─ 选课 → 进入学习
  └─ 已选课 → 继续学习 / 查看章节 / 知识图谱 / 我的记录
```

### 1.3 关键功能模块
| 模块 | 路由 | 数据来源 | 优先级 |
|------|------|----------|--------|
| 课程中心（列表） | `/student/courses` | `CourseService.getPublished` | P0 |
| 课程详情 | `/student/courses/:id` | `CourseService.getDetail` + `ChapterService.getChapterTree` | P0 |
| 我的学习（统计） | `/student/my-learning` | `LearningService.getDashboard` + `getMyCourses` | P0 |
| 知识图谱（详情内嵌） | `/student/courses/:id/graph` | 复用 `learning/knowledge-graph/*` | P1 |
| 学习/做题入口 | 详情页内嵌按钮 | 复用 `learning/exercise-learning` | P1 |
| 学习进度上报 | 详情页内联 | `LearningService.recordProgress` | P1（先预留） |

---

## 二、前端设计规划（Phase 1）

### 2.1 复用资源盘点
- ✅ **设计系统**：`_student-design.scss`（颜色/阴影/圆角/容器/标题/截断 mixin）
- ✅ **服务**：CourseService / ChapterService / LearningService / StudentExerciseRecordService
- ✅ **数据模型**：`CourseDto` / `CourseDetailDto` / `ChapterDto` / `KnowledgeResourceDto` / `LearningDashboardDto` / `StudentCourseDto`
- ✅ **后台服务**：`getPublished` / `getDetail` / `getChapterTree` / `getDashboard` / `getMyCourses`
- ✅ **知识图谱组件**：`learning/knowledge-graph/*-graph.component.ts`（树形/脑图/网络图）

### 2.2 三大页面布局

#### 页面 A：课程中心（`/student/courses`）
```
┌─────────────────────────────────────────────────────┐
│ Hero 区（轮播 + 4 宫格数据卡）                          │
├─────────────────────────────────────────────────────┤
│ 搜索 pill + 分类 chips（专业/学期/难度/状态）           │
├─────────────────────────────────────────────────────┤
│ 主区（1fr）                  │  侧栏（320px sticky）     │
│  - 我的课程（含进度）          │  - 学习计划              │
│  - 全部课程（grid 3 列）       │  - 本周热门              │
│                              │  - 学习排行（mock）        │
└─────────────────────────────────────────────────────┘
```

#### 页面 B：课程详情（`/student/courses/:id`）
```
┌─────────────────────────────────────────────────────┐
│ 面包屑 + 返回                                          │
├─────────────────────────────────────────────────────┤
│ Hero 卡（封面 + 标题 + meta + 进度 + 操作按钮）         │
├─────────────────────────────────────────────────────┤
│ Tabs：章节 | 知识图谱 | 相关课程 | 资源                 │
├─────────────────────────────────────────────────────┤
│ 主区（1fr）                  │  侧栏（320px sticky）     │
│  - 章节树（递归展开）         │  - 课程信息              │
│  - 知识图谱嵌入              │  - 我的进度              │
│  - 相关课程列表              │  - 教师信息              │
│                              │  - 同专业课程            │
└─────────────────────────────────────────────────────┘
```

#### 页面 C：我的学习（`/student/my-learning`）
```
┌─────────────────────────────────────────────────────┐
│ Hero 区（4 宫格学习数据 + 学习曲线占位）                │
├─────────────────────────────────────────────────────┤
│ 主区（1fr）                  │  侧栏（320px sticky）     │
│  - 进行中课程（带进度）       │  - 知识掌握度（雷达图占位）│
│  - 已完成课程                │  - 最近学习              │
│  - 学习记录表（章节/习题）    │  - 学习目标              │
└─────────────────────────────────────────────────────┘
```

### 2.3 复用 vs 新建对照表

| 组件 | 来源 | 处理方式 |
|------|------|----------|
| 品牌栏 + Tab 栏 | student-layout | **直接复用**，仅在 tab-bar 增加"课程中心" |
| 容器/标题/截断 mixin | `_student-design.scss` | **直接复用** |
| Hero 轮播 + 数据卡 | student-news/resources | **复用结构**，换主题色（紫/青）和文案 |
| 章节树 | learning/course-detail | **直接复用** `treeNode` 渲染逻辑 |
| 知识图谱 | learning/knowledge-graph/* | **直接复用**（作为详情页 Tab 嵌入） |
| 课程卡片 | student-resources news-card | **复用** 风格，新增进度条/选课按钮 |
| 学习统计表 | learning/learning-statistics | **简化复用**（去掉教师视角的过滤维度） |

---

## 三、Phase 2 后端扩展（评估 · 暂不实现）

以下为可能需要的新增模型/接口（已记录但不立即开发）：

| 需求 | 缺失项 | 备注 |
|------|--------|------|
| 章节资源预览 | 章节 → 资源列表 API | 当前 `ChapterDto.knowledgeResources` 已包含 |
| 学习位置 | `lastPosition` 解析 | `LearningProgressDto` 已有字段 |
| 学习轨迹 | 时间序列 | 可由 `RecordProgress` + 时间戳聚合 |
| 学生排行 | 同班同课排行 | 需要新增 `getClassRank` 接口 |
| 知识点掌握度 | 雷达图 | `getKnowledgeMastery` 已有 |

> **结论**：当前后端数据已能支撑 P0 全部功能。Phase 1 全部用现有数据开发，Phase 2 视使用反馈再扩展。

---

## 四、开发任务清单（Phase 1）

### Step 1：导航扩展
- [x] `student-layout.component.html` 添加"课程中心" + "我的学习" Tab
- [x] `student-layout.component.ts` 添加 nav 数据
- [x] `student.routes.ts` 添加 4 条新路由（含详情与重定向）

### Step 2：课程中心（列表）页
- [x] `student-courses/student-courses.component.ts` — 信号 + 服务
- [x] `student-courses/student-courses.component.html` — Hero + Chips + Grid + Sidebar
- [x] `student-courses/student-courses.component.scss` — 完整设计

### Step 3：课程详情页
- [x] `student-course-detail/student-course-detail.component.ts` — 含章节树与知识图谱嵌入
- [x] `student-course-detail/student-course-detail.component.html` — Hero + Tabs + Sidebar
- [x] `student-course-detail/student-course-detail.component.scss` — 完整设计

### Step 4：我的学习页
- [x] `student-my-learning/student-my-learning.component.ts`
- [x] `student-my-learning/student-my-learning.component.html`
- [x] `student-my-learning/student-my-learning.component.scss`

### Step 5：构建验证
- [x] `ng build --configuration development` 通过
- [x] 路由 /student/courses /student/courses/:id /student/my-learning 可达

---

## 五、Phase 2 - 未完成任务（需继续开发）

### 5.1 现状分析

现有学生端三个页面只提供了“入口”和“统计”，但**学生还未真正进入学习闭环**：
- 课程详情页的“开始练习”按钮目前路由到 `/learning/exercise-learning/:id`（教师页面）
- “继续学习”按钮未实现跳转目标
- 学生无法查看章节的知识点资源、做题、查看提交记录
- 我的学习页面的“最近提交记录”只取自`getRecordsByCourse`（需要先有课），并没有“跨课程最近记录”API

### 5.2 待开发任务

#### 任务 1：后端 - 新增「我的最近提交」API
- 接口：`GET /api/app/student-exercise-record/my-recent`
- 位置：`IStudentExerciseRecordAppService`
- 作用：返回当前学生在所有课程上最近 N 条记录，供 my-learning 页面使用
- 输入：`GetMyRecentRecordsInput { skipCount, maxResultCount, courseId? }`
- 输出：`PagedResultDto<StudentExerciseRecordDto>`

#### 任务 2：前端 - 学生学习页面（新页面）
- 路由：`/student/courses/:id/learn/:chapterId?`
- 布局：
  - 左侧：章节树（点击切换），显示当前选中章节
  - 右侧：Tab 区域
    - 资源 Tab：`KnowledgeResource[]`（本章节），点击查看内容
    - 习题 Tab：`Exercise[]`（本章节），单选/多选/判断/填空题答题表单，提交后查看结果
    - 提交记录 Tab：`StudentExerciseRecord[]`（本章节），可查看之前的答案
- 行为：
  - 进入页面/切换章节时调用 `LearningService.recordProgress` 上报进度
  - 提交答案调用 `StudentExerciseRecordService.saveOrUpdateRecord`
  - 查看答案调用 `markAnswerViewed`

#### 任务 3：前端 - 修复课程详情页按钮路由
- “开始练习”、“继续学习”按钮改为跳转到新学习页面
- “查看知识图谱”按钮保留当前 tab 切换

#### 任务 4：前端 - my-learning 页面接入真实最近记录
- 使用新增的 `getMyRecentRecords` API
- 移除现在“以第一门课程的记录作占位”的逻辑
- 添加 my-learning 记录的空状态和加载态

### 5.3 任务优先级

| 优先级 | 任务 | 原因 |
|--------|------|------|
| P0 | 任务 1（后端） | 后续前端任务依赖 |
| P0 | 任务 2（学习页面） | 完整学习闭环的核心 |
| P0 | 任务 3（按钮路由） | 与任务 2 同步发布 |
| P1 | 任务 4（my-learning 真实记录） | 优化体验 |

### 5.4 预期文件

后端：
- `src/KnowledgeHub.Application.Contracts/Learning/IStudentExerciseRecordAppService.cs`（修改）
- `src/KnowledgeHub.Application/Learning/StudentExerciseRecordAppService.cs`（修改）
- `src/KnowledgeHub.Application.Contracts/Learning/Dtos/*.cs`（可能需新增 DTO）

前端：
- `angular/src/app/student/course-learn/student-course-learn.component.{ts,html,scss}`（新文件）
- `angular/src/app/student/student.routes.ts`（修改，新增路由）
- `angular/src/app/student/course-detail/student-course-detail.component.ts`（修改）
- `angular/src/app/student/my-learning/student-my-learning.component.ts`（修改）

---

## 五、风险与对策

| 风险 | 对策 |
|------|------|
| LeptonX 主题与学生端布局冲突 | 复用 student-layout 已有方案；详情页内容区用 `student-container` 限定 |
| 课程/章节数据为 null 时页面崩溃 | 全部使用 `@else` + 空状态组件 + 加载态 |
| 知识图谱组件依赖大 | 仅在 Tab 激活时按需加载（lazy import） |
| 选课/退课无确认反馈 | 用 nzMessageService + loading 信号，禁用按钮防重 |
| 进度为 0 时进度条不显示 | 仅当 `progress > 0` 时渲染进度条 |

---

## 六、完成标准

1. 三个新页面均通过 `ng build` 验证无错误
2. 设计与现有学生端（资源/资讯）视觉一致
3. 数据优先从后端获取，缺失字段使用 mock 并标注 TODO
4. 所有交互按钮有视觉反馈与防重
5. 响应式：在 1100px / 768px 断点优雅降级
