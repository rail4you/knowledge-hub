---
name: KnowledgeHub (易课通)
description: 职业院校教育资源平台 — 学生门户设计系统
colors:
  primary: "#1e6ce8"
  primary-hover: "#0c4cb8"
  primary-light: "#eef4ff"
  accent: "#00b7ff"
  success: "#10b981"
  warning: "#f59e0b"
  ink: "#1f2937"
  ink-muted: "#5b6573"
  ink-subtle: "#8a93a6"
  canvas: "#ffffff"
  surface: "#f4f7fb"
  surface-soft: "#f5f8fc"
  border: "#dde4ee"
  border-soft: "#eaf0f7"
typography:
  display:
    fontFamily: "-apple-system, BlinkMacSystemFont, 'SF Pro Display', 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', 'Helvetica Neue', sans-serif"
    fontSize: "clamp(1.75rem, 4vw, 3rem)"
    fontWeight: 800
    lineHeight: 1.2
    letterSpacing: -0.02em
  headline:
    fontFamily: "-apple-system, BlinkMacSystemFont, 'SF Pro Display', 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', 'Helvetica Neue', sans-serif"
    fontSize: "clamp(1.25rem, 2.5vw, 1.625rem)"
    fontWeight: 700
    lineHeight: 1.3
    letterSpacing: -0.01em
  title:
    fontFamily: "-apple-system, BlinkMacSystemFont, 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', '-apple-system', sans-serif"
    fontSize: "clamp(0.9375rem, 1.4vw, 1.125rem)"
    fontWeight: 600
    lineHeight: 1.45
    letterSpacing: normal
  body:
    fontFamily: "-apple-system, BlinkMacSystemFont, 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', '-apple-system', sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.7
    letterSpacing: normal
  label:
    fontFamily: "-apple-system, BlinkMacSystemFont, 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', '-apple-system', sans-serif"
    fontSize: "0.75rem"
    fontWeight: 500
    lineHeight: 1.4
    letterSpacing: 0.01em
rounded:
  sm: "6px"
  md: "8px"
  lg: "14px"
  xl: "20px"
  full: "999px"
spacing:
  xs: "6px"
  sm: "10px"
  md: "14px"
  lg: "18px"
  xl: "24px"
  xxl: "48px"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.canvas}"
    rounded: "{rounded.md}"
    padding: "0 18px"
    height: "36px"
  button-primary-hover:
    backgroundColor: "{colors.primary-hover}"
    textColor: "{colors.canvas}"
    rounded: "{rounded.md}"
    padding: "0 18px"
    height: "36px"
  button-ghost:
    backgroundColor: transparent
    textColor: "{colors.ink-muted}"
    rounded: "{rounded.sm}"
    padding: "0 16px"
    height: "36px"
  card-default:
    backgroundColor: "{colors.canvas}"
    rounded: "{rounded.lg}"
  tab-active-indicator:
    backgroundColor: "{colors.primary}"
    height: "3px"
    rounded: "3px 3px 0 0"
  chip-filter:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.ink-muted}"
    rounded: "{rounded.full}"
    padding: "0 14px"
  chip-filter-active:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.canvas}"
    rounded: "{rounded.full}"
    padding: "0 14px"
  input-search:
    backgroundColor: "{colors.canvas}"
    rounded: "{rounded.full}"
  search-pill-btn:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.canvas}"
    rounded: "{rounded.full}"
    padding: "0 18px"
    height: "36px"
---

# Design System: KnowledgeHub (易课通)

## 1. Overview

**Creative North Star: "数字课堂"**

干净、明亮、专注内容的教育平台。设计语言围绕"内容即界面"展开——排版本身承载视觉节奏，色彩服务于信息层级而非装饰。每个页面让学生一眼理解结构，三秒找到目标内容。

这是一个**教育产品**的设计系统，不是营销网站。它的气质是**专业的克制**：蓝色主色调传递可信赖感，冷色辅助色（青、绿、蓝）保持清晰不喧哗，大面积留白让内容呼吸。背景不使用渐变色，不使用紫红/玫红/暖橙等偏色系。

**Key Characteristics:**
- 蓝色双色（深蓝 + 亮蓝）作为唯一醒目色，使用率 ≤15% 屏幕面积
- 纯色背景，无渐变色背景、无毛玻璃装饰、无过度圆角
- 信息层级靠字号/字重/间距构建，而非色块或装饰线
- 卡片以柔和阴影表达层次，而非彩色边框或色条
- 移动端和桌面端使用同一套语义层级，在小屏上不减配信息密度
- 所有圆角上限：卡片 ≤14px，按钮 ≤8px，标签/搜索条可使用全圆

## 2. Colors

蓝色双色方案 + 冷色辅助色，保持专业、清晰、教育领域特征。

### Primary
- **主色蓝** `#1e6ce8` / `oklch(54% 0.185 264)`: 按钮填充、导航激活指示、筛选标签激活态、链接文字。这种蓝色饱和但不过度张扬，在白色背景上具有足够的权威性，适合教育平台。
- **深蓝** `#0c4cb8` / `oklch(42% 0.17 261)`: 主色按钮 hover 态、卡片 footer 主按钮悬停。
- **浅蓝** `#eef4ff` / `oklch(94% 0.03 264)`: 卡片 hover 态背景、列表项悬停背景、表格行选中态。

### Secondary
- **亮蓝** `#00b7ff` / `oklch(73% 0.18 238)`: 与主色蓝配合使用在渐变按钮上（现遗留用法），未来逐步移除。保留作为品牌视觉点缀，不用于操作按钮。

### 辅助色
- **青色/成功** `#10b981` / `oklch(68% 0.18 164)`: 已报名/已完成状态标记、进度条填充、徽章绿色态。
- **暖色/警告** `#f59e0b` / `oklch(78% 0.165 80)`: 需要引起注意的信息，如即将到期的课程提示。使用频率极低，每页不超过一次。
- **青色-知识** `#0891b2`: 知识图谱和分类标签可选项。

### Neutral
- **墨色** `#1f2937` / `oklch(28% 0.025 264)`: 正文文字、卡片标题。对比度约 13:1，保证极高可读性。
- **墨色-柔和** `#5b6573` / `oklch(48% 0.025 264)`: 辅助文字、描述文字、卡片 meta 信息。对比度约 5.5:1，满足 WCAG AA。
- **墨色-浅** `#8a93a6` / `oklch(62% 0.025 264)`: placeholder 文字、次级标注、统计数字后缀。对比度约 4.5:1，按规范精准达标。
- **画布** `#ffffff`: 卡片、页面主体背景。
- **表面** `#f4f7fb` / `oklch(96% 0.01 255)`: 页面整体背景色。冷调浅灰，比纯白减少视觉刺激，适合长时间浏览。
- **表面-柔和** `#f5f8fc` / `oklch(97% 0.008 255)`: 卡片内部次级背景、筛选区域背景。
- **边框** `#dde4ee` / `oklch(88% 0.012 255)`: 卡片边框、分割线。
- **边框-柔和** `#eaf0f7` / `oklch(93% 0.01 255)`: 更轻的边框，用于卡片内部分隔、列表项分隔。

### Named Rules

**The 单一醒目色 Rule.** 蓝色主色在任意屏幕上的覆盖率 ≤15%。它的稀缺性是重点所在。用排版和间距表达层级，不用反复刷色。

**The 无渐变背景 Rule.** 任何区域（hero、卡片、按钮底纹、导航）不使用大面积的渐变背景色。纯色为主。

## 3. Typography

**Body Font:** 系统原生字体栈（PingFang SC + Hiragino Sans GB + SF Pro Display）—— 无需额外加载 web font，中文显示效果经过各平台优化。

**Character:** 干净的中文排版。字距宽松适度（`letter-spacing: -0.01em` 仅用于 display/headline 增加紧凑感），行高宽裕（body 1.7 行距），保证中文长段落在手机上可读。不使用衬线字体，不使用全大写标题，不使用 em dash。

### Hierarchy
- **Display** (800, `clamp(1.75rem, 4vw, 3rem)`, 1.2): Hero 中的页面主标题。仅用于学生首页的轮播 banner 标题，每页最多一个。
- **Headline** (700, `clamp(1.25rem, 2.5vw, 1.625rem)`, 1.3): 区块标题，如"课程中心"、"资源库"、"热门推荐"。section 级别的标题，配合左侧 4px 蓝色竖条装饰使用。
- **Title** (600, `clamp(0.9375rem, 1.4vw, 1.125rem)`, 1.45): 卡片标题、列表项标题。两行截断，保证信息密度。
- **Body** (400, `0.875rem`, 1.7): 正文描述、课程简介、内容说明。max-width 控制在 65–75ch 以保证段落可读性。
- **Label** (500, `0.75rem`, 1.4): 元信息标签、日期、作者、学习人数、筛选标签文字、按钮辅助文字。

### Named Rules

**The 单字体栈 Rule.** 不使用 web font（避免加载延迟和 FOIT），使用系统原生字体栈。不同平台使用各自最优中文字体（macOS PingFang，Windows Microsoft YaHei），但英文部分统一回退到 San Francisco / Segoe UI。

**The 行距宽松 Rule.** 中文无衬线字体在 0.75rem–0.875rem 字号下，line-height 不得低于 1.6。文字密集区域（课程描述、列表副文本）使用 1.7 保证清晰度。

## 4. Elevation

系统使用**柔和阴影**表达层次，而非毛玻璃或彩色边框。卡片与背景通过阴影的扩散和 Y 轴偏移来区分层级：rest 态用微小阴影（几乎不可见），hover 态用稍大的蓝色调阴影表示可交互。

任何表面在 rest 态下不设阴影也不算错误——边框本身已提供足够的视觉分割。阴影是可选增强，不是必需属性。

### Shadow Vocabulary
- **柔和阴影** (`0 1px 2px rgba(15, 23, 42, 0.04), 0 4px 12px rgba(15, 23, 42, 0.04)`): 卡片 rest 态默认阴影。透明度极低，仅防止卡片在白色背景上"漂浮"。
- **卡片阴影** (`0 6px 20px rgba(15, 23, 42, 0.06)`): 侧边栏卡片、独立容器。
- **悬停阴影** (`0 12px 28px rgba(30, 108, 232, 0.14)`): 卡片 hover 态。蓝色调投影暗示可点击，不透明。
- **搜索按钮阴影** (`0 4px 12px rgba(30, 108, 232, 0.24)`): 主要操作按钮的默认阴影，hover 时加深至 `0 8px 18px rgba(30, 108, 232, 0.32)`。

### Named Rules

**The 柔和提升 Rule.** 阴影仅作为层级的温和提示，而非视觉装饰。任何超过 12px blur 的阴影必须带有蓝色调以保持品牌一致性，不得使用纯黑/纯灰的大面积扩散阴影。

## 5. Components

### Buttons
- **Shape:** 直角矩形（`border-radius: 8px`），中等高度（36px 常规，44px 搜索栏，52px hero CTA）。不使用全圆按钮（圆形操作按钮除外）。
- **Primary:** 纯蓝色填充 `#1e6ce8` + 白色文字。hover 态加深至 `#0c4cb8`，可选外部蓝色阴影（`box-shadow: 0 4px 12px rgba(30, 108, 232, 0.24)`）。无渐变背景。
- **Primary (hero CTA):** 白色填充 + 蓝色文字 `#1e6ce8`。hover 态向上位移 2px + 加深阴影。仅用于首页 hero 区域的 CTA，不用于页面内操作。
- **Ghost:** 透明背景 + 1px 边框 + 灰色文字。hover 态变蓝色文字。用于次要操作和取消按钮。
- **Footer button:** 浅灰背景（`$student-surface-soft: #f5f8fc`）+ 1px 边框。hover 态变蓝色边框 + 蓝色文字。用于卡片底部的操作栏。

### Filter Chips（筛选标签）
- **Shape:** 全圆（`border-radius: 999px`），高度 32px，内边距 0 14px。
- **Unselected:** 白色背景 + 1px 边框 `#dde4ee` + 灰色文字。hover 态边框和文字变为蓝色。
- **Selected:** 纯蓝色填充 `#1e6ce8` + 白色文字 + 可选蓝色外部阴影。
- **Emoji/Icon:** 可选前置图标（如 `💰`、`⚡`），颜色通过 CSS 变量 `--chip-color` 控制。
- **Difficulty chips:** 方块形（`border-radius: 6px`），高度 28px。激活态同填充蓝色方案。

### Cards / Containers
- **Corner Style:** `border-radius: 14px`。课程卡片、资源卡片、信息卡片统一使用。
- **Background:** 纯白 `#ffffff`，外层背景 `#f4f7fb`。
- **Shadow Strategy:** 默认 `box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04), 0 4px 12px rgba(15, 23, 42, 0.04)`，hover 态升级为蓝色调阴影 `0 12px 28px rgba(30, 108, 232, 0.14)`。
- **Border:** 1px solid `#dde4ee`。hover 态 border-color 变为 `transparent`（阴影取代边框）。
- **Internal Padding:** 16px–18px 水平 + 18px 底部 + 16px 顶部。

### Tab Navigation（学生导航栏）
- **Style:** 白色背景 + 下划线指示器。文字为 `#5b6573` 灰色，激活态为蓝色 `#1e6ce8` + 3px 蓝色下划线。
- **Typography:** 15px、500 weight（常规），激活态 600 weight。
- **Hover:** 文字变蓝色，无背景变化。
- **Mobile:** 滚动容器（隐藏滚动条），下划线宽度自适应。

### Input / Search
- **Search Pill:** 全圆（`border-radius: 999px`），高度 44px。白色填充 + 1px 边框 `#dde4ee`。focus-within 边框变蓝色 + 外发光 `0 0 0 3px rgba(30, 108, 232, 0.08)`。
- **Action Button Inside:** 蓝色填充按钮，右侧内嵌于搜索栏，圆角 `999px`。

### Stat Cells（统计数据项）
- **Structure:** 两列网格布局（每列一个 stat）。左侧 36px 彩色圆角图标 + 右侧粗体数字 + 标签。
- **Background:** `#f5f8fc` 浅色背景，hover 态变 `#eef5ff` 蓝色浅底。
- **Use case:** 仅用于学生首页 hero 右侧的数据面板，不在其他地方复用。

### Progress Bar（进度条）
- **Style:** 细小（4px 高），圆角。填充用蓝色渐变 (`linear-gradient(90deg, #1e6ce8, #00b7ff)`)，背景用 `#eaf0f7` 浅灰色。
- **Label:** 上方显示 "已学习 X%" 文字，右侧百分比加粗蓝色。

### Hero Banner
- **Background:** 纯蓝色背景 `linear-gradient(120deg, #1e6ce8 0%, #00b7ff 100%)`。**注意：这是一个现有渐变——未来迭代中可考虑简化为纯色 `#1e6ce8`。**
- **Shape:** 装饰性圆形（半透明白色 `rgba(255, 255, 255, 0.05-0.12)`）作为唯一视觉点缀。
- **Carousel:** 左侧文案（标题 + 描述 + tag）+ 右侧数据面板两栏布局。
- **Mobile:** 堆叠为单栏，圆形装饰隐藏，数据面板保留。

## 6. Do's and Don'ts

### Do:
- **Do** 保持所有主要操作按钮为纯色 `#1e6ce8` 填充，hover 态加深。
- **Do** 使用 #f4f7fb 作为页面背景色，白色卡片浮于其上。
- **Do** 用 14px 圆角统一卡片风格。
- **Do** 在 hover 态使用蓝色调阴影增强可交互暗示。
- **Do** 使用排版层级（字号 28→22→18→14→12px）构建信息结构。
- **Do** 保持 section heading 左侧 4px 蓝色竖条装饰（`border-radius: 2px`）。
- **Do** 在移动端上将内容间距从 24px 缩减至 16px，保持可用性。
- **Do** 使用系统原生字体栈，不引入 web font。

### Don't:
- **Don't** 使用渐变色作为背景（hero 区域例外，但应逐步迁移至纯色）。
- **Don't** 使用紫红色、玫红色、暖橙红色等偏色系。
- **Don't** 使用毛玻璃效果（`backdrop-filter: blur` 仅限导航栏 sticky 场景）。
- **Don't** 使用大于 14px 的卡片圆角。
- **Don't** 使用左/右侧彩色边框条装饰（大于 1px 的 `border-left` 色条）。
- **Don't** 使用纯黑 `#000` 或纯黑阴影——所有阴影必须带蓝色调或极低透明度。
- **Don't** 在卡片、列表、表单外使用多余装饰图案（网格背景、虚线、手绘插画）。
- **Don't** 使用 SaaS 大数字 hero 数据条模板。
- **Don't** 使用玻璃拟态作为默认设计方案。
- **Don't** 使用营销话术文案（"赋能""助力""高效""无缝"等）。
