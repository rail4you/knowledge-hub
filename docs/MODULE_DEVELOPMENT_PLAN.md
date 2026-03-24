# KnowledgeHub 模块开发方案文档

## 一、项目概述

### 1.1 目标模块
1. **在线课程中心学习** - 完整的在线学习管理系统（含知识图谱）
2. **专业大模型应用** - 基于 Microsoft Agent Framework + Qwen Plus

### 1.2 技术栈
| 层级 | 技术 |
|------|------|
| 后端 | ABP Framework 10 + .NET 10 + Microsoft.Agents.AI |
| AI | Qwen Plus (OpenAI 兼容 API) |
| 前端 | Angular 21 + ng-zorro-antd 21 + ECharts |
| 数据库 | PostgreSQL |
| 搜索 | MeiliSearch (已有) |

### 1.3 不需要新增项目 DLL
两个模块都放入现有项目：
- 在线课程中心学习：`*.Domain/Courses/`, `*.Application/Courses/`
- 专业大模型应用：`*.Application/AI/`

---

## 二、后端 API 路由规划

### 2.1 在线课程中心学习 (`/api/learning/`)

| 端点 | 方法 | 权限 | 描述 |
|------|------|------|------|
| `/api/learning/courses` | GET | All | 获取课程列表 (支持筛选) |
| `/api/learning/courses/{id}` | GET | All | 获取课程详情 |
| `/api/learning/courses` | POST | Teacher | 创建课程 |
| `/api/learning/courses/{id}` | PUT | Teacher/Edit | 更新课程 |
| `/api/learning/courses/{id}/audit` | POST | Admin/Audit | 审核课程 |
| `/api/learning/courses/{id}/enroll` | POST | Student | 选课 |
| `/api/learning/courses/{id}/drop` | POST | Student | 退课 |
| `/api/learning/my-courses` | GET | Student | 我的课程 |
| `/api/learning/progress/{courseId}` | GET | Student | 学习进度 |
| `/api/learning/progress` | POST | Student | 记录进度 |
| `/api/learning/dashboard` | GET | Student | 学习仪表盘 |
| `/api/learning/exams` | GET | All | 考试列表 |
| `/api/learning/exams/{id}/start` | POST | Student | 开始考试 |
| `/api/learning/exams/{id}/submit` | POST | Student | 提交答案 |
| `/api/learning/exams/{id}/grade` | POST | Teacher | 批改主观题 |
| `/api/learning/exercises/generate` | POST | Teacher | AI 生成习题 |
| `/api/learning/knowledge-graph/{courseId}` | GET | All | 获取知识图谱 |
| `/api/learning/knowledge-graph/path/{courseId}` | GET | Student | 获取推荐学习路径 |
| `/api/learning/statistics/dashboard` | GET | By Role | 数据仪表盘 |
| `/api/learning/statistics/export` | POST | Teacher+ | 导出报表 |

### 2.2 专业大模型应用 (`/api/ai/`)

| 端点 | 方法 | 权限 | 描述 |
|------|------|------|------|
| `/api/ai/chat` | POST (SSE) | All | 智能问答 (流式响应) |
| `/api/ai/thread/new` | POST | All | 创建会话 |
| `/api/ai/thread/{id}` | GET | All | 获取会话历史 |

---

## 三、Domain 层实体

### 3.1 目录结构
```
src/KnowledgeHub.Domain/
├── Courses/
│   ├── Course.cs
│   ├── Chapter.cs
│   ├── KnowledgeResource.cs
│   └── ICourseRepository.cs
├── Learning/
│   ├── StudentCourse.cs
│   ├── LearningProgress.cs
│   ├── KnowledgeMastery.cs
│   └── ILearningRepository.cs
├── Exams/
│   ├── Exam.cs
│   ├── Exercise.cs
│   ├── ExamExercise.cs
│   ├── StudentExam.cs
│   ├── StudentAnswer.cs
│   └── IExamRepository.cs
├── KnowledgeGraph/
│   ├── KnowledgeNode.cs
│   ├── KnowledgeRelation.cs
│   └── IKnowledgeGraphRepository.cs
└── AI/
    ├── ChatThread.cs
    └── ChatMessage.cs
```

### 3.2 枚举定义 (Domain.Shared)
```
src/KnowledgeHub.Domain.Shared/
├── Courses/
│   └── Enums/
│       └── CourseStatus.cs
├── Learning/
│   └── Enums/
│       ├── StudentCourseStatus.cs
│       └── MasteryLevel.cs
├── Exams/
│   └── Enums/
│       ├── ExerciseType.cs
│       ├── ExamType.cs
│       └── ExamStatus.cs
└── KnowledgeGraph/
    └── Enums/
        └── RelationType.cs
```

---

## 四、权限设计

```csharp
public static class KnowledgeHubPermissions
{
    public static class Courses
    {
        public const string Default = GroupName + ".Courses";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Audit = Default + ".Audit";
        public const string View = Default + ".View";
        public const string Enroll = Default + ".Enroll";
    }
    
    public static class Learning
    {
        public const string Default = GroupName + ".Learning";
        public const string ViewOwn = Default + ".ViewOwn";
        public const string ViewClass = Default + ".ViewClass";
        public const string ViewSchool = Default + ".ViewSchool";
        public const string ViewAll = Default + ".ViewAll";
        public const string Export = Default + ".Export";
    }
    
    public static class Exams
    {
        public const string Default = GroupName + ".Exams";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Grade = Default + ".Grade";
        public const string Take = Default + ".Take";
    }
    
    public static class KnowledgeGraph
    {
        public const string Default = GroupName + ".KnowledgeGraph";
        public const string View = Default + ".View";
        public const string Manage = Default + ".Manage";
    }
    
    public static class AI
    {
        public const string Default = GroupName + ".AI";
        public const string Chat = Default + ".Chat";
    }
}
```

---

## 五、前端路由

| 路由 | 组件 | 权限 | 描述 |
|------|------|------|------|
| `/ai/chat` | ChatComponent | AI.Chat | 智能问答 |
| `/learning/courses` | CourseListComponent | Courses.View | 课程列表 |
| `/learning/courses/:id` | CourseDetailComponent | Courses.View | 课程详情 |
| `/learning/courses/new` | CourseFormComponent | Courses.Create | 创建课程 |
| `/learning/courses/:id/edit` | CourseFormComponent | Courses.Edit | 编辑课程 |
| `/learning/my-courses` | MyCoursesComponent | Learning.ViewOwn | 我的课程 |
| `/learning/progress` | ProgressDashboardComponent | Learning.ViewOwn | 学习进度 |
| `/learning/exams` | ExamListComponent | Exams.Default | 考试列表 |
| `/learning/exams/:id/take` | ExamTakingComponent | Exams.Take | 参加考试 |
| `/learning/knowledge-graph/:courseId` | GraphViewerComponent | KnowledgeGraph.View | 知识图谱 |
| `/learning/statistics` | StatsDashboardComponent | Learning.ViewOwn | 数据统计 |

---

## 六、开发计划

| 阶段 | 内容 | 状态 | 备注 |
|------|------|------|------|
| **Phase 1** | Domain 实体 + 数据库迁移 | ✅ 已完成 | 所有模块实体已创建 |
| **Phase 2** | 课程 CRUD + 选课功能 (后端) | ⚠️ 部分完成 | CourseAppService 存在，前端组件存在，选课/退课未实现 |
| **Phase 3** | 智能问答模块 (后端 + 前端基础) | ⚠️ 部分完成 | ChatAppService 存在，缺少 AI 包引用，前端 chat 组件存在 |
| **Phase 4** | 学习进度追踪 (后端 + 前端) | ⚠️ 部分完成 | LearningAppService 存在但不完整，前端 progress 组件存在 |
| **Phase 5** | 习题 + 考试系统 | ❌ 未开始 | 仅 Domain 实体，Application 层未实现 |
| **Phase 6** | AI 习题生成 + 批改 | ❌ 未开始 | - |
| **Phase 7** | 知识图谱 + ECharts 可视化 | ❌ 未开始 | 仅有 Domain 实体 |
| **Phase 8** | 统计 + 报表导出 | ❌ 未开始 | - |
| **Phase 9** | 权限细化 + 前端完善 + 测试 | ❌ 未开始 | - |

### 详细完成情况

#### Domain 层实体 (✅ 全部完成)
```
src/KnowledgeHub.Domain/
├── Courses/           ✅ Course, Chapter, KnowledgeResource, ICourseRepository
├── Learning/         ✅ StudentCourse, LearningProgress, KnowledgeMastery, ILearningRepository
├── Exams/            ✅ Exam, Exercise, ExamExercise, StudentExam, StudentAnswer, IExamRepository
├── KnowledgeGraph/   ✅ KnowledgeNode, KnowledgeRelation, IKnowledgeGraphRepository
├── AI/               ✅ ChatThread, ChatMessage
└── Resources/        ✅ Resource, ResourceCategory, ResourceCollection, ResourceVersion 等
```

#### Application 层服务
| 模块 | 服务状态 | 说明 |
|------|----------|------|
| Courses | ⚠️ 部分完成 | CourseAppService 存在，CRUD 基本可用 |
| Learning | ⚠️ 部分完成 | LearningAppService 存在但不完整 |
| Exams | ❌ 未开始 | 仅 Domain 实体 |
| KnowledgeGraph | ❌ 未开始 | 仅 Domain 实体 |
| AI | ⚠️ 部分完成 | ChatAppService 存在，缺少 Microsoft.Agents.AI / OpenAI 包引用 |
| Resources/Search | ✅ 已完成 | Meilisearch 集成，文档索引和搜索功能完整 |

#### 前端组件
| 路由 | 组件 | 状态 |
|------|------|------|
| `/ai/chat` | chat | ⚠️ 部分完成 |
| `/learning/courses` | course-list | ⚠️ 部分完成 |
| `/learning/courses/:id` | course-detail | ⚠️ 部分完成 |
| `/learning/my-courses` | my-courses | ⚠️ 部分完成 |
| `/learning/progress` | progress | ⚠️ 部分完成 |
| `/search` | search | ✅ 已完成 |
| `/document-viewer` | document-viewer | ✅ 已完成 |

#### 额外完成功能
- ✅ Meilisearch 全文搜索集成
- ✅ 知乎风格搜索结果 UI
- ✅ 搜索关键词高亮显示
- ✅ 文档预览组件
- ✅ 后台文档索引作业 (DocumentIndexingBackgroundJob)
- ✅ 禁用开发环境自动打开浏览器

### 待解决问题
1. **AI 模块**: 缺少 NuGet 包引用 (Microsoft.Agents.AI, OpenAI)
2. **考试模块**: 后端 Application 服务未实现
3. **知识图谱**: ECharts 可视化组件缺少模板文件 (.html, .scss)

---

## 七、配置

### appsettings.json
```json
{
  "Qwen": {
    "ApiKey": "${QWEN_API_KEY}",
    "BaseUrl": "https://dashscope.aliyuncs.com/compatible-mode/v1",
    "Model": "qwen-plus"
  }
}
```
