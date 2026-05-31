# 学生端迁移计划：React → Angular

## 一、项目概述

### 目标
将现有的 React 学生端（student-react）功能迁移到 Angular 前端，移除 React 项目，统一使用 Angular 作为唯一前端技术栈，同时保留 ABP 管理端模板。

### 当前状态
- **管理端**: Angular + ABP LeptonX 主题 + ng-zorro-antd
- **学生端**: React + Vite + Tailwind CSS
- **后端**: ABP Framework (.NET)

### 迁移后的目标架构
```
┌─────────────────────────────────────────────────────────────┐
│                      Angular Frontend                       │
├──────────────────────────────┬──────────────────────────────┤
│        管理端 (LeptonX)       │       学生端 (ng-zorro-antd)   │
│   - ABP Identity/Account     │   - 自定义学生首页            │
│   - ng-zorro-antd 组件       │   - 资源库浏览                │
│   - 权限控制路由守卫         │   - 课程学习                  │
│   - /admin/*, /identity/*    │   - /student/*               │
└──────────────────────────────┴──────────────────────────────┘
                              │
                              ▼
                    ┌─────────────────┐
                    │   ABP Backend   │
                    │  (统一 API)     │
                    └─────────────────┘
```

---

## 二、现状分析（2026年5月31日）

### 2.1 当前已完成的工作

**已存在的 Angular 学生端组件**（位于 `angular/src/app/student/`）：
| 组件 | 状态 | 说明 |
|------|------|------|
| `student-layout` | ✅ 已创建 | 学生端布局组件，使用 `eLayoutType.empty` |
| `student-resources` | ✅ 已创建 | 资源浏览页面 |
| `student-favorites` | ✅ 已创建 | 收藏页面 |
| `student-news` | ✅ 已创建 | 新闻列表 |
| `student-news-detail` | ✅ 已创建 | 新闻详情 |
| `student-agent-tasks` | ✅ 已创建 | 智能体任务列表 |
| `student-agent-task-detail` | ✅ 已创建 | 智能体任务详情 |
| `student-portal.guard.ts` | ✅ 已创建 | 学生角色路由守卫 |

**后端 Portal 接口**：
| 接口 | 状态 | 说明 |
|------|------|------|
| `IPortalAppService.GetHomeDataAsync()` | ✅ 已实现 | 返回租户信息、统计、微专业、课程等 |
| `PortalAppService` | ✅ 已实现 | 在 `src/KnowledgeHub.Application/Portal/` |

### 2.2 关键问题（Blocker）

#### ❌ 问题 1：学生路由未注册

**问题描述**：学生端组件已创建，但**未在 `app.routes.ts` 中注册路由**，导致这些组件无法访问。

**当前状态**：
- `app.routes.ts` 中没有 `/student/*` 路由
- 学生组件只是孤立文件，无法通过 URL 访问

**解决方案**：在 `app.routes.ts` 中添加学生路由配置

#### ❌ 问题 2：登录跳转逻辑未更新

**问题描述**：`home.component.ts` 第 30 行仍然跳转到 React 端：
```typescript
window.location.href = 'http://localhost:3000';
```

**解决方案**：改为跳转到 `/student/resources`

#### ❌ 问题 3：缺少租户列表 API

**问题描述**：主页需要展示所有租户的资源库，但 `PortalAppService` 只支持按租户 ID 获取数据。

**解决方案**：添加 `GetTenantsAsync()` 方法或修改现有接口支持 null tenantId

#### ❌ 问题 4：React 学生端仍存在

**问题描述**：`student-react/` 目录仍然存在，需要删除。

**解决方案**：删除整个目录

### 2.1 访问主页（未登录）

**功能需求：**
- 主页按租户展示资源库列表
- 每个租户显示：资源库名称、简介、课程数量、资源数量
- 未登录用户可预览资源库介绍页面，但看不到具体资源和课程详情
- 页面右上角有登录按钮

**API 接口需求：**
```csharp
// 需要新增或使用现有的 Portal 接口
GET /api/app/portal/tenants  // 获取所有租户及其资源库信息
GET /api/app/portal/tenant/{id}/preview  // 获取租户资源库预览信息
```

### 2.2 登录功能

**功能需求：**
- 点击登录按钮打开 ABP 登录页面
- 登录成功后根据用户角色跳转：
  - **管理端/教师端用户** → ABP 管理界面（保留现有行为）
  - **学生用户** → 学生端首页（/student/home）

**当前实现：**
- `home.component.ts` 中已有 `isStudent` 判断
- 学生登录后跳转到 `http://localhost:3000`（React 端）

**修改方案：**
- 改为跳转到 Angular 学生端路由 `/student/home`

### 2.4 学生端页面设计

**技术选型**：使用 **Angular + ng-zorro-antd**（现有组件已使用）
- 学生端组件已使用 ng-zorro-antd 组件库
- 与 Angular 生态一致，TypeScript 支持更好
- 不需要安装 Tailwind CSS

**设计风格**：
- 现代化、轻量级界面
- 使用 ng-zorro-antd 组件
- 自定义样式覆盖默认样式

**主要页面**：
| 页面 | 路由 | 状态 | 说明 |
|------|------|------|------|
| 学生首页 | /student/resources | ✅ 已存在 | 默认展示资源库 |
| 资源库 | /student/resources | ✅ 已存在 | 资源浏览 |
| 收藏 | /student/favorites | ✅ 已存在 | 收藏的资源 |
| 新闻 | /student/news | ✅ 已存在 | 租户新闻 |
| 新闻详情 | /student/news/:id | ✅ 已存在 | 新闻详情 |
| 智能体任务 | /student/agent-tasks | ✅ 已存在 | 任务列表 |
| 任务详情 | /student/agent-tasks/:id | ✅ 已存在 | 任务详情 |
| 微专业 | /student/micro-majors | ❌ 需创建 | 微专业列表 |
| 课程详情 | /student/courses/:id | ❌ 需创建 | 课程详情 |
| 学习进度 | /student/progress | ❌ 需创建 | 个人进度 |

---

## 三、详细实施计划

### Phase 0: 现状确认与清理（0.5 天）

#### 0.1 删除 React 学生端项目

```bash
# 删除 student-react 目录
rm -rf student-react/

# 从 git 中移除（如果已跟踪）
git rm -rf student-react/
```

#### 0.2 确认现有组件状态

```bash
# 检查已存在的学生端组件
ls -la angular/src/app/student/

# 检查学生路由守卫
cat angular/src/app/student/student-portal.guard.ts
```

---

### Phase 1: 关键修复（1 天）— **Blocker 必须先解决**

#### 1.1 注册学生端路由（最重要！）

**文件**: `angular/src/app/app.routes.ts`

```typescript
// 在 APP_ROUTES 中添加学生路由
{
  path: 'student',
  canActivate: [authGuard, studentPortalGuard],
  children: [
    {
      path: '',
      redirectTo: 'resources',
      pathMatch: 'full'
    },
    {
      path: 'resources',
      loadComponent: () => import('./student/resources/student-resources.component')
        .then(c => c.StudentResourcesComponent),
    },
    {
      path: 'favorites',
      loadComponent: () => import('./student/favorites/student-favorites.component')
        .then(c => c.StudentFavoritesComponent),
    },
    {
      path: 'news',
      loadComponent: () => import('./student/news/student-news.component')
        .then(c => c.StudentNewsComponent),
    },
    {
      path: 'news/:id',
      loadComponent: () => import('./student/news/student-news-detail.component')
        .then(c => c.StudentNewsDetailComponent),
    },
    {
      path: 'agent-tasks',
      loadComponent: () => import('./student/agent-tasks/student-agent-task-list.component')
        .then(c => c.StudentAgentTaskListComponent),
    },
    {
      path: 'agent-tasks/:id',
      loadComponent: () => import('./student/agent-tasks/student-agent-task-detail.component')
        .then(c => c.StudentAgentTaskDetailComponent),
    },
  ]
},
```

#### 1.2 修改登录跳转逻辑

**文件**: `angular/src/app/home/home.component.ts`

```typescript
// 修改 ngOnInit 方法中的跳转逻辑
ngOnInit() {
  if (this.hasLoggedIn) {
    const returnUrl = this.route.snapshot.queryParams['returnUrl'];
    if (!returnUrl && this.isStudent) {
      // 学生登录后跳转到 Angular 学生端资源页
      this.router.navigate(['/student/resources']);
    }
  }
}

// 同时修改 logout 方法
logout() {
  this.authService.logout().subscribe(() => {
    // 使用完整页面刷新清除 ABP 框架缓存的布局状态
    window.location.href = '/';
  });
}
```

#### 1.3 确保 studentPortalGuard 正确导入

```typescript
// 在 app.routes.ts 顶部添加导入
import { studentPortalGuard } from './student/student-portal.guard';
```

---

### Phase 2: 后端 API 扩展（1 天）

#### 2.1 添加租户列表 API

**文件**: `src/KnowledgeHub.Application.Contracts/Portal/IPortalAppService.cs`

```csharp
public interface IPortalAppService : IApplicationService
{
    // 现有方法
    Task<PortalHomeDataDto> GetHomeDataAsync(Guid tenantId);
    
    // 新增方法：获取所有租户列表（公开访问）
    [AllowAnonymous]
    Task<List<TenantResourceSummaryDto>> GetPublicTenantListAsync();
    
    // 新增方法：获取单个租户资源库预览（公开访问）
    [AllowAnonymous]
    Task<TenantResourcePreviewDto> GetTenantPreviewAsync(Guid tenantId);
}
```

**文件**: `src/KnowledgeHub.Application/Portal/PortalAppService.cs`

```csharp
// 实现新方法
[AllowAnonymous]
public async Task<List<TenantResourceSummaryDto>> GetPublicTenantListAsync()
{
    // 查询所有启用的租户
    // 返回每个租户的名称、简介、资源数、课程数
}

[AllowAnonymous]
public async Task<TenantResourcePreviewDto> GetTenantPreviewAsync(Guid tenantId)
{
    // 查询指定租户的资源库预览信息
    // 返回租户详情、热门资源、推荐课程等
}
```

---

### Phase 3: 重构主页（1 天）

#### 3.1 重构 home.component.html

**目标**：未登录用户可以看到租户资源库卡片列表

```html
<!-- 使用 ng-zorro-antd 卡片展示租户 -->
<div class="tenant-grid">
  <nz-card *ngFor="let tenant of tenants" 
           [nzHoverable]="true"
           (click)="previewTenant(tenant.id)">
    <h3>{{ tenant.name }}</h3>
    <p>{{ tenant.description }}</p>
    <div class="tenant-stats">
      <span>{{ tenant.resourceCount }} 资源</span>
      <span>{{ tenant.courseCount }} 课程</span>
    </div>
  </nz-card>
</div>
```

#### 3.2 添加租户预览页面

**文件**: `angular/src/app/home/tenant-preview.component.ts`

```typescript
// 租户资源库预览页面（公开访问）
// 显示租户的简介、热门资源、推荐课程
// 不显示具体资源内容
```

---

### Phase 4: 完善学生端页面（3 天）

#### 4.1 创建缺失的页面

| 页面 | 优先级 | 说明 |
|------|--------|------|
| `/student/micro-majors` | 高 | 微专业列表（复用 `micro-majors/` 组件） |
| `/student/courses/:id` | 高 | 课程详情（复用 `learning/course-detail/`） |
| `/student/progress` | 中 | 学习进度页面 |
| `/student/home` | 低 | 学生仪表盘（可选，resources 可作为默认页） |

#### 4.2 创建学生微专业页面

**文件**: `angular/src/app/student/micro-majors/student-micro-majors.component.ts`

```typescript
// 参考: angular/src/app/micro-majors/micro-major-list.component.ts
// 使用相同的结构和样式
// 使用学生端布局 (student-layout)
```

#### 4.3 创建学生课程详情页面

**文件**: `angular/src/app/student/courses/student-course-detail.component.ts`

```typescript
// 参考: angular/src/app/learning/course-detail/course-detail.component.ts
// 使用学生端布局
// 添加学习进度跟踪功能
```

---

### Phase 5: 测试与部署（1 天）

#### 5.1 功能测试清单

- [ ] 未登录访问主页显示租户列表
- [ ] 点击租户卡片显示预览页面
- [ ] 点击登录按钮跳转 ABP 登录页
- [ ] 学生用户登录后跳转到 `/student/resources`
- [ ] 管理用户登录后保持在 ABP 管理界面
- [ ] 学生端各页面正常访问
- [ ] 学生路由守卫正确阻止非学生用户

#### 5.2 回归测试

- [ ] 管理端功能正常（ABP 模板）
- [ ] 资源管理页面正常
- [ ] 课程管理页面正常
- [ ] AI 功能正常

---

### Phase 6: 清理与文档（0.5 天）

#### 6.1 确认 React 项目已删除

```bash
ls -la student-react/  # 应该报错：No such file or directory
```

#### 6.2 更新文档

- 更新 `AGENTS.md` 中的学生端路由说明
- 更新 `README.md` 中的项目说明

---

## 四、文件变更清单

### 4.1 新增文件

```
angular/src/app/student/
├── micro-majors/                  # 学生微专业（复用管理端组件）
│   ├── student-micro-majors.component.ts
│   └── student-micro-majors.component.html
├── courses/                       # 学生课程详情（复用管理端组件）
│   └── student-course-detail.component.ts
└── progress/                      # 学习进度页面
    └── student-progress.component.ts

src/KnowledgeHub.Application/
└── Portal/PortalAppService.cs    # 添加公开访问的方法
```

### 4.2 修改文件

```
angular/src/app/
├── app.routes.ts                  # 【关键】添加学生路由注册
├── home/
│   ├── home.component.ts          # 修改登录跳转逻辑
│   └── home.component.html        # 重构为租户展示页面
└── proxy/portal/                  # 使用 abp generate-proxy 重新生成

src/KnowledgeHub.Application.Contracts/
└── Portal/IPortalAppService.cs    # 添加公开 API 方法声明

src/KnowledgeHub.Application/
└── Portal/PortalAppService.cs    # 实现公开 API 方法
```

### 4.3 删除文件

```
student-react/                     # 整个目录删除
```

### 4.4 无需修改的文件

以下文件已在之前创建，无需重新开发：
- ✅ `angular/src/app/student/layout/` - 布局组件已存在
- ✅ `angular/src/app/student/resources/` - 资源页面已存在
- ✅ `angular/src/app/student/favorites/` - 收藏页面已存在
- ✅ `angular/src/app/student/news/` - 新闻页面已存在
- ✅ `angular/src/app/student/agent-tasks/` - 智能体任务已存在
- ✅ `angular/src/app/student/student-portal.guard.ts` - 路由守卫已存在

---

## 五、实施顺序与时间估算

### 修订后的时间估算

> ⚠️ **重要更新**：经过代码审查，发现大部分学生端组件已经存在，实际工作量大幅减少。

| 阶段 | 工作内容 | 时间 | 依赖 |
|------|---------|------|------|
| Phase 0 | 清理 React 项目 | 0.5 天 | 无 |
| Phase 1 | **注册学生路由 + 修改登录跳转** | 1 天 | 无 |
| Phase 2 | 扩展 Portal API（租户列表） | 1 天 | Phase 1 |
| Phase 3 | 重构主页（租户展示） | 1 天 | Phase 2 |
| Phase 4 | 完善学生端页面 | 3 天 | Phase 1 |
| Phase 5 | 测试与部署 | 1 天 | Phase 2-4 |
| Phase 6 | 清理与文档 | 0.5 天 | Phase 5 |
| **总计** | | **8 天** | |

### 关键路径（Critical Path）

```
Phase 0 → Phase 1 → Phase 2 → Phase 3 → Phase 5
                 ↓
              Phase 4 (可并行执行)
```

### 可并行执行的任务

| 任务 | 可并行 | 说明 |
|------|--------|------|
| 删除 React 项目 | ✅ | Phase 0，可立即执行 |
| 注册学生路由 | ❌ | Phase 1，必须先完成 |
| 修改登录跳转 | ✅ | Phase 1，与路由注册并行 |
| 扩展 Portal API | ✅ | Phase 2，与 Phase 1 并行 |
| 创建学生微专业页面 | ✅ | Phase 4，与 Phase 2 并行 |
| 创建学生课程详情页面 | ✅ | Phase 4，与 Phase 2 并行 |

---

## 六、风险与注意事项

### 6.1 修订后的风险评估

| 风险 | 级别 | 说明 | 解决方案 |
|------|------|------|---------|
| 学生路由未注册 | 🔴 高 | 当前最大风险 | Phase 1 优先解决 |
| 登录跳转逻辑未更新 | 🔴 高 | 学生仍跳转到 React | Phase 1 修改 home.component.ts |
| 租户列表 API 缺失 | 🟡 中 | 主页无法展示租户 | Phase 2 添加 API |
| 样式冲突 | 🟢 低 | 学生端与管理端样式隔离 | 使用 student-layout 独立布局 |
| 权限控制失效 | 🟢 低 | studentPortalGuard 已存在 | 验证路由守卫正确触发 |

### 6.2 注意事项

1. **不要修改管理端代码**: 保持 ABP 模板完整
2. **复用现有组件**: 学生端可直接复用管理端的微专业、课程等组件
3. **使用 standalone 组件**: 新组件使用 Angular 17+ 的 standalone 模式
4. **样式隔离**: 学生端使用独立的 student-layout，不加载 LeptonX 布局
5. **懒加载**: 学生端页面使用懒加载优化首屏加载
6. **Portal API [AllowAnonymous]**: 公开访问的 API 需要添加此特性

---

## 七、关键实现细节

### 7.1 注册学生路由（Phase 1 最关键）

**文件**: `angular/src/app/app.routes.ts`

```typescript
// 在 APP_ROUTES 数组开头添加（install 之后）
{
  path: 'student',
  canActivate: [authGuard, studentPortalGuard],
  loadChildren: () => import('./student/student.routes')
    .then(m => m.STUDENT_ROUTES),
},
```

**文件**: `angular/src/app/student/student.routes.ts`

```typescript
import { Routes } from '@angular/router';
import { studentPortalGuard } from './student-portal.guard';

export const STUDENT_ROUTES: Routes = [
  {
    path: '',
    component: StudentLayoutComponent,  // 使用学生端布局
    canActivate: [studentPortalGuard],
    children: [
      { path: '', redirectTo: 'resources', pathMatch: 'full' },
      { path: 'resources', loadComponent: () => import('./resources/student-resources.component') },
      { path: 'favorites', loadComponent: () => import('./favorites/student-favorites.component') },
      { path: 'news', loadComponent: () => import('./news/student-news.component') },
      { path: 'news/:id', loadComponent: () => import('./news/student-news-detail.component') },
      { path: 'agent-tasks', loadComponent: () => import('./agent-tasks/student-agent-task-list.component') },
      { path: 'agent-tasks/:id', loadComponent: () => import('./agent-tasks/student-agent-task-detail.component') },
      { path: 'micro-majors', loadComponent: () => import('./micro-majors/student-micro-majors.component') },
    ]
  }
];
```

### 7.2 修改登录跳转逻辑

**文件**: `angular/src/app/home/home.component.ts`

```typescript
// 修改 ngOnInit
ngOnInit() {
  if (this.hasLoggedIn) {
    const returnUrl = this.route.snapshot.queryParams['returnUrl'];
    if (!returnUrl && this.isStudent) {
      // 学生登录后跳转到 Angular 学生端
      this.router.navigate(['/student']);
    }
  }
}

// logout 方法保持不变
logout() {
  this.authService.logout().subscribe(() => {
    window.location.href = '/';
  });
}
```

### 7.3 扩展 Portal API

**文件**: `src/KnowledgeHub.Application.Contracts/Portal/IPortalAppService.cs`

```csharp
[AllowAnonymous]
Task<List<TenantResourceSummaryDto>> GetPublicTenantListAsync();

[AllowAnonymous]
Task<TenantResourcePreviewDto> GetTenantPreviewAsync(Guid tenantId);
```

**实现**: `src/KnowledgeHub.Application/Portal/PortalAppService.cs`

```csharp
[AllowAnonymous]
public async Task<List<TenantResourceSummaryDto>> GetPublicTenantListAsync()
{
    // 使用 IRepository<Tenant, Guid> 查询所有租户
    // 使用 IAuthorizationService 检查租户是否有公开资源
    // 返回租户摘要列表
}

[AllowAnonymous]
public async Task<TenantResourcePreviewDto> GetTenantPreviewAsync(Guid tenantId)
{
    // 使用 ICurrentTenant 切换到指定租户上下文
    // 查询该租户的资源、课程、新闻等预览信息
}
```

---

## 八、验收标准

### 8.1 功能验收

| 编号 | 验收项 | 预期结果 | 优先级 |
|------|--------|---------|--------|
| V1 | 未登录访问主页 | 显示租户资源库卡片列表 | 🔴 必须 |
| V2 | 点击租户卡片 | 显示租户预览页面 | 🟡 重要 |
| V3 | 点击登录按钮 | 跳转到 ABP 登录页面 | 🔴 必须 |
| V4 | 学生用户登录 | 跳转到 `/student` (资源库) | 🔴 必须 |
| V5 | 管理用户登录 | 保持在 ABP 管理界面 | 🔴 必须 |
| V6 | 学生访问资源库 | 正常显示资源列表 | 🔴 必须 |
| V7 | 学生访问收藏 | 正常显示收藏列表 | 🟡 重要 |
| V8 | 学生访问新闻 | 正常显示新闻列表 | 🟡 重要 |
| V9 | 非学生用户访问 /student | 被重定向到首页 | 🔴 必须 |
| V10 | 学生退出登录 | 返回主页 | 🟡 重要 |

### 8.2 技术验收

| 编号 | 验收项 | 预期结果 | 优先级 |
|------|--------|---------|--------|
| T1 | React 项目已删除 | `student-react/` 目录不存在 | 🔴 必须 |
| T2 | 学生路由已注册 | `/student/*` 路由可访问 | 🔴 必须 |
| T3 | 学生使用独立布局 | 不加载 LeptonX 布局 | 🔴 必须 |
| T4 | Portal API 可访问 | `/api/app/portal/*` 正常返回 | 🔴 必须 |
| T5 | API 代理已生成 | `proxy/portal/` 包含所有服务 | 🟡 重要 |

### 8.3 回归验收

| 编号 | 验收项 | 预期结果 | 优先级 |
|------|--------|---------|--------|
| R1 | 管理端功能正常 | ABP 模板页面正常访问 | 🔴 必须 |
| R2 | 资源管理页面 | 正常显示和管理资源 | 🔴 必须 |
| R3 | 课程管理页面 | 正常管理课程内容 | 🟡 重要 |
| R4 | AI 功能正常 | Chat、教案等页面正常 | 🟡 重要 |

---

## 九、附录

### A. 相关文件路径

```
# 学生端组件（已存在）
angular/src/app/student/
├── layout/
├── resources/
├── favorites/
├── news/
└── agent-tasks/

# 管理端组件（可复用）
angular/src/app/
├── micro-majors/
├── learning/course-detail/
└── learning/my-courses/

# 后端服务
src/KnowledgeHub.Application/Portal/PortalAppService.cs
src/KnowledgeHub.Application.Contracts/Portal/IPortalAppService.cs
```

### B. 参考文档

- ABP Angular 路由配置: `angular/src/app/app.routes.ts`
- ABP 替换组件: `angular/src/app/app.config.ts`
- 学生路由守卫: `angular/src/app/student/student-portal.guard.ts`
- 当前首页: `angular/src/app/home/home.component.ts`