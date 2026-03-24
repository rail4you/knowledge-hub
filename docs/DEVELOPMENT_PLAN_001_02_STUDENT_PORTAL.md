# 学生端页面开发计划

## 文档信息

| 项目 | 内容 |
|------|------|
| 文档编号 | DEVELOPMENT_PLAN_001_02 |
| 所属系统 | KnowledgeHub |
| 开发阶段 | Phase 1: 学生端 |
| 最后更新 | 2026-03-24 |

---

## 1. 模块概述

### 1.1 功能目标

为学生创建一个独立、专业、简洁的学习门户页面，包含以下功能：

1. **知识库资源列表** - 查看联盟审核通过的资源
2. **知识库搜索** - 搜索资源
3. **智能问答模块** - 复用现有的AI功能（智能问答、案例分析、职业规划）
4. **我的课程** - 查看已选课程信息

### 1.2 设计要求

- **布局**：独立设计，不用Lepton Theme
- **导航**：水平Tab导航
- **风格**：专业、简洁

### 1.3 需求对照

| 需求编号 | 需求描述 | 实现说明 |
|----------|----------|----------|
| 1 | 课程学习与进度管理 | 复用learning/my-courses |
| 2 | 可视化知识图谱 | 待后续阶段 |
| 4 | 章节测试与反馈 | 待后续阶段 |
| 5 | 学习数据统计 | 待后续阶段 |

---

## 2. 现状分析

### 2.1 已有功能

**智能问答模块** ✅：
- `angular/src/app/ai/chat/` - 智能问答
- `angular/src/app/ai/lesson-plan/` - 教案生成
- `angular/src/app/ai/case-analysis/` - 案例分析
- `angular/src/app/ai/career-guidance/` - 职业规划

**知识库资源**（教师/管理端）：
- `angular/src/app/resources/` - 资源管理组件

### 2.2 缺失功能

1. **学生端入口** - 没有专门的学生门户页面
2. **知识库列表（学生视图）** - 学生看不到联盟审核通过的资源
3. **知识库搜索（学生视图）** - 学生无法搜索资源
4. **水平Tab导航** - 需要重新设计导航

### 2.3 已有数据流

**资源审核流程**：
```
草稿 → 待审核 → 院校审核通过 → 联盟审核通过 → 发布
```

学生只能看到 **联盟审核通过** 的资源。

---

## 3. 页面结构

### 3.1 整体布局

```
┌─────────────────────────────────────────────────────────────┐
│  Logo    [知识库] [搜索] [智能问答] [我的课程]    [用户信息] │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│                      内容区域                                │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 Tab内容

| Tab | 路径 | 说明 |
|-----|------|------|
| 知识库 | /student/resources | 资源列表 |
| 搜索 | /student/search | 搜索页面 |
| 智能问答 | /student/ai/* | AI子菜单 |
| 我的课程 | /student/courses | 已选课程 |

### 3.3 智能问答子菜单

```
智能问答
├── 智能问答 (chat)
├── 教案生成 (lesson-plan)
├── 案例分析 (case-analysis)
└── 职业规划 (career-guidance)
```

---

## 4. 开发任务

### 4.1 路由配置

#### T2.1: 添加学生端路由

**文件**：`angular/src/app/app.routes.ts`

```typescript
{
  path: 'student',
  children: [
    // 知识库资源
    { path: 'resources', component: StudentResourcesComponent },
    // 搜索
    { path: 'search', component: StudentSearchComponent },
    // 智能问答（复用现有组件）
    { path: 'ai/chat', component: ChatComponent },
    { path: 'ai/lesson-plan', component: LessonPlanComponent },
    { path: 'ai/case-analysis', component: CaseAnalysisComponent },
    { path: 'ai/career-guidance', component: CareerGuidanceComponent },
    // 我的课程
    { path: 'courses', component: MyCoursesComponent },
  ]
}
```

### 4.2 学生端布局组件

#### T2.2: 创建学生端布局组件

**文件**：`angular/src/app/student/layout/`

- `student-layout.component.ts` - 水平Tab导航布局
- `student-layout.component.html` - Tab导航模板
- `student-layout.component.scss` - 样式

**功能**：
- 顶部Logo + 用户信息
- 水平Tab导航栏
- 内容投影区 (router-outlet)

```html
<div class="student-layout">
  <header class="student-header">
    <div class="logo">KnowledgeHub</div>
    <nav class="student-tabs">
      <a routerLink="/student/resources" routerLinkActive="active">
        <span nz-icon nzType="book"></span>
        知识库
      </a>
      <a routerLink="/student/search" routerLinkActive="active">
        <span nz-icon nzType="search"></span>
        搜索
      </a>
      <a routerLink="/student/ai/chat" routerLinkActive="active">
        <span nz-icon nzType="robot"></span>
        智能问答
      </a>
      <a routerLink="/student/courses" routerLinkActive="active">
        <span nz-icon nzType="read"></span>
        我的课程
      </a>
    </nav>
    <div class="user-info">
      <!-- 用户头像、退出等 -->
    </div>
  </header>
  <main class="student-content">
    <router-outlet></router-outlet>
  </main>
</div>
```

### 4.3 知识库资源列表

#### T2.3: 创建学生资源列表组件

**文件**：`angular/src/app/student/resources/`

- `student-resources.component.ts`
- `student-resources.component.html`
- `student-resources.component.scss`

**功能**：
- 调用API获取联盟审核通过的资源 (`GET /api/resources/league-approved`)
- 分类展示（文档、视频、音频等）
- 下载资源文件
- 收藏/分享功能

**API调用**：
```typescript
// 需要后端新增 endpoint
GET /api/resources/league-approved
```

### 4.4 知识库搜索

#### T2.4: 复用搜索组件

直接复用现有的 `SearchComponent`，在学生端路由指向它。

```typescript
{ path: 'search', component: SearchComponent }
```

### 4.5 智能问答

#### T2.5: 复用AI组件

智能问答模块已完整实现，学生端直接复用：

| 路由 | 组件 | 状态 |
|------|------|------|
| /student/ai/chat | ChatComponent | ✅ |
| /student/ai/lesson-plan | LessonPlanComponent | ✅ |
| /student/ai/case-analysis | CaseAnalysisComponent | ✅ |
| /student/ai/career-guidance | CareerGuidanceComponent | ✅ |

### 4.6 我的课程

#### T2.6: 复用课程组件

复用现有的 `MyCoursesComponent`：

```typescript
{ path: 'courses', component: MyCoursesComponent }
```

**前提**：`MyCoursesComponent` 已连接API（参见课程模块开发文档 T1.8）

---

## 5. 菜单配置

### 5.1 学生端菜单

**文件**：`angular/src/app/route.provider.ts`

```typescript
{
  path: '/student',
  name: '::Menu:StudentPortal',
  iconClass: 'fas fa-user-graduate',
  order: 1,
  layout: eLayoutType.application,
  parentName: null, // 顶级菜单
}
```

### 5.2 本地化文本

**文件**：`src/KnowledgeHub.Domain.Shared/Localization/KnowledgeHub/zh-Hans.json`

```json
{
  "Menu:StudentPortal": "学生门户",
  "Menu:Resources": "知识库",
  "Menu:Search": "搜索",
  "Menu:AI": "智能问答",
  "Menu:MyCourses": "我的课程",
  "Menu:AIChat": "智能问答",
  "Menu:LessonPlan": "教案生成",
  "Menu:CaseAnalysis": "案例分析",
  "Menu:CareerGuidance": "职业规划"
}
```

---

## 6. 后端任务

### T2.7: 新增学生端API

#### 获取联盟审核通过的资源

**文件**：`src/KnowledgeHub.Application/Resources/ResourceAppService.cs`

```csharp
public async Task<PagedResultDto<ResourceDto>> GetLeagueApprovedAsync(
    PagedResultRequestDto input)
{
    // 过滤条件：Status == ResourceStatus.LeagueApproved
    // 返回资源列表
}
```

#### 权限配置

学生角色默认有查看和下载权限。

---

## 7. 验收标准

### 7.1 页面展示

- [ ] 学生端使用水平Tab导航
- [ ] 页面风格专业简洁
- [ ] Logo和用户信息正确显示

### 7.2 功能验证

- [ ] 知识库Tab显示联盟审核通过的资源的列表
- [ ] 资源可以下载
- [ ] 搜索Tab可以搜索资源
- [ ] 智能问答Tab显示4个AI功能
- [ ] 我的课程Tab显示学生已选的课程

---

## 8. 技术栈

- **前端**：Angular 21, ng-zorro-antd, Signals
- **样式**：SCSS, 自定义学生端主题

---

## 9. 依赖关系

```
T2.1 (路由配置)     → T2.2, T2.3, T2.4, T2.5, T2.6
T2.7 (后端API)      → T2.3 (资源列表)
课程模块 T1.8       → T2.6 (我的课程)
```

**说明**：T2.5 和 T2.6 复用现有组件，无新增依赖
