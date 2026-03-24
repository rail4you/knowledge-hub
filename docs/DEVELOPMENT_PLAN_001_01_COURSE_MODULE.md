# 课程模块开发计划

## 文档信息

| 项目 | 内容 |
|------|------|
| 文档编号 | DEVELOPMENT_PLAN_001_01 |
| 所属系统 | KnowledgeHub |
| 开发阶段 | Phase 1: 课程模块 |
| 最后更新 | 2026-03-24 |

---

## 1. 模块概述

### 1.1 功能目标

实现教师端的课程管理和学生端的选课功能，满足以下需求：

- **课程创建**：教师创建课程，设置基本信息（名称、学期、学分、难度等）
- **学生选课**：学生选择已发布的课程进行学习
- **课程管理**：教师管理自己创建的课程

### 1.2 需求对照

| 需求编号 | 需求描述 | 实现状态 |
|----------|----------|----------|
| 1 | 课程学习与进度管理 | 部分实现 |
| 2 | 可视化知识图谱 | 待开发 |
| 4 | 章节测试与反馈 | 待开发 |
| 5 | 学习数据统计 | 待开发 |

---

## 2. 现状分析

### 2.1 已有的后端实体

```
src/KnowledgeHub.Domain/
├── Courses/
│   ├── Course.cs          ✅ 课程实体
│   └── Chapter.cs        ✅ 章节实体
└── Learning/
    ├── StudentCourse.cs   ✅ 学生选课记录
    ├── LearningProgress.cs ✅ 学习进度
    └── KnowledgeMastery.cs ✅ 知识点掌握
```

**Course 实体字段**：
- Title, Description, CoverImageUrl
- Major, Semester, Credits, SemesterHours
- Status, Difficulty
- TeacherId, CategoryId
- Chapters (导航属性)

**StudentCourse 实体字段**：
- StudentId, CourseId
- Status (Enrolled=0, InProgress=1, Completed=2, Dropped=3)
- Progress, EnrolledAt, StartedAt, CompletedAt

### 2.2 后端服务接口状态

**ICourseAppService**：
- `CreateAsync` - ✅ 已实现
- `UpdateAsync` - ✅ 已实现
- `GetDetailAsync` - ✅ 已实现
- `EnrollAsync` - ✅ 已实现
- `DropAsync` - ✅ 已实现
- `GetPublishedAsync` - ✅ 已实现 (2026-03-25)
- `GetMyCoursesAsync` - ✅ 已实现 (2026-03-25)
- `GetByFilterAsync` - ✅ 已实现 (2026-03-25)
- `AuditAsync` - ✅ 已实现

**IChapterAppService**：
- `GetAsync` - ✅ 已实现
- `GetListAsync` - ✅ 已实现
- `CreateAsync` - ✅ 已实现
- `UpdateAsync` - ✅ 已实现
- `DeleteAsync` - ✅ 已实现
- `GetChaptersByCourseAsync` - ✅ 已实现
- `GetChapterTreeAsync` - ✅ 已实现

### 2.3 前端组件状态

```
angular/src/app/learning/
├── my-courses/        ✅ 连接API
├── course-list/       ✅ 连接API
├── course-detail/     ✅ 连接API
├── chapter/          ✅ 章节树组件
├── teacher/course-create/  ✅ 教师创建组件
└── progress/         ✅ 进度仪表板(ECharts)
```

### 2.4 已完成的功能

1. **后端**：
   - ✅ ICourseRepository 注册 (KnowledgeHubEntityFrameworkCoreModule)
   - ✅ 数据库迁移 (AddCourseTables)
   - ✅ 课程API手动映射解决ObjectMapper问题
   - ✅ Course实体、Chapter实体配置完成

2. **前端**：
   - ✅ Learning路由已配置
   - ✅ 课程API代理已生成
   - ✅ 教师端课程创建组件
   - ✅ 章节树组件 (placeholder)

---

## 3. 开发任务

### 3.1 后端任务

#### T1.1: 实现 GetPublishedAsync 方法 ✅ 已完成

**文件**：`src/KnowledgeHub.Application/Courses/CourseAppService.cs`

```csharp
public async Task<PagedResultDto<CourseDto>> GetPublishedAsync(PagedCourseRequestDto input)
{
    // 返回所有已发布的课程（LeagueApproved状态）
    // 过滤条件：Status == CourseStatus.Published
    // 支持分页
}
```

#### T1.2: 实现 GetMyCreatedCoursesAsync (教师) ✅ 已完成

**文件**：`src/KnowledgeHub.Application/Courses/CourseAppService.cs`

```csharp
public async Task<PagedResultDto<CourseDto>> GetMyCreatedCoursesAsync(Guid teacherId)
{
    // 返回指定教师创建的所有课程
    // 过滤条件：TeacherId == teacherId
}
```

#### T1.3: 修复 CreateAsync 设置 TeacherId ✅ 已完成

**文件**：`src/KnowledgeHub.Application/Courses/CourseAppService.cs`

在创建课程时，从当前用户获取 TeacherId 并设置。

#### T1.4: 添加课程权限 ✅ 已完成

**文件**：`src/KnowledgeHub.Domain/KnowledgeHubPermissions.cs`

```csharp
public const string Courses = GroupName + ".Courses";
public const string Courses.Create = Courses + ".Create";
public const string Courses.Edit = Courses + ".Edit";
public const string Courses.Delete = Courses + ".Delete";
public const string Courses.Enroll = Courses + ".Enroll";
```

### 3.2 前端任务

#### T1.5: 添加 Learning 路由 ✅ 已完成

**文件**：`angular/src/app/app.routes.ts`

```typescript
{
  path: 'learning',
  children: [
    { path: 'my-courses', component: MyCoursesComponent },
    { path: 'course-list', component: CourseListComponent },
    { path: 'course-detail/:id', component: CourseDetailComponent },
    { path: 'teacher/create', component: CourseCreateComponent },
    { path: 'teacher/manage', component: TeacherCourseManageComponent },
  ]
}
```

#### T1.6: 生成课程 API 代理 ✅ 已完成

```bash
cd angular
abp generate-proxy -t ng
```

#### T1.7: 创建教师课程创建组件 ✅ 已完成

**文件**：`angular/src/app/learning/teacher/course-create/`

- `course-create.component.ts`
- `course-create.component.html`
- `course-create.component.scss`

**功能**：
- 表单：课程名称、描述、封面图片
- 下拉：专业、学期、难度、分类
- 输入：学分、课时
- 提交调用 `CourseService.create()`

#### T1.8: 完善学生选课组件 ✅ 已完成

**文件**：`angular/src/app/learning/course-list/`

- 连接 `GetPublishedAsync` API
- 实现筛选功能（专业、难度）
- 添加"选课"按钮

### 3.3 菜单配置

**文件**：`angular/src/app/route.provider.ts`

```typescript
{
  path: '/learning',
  name: '::Menu:Learning',
  iconClass: 'fas fa-book',
  order: 5,
  layout: eLayoutType.application,
},
{
  path: '/learning/course-list',
  name: '::Menu:CourseList',
  parentName: '::Menu:Learning',
},
{
  path: '/learning/my-courses',
  name: '::Menu:MyCourses',
  parentName: '::Menu:Learning',
},
{
  path: '/learning/teacher/create',
  name: '::Menu:CreateCourse',
  parentName: '::Menu:Learning',
},
```

---

## 4. 数据流

### 4.1 教师创建课程流程

```
教师点击"创建课程"
    ↓
填写课程表单（名称、专业、学期、学分等）
    ↓
调用 POST /api/courses
    ↓
CourseAppService.CreateAsync()
    ↓
设置 TeacherId = CurrentUser.Id
    ↓
保存到数据库 (Status = Draft)
```

### 4.2 学生选课流程

```
学生浏览课程列表
    ↓
调用 GET /api/courses/published
    ↓
展示课程列表（可筛选）
    ↓
学生点击"选课"按钮
    ↓
调用 POST /api/courses/{id}/enroll
    ↓
StudentCourse 创建 (Status = Enrolled)
    ↓
学生可在"我的课程"查看
```

---

## 5. API 端点

| 方法 | 端点 | 说明 | 权限 |
|------|------|------|------|
| GET | /api/courses | 获取课程列表 | - |
| GET | /api/courses/published | 获取已发布课程 | - |
| GET | /api/courses/{id} | 获取课程详情 | - |
| POST | /api/courses | 创建课程 | Courses.Create |
| PUT | /api/courses/{id} | 更新课程 | Courses.Edit |
| DELETE | /api/courses/{id} | 删除课程 | Courses.Delete |
| POST | /api/courses/{id}/enroll | 学生选课 | Courses.Enroll |
| DELETE | /api/courses/{id}/drop | 学生退课 | - |
| GET | /api/courses/my | 获取我的课程 | - |
| GET | /api/courses/teacher/my | 获取我创建的课程 | - |

---

## 6. 验收标准

### 6.1 教师端

- [x] 教师可以创建新课程
- [x] 教师可以编辑自己创建的课程
- [x] 教师可以删除自己创建的课程
- [x] 教师可以在列表中看到自己创建的课程

### 6.2 学生端

- [x] 学生可以看到所有已发布的课程
- [x] 学生可以按专业、难度筛选课程
- [x] 学生可以选修课程
- [x] 学生可以在"我的课程"查看已选课程

---

## 7. 技术栈

- **后端**：ASP.NET Core, ABP Framework, Entity Framework Core
- **前端**：Angular 21, ng-zorro-antd, Signals
- **数据库**：PostgreSQL

---

## 8. 依赖关系

```
T1.1 (GetPublishedAsync)     →  T1.6 (API代理生成)
T1.3 (Fix TeacherId)         →  T1.7 (创建组件)
T1.4 (权限定义)              →  T1.5 (路由配置)
T1.5 (路由配置)              →  T1.7, T1.8 (组件开发)
```

**并行任务**：T1.1, T1.2, T1.3, T1.4 可并行开发
**串行任务**：T1.5 → T1.6 → T1.7, T1.8

---

## 9. 更新记录

| 日期 | 版本 | 更新内容 | 作者 |
|------|------|----------|------|
| 2026-03-24 | 1.0 | 初始版本 | - |
| 2026-03-25 | 1.1 | 完成课程模块后端API实现，添加ICourseRepository注册，数据库迁移，手动映射修复 | AI |
| 2026-03-25 | 1.2 | 完成前端组件连接API，验收标准全部通过 | AI |
