# 学生端 UI 风格重构 v2

## 概述

本次重构对学生端所有页面进行了统一的风格改造，移除了"AI 味道"的蓝色渐变、紫色/天蓝等杂色，统一为**蓝色主题 + 白色卡片 + 浅灰背景**的简洁教育风格。

---

## 设计系统变更

### 文件: `angular/src/app/student/styles/_student-design.scss`

| 变量 | 旧值 | 新值 |
|------|------|------|
| `$student-primary` | `#0f766e` (青绿) | `#2563eb` (蓝色) |
| `$student-primary-dark` | `#0d5e56` | `#1d4ed8` |
| `$student-primary-light` | `#14b8a6` | `#3b82f6` |
| `$student-accent` | `#d97706` (琥珀) | `#f59e0b` |
| `$student-bg` | `#fafaf9` (暖白) | `#f8fafc` (浅灰) |
| `$student-surface-soft` | `#f5f5f4` | `#f1f5f9` |
| `$student-border` | `#e7e5e4` | `#e2e8f0` |
| `$student-text` | `#1c1917` (暖褐) | `#1e293b` (冷灰蓝) |
| `$student-text-muted` | `#78716c` | `#64748b` |

- `section-heading` mixin 移除了左侧 `::before` 装饰条

---

## 布局组件

### 文件: `angular/src/app/student/layout/student-layout.component.html`

- 品牌栏布局重构：品牌 → 主站首页 → 移动端菜单按钮 → 用户区域（右侧）
- Logo SVG 渐变色更新为 `#2563EB → #3B82F6`
- 品牌栏去除了 `flex: 1 1 auto` 的 title 颜色

### 文件: `angular/src/app/student/layout/student-layout.component.scss`

- 品牌栏高度: `68px` → `60px`
- Logo 尺寸: `44px` → `36px`
- 品牌标题字号: `22px` → `16px`
- 品牌副标题字号: `14px` → `13px`
- 品牌栏内边距 gap 统一
- "主站首页"按钮: 高度 `36px` → `32px`，字号 `13px` → `12px`
- 用户区域: 高度 `44px` → `36px`，头像 `32px` → `28px`
- Tab 栏 hover 态: `#ecfdf5` → `#e8f0fe`
- Tab 下划线: 3px → 2.5px，颜色 `#1e6ce8` → `ds.$student-primary`
- 下拉菜单 hover: `#ecfdf5` → `#f5f9ff`
- 页脚背景: `#1f2a44` → `#1c1917`
- `brand-bar__actions` 加 `margin-left: auto` 将用户信息推到右侧

---

## Hero 组件（所有页面通用）

### 文件: `angular/src/app/student/shared/student-hero/student-hero.component.html`

- 统计卡片从带图标的卡片网格改为纯文本统计（数字+标签）
- 移除 `hero-stats-card`、`hero-stat-cell`、`hero-stats-grid` 等结构

### 文件: `angular/src/app/student/shared/student-hero/student-hero.component.scss`

- Hero 从蓝色渐变 → 白色卡片 + 浅灰边框
- 标题: 28px 白色 → 24px 深色
- 描述: 白色半透明 → `ds.$student-text-muted`
- 统计卡片: 浅灰背景卡片 → 纯文本行内显示
- 所有装饰元素 (`student-hero__bg`, `student-hero__shape`) 已移除

---

## 页面级 Hero 变更

以下页面之前有独立的 Hero 样式，已统一为白色卡片风格：

| 页面 | 文件 | 旧风格 | 新风格 |
|------|------|--------|--------|
| AI 助手 | `student-chat.component.scss` | 蓝色渐变 `#1e6ce8→#0891b2` | 白色卡片 + 边框 |
| 智能搜索 | `student-search.component.scss` | 蓝色渐变 `#1e6ce8→#0ea5e9` | 白色卡片 + 边框 |
| 我的收藏 | `student-favorites.component.scss` | 蓝色渐变 `#1e6ce8` | 白色卡片 + 边框 |
| 就业服务大厅 | `student-jobs.component.scss` | 蓝色渐变 `#1e6ce8→#0c4cb8` | 白色卡片 + 边框 |
| 我的投递 | `student-my-applications.component.scss` | 蓝色渐变 | 白色卡片 + 边框 |
| 我的就业去向 | `student-my-outcomes.component.scss` | 青色渐变 `#0e7490` | 白色卡片 + 边框 |
| 我的简历 | `student-my-resumes.component.scss` | 蓝色渐变 `#1e6ce8` | 白色卡片 + 边框 |
| 招聘直播 | `student-recruitment-live.component.scss` | 紫色渐变 `#1e6ce8` | 白色卡片 + 边框 |
| 实训项目 | `student-practicums.component.scss` | 天蓝 `#0284c7` | 白色卡片 + 边框 |
| 智能体任务 | `student-agent-task-list.component.scss` | 蓝色渐变 `#1e6ce8` | 白色卡片 + 边框 |
| 我的实训 | `student-my-practicums.component.html` | 普通 div 标题 | 统一 hero 卡片结构 |
| 岗位详情 | `student-job-detail.component.scss` | 蓝色渐变 | 白色卡片 + 边框 |

---

## 统计标签统一

所有页面的统计标签从带背景/图标的卡片改为纯文本样式：

| 旧样式 | 新样式 |
|--------|--------|
| 彩色图标背景 + 白色卡片单元格 | 纯数字 + 标签文字 |
| 背景色块 `rgba(255,255,255,0.18)` | 浅灰背景 `ds.$student-surface-soft` |
| 统计圆点紫色/青色 | 统一蓝色 `ds.$student-primary` |

---

## 颜色归一化

所有页面中硬编码的蓝色值已统一替换：

| 旧色值 | 替换为 |
|--------|--------|
| `#1e6ce8` | `#2563eb` 或 `ds.$student-primary` |
| `#0c4cb8` | `#1d4ed8` 或 `ds.$student-primary-dark` |
| `#0284c7` | `#2563eb` 或 `ds.$student-primary` |
| `#0891b2` | 保留（用于流程状态指示） |
| `#e8f0fe` | 保留（hover 态） |
| `#f0f6ff` | 保留 |

---

## 影响文件清单

### 设计系统
- `angular/src/app/student/styles/_student-design.scss`

### 布局
- `angular/src/app/student/layout/student-layout.component.html`
- `angular/src/app/student/layout/student-layout.component.scss`
- `angular/src/app/student/layout/student-layout.component.ts`

### 共享组件
- `angular/src/app/student/shared/student-hero/student-hero.component.html`
- `angular/src/app/student/shared/student-hero/student-hero.component.scss`

### 页面组件
- `angular/src/app/student/ai/chat/student-chat.component.scss`
- `angular/src/app/student/search/student-search.component.html`
- `angular/src/app/student/search/student-search.component.scss`
- `angular/src/app/student/courses/student-courses.component.ts`
- `angular/src/app/student/courses/student-courses.component.scss`
- `angular/src/app/student/resources/student-resources.component.scss`
- `angular/src/app/student/resources/student-resources.component.ts`
- `angular/src/app/student/favorites/student-favorites.component.scss`
- `angular/src/app/student/micro-majors/student-micro-majors.component.ts`
- `angular/src/app/student/micro-majors/student-my-micro-majors.component.ts`
- `angular/src/app/student/micro-majors/student-my-micro-majors.component.scss`
- `angular/src/app/student/micro-majors/student-micro-major-detail.component.ts`
- `angular/src/app/student/micro-majors/student-micro-major-detail.component.scss`
- `angular/src/app/student/micro-majors/student-micro-majors.component.scss`
- `angular/src/app/student/employment/student-jobs.component.html`
- `angular/src/app/student/employment/student-jobs.component.scss`
- `angular/src/app/student/employment/student-jobs.component.ts`
- `angular/src/app/student/employment/student-job-detail.component.scss`
- `angular/src/app/student/employment/student-my-applications.component.html`
- `angular/src/app/student/employment/student-my-applications.component.scss`
- `angular/src/app/student/employment/student-my-outcomes.component.scss`
- `angular/src/app/student/employment/student-my-resumes.component.scss`
- `angular/src/app/student/recruitment-live/student-recruitment-live.component.scss`
- `angular/src/app/student/practicums/student-practicums.component.scss`
- `angular/src/app/student/practicums/student-practicums.component.ts`
- `angular/src/app/student/practicums/student-my-practicums.component.html`
- `angular/src/app/student/practicums/student-my-practicums.component.scss`
- `angular/src/app/student/practicums/student-my-practicums.component.ts`
- `angular/src/app/student/practicums/student-practicum-detail.component.scss`
- `angular/src/app/student/agent-tasks/student-agent-task-list.component.scss`
- `angular/src/app/student/agent-tasks/student-agent-task-list.component.ts`