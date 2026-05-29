# KnowledgeHub 学生门户重构 — TODO 开发计划

> **Reviewed**: 2026-05-29 by reviewer (deepseek-v4-flash) — 详见 Phase 0 发现清单

## 文档信息

| 项目 | 内容 |
|------|------|
| 计划编号 | DEVELOPMENT_PLAN_002 |
| 项目名称 | 学生门户（React）全面重构 |
| 参考网站 | https://zyk.icve.com.cn/project |
| 目标项目 | `student-react/` |
| 起始日期 | 2026-05-29 |
| 开发方式 | 逐阶段 TODO → 完成 → `npm run build` → Reviewer（deepseek-v4-flash）验证 → 下一阶段 |

---

## 核心概念映射

| icve | KnowledgeHub | 实体 |
|------|-------------|------|
| Project | Tenant（租户） | `AbpTenants` |
| 专业群 | 微专业 | `MicroMajor` |
| 课程 | 课程 | `Course` |
| 素材 | 素材/资源 | `Resource` |
| 知识图谱 | 知识图谱 | `KnowledgeNode` + `KnowledgeRelation` |
| 资讯/咨询 | 资讯 | `NewsArticle` |

---

## Phase 0: 发现与准备（前置）

> **目标**：梳理已有 API、补齐缺失的基础设施，避免后续重复开发。

### Reviewer 发现的关键问题

| # | 问题 | 严重度 |
|---|------|--------|
| 1 | `IKnowledgeGraphAppService` 接口存在但 **实现为空**（`Application/KnowledgeGraph/` 空目录） | 🔴 阻塞 P5 |
| 2 | `RelationType` 枚举缺少 `contains`/`parallel`/`references` 值 | 🟡 阻塞 P5 |
| 3 | `KnowledgeResource` ≠ `Resource`：前者是教学知识点（文本内容），后者是文件素材 | 🟡 影响 P3/P4 |
| 4 | 约 30% 提议 API 已有实现（`MicroMajor.GetPublishedAsync`、`NewsComment`、`Exercise.GetByCourseAsync`） | 🟢 复用即可 |
| 5 | 需新建 `KnowledgeResourceAppService`（尚无 AppService） | 🔴 阻塞 P3 |
| 6 | MicroMajor → Resource 无直接关联，间接查询链路不可靠 | 🟡 影响 P4 |

### 储备知识: 已有可直接复用的 API

| 已有 API | 提供者 | 用途 |
|----------|--------|------|
| `IMicroMajorAppService.GetPublishedAsync()` | `MicroMajorAppService` | 公开微专业列表（带分页/搜索） |
| `IMicroMajorAppService.GetDetailAsync()` | `MicroMajorAppService` | 微专业详情含课程列表 |
| `IMicroMajorAppService.EnrollAsync()` / `GetMyEnrollmentsAsync()` | `MicroMajorAppService` | 报名/我的报名 |
| `IMicroMajorAppService.IssueCertificateAsync()` / `GetMyCertificatesAsync()` | `MicroMajorAppService` | 证书 |
| `IExerciseAppService.GetByCourseAsync()` | `ExerciseAppService` | 课程习题 |
| `IExerciseAppService.GetByChapterAsync()` | `ExerciseAppService` | 章节习题 |
| `IStudentExerciseRecordAppService.GetChapterProgressAsync()` | `StudentExerciseRecordAppService` | 章节完成进度 |
| `IStudentExerciseRecordAppService.CreateAsync()` | `StudentExerciseRecordAppService` | 提交答题记录 |
| `INewsCommentAppService.GetApprovedListByArticleAsync()` | `NewsCommentAppService` | 资讯评论 |
| `INewsCommentAppService.CreateAsync()` | `NewsCommentAppService` | 发表评论 |
| `ICourseAppService.GetDetailAsync()` | `CourseAppService` | 课程详情含章节 |
| `IKnowledgeGraphAppService.GetGraphAsync()` | —（接口，待实现） | 课程知识图谱 |

### TODO

- [ ] T0.1 **梳理已有 API**：对照上方表格，在 `lib/api.ts` 中封装调用函数（避免重复造轮子）
- [ ] T0.2 **扩展 `RelationType` 枚举**：`src/KnowledgeHub.Domain.Shared/KnowledgeGraph/Enums/RelationType.cs` 添加 `Contains=3`, `Parallel=4`, `References=5`
- [ ] T0.3 **实现 `KnowledgeGraphAppService`**：创建 `src/KnowledgeHub.Application/KnowledgeGraph/KnowledgeGraphAppService.cs`，至少实现 `GetGraphAsync(courseId)` 返回 `KnowledgeGraphDto`（从 `KnowledgeNode` + `KnowledgeRelation` 仓储查询）
- [ ] T0.4 **创建 `KnowledgeResourceAppService`**：新建 `IKnowledgeResourceAppService` 和 `KnowledgeResourceAppService`，提供 `GetByCourseAsync(courseId)`、`GetRelatedCoursesAsync(resourceId)`
- [ ] T0.5 **创建 MicroMajorResource 桥接实体**：新建 `src/KnowledgeHub.Domain/MicroMajors/MicroMajorResource.cs`（`MicroMajorId` + `ResourceId` + `SortOrder`），生成 EF 迁移
- [ ] T0.6 **创建 `PortalAppService`**：`src/KnowledgeHub.Application/Portal/PortalAppService.cs`，实现 `GetHomeDataAsync(tenantId)` 聚合查询
- [ ] T0.7 **创建 `TenantPublicAppService`**：`src/KnowledgeHub.Application/Public/TenantPublicAppService.cs`，实现 `GetTenantsWithStatsAsync()` 和 `GetTenantStatsAsync(tenantId)`
- [ ] T0.8 **明确租户解析策略**：
  - URL 路径 `/:tenantId` → 查找 `AbpTenants` → 验证存在 → `setTenantId(tenantId)`
  - 公开页面（`/`, `/api/public/*`）无需认证
  - `/tenant/:tenantId/*` 公开浏览门户内容（只读），登录后 `__tenant` header 启用租户上下文
  - 从 `AuthContext.currentTenantId` 获取当前租户 ID（已有 `setCurrentTenantId`）
- [ ] T0.9 验证：`dotnet build` 通过，`dotnet ef migrations add AddMicroMajorResource` 成功

### Reviewer 验证

- [ ] `dotnet build` 无错误
- [ ] 迁移可正常执行
- [ ] `RelationType` 枚举已扩展
- [ ] API 清单与实现匹配无误

---

## Phase 1: 全局布局 + 首页

> **目标**：替换现有 `HomePage.tsx` 和 `StudentLayout.tsx`，建立 icve 风格的新布局和首页。  
> **依赖**：Phase 0 完成（后端 API 就绪）

### TODO

- [ ] T1.1 **重构设计令牌** — 创建 `styles/tokens.css`，定义 icve 风格 CSS 变量
  ```css
  --color-primary: #0056D2;
  --color-primary-light: #E8F0FE;
  --gradient-hero: linear-gradient(135deg, #0b6bcb 0%, #0f8fbe 46%, #18a999 100%);
  --color-bg-page: #F5F7FA;
  --color-bg-card: #FFFFFF;
  --color-text-primary: #1a1a1a;
  --color-border: #E8E8E8;
  --shadow-card: 0 1px 3px rgba(0,0,0,0.06);
  --shadow-card-hover: 0 4px 12px rgba(0,0,0,0.1);
  --radius-card: 8px;
  ```
- [ ] T1.2 **创建 `layouts/PortalLayout.tsx`** — 全局布局（TopBar + `<Outlet />` + Footer）
- [ ] T1.3 **创建 `components/layout/TopBar.tsx`** — Logo + 导航（资源库/课程/微专业/素材/资讯）+ 搜索 + 登录按钮/用户头像
- [ ] T1.4 **创建 `components/layout/Footer.tsx`** — icve 风格底部（友情链接、联系方式、二维码、ICP 备案）
- [ ] T1.5 **创建 `pages/PortalHomePage.tsx`** — 总门户首页（对应 icve `/project`）
- [ ] T1.6 **创建 `components/home/HeroBanner.tsx`** — 渐变背景 + 角色入口卡片 + 搜索栏
- [ ] T1.7 **创建 `components/home/TenantCardGrid.tsx`** — 租户卡片网格（顶部 Tab 按来源筛选: 国家级/省级/校级/企业/学会）
- [ ] T1.8 **创建 `components/home/StatsSection.tsx`** — 平台整体统计（课程数/素材数/学员数）
- [ ] T1.9 **前端 API 调用** — 封装 `getTenantsWithStats()` → `GET /api/public/tenants-with-stats`
- [ ] T1.10 **更新 `App.tsx` 路由** — 引入新路由，旧路由暂留兼容
- [ ] T1.11 **`npm run build` 通过**

### 路由变更

| 旧路由 | 新路由 | 说明 |
|--------|--------|------|
| `/` → HomePage | `/` → PortalHomePage | 总门户首页 |
| — | `/tenant/:tenantId` → TenantPortalPage | 租户门户首页（新增） |

### Reviewer 验证

- [ ] `npm run build` + `npm run lint` 无错误
- [ ] `/` 首页渲染正常（hero + 租户网格 + 统计）
- [ ] TopBar 导航所有链接正确
- [ ] 租户卡片按 Tab 筛选正常
- [ ] Footer 链接和备案号正确
- [ ] 搜索框可输入

---

## Phase 2: 租户门户首页

> **目标**：创建 `/tenant/:tenantId` 租户专属门户首页，对应 icve 项目内部首页。  
> **依赖**：Phase 1 完成

### TODO

- [ ] T2.1 **创建 `pages/TenantPortalPage.tsx`** — 租户门户主页面
- [ ] T2.2 **创建 `components/portal/IntroSection.tsx`** — 资源库简介文字 + 统计数字（课程数/素材数/学员数）
- [ ] T2.3 **创建 `components/portal/MicroMajorSection.tsx`** — 微专业/专业建设卡片展示
- [ ] T2.4 **创建 `components/portal/CourseSystemSection.tsx`** — 课程体系展示（Tab: 基础课/核心课/拓展课 + 课程卡片）
- [ ] T2.5 **创建 `components/portal/MaterialCenterSection.tsx`** — 素材中心（素材卡片网格 + 查看更多）
- [ ] T2.6 **创建 `components/portal/PartnerSection.tsx`** — 协作单位/参建单位横向滚动展示
- [ ] T2.7 **创建 `components/portal/MiniKnowledgeGraph.tsx`** — 首页知识图谱缩略图（静态 SVG，非交互）
- [ ] T2.8 **前端 API 封装** — `getPortalHomeData(tenantId)` → `GET /api/app/portal/{tenantId}/home-data`
  ```typescript
  interface PortalHomeData {
    tenantInfo: { id, name, description, logoUrl, industryField };
    stats: { courseCount, resourceCount, studentCount, microMajorCount };
    microMajors: { id, title, coverImageUrl, courseCount }[];
    featuredCourses: { id, title, coverImageUrl, teacherName, studentCount }[];
    latestMaterials: { id, name, fileExtension, downloadCount, coverUrl }[];
    partners: { id, name }[];
    latestNews: { id, title, publishedAt }[];
  }
  ```
- [ ] T2.9 **`npm run build` 通过**

### Reviewer 验证

- [ ] `/tenant/:tenantId` 正常渲染所有 Section
- [ ] 统计数据与实际数据库一致
- [ ] 微专业/课程卡片点击跳转到对应详情页
- [ ] 课程 Tab 切换正常
- [ ] 素材卡片网格布局正常
- [ ] 首页知识图谱缩略图渲染正确
- [ ] 协作单位横向滚动正常

---

## Phase 3: 课程详情页（Tab 化）

> **目标**：将课程详情页改造为 6-Tab 布局，整合习题、素材、知识图谱。  
> **依赖**：Phase 2 完成 + Phase 0 的 `KnowledgeGraphAppService` 和 `KnowledgeResourceAppService` 就绪

### TODO

- [ ] T3.1 **重写 `pages/courses/CourseDetailPage.tsx`** — Tab 布局课程详情
  - Tab 1: **课程信息** — 封面/描述/标签/教师/学分/学习进度
  - Tab 2: **章节学习** — 章节树 + 进度条 + `[进入练习]` 按钮（复用现有 `chapter-tree` API）
  - Tab 3: **习题练习** — 按章节筛选习题列表 + 在线作答（`ExerciseList` + `ExercisePlayer`）
  - Tab 4: **教学素材** — 课程关联的知识资源（视频/文档/PPT），视频可内嵌播放
  - Tab 5: **知识图谱** — 嵌入互动知识图谱组件（Phase 5 完成后接入，Phase 3 用占位：显示"知识图谱建设中"）
  - Tab 6: **学习统计** — 章节完成率/正确率/学习时长（复用已有 `chapter-progress` API）
- [ ] T3.2 **创建 `components/course/ExerciseList.tsx`** — 按章节分组的习题列表（折叠面板）
- [ ] T3.3 **创建 `components/course/ExercisePlayer.tsx`** — 答题交互组件，支持：
  - 单选题（单选按钮组）
  - 多选题（复选框组）
  - 判断题（对/错按钮）
  - 填空题（文本输入）
  - 提交后显示答案和解析
- [ ] T3.4 **创建 `components/course/VideoPlayer.tsx`** — 简单视频播放器（HTML5 `<video>`）
- [ ] T3.5 **前端 API 封装**：
  - `getCourseExercises(courseId, chapterId?)` → `IExerciseAppService.GetByCourseAsync()` / `.GetByChapterAsync()`（已有，直接封装）
  - `submitExerciseAnswer(dto)` → `IStudentExerciseRecordAppService.CreateAsync()`（已有）
  - `getCourseKnowledgeResources(courseId)` → `IKnowledgeResourceAppService.GetByCourseAsync()`（Phase 0 新建）
  - `getChapterProgress(courseId)` → `IStudentExerciseRecordAppService.GetChapterProgressAsync()`（已有）
- [ ] T3.6 **`npm run build` 通过**

### Reviewer 验证

- [ ] `/tenant/:tenantId/courses/:courseId` 6 个 Tab 全部可见
- [ ] Tab 切换不丢失滚动位置/状态
- [ ] 习题可正常作答（单选/多选/判断/填空），提交后显示结果
- [ ] 视频播放器可正常播放素材
- [ ] 章节进度百分比正确
- [ ] 学习统计数据与实际记录一致

---

## Phase 4: 微专业 + 素材 + 资讯详情页

> **目标**：创建微专业和素材的 Tab 详情，优化资讯详情。  
> **依赖**：Phase 3 完成 + Phase 0 MicroMajorResource 桥接表就绪

### TODO — 微专业

- [ ] T4.1 **创建 `pages/micro-majors/MicroMajorListPage.tsx`** — 微专业列表（搜索 + 分页）
- [ ] T4.2 **创建 `pages/micro-majors/MicroMajorDetailPage.tsx`** — 6 Tab 微专业详情
  - Tab 1: **微专业信息** — 封面/描述/行业领域/协作单位/统计数字
  - Tab 2: **包含课程** — 课程列表 + 各课程学习进度（复用已有 `GetDetailAsync()`）
  - Tab 3: **素材中心** — 微专业直接关联素材（`MicroMajorResource` 桥接表查询）
  - Tab 4: **知识图谱** — Phase 5 微专业级别图谱（Phase 4 用"建设中"占位）
  - Tab 5: **报名与证书** — 报名状态/完成率/证书查看/申请证书（复用已有 `EnrollAsync`, `GetMyEnrollmentsAsync`, `GetMyCertificatesAsync`, `IssueCertificateAsync`）
  - Tab 6: **相关资讯** — 微专业相关资讯列表（可按 `industryField` 标签匹配）

### TODO — 素材

- [ ] T4.3 **创建 `pages/resources/ResourceDetailPage.tsx`** — 5 Tab 素材详情
  - Tab 1: **预览** — PDF/Word/PPT/视频（复用现有 `FilePreviewModal` 组件逻辑）
  - Tab 2: **基本信息** — 文件名/大小/类型/上传者/下载量/浏览量
  - Tab 3: **关联课程** — 哪些课程/章节引用了该素材（`KnowledgeResourceAppService.GetRelatedCoursesAsync`）
  - Tab 4: **知识图谱** — Phase 5 素材关联图谱（Phase 4 用"建设中"占位）
  - Tab 5: **相似推荐** — 同分类/同标签素材推荐
- [ ] T4.4 **保留优化 `pages/resources/ResourceListPage.tsx`** — 素材列表（搜索/分类筛选/分页）

### TODO — 资讯

- [ ] T4.5 **优化 `pages/news/NewsDetailPage.tsx`** — 添加评论 Tab（复用已有 `NewsCommentAppService` API）
- [ ] T4.6 **保留优化 `pages/news/NewsListPage.tsx`** — 资讯列表（已有，调整样式匹配新 UI）

### TODO — 后端 & 类型

- [ ] T4.7 **后端**：
  - `GET /api/app/micro-major/{id}/resources` — 查询 `MicroMajorResource` 桥接表（直接关联）
  - `GET /api/app/resource/{id}/similar` — 按相同分类/标签推荐（`ResourceAppService` 新增方法）
  - `GET /api/app/news-article/by-micro-major/{id}` — 按行业标签匹配微专业资讯（`NewsArticleAppService` 新增方法）
- [ ] T4.8 **更新 `lib/types.ts`** — 添加 `MicroMajorDto`, `MicroMajorDetailDto`, `ResourceDetailDto`, `NewsCommentDto` 等
- [ ] T4.9 **更新 `lib/api.ts`** — 封装所有新 API
- [ ] T4.10 **`npm run build` 通过**

### Reviewer 验证

- [ ] 微专业列表搜索/分页正常
- [ ] 微专业详情 6 个 Tab 全部可切换
- [ ] 微专业报名/证书功能正常
- [ ] 素材详情 5 个 Tab 全部可切换
- [ ] 素材预览支持 PDF/Word/PPT/视频
- [ ] 素材关联课程信息正确
- [ ] 资讯评论可发表/查看
- [ ] 所有列表分页正常

---

## Phase 5: 知识图谱组件

> **目标**：创建可复用互动知识图谱组件，支持三个维度。  
> **依赖**：Phase 4 完成 + Phase 0 的 `KnowledgeGraphAppService` 实现就绪 + `RelationType` 枚举已扩展

### 技术选型

**ECharts**（`echarts` + `echarts-for-react`）：
- 内置 Tree（树图）、Graph（力导向图）布局
- gzip 后 ~300KB，按需引入
- 与现有 Tailwind + React 兼容良好

### TODO

- [ ] T5.1 `npm install echarts echarts-for-react`
- [ ] T5.2 **创建 `components/knowledge-graph/types.ts`** — 图谱类型定义
  ```typescript
  // 节点类型与 RelationType 枚举对齐
  type NodeType = 'micro-major' | 'course' | 'knowledge-point' | 'resource' | 'exercise';
  type EdgeType = 'contains' | 'prerequisite' | 'parallel' | 'references' | 'corequisite' | 'related';
  interface GraphNode { id: string; name: string; type: NodeType; importanceLevel?: string; metadata?: Record<string, any>; }
  interface GraphEdge { source: string; target: string; type: EdgeType; weight?: number; }
  ```
- [ ] T5.3 **创建 `components/knowledge-graph/KnowledgeGraph.tsx`** — 主组件（根据 `mode` 属性自动选布局）
- [ ] T5.4 **创建 `components/knowledge-graph/CourseGraph.tsx`** — 课程知识图谱（力导向布局）
  - 节点 = 知识点（KnowledgeNode）
  - 边 = 先修/并列/包含关系
  - 节点大小/颜色按 `importanceLevel` 区分
- [ ] T5.5 **创建 `components/knowledge-graph/MicroMajorGraph.tsx`** — 微专业知识图谱（树图布局）
  - 根节点 = 微专业 → 二级 = 课程 → 三级 = 知识点 → 四级 = 素材
  - 从 `IKnowledgeGraphAppService.GetGraphForMicroMajorAsync()` 获取数据
- [ ] T5.6 **创建 `components/knowledge-graph/ResourceGraph.tsx`** — 素材关联图谱（星型辐射布局）
  - 中心 = 当前素材
  - 辐射 = 关联知识点 → 课程 → 微专业
  - 从 `IKnowledgeGraphAppService.GetGraphForResourceAsync()` 获取数据
- [ ] T5.7 **创建 `components/knowledge-graph/MiniGraph.tsx`** — 首页缩略图（静态 SVG）
- [ ] T5.8 **创建 `components/knowledge-graph/GraphLegend.tsx`** — 图例组件
- [ ] T5.9 **创建 `components/knowledge-graph/NodeDetail.tsx`** — 节点点击详情侧面板
- [ ] T5.10 **后端**：
  - 在 `IKnowledgeGraphAppService` 新增 `GetGraphForMicroMajorAsync(Guid microMajorId)`
  - 在 `IKnowledgeGraphAppService` 新增 `GetGraphForResourceAsync(Guid resourceId)`
  - 实现 `KnowledgeGraphAppService`（Phase 0 已建基础），补充上述两个方法
- [ ] T5.11 **嵌入图谱组件**：
  - 课程详情 Tab 5 替换占位 → `<KnowledgeGraph mode="course" :courseId />`
  - 微专业详情 Tab 4 替换占位 → `<KnowledgeGraph mode="micro-major" :microMajorId />`
  - 素材详情 Tab 4 替换占位 → `<KnowledgeGraph mode="resource" :resourceId />`
- [ ] T5.12 **`npm run build` 通过**

### 节点样式规范

| 节点类型 | 图标 | 颜色 | ECharts symbolSize |
|----------|------|------|---------------------|
| 微专业 | `GraduationCap` | `#0056D2` | 60 |
| 课程 | `BookOpen` | `#1890FF` | 45 |
| 知识点 | `Zap` | `#52C41A`（low）/ `#FAAD14`（mid）/ `#FF4D4F`（high） | 30 |
| 素材 | `FileText` / `PlayCircle` | `#722ED1` | 25 |
| 习题 | `CheckSquare` | `#EB2F96` | 25 |

### 边样式规范

| 关系类型 | 线型 | 颜色 |
|----------|------|------|
| contains（包含） | 实线 + 箭头 | `#0056D2` |
| prerequisite（先修） | 虚线 + 箭头 | `#FAAD14` |
| parallel（并列） | 点线 | `#52C41A` |
| references（引用） | 实线 + 圆点 | `#722ED1` |
| corequisite（共修） | 点划线 | `#1890FF` |
| related（相关） | 虚线 | `#999999` |

### Reviewer 验证

- [ ] 课程图谱：力导向布局正确，节点可拖拽/缩放，点击节点显示详情
- [ ] 微专业图谱：树图布局层级正确（微专业 → 课程 → 知识点 → 素材）
- [ ] 素材图谱：星型布局，中心=素材，辐射关联节点
- [ ] 首页缩略图静态渲染正确
- [ ] 图例颜色/类型对应正确
- [ ] 所有图谱组件正常加载，无控制台错误

---

## Phase 6: 收尾 — 代码清理 + 路由合并

> **目标**：删除旧代码，清理路由，确保新旧平滑过渡。  
> **依赖**：Phase 5 完成

### TODO

- [ ] T6.1 **删除旧组件**：
  - `pages/HomePage.tsx`（旧首页）
  - `layouts/StudentLayout.tsx` + `StudentLayout.css`（旧布局）
- [ ] T6.2 **删除旧 placeholder 引用**
- [ ] T6.3 **清理 `styles/globals.css`** — 删除已不再使用的样式规则
- [ ] T6.4 **清理 `lib/api.ts`** — 删除旧的 API 函数（`getPublishedCourses` 等已不再使用）
- [ ] T6.5 **精简 `App.tsx` 路由** — 仅保留新路由，移除旧路由注释
- [ ] T6.6 **添加旧路由重定向**：
  ```typescript
  // 旧路由 → 新路由 兼容重定向
  <Route path="/student/resources" element={<Navigate to="/" />} />
  <Route path="/student/courses" element={<Navigate to="/" />} />
  <Route path="/student/course-detail/:id" element={<Navigate to="/" />} />
  ```
  （旧路由统一导向总门户首页，用户在首页选择租户后进入对应门户）
- [ ] T6.7 **`npm run build` — 零错误**
- [ ] T6.8 **`npm run lint` — 零警告**
- [ ] T6.9 **TypeScript 严格检查**：确认 `tsconfig.json` 中 `strict: true`，无类型错误

### Reviewer 验证

- [ ] `npm run build` 零错误
- [ ] `npm run lint` 零警告
- [ ] 无未使用的 import/组件/文件
- [ ] 旧路由访问时正确重定向
- [ ] CSS 无冗余样式（可用 Chrome DevTools Coverage 验证）
- [ ] TypeScript 严格模式无报错

---

## 路由总览

```
/                                            # 总门户首页（租户选择/浏览）
/tenant/:tenantId                            # 租户门户首页
/tenant/:tenantId/resources                  # 素材列表
/tenant/:tenantId/resources/:resourceId      # 素材详情（5 Tab）
/tenant/:tenantId/courses                    # 课程列表
/tenant/:tenantId/courses/:courseId          # 课程详情（6 Tab）
/tenant/:tenantId/micro-majors               # 微专业列表
/tenant/:tenantId/micro-majors/:id           # 微专业详情（6 Tab）
/tenant/:tenantId/news                        # 资讯列表
/tenant/:tenantId/news/:id                    # 资讯详情
/login                                        # 登录
/auth/callback                                # OIDC 回调
```

---

## 后端 API 总览

| Phase | Method | Path | 实现方式 | 优先级 |
|-------|--------|------|----------|--------|
| P0 | GET | `/api/public/tenants-with-stats` | 新建 `TenantPublicAppService` | P0 |
| P0 | GET | `/api/public/tenant-stats/{tenantId}` | 同上 | P0 |
| P0 | — | `RelationType` 枚举扩展 | 修改 `RelationType.cs` | P0 |
| P0 | — | `KnowledgeGraphAppService` | 新建实现（接口已有） | P0 |
| P0 | — | `KnowledgeResourceAppService` | 新建接口+实现 | P0 |
| P0 | — | `MicroMajorResource` 桥接表 | 新建实体+迁移 | P0 |
| P2 | GET | `/api/app/portal/{tenantId}/home-data` | `PortalAppService`（聚合查询） | P0 |
| P3 | GET | `/api/app/exercise/by-course/{courseId}?chapterId=` | 复用已有 `ExerciseAppService` | — |
| P3 | POST | `/api/app/student-exercise-record` | 复用已有 `StudentExerciseRecordAppService` | — |
| P3 | GET | `/api/app/student-exercise-record/chapter-progress/{courseId}` | 复用已有 | — |
| P3 | GET | `/api/app/knowledge-resource/by-course/{courseId}` | `KnowledgeResourceAppService`（P0 新建） | P1 |
| P4 | GET | `/api/app/micro-major/published` | 复用已有 `MicroMajorAppService` | — |
| P4 | GET | `/api/app/micro-major/{id}/resources` | `MicroMajorAppService` 新增方法（桥接表查询） | P1 |
| P4 | GET | `/api/app/resource/{id}/similar` | `ResourceAppService` 新增方法 | P2 |
| P4 | GET | `/api/app/knowledge-resource/{id}/related-courses` | `KnowledgeResourceAppService`（P0 新建） | P2 |
| P4 | GET | `/api/app/news-article/by-micro-major/{id}` | `NewsArticleAppService` 新增方法 | P2 |
| P4 | GET/POST | `/api/app/news-comment` | 复用已有 `NewsCommentAppService` | — |
| P5 | GET | `/api/app/knowledge-graph/{courseId}` | `KnowledgeGraphAppService`（P0 实现） | P1 |
| P5 | GET | `/api/app/knowledge-graph/micro-major/{id}` | `KnowledgeGraphAppService` 新增方法 | P1 |
| P5 | GET | `/api/app/knowledge-graph/resource/{resourceId}` | `KnowledgeGraphAppService` 新增方法 | P2 |

---

## 阶段依赖关系与时间估算

```
Phase 0 (发现+准备) ──┬── Phase 1 (布局+首页) ── Phase 2 (租户门户)
  3-5天              │       3-5天                  3-5天
                      │
                      ├── Phase 3 (课程详情 Tab) ── Phase 4 (微专业/素材/资讯详情) ── Phase 6 (收尾)
                      │       5-7天                     5-8天                           2-3天
                      │                                    │
                      └── Phase 5 (知识图谱) ───────────────┘
                              6-8天
```

| Phase | 内容 | 预估 |
|-------|------|------|
| P0 | 发现与准备（后端基础设施） | 3-5 天 |
| P1 | 全局布局 + 首页 | 3-5 天 |
| P2 | 租户门户首页 | 3-5 天 |
| P3 | 课程详情页 Tab 化 | 5-7 天 |
| P4 | 微专业 + 素材 + 资讯详情页 | 5-8 天 |
| P5 | 知识图谱组件 | 6-8 天 |
| P6 | 收尾清理 | 2-3 天 |
| **合计** | | **27-41 天** |
