---
name: KnowledgeHub (易课通)
description: 职业院校教育资源平台 — 学生门户设计系统
colors:
  # home-v2 墨青绿主题（与首页 / 资源库页 / 学生端统一）
  primary: "#087f7a"
  primary-hover: "#086964"
  primary-light: "#e5f3ef"
  primary-light-soft: "#4aa79f"
  deep: "#103c43"
  deep-deep: "#0c282b"
  accent: "#d1ad73"
  success: "#10b981"
  warning: "#f59e0b"
  ink: "#183444"
  ink-muted: "#697e88"
  ink-subtle: "#8a99a1"
  canvas: "#ffffff"
  surface: "#f8f9f6"
  surface-soft: "#f2f5f1"
  surface-featured: "#eef2e9"
  border: "#e1e7e0"
  border-soft: "#edf0eb"
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

> **2026-10 更新**：全站已从「学院蓝」迁移到 **home-v2 墨青绿主题**。
> 本次重构的完整记录见 [`docs/UI重构总结-首页与资源库与学生端.md`](docs/UI重构总结-首页与资源库与学生端.md)。

## 1. Overview

**Creative North Star: "数字课堂"**

干净、明亮、专注内容的教育平台。设计语言围绕"内容即界面"展开——排版本身承载视觉节奏，色彩服务于信息层级而非装饰。每个页面让学生一眼理解结构，三秒找到目标内容。

这是一个**教育产品**的设计系统，不是营销网站。它的气质是**专业的克制**：墨青绿主色调传递可信赖与成长的感受，暖白底色 + 沙金点缀保持清晰不喧哗，大面积留白让内容呼吸。背景不使用渐变色，不使用紫红/玫红/暖橙等偏色系。

**Key Characteristics:**
- 墨青绿作为唯一醒目色，使用率 ≤15% 屏幕面积；沙金仅作点缀
- 纯色背景，无渐变色背景、无毛玻璃装饰、无过度圆角
- 信息层级靠字号/字重/间距构建，而非色块或装饰线
- 卡片以柔和阴影表达层次，而非彩色边框或色条
- 移动端和桌面端使用同一套语义层级，在小屏上不减配信息密度
- 所有圆角上限：卡片 ≤14px，按钮 ≤8px，标签/搜索条可使用全圆

## 2. Colors

墨青绿主色 + 暖白底 + 沙金点缀，保持专业、清晰、教育领域特征（home-v2 主题）。

### Primary
- **主色墨青绿** `#087f7a`: 按钮填充、导航激活指示、筛选标签激活态、链接文字、进度条填充。沉稳且带有生长/健康的语义，适合教育平台。
- **深墨青绿** `#086964`: 主色按钮 hover 态、卡片 footer 主按钮悬停。
- **浅墨青绿** `#4aa79f`: 品牌辅助色，用于封面渐变、图表第二色。
- **品牌浅底** `#e5f3ef`: 卡片 hover 态背景、列表项悬停背景、标签底、表格行选中态。

### Secondary（深色区）
- **深墨青** `#103c43`: Hero / 欢迎横幅 / 实训横幅的深色底。
- **深墨青-更深** `#0c282b`: Hero 遮罩渐变末端。

### 辅助色
- **沙金** `#d1ad73`: 次要强调、暖色点缀、推荐徽章。使用频率极低。
- **沙金-深** `#b0803a`: 沙金的文字/图标态。
- **成功** `#10b981`: 已报名/已完成状态标记、徽章绿色态。
- **警告** `#f59e0b`: 需要引起注意的信息。使用频率极低，每页不超过一次。

### Neutral
- **墨色** `#183444`: 正文文字、卡片标题。
- **墨色-柔和** `#697e88`: 辅助文字、描述文字、卡片 meta 信息。满足 WCAG AA。
- **墨色-浅** `#8a99a1`: placeholder 文字、次级标注、统计数字后缀。
- **画布** `#ffffff`: 卡片、页面主体背景。
- **表面** `#f8f9f6`: 页面整体背景色。暖调浅灰，比纯白减少视觉刺激，适合长时间浏览。
- **表面-柔和** `#f2f5f1`: 卡片内部次级背景、筛选区域背景。
- **表面-浅绿** `#eef2e9`: 精选课程等"强调区块"的背景（与白卡形成柔和对比）。
- **边框** `#e1e7e0`: 卡片边框、分割线。
- **边框-柔和** `#edf0eb`: 更轻的边框，用于卡片内部分隔、列表项分隔。

### 封面色块（Card Cover）
原型 `.cover` 的四种清新底色，用于无真实封面图时的课程/微专业卡片封面：

| 类 | 背景 | 文字 |
|---|---|---|
| `c0` | `#dcece4` | `#295848` |
| `c1` | `#e0e8f1` | `#3c5e7c` |
| `c2` | `#ede6d8` | `#806440` |
| `c3` | `#e7e3ef` | `#655781` |

### Named Rules

**The 单一醒目色 Rule.** 墨青绿主色在任意屏幕上的覆盖率 ≤15%。它的稀缺性是重点所在。用排版和间距表达层级，不用反复刷色。

**The 无渐变背景 Rule.** 除 Hero / 欢迎横幅 / 实训横幅这类整屏深色区可用同色系渐变外，卡片、按钮、导航一律使用纯色。

## 3. Typography

**Body Font:** 系统原生字体栈（PingFang SC + Hiragino Sans GB + SF Pro Display）—— 无需额外加载 web font，中文显示效果经过各平台优化。

**Character:** 干净的中文排版。字距宽松适度（`letter-spacing: -0.01em` 仅用于 display/headline 增加紧凑感），行高宽裕（body 1.7 行距），保证中文长段落在手机上可读。不使用衬线字体，不使用全大写标题，不使用 em dash。

### Hierarchy
- **Display** (800, `clamp(1.75rem, 4vw, 3rem)`, 1.2): Hero 中的页面主标题。仅用于学生首页的轮播 banner 标题，每页最多一个。
- **Headline** (700, `clamp(1.25rem, 2.5vw, 1.625rem)`, 1.3): 区块标题，如"课程中心"、"资源库"、"热门推荐"。section 级别的标题，配合左侧 4px 品牌竖条装饰使用。
- **Title** (600, `clamp(0.9375rem, 1.4vw, 1.125rem)`, 1.45): 卡片标题、列表项标题。两行截断，保证信息密度。
- **Body** (400, `0.875rem`, 1.7): 正文描述、课程简介、内容说明。max-width 控制在 65–75ch 以保证段落可读性。
- **Label** (500, `0.75rem`, 1.4): 元信息标签、日期、作者、学习人数、筛选标签文字、按钮辅助文字。

### Named Rules

**The 单字体栈 Rule.** 不使用 web font（避免加载延迟和 FOIT），使用系统原生字体栈。不同平台使用各自最优中文字体（macOS PingFang，Windows Microsoft YaHei），但英文部分统一回退到 San Francisco / Segoe UI。

**The 行距宽松 Rule.** 中文无衬线字体在 0.75rem–0.875rem 字号下，line-height 不得低于 1.6。文字密集区域（课程描述、列表副文本）使用 1.7 保证清晰度。

## 4. Elevation

系统使用**柔和阴影**表达层次，而非毛玻璃或彩色边框。卡片与背景通过阴影的扩散和 Y 轴偏移来区分层级：rest 态用微小阴影（几乎不可见），hover 态用稍大的品牌色调阴影表示可交互。

任何表面在 rest 态下不设阴影也不算错误——边框本身已提供足够的视觉分割。阴影是可选增强，不是必需属性。

### Shadow Vocabulary
- **柔和阴影** (`0 1px 2px rgba(15, 23, 42, 0.04), 0 4px 12px rgba(15, 23, 42, 0.04)`): 卡片 rest 态默认阴影。透明度极低，仅防止卡片在白色背景上"漂浮"。
- **卡片阴影** (`0 6px 20px rgba(15, 23, 42, 0.06)`): 侧边栏卡片、独立容器。
- **悬停阴影** (`0 12px 28px rgba(8, 127, 122, 0.16)`): 卡片 hover 态。品牌色调投影暗示可点击，不透明。
- **主按钮阴影** (`0 4px 12px rgba(8, 127, 122, 0.20)`): 主要操作按钮的默认阴影，hover 时加深至 `0 8px 18px rgba(8, 127, 122, 0.28)`。

### Named Rules

**The 柔和提升 Rule.** 阴影仅作为层级的温和提示，而非视觉装饰。任何超过 12px blur 的阴影必须带有品牌墨青绿调以保持品牌一致性，不得使用纯黑/纯灰的大面积扩散阴影。

## 5. Components

### Buttons
- **Shape:** 小圆角矩形（`border-radius: 5px`–`8px`），中等高度（44px 常规，49px hero CTA）。不使用全圆按钮（圆形操作按钮除外）。
- **Primary:** 纯墨青绿填充 `#087f7a` + 白色文字。hover 态加深至 `#086964`，可选外部品牌阴影（`box-shadow: 0 4px 12px rgba(8, 127, 122, 0.20)`）。无渐变背景。
- **Hero CTA:** 浅绿白填充 `#eef4e9` + 深绿文字 `#16483f`，hover 变纯白。仅用于 Hero / 深色区。
- **Light (深色区):** 半透明白底 `rgba(255,255,255,.08)` + 1px 半透明白边 + 白字。用于实训横幅等深色区域。
- **Ghost:** 透明背景 + 1px 边框 + 灰色文字。hover 态变品牌色文字。用于次要操作和取消按钮。
- **Card footer button:** 浅底 `#f8faf6` + 1px 边框 `#e1e8de` + 深绿文字 `#375d44`。用于卡片底部的操作栏。

### Filter Chips / Tabs（筛选标签）
- **Shape:** 小圆角（`border-radius: 5px`），高度 39px，内边距 0 18px。
- **Unselected:** 透明背景 + 1px 边框 `#e1e7df` + 正文色。hover 态浅绿底 `#e5f3ef`。
- **Selected:** 深墨青填充 `#264f42` + 白色文字。
- **Sidebar active:** 浅绿底 `#f2f5f1` + 品牌左竖条 3px + 品牌深色文字。

### Cards / Containers
- **Corner Style:** `border-radius: 11px`（卡片）/ `14px`（大卡）/ `16px`（区块）。
- **Background:** 纯白 `#ffffff`；页面底 `#f8f9f6`；强调区块 `#eef2e9`。
- **Shadow Strategy:** rest 态使用极轻阴影或仅边框；hover 态 `0 14px 30px rgba(25, 59, 71, .07)`。
- **Border:** 1px solid `#e1e7e0`。
- **Internal Padding:** 卡片 17–20px；大卡 27–30px。
- **Card Cover（重点）：** 无真实封面图时使用四种清新色块（`c0`~`c3`）+ 装饰圆环 + 左上标签 + 大标题；有封面图时用图片。

### Top Nav（首页 / 资源库页 / 学生端统一）
- **Style:** 88px 白底 + 1px 下边框；导航居中，激活项使用 3px 品牌色下划线。
- **Mobile:** 折行 + 横向滚动。
- **学生端为左侧边栏布局：** 232px 固定侧栏 + 76px sticky 信息栏（面包屑 + 快捷操作 + 用户胶囊）。

### Input / Search
- **Search Bar:** 小圆角（`border-radius: 5px`），高度 42–44px。白色填充 + 1px 边框 `#e1e7e0`。focus-within 边框变品牌色。
- **Action Button Inside:** 品牌色填充按钮，右侧内嵌于搜索栏。

### Stat Cells（统计数据项）
- **Page header stats:** 页面标题栏右侧，数值用品牌深色 `#086964` + 等宽数字，左侧 1px 分隔线。
- **Background:** 无独立背景（纯文本统计）。

### Progress Bar（进度条）
- **Style:** 6px 高，全圆角。填充纯品牌色 `#087f7a`，轨道 `#f2f5f1`。
- **Thin variant:** 5px，用于目标进度。

### Hero / Banner
- **首页 Hero:** 全幅背景图（`assets/images/home-v2/hero-{1,2,3}.webp`）+ 两层深色渐变遮罩；左对齐文案 + 底部 selector/箭头/计数器/暂停按钮。
- **资源库页 Hero:** 固定使用 `hero-1.webp`（图书馆场景）+ 同类遮罩；文案 + 内联统计 + 4 个磨砂玻璃分区导航卡。
- **学生端欢迎横幅:** `linear-gradient(105deg, #163f4b, #24615f)` + 右侧 `Learn.` 衬线水印。
- **实训横幅:** `#193e35` + 背景图（opacity .28）+ 「知行合一」水印。
- **Mobile:** 文案区上下堆叠，装饰水印隐藏。

## 6. Do's and Don'ts

### Do:
- **Do** 保持所有主要操作按钮为纯色 `#087f7a` 填充，hover 态加深至 `#086964`。
- **Do** 使用 `#f8f9f6` 作为页面背景色，白色卡片浮于其上。
- **Do** 卡片统一 11px 圆角（大卡 14px），区块 16px。
- **Do** 在 hover 态使用品牌色调阴影增强可交互暗示。
- **Do** 使用排版层级（字号 31→26→22→17→14→12px）构建信息结构。
- **Do** 在页面标题 / 区块标题左侧使用 3–4px 品牌竖条或顶部品牌细线。
- **Do** 无封面图时使用 `c0`~`c3` 色块封面 + 装饰圆环 + 大标题（原型卡片语言）。
- **Do** 在移动端上将内容间距从 24px 缩减至 16px，保持可用性。
- **Do** 使用系统原生字体栈，不引入 web font。

### Don't:
- **Don't** 使用渐变色作为卡片/按钮背景（整屏深色 Hero / 欢迎横幅例外）。
- **Don't** 使用紫红色、玫红色、暖橙红色等偏色系。
- **Don't** 使用毛玻璃效果（`backdrop-filter: blur` 仅限 Hero 分区导航卡 / sticky 导航）。
- **Don't** 使用大于 16px 的卡片圆角。
- **Don't** 在页面文本中使用英文描述（如 `KNOWLEDGE GRAPH`、`RESOURCE LIBRARIES`），文案一律中文。
- **Don't** 使用纯黑 `#000` 或纯黑阴影——所有阴影必须带品牌色调或极低透明度。
- **Don't** 使用 SaaS 大数字 hero 数据条模板。
- **Don't** 使用玻璃拟态作为默认设计方案。
- **Don't** 使用营销话术文案（"赋能""助力""高效""无缝"等）。
