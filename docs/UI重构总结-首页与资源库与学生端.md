# 首页 / 资源库页 / 学生端 UI 重构总结

> 重构基准：交互设计原型 `iketong-prototype.html`（首页 `#/home`、学生工作台 `#/s-dashboard`）
> 重构目标：全站统一为 home-v2 视觉语言 —— **墨青绿主色 + 暖白底 + 沙金点缀 + 色块封面卡片**
> 约束：**数据对接不变、路由不变、业务功能不变**，只改样式与布局

---

## 目录

- [一、统一设计令牌](#一统一设计令牌)
- [二、首页（PortalHome）](#二首页portalhome)
- [三、资源库页（TenantHomepage）](#三资源库页tenanthomepage)
- [四、学生端](#四学生端)
- [五、去英文文案](#五去英文文案)
- [六、顺带修复的问题](#六顺带修复的问题)
- [七、新增资产](#七新增资产)
- [八、验证结果](#八验证结果)
- [九、后续可继续项](#九后续可继续项)

---

## 一、统一设计令牌

全站（首页 / 资源库页 / 学生端）使用同一套色板：

| 用途 | 变量 | 值 |
|---|---|---|
| 主品牌色 | `--brand` / `$student-primary` | `#087f7a` |
| 品牌深色 | — | `#086964` |
| 品牌浅色 | — | `#4aa79f` |
| 品牌浅底 | — | `#e5f3ef` |
| 暖白页面底 | `$page` / `$student-bg` | `#f8f9f6` |
| 深色 Hero | — | `#103c43` / `#0c282b` |
| 正文 | `$ink` / `$student-text` | `#183444` |
| 次要文字 | — | `#697e88` |
| 弱化文字 | — | `#8a99a1` |
| 分割线 | — | `#e1e7e0` |
| 细分割线 | — | `#edf0eb` |
| 沙金点缀 | `$student-accent` | `#d1ad73` |
| 卡片圆角 | — | `11px`（卡片）/ `14px`（大卡）|

**学生端令牌变更**（`student/styles/_student-design.scss`）：由 v3 学院蓝主题升级为 v4 墨青绿主题。

| 令牌 | 旧值 | 新值 |
|---|---|---|
| `$student-primary` | `#2b6cd4` | `#087f7a` |
| `$student-primary-dark` | `#1f56ad` | `#086964` |
| `$student-primary-light` | `#5b93db` | `#4aa79f` |
| `$student-accent` | `#f59e0b` | `#d1ad73` |
| `$student-bg` | `#f8fafc` | `#f8f9f6` |
| `$student-surface-soft` | `#f1f5f9` | `#f2f5f1` |
| `$student-border` | `#e2e8f0` | `#e1e7e0` |
| `$student-border-soft` | `#eef2f6` | `#edf0eb` |
| `$student-text` | `#1e293b` | `#183444` |
| `$student-text-muted` | `#64748b` | `#697e88` |
| `$student-text-light` | `#94a3b8` | `#8a99a1` |

同时更新 `angular/src/styles.scss` 中 `body:has(.student-layout)` 的 ng-zorro 主题收敛块（按钮 / 分页 / 进度条 / 开关 / 复选 / 单选 / 标签 / Tabs / 菜单 / 表格排序 / Spin），共 18 处硬编码色。

---

## 二、首页（PortalHome）

**文件**：`angular/src/app/home/portal-home.component.{html,scss,ts}`

### 结构（严格对齐原型 home-v2）

```
Top Nav（88px 白底 + 居中导航 + 绿色按钮）
└ Hero 轮播（3 张背景图 + 深色渐变遮罩 + 底部 selector/箭头/暂停）
└ home-content
  ├ library-section  资源库（按租户生成）
  ├ featured-section 精选课程（#eef2e9 浅绿卡区）
  ├ micro-section    微专业（方向筛选 + 微头卡片）
  ├ practice-banner  实训横幅（深绿 + 知行合一水印）
  ├ home-bottom      教学资源 + 最新资讯（双栏）
  └ browse-section   全部资源（课程/资源/微专业 tabs + 筛选 + 分页）
└ home-footer（浅绿单行）
```

### 关键实现

| 项 | 说明 |
|---|---|
| Hero 轮播 | 3 张本地 webp 背景图 + 三层渐变遮罩；底部 `selector`（编号 + 标签 + 下划线指示器）、计数器 `01 / 03`、圆形左右箭头、**暂停/播放**按钮 |
| 卡片视图模型 | 新增 `LibraryCardVm` / `MicroCardVm` 计算属性，把租户 / 微专业 / 课程数据映射为原型卡片结构 |
| 资源库卡片 | 顶部色条 + 图标 + 编号 + 英文行 + 标题 + 简介 + 精选课程列表 + 统计标签 + 「进入资源库」 |
| 课程卡片 | 原型 `.cover` 色块（c0~c3 四种）+ 装饰圆环 + 标签 + 大标题；有真实封面图时用图片 |
| 微专业卡片 | 微头色块（sage/blue/sand 轮转）+ 编号 + 水印图标 + 分类 + 标题 + 「你将学习」课程列表 |
| 微专业筛选 | `全部方向` + 按租户动态生成的分类按钮 |
| 全部资源 | 保留原有租户/专业筛选、搜索、分页，用 home-v2 卡片语言重绘 |

> **踩坑记录**：`.home-v2 button { background: none }` 的特指度（0,3,1）会盖住 `.btn.btn--primary`（0,3,0），导致主按钮透明。已改用 `:where(button:not(.ant-btn):not(.ant-pagination-item-link))` 降低特指度。

---

## 三、资源库页（TenantHomepage）

**文件**：`angular/src/app/admin/tenant-info/tenant-homepage.component.{html,scss,ts}`

### 结构

```
Top Nav（与首页完全一致）
└ Hero（首页背景图 hero-1.webp 图书馆场景 + 深色遮罩 + 标题/简介/统计 + 4 个分区导航卡）
└ page-content
  ├ 资源库简介（白卡 + 绿色顶条 + 内联统计）
  ├ 专业建设（2 列卡片 + 绿色图标块）
  ├ 特色项目（浅绿卡片 + 编号胶囊）
  ├ 学历课程（课程卡片网格 + 难度筛选 + 已选课徽章 + 预览弹窗）
  └ 知识图谱（白卡容器）
└ home-footer
```

### 关键实现

- **Hero 背景**：固定使用首页资产 `assets/images/home-v2/hero-1.webp`（图书馆内景，左侧深色区正好承载文字），不再使用租户自身封面，保证各资源库页视觉统一
- **课程卡片**：由原来的行式列表改为原型色块卡片网格，保留难度筛选、已选课徽章、点击预览/进入
- **知识图谱配色**：节点色改为首页调色板 —— 资源库 `#087f7a`、专业 `#3f7fa8`、课程 `#b0803a`；连线与 tooltip 同步
- **课程预览弹窗**：头部渐变改为 `#103c43 → #087f7a`

---

## 四、学生端

### 4.1 布局骨架：顶栏 → 左侧边栏

**文件**：`angular/src/app/student/layout/student-layout.component.{html,scss,ts}`

| 区域 | 规格 |
|---|---|
| 侧边栏 | 232px 固定，白底 + 右细线；品牌 lockup + **机构卡片** + 分组导航 |
| 分组 | `学习空间`（10 项）/ `成长与服务`（8 项）/ `更多`（返回主站 / 退出） |
| 信息栏 | 76px sticky，面包屑（`学生空间 / 当前页`）+ 搜索/铃铛快捷入口 + 用户胶囊下拉 |
| 内容区 | 自适应，`$student-bg` 暖白底 |

- **激活态**：浅底 + 品牌左竖条
- **路由匹配**：新增「最具体路由匹配」算法，避免 `/student/practicums` 与 `/student/practicums/my` 同时点亮
- **面包屑**：由 `pageTitle()` 从侧边栏分组反查当前路由标题
- **移动端**（≤900px）：侧边栏隐藏，保留原右侧全屏抽屉（汉堡、遮罩、钻取子菜单、账户区）

### 4.2 新增「学习工作台」页

**文件**：`angular/src/app/student/dashboard/student-dashboard.component.{ts,html,scss}`

路由 `/student/dashboard`，并把 `/student` 的默认重定向由 `courses` 改为 `dashboard`。

| 区块 | 数据来源 |
|---|---|
| 标题栏 4 指标（在学课程 / 学习时长 / 已完成 / 习题练习） | `LearningService.getDashboard()` |
| 深色欢迎横幅 + `Learn.` 水印 + 双 CTA | 用户名（`ConfigStateService`）+ 静态文案 |
| 继续上次的学习（课程 + 进度条 + 习题 x/y） | `getMyCourses()` 过滤进度 0~100，按进度降序取 4 条 |
| 学习节奏（7 天柱状）+ 本月目标（2 条进度） | `dailyTimeLabels` / `dailyTimeValues`、`completedCourses`、`totalExerciseRecords` |
| 为你推荐 4 入口 | 课程资源 / 智能答疑 / 实训中心 / 就业机会 |

### 4.3 共享页头组件升级

**文件**：`angular/src/app/student/shared/student-hero/`

所有学生页共用，一次改动全站受益：

- 顶部 **品牌渐变细线**
- 标题前 **品牌竖条**，字号 24 → 26px
- 统计区 **左侧分隔线 + 品牌色数值**（等宽数字）
- 移动端：竖条变细、统计收进浅底网格

### 4.4 课程中心卡片

**文件**：`angular/src/app/student/courses/`

- 封面改为 **色块 c0~c3（四种清新底色）+ 装饰圆环 + 左上级别标签 + 大标题**；有真实封面图时仍显示图片
- 难度 / 已选课 / 推荐徽章叠加在封面上
- 正文：标题 + 专业标签 → `教师 · 学时 · 学分` → 学习人数/章节 → 学习进度 → 未选课禁用按钮
- 网格由 `auto-fill` 改为原型三列（1280px 两列 / 760px 单列）

### 4.5 微专业卡片

**文件**：`angular/src/app/student/micro-majors/`

- 新增 **微头色块**（tone-0/1/2 三色轮转）：编号 + 奖杯水印 + 分类标签 + 大标题 + 简述
- 有封面图时叠加白渐遮罩，保证文字可读
- 保留属性行（行业 / 门课程 / 人报名）、报名状态徽章、报名按钮、分页

### 4.6 旧配色清理

| 位置 | 数量 | 改动 |
|---|---|---|
| 学生端 SCSS 硬编码旧蓝 | 155 处 | `#2b6cd4` / `#1f56ad` / `#5b93db` / `rgba(43,108,212,…)` → 品牌绿系 |
| 学生端 TS 内联色（ECharts 渐变、统计色） | 91 处 | 同上色相迁移 |
| 封面占位渐变 | 3 处 | `#1e3a8a→#0f172a`、`#21598c→#0f172a` → `#4aa79f→#087f7a` |
| 深色块 | 2 处 | `#1e293b` 系 → `#103c43` 系（保留 wasm 播放器深色画布） |
| 占位 SVG | 14 个 | 整套蓝色系色相映射为品牌绿，保留红/绿/琥珀等语义色 |

---

## 五、去英文文案

按要求移除页面中所有「英文描述类」文案（保留真实数据中的英文）：

| 页面 | 移除内容 |
|---|---|
| 首页 | Hero eyebrow（`KNOWLEDGE OPENS NEW POSSIBILITIES` 等 3 条）、`RESOURCE LIBRARIES`、`FEATURED COURSES`、`MICRO PROGRAMS`、`MICRO PROGRAM`、`LEARNING BY DOING`、`RESOURCES`、`WHAT'S NEW`、`BROWSE ALL`、资源库卡英文行、品牌英文副标题、`VIDEO` → 「视频」 |
| 资源库页 | Hero eyebrow（`KNOWLEDGE RESOURCE LIBRARY` / 行业领域大写）、`OVERVIEW`、`CONSTRUCTION`、`SPECIAL PROJECTS`、`COURSES`、`KNOWLEDGE GRAPH`、品牌英文副标题 |
| 同步废弃代码 | `HeroSlide.en`、`LibraryCardVm.en`、`MicroCardVm.en`、`heroEyebrow` computed 及相关 SCSS（`.home-kicker` / `.eyebrow` / `.library-en`）|

---

## 六、顺带修复的问题

| 问题 | 根因 | 处理 |
|---|---|---|
| 主按钮透明 | `.home-v2 button` 特指度高于 `.btn.btn--primary` | 改用 `:where()` 降低特指度 |
| 学生端大量 `assets/outline/*.svg` 404 | 模板用到但未注册的 ng-zorro 图标 | 补齐 **32 个**图标注册（`MenuOutline`、`PieChartOutline`、`HourglassOutline`、`PushpinOutline`、`FolderFill`、`LockOutline`、`WarningOutline` 等）|
| 我的学习页 ECharts 告警 | 使用了 `title` 组件但未 import | 补 `TitleComponent` |
| 测试账号 `zmq` 无法登录 | 密码非 `123456` | 用标准 ASP.NET Identity V3 格式（PBKDF2-HMAC-SHA512 / 100000 / 16B salt）重置为 `123456`，并清空失败计数与锁定时间 |
| Angular 模板编译错误 | 手写 `} else {` 缺 `@` | 改为 `} @else {` |

---

## 七、新增资产

```
angular/src/assets/images/home-v2/
  hero-1.webp     图书馆内景（首页轮播 01 + 资源库页 Hero）
  hero-2.webp     学生学习场景（首页轮播 02）
  hero-3.webp     实验室场景（首页轮播 03）
  practice.webp   实训横幅背景
```

提取自原型内嵌的 base64 webp 图片，转为本地静态资源。

---

## 八、验证结果

### 首页 / 资源库页

- `/` 首页：Hero 轮播切换、暂停/播放、selector、微专业筛选、全部资源 tabs + 筛选 + 分页、全部跳转链接正常
- `/tenant/:id`：两个租户（有封面 / 无封面）Hero 背景均正确回退到 `hero-1.webp`
- 桌面 1440 与移动 390 两套布局正常，控制台 0 error

### 学生端（20 个路由全部 0 error）

```
dashboard  courses  resources  micro-majors  my-micro-majors  my-learning
favorites  news  agent-tasks  practicums  practicums/my
employment/{jobs,my-resumes,my-applications,my-outcomes,guidance}
recruitment-live  ai/chat  search  info/about
```

> 仅剩少量 `resource-file` 缩略图 404，属测试数据本身没有文件，非代码问题。

---

## 九、后续可继续项

1. **课程学习页**：播放器区 + 笔记/资料/答疑 tabs 的进一步细化（当前为章节树 + 章节资源表格，功能完整）
2. **资源详情页**：右侧信息栏卡片化、相关资源卡片对齐
3. **微专业详情页**：培养路径时间轴可视化
4. **实训详情 / 仿真页**：任务步骤条与仿真容器视觉统一
5. **岗位详情页**：1483 行 SCSS 可精简，卡片与 info 区块对齐 home-v2
6. **教师端 / 管理后台**：`admin/employment/employment-application-management`、`learning/knowledge-graph`、`recruitment-live/live-room`、`practicum/practicum-chat` 共用学生端令牌，已完成色相迁移，但尚未做结构级重构

---

## 附：改动规模

| 项 | 数值 |
|---|---|
| 改动文件 | 82 |
| 新增目录 | `student/dashboard/`、`assets/images/home-v2/` |
| 代码变化 | +4146 / −4445 行 |
| 核心文件 | `portal-home.*`（+917/−…）、`tenant-homepage.*`、`student-layout.*`、`_student-design.scss`、`styles.scss`、`app.config.ts` |
