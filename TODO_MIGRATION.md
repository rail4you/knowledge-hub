# 学生端迁移开发任务清单

## 📋 概述

**目标**: 将 React 学生端迁移到 Angular，移除 React 项目，统一使用 Angular 作为唯一前端技术栈

**当前状态**: 
- 大部分学生端组件已创建，但路由未注册（最大 blocker）
- 后端 PortalAppService 已实现
- React 项目仍存在，需要删除

**预计工期**: 8 天

---

## ✅ 完成的任务

### Phase 0: 规划
- [x] 创建迁移计划文档 MIGRATION_PLAN.md
- [x] 审查计划并识别关键 blocker
- [x] 提交计划到 git

---

## 🎯 待执行任务

### Phase 0: 清理 React 项目

- [ ] **删除 student-react 目录**
  ```bash
  rm -rf student-react/
  git add -A
  git commit -m "chore: 移除 React 学生端项目"
  ```

### Phase 1: 关键修复（Blocker）

#### 1.1 注册学生端路由 ⚠️ **最重要**
- [ ] 在 `app.routes.ts` 中添加学生路由
- [ ] 导入 `studentPortalGuard`
- [ ] 创建 `student.routes.ts` 模块
- [ ] 验证路由可访问

**文件**: `angular/src/app/app.routes.ts`
```typescript
{
  path: 'student',
  canActivate: [authGuard, studentPortalGuard],
  loadChildren: () => import('./student/student.routes').then(m => m.STUDENT_ROUTES),
},
```

#### 1.2 修改登录跳转逻辑
- [ ] 修改 `home.component.ts` 的 ngOnInit
- [ ] 从 `window.location.href = 'http://localhost:3000'` 改为 `router.navigate(['/student'])`
- [ ] 测试学生登录后跳转

**文件**: `angular/src/app/home/home.component.ts`

#### 1.3 创建学生路由模块
- [ ] 创建 `angular/src/app/student/student.routes.ts`
- [ ] 注册所有学生端路由
- [ ] 配置学生布局组件

### Phase 2: 后端 API 扩展

#### 2.1 添加租户列表 API
- [ ] 在 `IPortalAppService` 添加 `GetPublicTenantListAsync()`
- [ ] 在 `PortalAppService` 实现该方法
- [ ] 添加 `[AllowAnonymous]` 特性
- [ ] 添加对应的 DTO 类

**文件**:
- `src/KnowledgeHub.Application.Contracts/Portal/IPortalAppService.cs`
- `src/KnowledgeHub.Application/Portal/PortalAppService.cs`

#### 2.2 添加租户预览 API
- [ ] 在 `IPortalAppService` 添加 `GetTenantPreviewAsync(Guid tenantId)`
- [ ] 实现租户资源库预览查询
- [ ] 添加对应的 DTO 类

### Phase 3: 重构主页

#### 3.1 重构 home.component.html
- [ ] 添加租户卡片列表展示
- [ ] 添加 nz-card 组件
- [ ] 添加加载状态处理

#### 3.2 创建租户预览页面
- [ ] 创建 `home/tenant-preview.component.ts`
- [ ] 实现租户资源库预览功能
- [ ] 添加公开访问支持

### Phase 4: 完善学生端页面

#### 4.1 微专业页面
- [ ] 创建 `student/micro-majors/student-micro-majors.component.ts`
- [ ] 复用管理端 `micro-majors/` 组件
- [ ] 使用学生端布局

#### 4.2 课程详情页面
- [ ] 创建 `student/courses/student-course-detail.component.ts`
- [ ] 复用 `learning/course-detail/` 组件
- [ ] 添加学习进度跟踪

#### 4.3 学习进度页面
- [ ] 创建 `student/progress/student-progress.component.ts`
- [ ] 实现进度统计图表
- [ ] 添加课程完成情况

### Phase 5: 测试与部署

#### 5.1 功能测试
- [ ] 未登录访问主页显示租户列表
- [ ] 点击登录按钮跳转 ABP 登录页
- [ ] 学生登录后跳转到 /student/resources
- [ ] 管理用户登录后保持在 ABP 管理界面
- [ ] 学生端各页面正常访问
- [ ] 学生路由守卫正确阻止非学生用户

#### 5.2 回归测试
- [ ] 管理端功能正常
- [ ] 资源管理页面正常
- [ ] 课程管理页面正常
- [ ] AI 功能正常

### Phase 6: 清理与文档

- [ ] 确认 React 项目已删除
- [ ] 更新 AGENTS.md 中的学生端路由说明
- [ ] 更新 README.md 中的项目说明
- [ ] 生成 Angular API 代理

---

## 📁 文件变更清单

### 新增文件
```
angular/src/app/student/
├── student.routes.ts              # 学生路由模块
├── micro-majors/
│   └── student-micro-majors.component.ts
├── courses/
│   └── student-course-detail.component.ts
└── progress/
    └── student-progress.component.ts

angular/src/app/home/
└── tenant-preview.component.ts    # 租户预览页面

src/KnowledgeHub.Application/
└── Portal/PortalAppService.cs    # 扩展公开 API
```

### 修改文件
```
angular/src/app/
├── app.routes.ts                  # 【关键】添加学生路由
├── home/
│   ├── home.component.ts          # 修改登录跳转逻辑
│   └── home.component.html       # 重构为租户展示页面

src/KnowledgeHub.Application.Contracts/
└── Portal/IPortalAppService.cs   # 添加公开 API 方法

src/KnowledgeHub.Domain/
└── (可能需要添加新的 DTO)
```

### 删除文件
```
student-react/                     # 整个目录删除
```

---

## 🧪 验收标准

| 编号 | 验收项 | 预期结果 |
|------|--------|---------|
| V1 | 未登录访问主页 | 显示租户资源库卡片列表 |
| V2 | 点击租户卡片 | 显示租户预览页面 |
| V3 | 点击登录按钮 | 跳转到 ABP 登录页面 |
| V4 | 学生用户登录 | 跳转到 /student (资源库) |
| V5 | 管理用户登录 | 保持在 ABP 管理界面 |
| V6 | 学生访问资源库 | 正常显示资源列表 |
| V7 | 非学生用户访问 /student | 被重定向到首页 |
| T1 | React 项目已删除 | student-react/ 目录不存在 |
| T2 | 学生路由已注册 | /student/* 路由可访问 |
| R1 | 管理端功能正常 | ABP 模板页面正常访问 |

---

## ⏱️ 进度追踪

| 日期 | 阶段 | 完成度 | 备注 |
|------|------|--------|------|
| 2026-05-31 | Phase 0 | ✅ | 完成规划 |
| 2026-05-31 | Phase 1 | ⬜ 0% | **进行中** |
| ... | Phase 2-6 | ⬜ 0% | 未开始 |

---

## 🔗 相关文档

- [MIGRATION_PLAN.md](./MIGRATION_PLAN.md) - 完整迁移计划
- [AGENTS.md](./AGENTS.md) - 项目指南
- [CLAUDE.md](./CLAUDE.md) - 开发规则