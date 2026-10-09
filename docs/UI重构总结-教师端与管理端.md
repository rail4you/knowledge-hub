# 教师端 / 管理端 UI 重构总结

> 重构基准：交互设计原型 `iketong-prototype.html`（教师端 `#/t-dashboard`、管理端 `#/a-dashboard` 及 `#/t-courses`、`#/a-users` 等列表页）
> 重构目标：把首页 / 资源库页 / 学生端已经落地的 **home-v2 视觉语言**（墨青绿主色 + 暖白底 + 沙金点缀 + 色块/发丝线卡片）推广到教师端与管理端
> 约束：**数据对接不变、路由不变、业务功能不变**，只改样式、布局、卡片、表格等 UI 元素

---

## 目录

- [一、统一设计令牌](#一统一设计令牌)
- [二、应用外壳（LeptonX）](#二应用外壳leptonx)
- [三、ng-zorro 组件层](#三ng-zorro-组件层)
- [四、新增共享 UI 原子](#四新增共享-ui-原子)
- [五、系统工作台（管理端 / 教师端首页）](#五系统工作台管理端--教师端首页)
- [六、旧配色清理](#六旧配色清理)
- [七、页面标题（eyebrow）](#七页面标题eyebrow)
- [八、验证结果](#八验证结果)
- [九、后续可继续项](#九后续可继续项)

---

## 一、统一设计令牌

**文件**：`angular/src/styles/_design-tokens.scss`

把原先的「品牌蓝 `#1890ff` + 冷灰画布 `#f5f7fa` + 深蓝黑侧边栏」整套 `--kh-*` 令牌，替换为与首页 home-v2 完全一致的调色板。**变量名保持不变**，因此所有已使用 `var(--kh-*)` 的页面自动跟随升级。

| 用途 | 变量 | 旧值 | 新值 |
|---|---|---|---|
| 画布 | `--kh-canvas` | `#f5f7fa` | `#f8f9f6` |
| 面板 | `--kh-panel` | `#ffffff` | `#ffffff` |
| 正文 | `--kh-ink` | `#1c2733` | `#183444` |
| 次要文字 | `--kh-ink-2` | `#46586a` | `#3d5563` |
| 弱化文字 | `--kh-muted` | `#5f6f81` | `#697e88` |
| 分割线 | `--kh-line` / `--kh-line-2` | 蓝相 `rgba(24,144,255,.10)` / `#e7edf5` | `#e1e7e0` / `#edf0eb` |
| 主品牌色 | `--kh-accent` | `#1890ff` | `#087f7a` |
| 品牌深色 | `--kh-accent-deep` | `#0a6fce` | `#086964` |
| 品牌浅色 | `--kh-accent-light`（新增） | — | `#4aa79f` |
| 品牌浅底 | `--kh-accent-soft` | `rgba(24,144,255,.10)` | `#e5f3ef` |
| 深色 Hero | `--kh-deep` / `--kh-deep-2`（新增） | 深蓝黑 | `#103c43` / `#0c282b` |
| 沙金点缀 | `--kh-gold` / `--kh-gold-soft`（新增） | — | `#d1ad73` / `#faf1df` |
| 语义色 | `--kh-ok` / `--kh-warn` / `--kh-bad` | `#52c41a` / `#e8871a` / `#e5484d` | `#2f9e68` / `#c98a2b` / `#cf4a44` |
| 侧边栏 | `--kh-side` | `#0d1b30` | `#ffffff` |
| 卡片圆角 | `--kh-r-shell` / `--kh-r-card` / `--kh-r-control` | 16 / 14 / 10 | 14 / 11 / 8 |
| 阴影 | `--kh-shadow-card` / `--kh-shadow-pop` | 蓝相分层阴影 | 暖调分层阴影 |

新增：`--kh-surface-soft`、`--kh-surface-sunk`、`--kh-card-bg`、`--kh-border`、`--kh-faint`、`--kh-on-deep`、`--kh-warm`、`--kh-ok-soft`、`--kh-warn-soft`、`--kh-warn-bg`、`--kh-bad-soft`、`--kh-info`、`--kh-info-soft`、`--kh-shadow-hover`。

---

## 二、应用外壳（LeptonX）

**文件**：`angular/src/styles/_leptonx-overrides.scss`

### 侧边栏：深蓝黑 → 白底（对齐原型）

| 项 | 旧 | 新 |
|---|---|---|
| 侧边栏底色 | `#0d1b30` 渐变 | `#ffffff` + 右侧发丝线 |
| Logo 容器 | 深色渐变 | 白色 + 底部发丝线 |
| 菜单文字 | `#c6d2e2` | `--kh-ink-2` |
| 菜单 hover | 白色半透明 | 品牌浅底 `--kh-accent-ghost` |
| 选中态 | 蓝色渐变胶囊 + 内描边 | **品牌浅底**（原型激活态，无左竖条） |
| 一级图标 | `#7e92ac` | `#7f939c`，选中转品牌绿 |
| 二级 / 三级菜单 | 深色半透明 | 白色 / `--kh-surface-soft`，选中品牌浅底 |
| 折叠按钮 | 白字深底 | 墨字浅底，hover 品牌绿底 |
| 折叠弹出子菜单 | 深色渐变 + 黑阴影 | 白底 + 暖色阴影 |

> 关键点：框架基线把 `.lpx-nav-menu .lpx-inner-menu-item .lpx-menu-item-link.selected` 写死为 `background:#222`（4 个类名），必须用 `#lpx-wrapper` / `.lpx-sidebar` 前缀提升特指度才能覆盖，否则二级菜单选中会残留黑底。

### 顶部栏 / 画布 / 页脚

- 画布 `--lpx-body-bg`、`--lpx-content-bg` → `#f8f9f6`
- `.lpx-topbar` → 白底 + 发丝线 + 底部 14px 圆角
- 面包屑 hover/当前项 → 品牌绿
- 头像 → 品牌绿渐变
- `--lpx-brand` / `--bs-primary` → `#087f7a`

---

## 三、ng-zorro 组件层

**文件**：`angular/src/styles/_ngzorro-overrides.scss`

| 组件 | 改动 |
|---|---|
| 卡片 `.ant-card` | 圆角 14px、发丝线、暖调阴影、标题 15.5px |
| 按钮 `.ant-btn-primary` | 品牌绿 + 墨绿 hover，去掉蓝色发光阴影 |
| 表格 | 表头 `#fafbf9` + 弱化色文字，**去掉斑马纹**，改为发丝线分隔 + hover 品牌浅底 |
| 标签 `.ant-tag` | 胶囊圆角；green/blue/orange/gold/red/cyan/geekblue 全部改为品牌 / 语义浅底 |
| 弹窗 / 抽屉 | 14px 圆角、暖色阴影、页脚浅底 |
| 分页 | 选中项品牌绿实底；hover 品牌绿描边 |
| 输入 / 选择 / 日期 | 8px 圆角，聚焦 3px 品牌浅色光环 |
| 开关 / 复选 / 单选 / 进度 / Spin / Tabs / 排序 | 全部蓝色 → 品牌绿 |
| 下拉选中项 | 品牌浅底 + 品牌深色文字 |

> 学生端原本单独有一段 `body:has(.student-layout)` 的 ng-zorro 收敛块（只作用学生端）；教师端 / 管理端现在由本文件统一收敛，两端不再分叉。

---

## 四、新增共享 UI 原子

**文件**：`angular/src/styles/_kh-ui.scss`（新增，在 `styles.scss` 中 `@import`）

把原型的核心区块抽象为纯类名，供教师端 / 管理端页面按需组合（无逻辑、无 JS）：

| 类名 | 对应原型 | 说明 |
|---|---|---|
| `.kh-page` | `.wrap` | 页面容器（max-width 1400 + 24px 间距 + 22px 纵向 gap） |
| `.kh-page-head` | `.pagehead` | 页头：标题 + 描述 + 右侧操作 |
| `.kh-eyebrow` | `.eyebrow` | 小号字距标签（全站中文化，不再用英文） |
| `.kh-banner` / `.kh-btn-light` | `.welcome` | 深绿欢迎横幅（渐变 + 水印 + 浅色按钮） |
| `.kh-metrics` / `.kh-metric` | `.metric` | 关键指标条（4 列 → 2 列 → 1 列响应式） |
| `.kh-card` / `.kh-card--pad` / `.kh-card--click` | `.card` | 圆角卡片 + 可点击上浮 |
| `.kh-section-head` | `.section-head` | 区块标题 |
| `.kh-list-row` | `.list-row` | 列表行（图标块 + 文本 + 操作） |
| `.kh-tile` | `.tile-icon` | 图标块（默认品牌 / gold / info / sm） |
| `.kh-chip` | `.chip` | 胶囊筛选按钮（active 品牌实底） |
| `.kh-tag` | `.tag` | 状态标签（默认 / gold / gray / ok / bad / info） |
| `.kh-progress` | `.progress` | 细进度条 |
| `.kh-filters` / `.kh-empty` | `.filters` / `.empty` | 筛选行 / 空态 |

---

## 五、系统工作台（管理端 / 教师端首页）

**文件**：`angular/src/app/admin/workbench/workbench.component.{html,scss,ts}`

原页面是「页头卡片 + 趋势卡 + 模块网格」，现按原型 `#/a-dashboard`、`#/t-dashboard` 重排：

```
kh-page
├ kh-page-head    eyebrow「管理工作台 / 教学工作台」+ 标题 + 描述 + 右侧（租户徽标 / 统计时间 / 刷新）
├ kh-banner       深绿欢迎横幅（水印「致远 / 教研」）+ 双 CTA
├ kh-metrics      4 张关键指标卡（资源总量 / 课程总数 / 活跃师生 / AI 成功率）
├ dashboard-stack
│  ├ trend-panel  使用趋势（指标下拉 + 折线图 + 今天/昨天/近7天汇总表）
│  └ modules-panel 模块概览（原有 9 个模块卡片，改为原型卡片语言）
```

- **全部数据对接保持不变**：仍然调用 `WorkbenchService.getStats()`，仍然支持 host 租户切换、`r` 键刷新、Ctrl/Cmd+K 快速导航。
- **角色自适应**（新增）：通过 `hasAnyRole` 判断「纯教师（持有 Teacher、不持有 SchoolAdmin/LeagueAdmin/admin/EnterpriseUser）」时，页头与横幅切换为教师文案（「教学工作台 · 让教学更从容」「我的教学课程 / AI 辅助备课」入口），其余保持管理端文案。教师账号（`Teacher` 属于 `ADMIN_ROLES`）登录后落到本页，因此本次等价于原型的「教师工作台」。
- 模块卡片图标块由 36px/9px 圆角升级为 40px/12px，hover 上浮；模块区底色由冷蓝渐变改为暖色 `--kh-surface-soft`。

---

## 六、旧配色清理

对 `angular/src/app/**`（**不含 `student/`**）与 `angular/src/styles/**` 做了脚本化色相迁移，共 **130+ 文件**：

| 类别 | 旧值 | 新值 |
|---|---|---|
| 品牌蓝 | `#1890ff` `#40a9ff` `#096dd9` `#0050b3` `#69c0ff` `#91d5ff` `#bae7ff` `#d6e4ff` `#e6f7ff` `#f0f5ff` `#1677ff` `#2563eb` `#1e6ce8` `#2b6cd4` `#2b6fce` `#3da5ff` `#4da7ff` | 品牌绿系 `#087f7a` / `#4aa79f` / `#086964` / `#e5f3ef` … |
| 蓝色透明 | `rgba(24, 144, 255, x)` | `rgba(8, 127, 122, x)` |
| 深蓝黑 | `#0d1b30` `#0e1c31` `#0b1729` `#13233a` `#123c4a` | 墨青深色 `#103c43` / `#12434b` / `#0c282b` |
| 冷灰底 | `#f5f7fa` `#f7fafd` `#eef3f9` `#f9fbfe` `#fbfcfe` `#f8fafd` `#f0f3f8` `#fafbfd` `#f7f9fc` … | 暖白 `#f8f9f6` / `#fafbf8` / `#fafbf9` / `#f0f3ee` … |
| 冷灰线 | `#e7edf5` `#e8edf3` `#eef3f9` `#eef0f3` `#e2e8f0` `#d4dde8` `#c3ccd8` | `#e8ece6` / `#edf0eb` / `#e1e7e0` / `#d2d9d2` / `#c2cbc4` |
| 中性灰 | `#fafafa` `#f5f5f5` `#f0f0f0` `#e8e8e8` `#d9d9d9` `#bfbfbf` `#8c8c8c` | 暖中性 `#fafbf8` / `#f4f6f2` / `#eff1ec` / `#e8ebe5` / `#d8dcd5` / `#b8c1ba` / `#8a99a1` |
| 冷灰文字 | `#64748b` `#6b7280` `#8a94a6` `#8a93a6` `#1f2937` `#1f2933` `#1f1f1f` `#262626` `#595959` | `#697e88` / `#183444` / `#3d5563` |

同时迁移了 TS 内联的 ECharts 渐变 / 统计色（如 `learning/progress`、`admin/employment/statistics` 等）。

**保留不做替换**：红 / 橙 / 紫等用于区分多数据系列的分类色（`#f5222d`、`#722ed1`、`#faad14` …），避免多序列图表退化单色。

---

## 七、页面标题（eyebrow）

给 18 个教师端 / 管理端重点页面补上原型 `pagehead` 的 eyebrow（中文化，取侧边栏分组名），页面标题区立刻与原型一致：

| 分组 | 页面 |
|---|---|
| 专业和课程 | 专业管理、微专业管理、课程资源、学习统计、我的课程、学习中心 |
| 就业管理 | 岗位管理、岗位投递和面试管理、就业去向管理、就业统计、就业指导记录、我的直播 |
| 实训管理 | 实训项目管理、实训任务管理、仿真镜像管理、实训智能体聊天 |
| 资讯与双高 | 资讯管理、双高评估项目 |

> 实现：在既有标题区插入一行 `<div class="kh-eyebrow">…</div>`，不改变任何绑定与结构；`.major-head` / `.mgmt-head` 增加 `__text` 包裹层以维持 `space-between` 布局。

---

## 八、验证结果

- `ng serve` 增量编译 **0 error**（仅保留原有 Sass `@import` 弃用警告与 `DecimalPipe` 未使用警告）。
- 已截图回归页面：`/admin/workbench`、`/resources`、`/admin/majors`、`/admin/micro-majors`、`/admin/employment/jobs`、`/admin/practicum/*`、`/admin/news`、`/learning/course-list`、`/learning/learning-statistics`、`/ai/chat`、`/ai/lesson-plan`、`/search`、`/account/login`。
- 侧边栏选中态、二级菜单选中态均已确认不再是框架默认黑底（`getComputedStyle` 得到 `rgb(229,243,239)`）。
- 首页 / 资源库页 / 学生端（未改动逻辑）不受影响；公开门户首页照常。

---

## 九、后续可继续项

1. **侧边栏机构卡片 + 分组标题**：原型侧边栏在品牌下方有机构卡片、「教学空间 / 智能教学」分组标题；LeptonX 侧边栏由框架渲染，需要替换 `eThemeLeptonXComponents.Sidebar` 或注入 DOM 才能实现，风险较高，本轮未做。
2. **列表页筛选胶囊行**：原型列表页在搜索栏下有「全部 / 已发布 / 待审核 / 草稿」胶囊筛选。当前各页仍是下拉框 / Tab，可逐页替换为 `.kh-chip`。
3. **管理端页脚**：LeptonX 直接内联渲染 `.lpx-footbar`（英文版权 + About/Privacy/Contact），`eThemeLeptonXComponents.Footer` 替换在该版本未生效，可后续通过替换 ApplicationLayout 或布局选项关闭。
4. **模块概览卡片可继续细分**：工作台 9 个模块卡片可进一步拆成「模块 + 迷你趋势」。
5. **未逐一改 HTML 的列表页**：本轮以「全局主题 + 工作台 + 18 个重点页眉」为主，其余页面已通过全局令牌与 ng-zorro 覆盖获得统一观感，但尚未逐页做结构级重构（筛选胶囊、通知条、卡片化工具条）。

---

## 附：改动规模

| 项 | 数值 |
|---|---|
| 新增文件 | `angular/src/styles/_kh-ui.scss` |
| 核心改动文件 | `_design-tokens.scss`、`_leptonx-overrides.scss`、`_ngzorro-overrides.scss`、`styles.scss`、`site-brand.component.ts`、`workbench.component.{html,scss,ts}` |
| 色相迁移文件 | 130+（`app/**` 去掉 `student/` 后全部 SCSS/TS/HTML + `styles/**`） |
| 页眉补充页面 | 18 |
