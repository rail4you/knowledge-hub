# 学生端迁移开发任务清单

## 📋 概述

**目标**: 将 React 学生端迁移到 Angular，移除 React 项目，统一使用 Angular 作为唯一前端技术栈

**当前状态**: 
- ✅ 学生端路由已注册
- ✅ 登录跳转逻辑已修改
- ✅ React 项目已删除
- ✅ 租户列表 API 已添加
- ✅ 主页可展示租户列表

**预计工期**: 8 天

---

## ✅ 完成的任务

### Phase 0: 规划
- [x] 创建迁移计划文档 MIGRATION_PLAN.md
- [x] 审查计划并识别关键 blocker
- [x] 提交计划到 git

### Phase 1: 关键修复（Blocker）
- [x] 创建 `student.routes.ts` 学生路由模块
- [x] 在 `app.routes.ts` 中注册学生路由
- [x] 修改 `home.component.ts` 登录跳转逻辑（从 localhost:3000 改为 /student）
- [x] 简化学生端布局导航菜单

### Phase 0.5：删除 React 项目
- [x] 删除 `student-react/` 目录
- [x] 提交删除到 git

### Phase 2: 后端 API 扩展
- [x] 在 `IPortalAppService` 添加 `GetPublicTenantListAsync()` 方法
- [x] 在 `PortalAppService` 实现租户列表查询
- [x] 添加 `TenantResourceSummaryDto` DTO

### Phase 3: 前端 Portal 服务
- [x] 创建 `proxy/portal/portal.service.ts`
- [x] 重构 `home.component` 展示租户资源库卡片
- [x] 添加租户区域样式

---

## 🎯 待执行任务

### Phase 4: 完善学生端页面
- [ ] 创建微专业页面 `/student/micro-majors`
- [ ] 创建课程详情页面 `/student/courses/:id`
- [ ] 创建学习进度页面 `/student/progress`

### Phase 5: 测试
- [ ] 测试学生登录流程
- [ ] 测试租户列表展示
- [ ] 测试学生端各页面
- [ ] 回归测试管理端功能

### Phase 6: 清理与文档
- [ ] 更新 AGENTS.md 中的学生端路由说明
- [ ] 更新 README.md

---

## 📁 文件变更清单

### 已修改文件

```
angular/src/app/
├── app.routes.ts                  # 添加学生路由
├── student/
│   ├── student.routes.ts          # 新增
│   └── layout/
│       └── student-layout.component.html  # 简化导航

src/KnowledgeHub.Application/
└── Portal/PortalAppService.cs    # 添加 GetPublicTenantListAsync

src/KnowledgeHub.Application.Contracts/
└── Portal/IPortalAppService.cs   # 添加公开 API 方法
```

---

## ⏱️ 进度追踪

| 日期 | 阶段 | 完成度 | 备注 |
|------|------|--------|------|
| 2026-05-31 | Phase 0-3 | ✅ | 路由注册、删除React、添加API |
| ... | Phase 4-6 | ⬜ 0% | 未开始 |

---

## 🔗 相关文档

- [MIGRATION_PLAN.md](./MIGRATION_PLAN.md) - 完整迁移计划
- [AGENTS.md](./AGENTS.md) - 项目指南