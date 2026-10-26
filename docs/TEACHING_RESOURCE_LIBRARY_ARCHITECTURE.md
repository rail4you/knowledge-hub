# 教学资源库 —— 架构设计说明

> 状态：后端已落地（管理端 + 教师端 API），前端页面待补。
> 关联需求：《软件需求功能清单和技术参数要求说明》 + 补充说明「租户 = 学校」。

---

## 1. 核心概念定义

| 概念 | 落点 | 说明 |
|------|------|------|
| **租户 = 学校** | `School`（全局实体，`TenantId` 唯一索引） | 徽标 `LogoUrl`、横幅 `BannerUrl`、简介 `Introduction`、联系方式、域名 / 子域名。由**院校管理员**（`SchoolAdmin`）维护，`Schools.Edit` 权限。 |
| **教学资源库** | `ResourceLibrary`（租户级实体） | 归属某所学校（`OwnerTenantId` = 主学校）。是**文件资源 + 课程 + 实训**的关联集合。**不包含**微专业、就业、咨询、双高等数据。 |
| **专业维度** | `ResourceLibrary.PrimaryMajorId`（可选） | 仅作为**库的业务标签**（门户展示 / 筛选 / 专业建设统计）。**不再限制**库内内容：课程 / 实训可多专业，资源专业可缺省，一律可入库（`ResourceLibraryAttachmentValidator` 只校验挂载权限）。 |

「资源库」这一概念在本系统内**统一为「教学资源库」**：许可、API、界面文案均以此为准；C# 类型名保留
`ResourceLibrary` / `ResourceLibraryMember` 以避免无谓的迁移与代理破坏。

---

## 2. 资源共享的重新对应（旧共享功能已下线）

### 2.1 为什么下线旧共享

旧模型用一张独立表 `AppResourceShares` 记录「资源 → 目标租户」的共享关系。这套机制与教学资源库
**功能重叠、语义冲突**：库里已经在做「谁能看到这个库、谁能看到库里的内容」，再加一条平行的共享通道，
就会出现两套可见性判定互相打架。

现已统一：**资源共享一律以教学资源库为载体**。

| | 旧共享（已下线） | 新模型（教学资源库） |
|---|---|---|
| 承载表 | `AppResourceShares` | `AppResources.LibraryId` → `AppResourceLibraries` |
| 可见性判定 | `ResourceShare.TargetTenantId` 精确匹配 | 库的 `Visibility` + 成员 + 教师分配 |
| 共享粒度 | 单个资源 → 单个租户 | 库 → 多个参与方（含教师分配） |
| 撤销共享 | 删 `AppResourceShares` 行 | 把内容从库里摘出（`LibraryId` 置空） |

### 2.2 跨校可见性（新口径）

「共享给我的资源」= **其它学校在院校共建库里共享进来的资源**。

判定（`ResourceAppService.GetIncomingLibraryIdsAsync`）：

```
本校作为「关联学校」参与的院校共建库
  （Visibility = CoBuilt 且 OwnerTenantId ≠ 本校 且 本校是成员）
  → 这些库里归属别校的资源，对本校可见
```

对应到代码与接口：

| 场景 | 旧实现 | 新实现 |
|------|--------|--------|
| 资源列表 | 合并 `ResourceShare.TargetTenantId = 本校` 的资源 | 合并共建库中归属别校、且已审核通过的资源 |
| 资源详情 | 查 `ResourceShare` 决定是否跨租户放行 | 查共建库归属决定是否跨租户放行 |
| 下载校验 | 查 `ResourceShare` | 查共建库归属 |
| 学院评审通过列表 | 查 `ResourceShare` | 查共建库归属 |
| 工作台共享数 | 统计 `AppResourceShares` 双向计数 | 统计共建库：共享进来 / 共享出去 |

### 2.3 数据迁移

迁移 `20261025154514_RemoveResourceShareAndAlignToLibrary`：

1. 按「共享来源 → 目标」租户对，新建一个名为 **历史共享资源库** 的院校共建库
   （主学校 = 共享来源方，`PrimaryMajorId` 留空 = 不限专业）；
2. 写入主学校(Owner) + 关联学校(Partner)两条成员记录；
3. 把此前已共享、尚未挂库的资源挂到该库上；
4. 删除 `AppResourceShares` 表。

> 刻意**不复用**「限定专业」的老库 —— 迁移过来的资源专业可能与该库主建专业不一致。
> （注：库内内容的专业一致性约束已在 2026-10-26 取消，老库同样可正常挂载。）

迁移 `20261025154937_ConsolidateMigratedSharedLibraries` 是配套修正：
首个迁移脚本里 `DISTINCT` 与 `gen_random_uuid()` 同层导致按「共享记录」而非「租户对」建库，
产生重复库；该迁移把同一（主学校, 关联学校）组合下的库合并为一个。

### 2.4 连带清理

- 实体 / 仓储 / AppService：`ResourceShare`、`IResourceShareRepository`、`EfCoreResourceShareRepository`、`ResourceShareAppService` 已删除。
- 权限：`Resources.Share` / `Resources.ManageShare` 已删除；**课程共享**改用新增的
  `Courses.Share` / `Courses.ManageShare`（课程维度的共享机制保留，不受资源库改造影响）。
- DTO：`ResourceDto` 移除 `IsShared` / `OutgoingShare*` / `SourceTenant*` / `Shared*` / `ShareNote`，
  改为 `LibraryId` / `LibraryName` / `IsSharedIn` / `OwnerTenantId` / `OwnerTenantName` / `TenantId`。
- 前端：删除 `resource-share.mixin.ts`、共享弹窗与共享按钮；资源列表「共享情况」列改为
  「教学资源库」列（库名 + 来源学校），只读判定改用 `isSharedIn`；代理重新生成。

---

## 3. 数据模型

```
School (全局)                          TenantId(唯一索引) ──┐
                                                        │ 1:1
ResourceLibrary (租户级)                TenantId = 主学校租户
  ├─ OwnerTenantId      主学校
  ├─ Visibility         Private(0) / Public(1) / CoBuilt(2)   ← 新增
  ├─ PrimaryMajorId     可选专业标签
  ├─ IsPublic           门户公开展示（与 Visibility 正交）
  └─ Status             Draft / Active / Archived
        │
        ├─ ResourceLibraryMember   共建成员：主学校(Owner) + 关联学校(Partner)
        └─ ResourceLibraryTeacher  教师分配：LibraryId + UserId + TenantId  ← 新增
                                  唯一索引 (LibraryId, UserId)

Resource.LibraryId      ← 已有
Course.LibraryId        ← 已有
PracticumProject.LibraryId  ← 新增（实训纳入资源库）
```

### 关键取舍：内容归属 vs 库归属

共建库里，各校教师共享的内容**仍归属各自租户**（`Resource.TenantId` / `Course.TenantId` 不变），
只通过 `LibraryId` 跨租户指向该库。

- 好处：内容归属清晰、不破坏 ABP 租户隔离、撤销共享只需清空 `LibraryId`。
- 代价：读取库内容必须**禁用 ABP 租户过滤器**再按 `LibraryId` 精确匹配
  （`_dataFilter.Disable<IMultiTenant>()`，见 `EfCoreResourceLibraryRepository` 与各 AppService）。

> ⚠️ **踩坑记录**：ABP 未启用 EF 懒加载代理。直接访问实体的 `Members` 集合会得到**空集合**，
> 导致「本租户是否为关联学校」被静默判成 `false`。因此库查询必须 `Include(x => x.Members)`
> （`IResourceLibraryRepository.GetByTenantOrMemberAsync` / `FindWithMembersAsync`），
> 且 DTO 组装后要把数据库里的成员同步回内存集合。

---

## 4. 可见性规则（`ResourceLibraryAccessService`）

| 可见性 | 谁能看到 | 谁可向库内共享 | 关联学校 |
|--------|---------|---------------|---------|
| **Private 私有** | 仅**被分配教师**（+ 本校院校管理员） | 被分配教师 | 不允许 |
| **Public 公开** | 归属学校**全体教师** | 归属学校全体教师 | 不允许 |
| **CoBuilt 院校共建** | 主学校 + 全部关联学校的教师 | 主学校 + 关联学校全体教师 | 必填，≥1 所 |

补充规则：

- 院校管理员（`SchoolAdmin` / host `admin`）对**本租户名下**（含作为共建方参与）的全部库有管理权，不受可见性限制。
- 切换可见性时强制维护不变量：`Private` / `Public` → 自动清空关联学校；切到 `CoBuilt` → 必须至少 1 所关联学校。
- **只能共享本校内容**到资源库（防止把别校内容据为己有）。
- 删除库 → 库内资源 / 课程 / 实训的 `LibraryId` 置空（内容本身保留），教师分配记录随库删除。

### 4.1 公共课程（公共资源库 → 所有学校的学生）

> 需求「公共课程其他学校的学生可以学习：把公共课程加到所有学生的课程列表里，在账户页面能看到；
> 只是加上一个区别 —— 有一个公共课程（要注明学校等信息）的区域。」

| 角色 | 公共资源库（`Visibility=Public` 且状态有效）里的**已发布**课程 |
|------|----------------------------------------------------------|
| **所有学校的学生** | ✅ 学生端课程中心「**公共课程**」区域可见（卡片注明**来源学校**），可选课、可学习 |
| 本校已发布课程区域 | ❌ 公共课程**不混入**「本校已发布课程」，保持两个区域互不干扰 |

实现：

- **列表**：`GET /api/app/course/public-library-courses`
  （`CourseAppService.GetPublicLibraryCoursesAsync`）—— 跨租户取公共资源库里的已发布课程，
  课程归属哪所学校都行（主库字段 + `AppResourceLibraryCourses` 关联表两个来源的并集）。
- **选课**：`EnrollAsync` 的跨租户放行条件为「`CourseShare` 共享 **或** 课程在公共资源库里」。
- **学习**：`LearningAppService.GetMyCoursesAsync` / `GetDashboardAsync` 对公共资源库的跨租户课程放行
  （原先 `course.TenantId != 当前租户` 会直接丢弃）。
- **前端**：学生端课程中心筛选范围新增第三项「公共课程」（`scope=public`），
  卡片打「公共课」标签并显示「来自 X学校」（`CourseDto.ownerTenantName`）。

> 课程详情（`GetAsync` / `GetDetailAsync`）本来就是跨租户可读，无需改动。

---

## 5. API 一览

### 管理端（院校管理员）`/api/app/resource-library`

| 方法 | 路由 | 权限 |
|------|------|------|
| GET | `/api/app/resource-library` | `ResourceLibraries.Default` |
| GET | `/api/app/resource-library/{id}` | `ResourceLibraries.Default` |
| POST | `/api/app/resource-library` | `ResourceLibraries.Create` |
| PUT | `/api/app/resource-library/{id}` | `ResourceLibraries.Edit` |
| DELETE | `/api/app/resource-library/{id}` | `ResourceLibraries.Delete` |
| POST | `/api/app/resource-library/{id}/member` | `ResourceLibraries.ManageMembers` |
| DELETE | `/api/app/resource-library/{id}/member/{memberTenantId}` | `ResourceLibraries.ManageMembers` |
| GET | `/api/app/resource-library/{id}/teachers` | `ResourceLibraries.Default` |
| GET | `/api/app/resource-library/assignable-teachers` | `ResourceLibraries.AssignTeachers` |
| POST | `/api/app/resource-library/{id}/assign-teacher` | `ResourceLibraries.AssignTeachers` |
| DELETE | `/api/app/resource-library/{id}/teacher/{userId}` | `ResourceLibraries.AssignTeachers` |

### 教师端（教学资源库建设）`/api/app/teaching-resource-library`

| 方法 | 路由 | 权限 |
|------|------|------|
| GET | `/api/app/teaching-resource-library/my-libraries` | `ResourceLibraries.Default` |
| GET | `/api/app/teaching-resource-library/{id}/library` | `ResourceLibraries.Default` |
| GET | `/api/app/teaching-resource-library/{id}/contents` | `ResourceLibraries.Default` |
| POST | `/api/app/teaching-resource-library/{id}/attach` | `ResourceLibraries.Build` |
| POST | `/api/app/teaching-resource-library/{id}/detach/{contentId}` | `ResourceLibraries.Build` |
| POST | `/api/app/teaching-resource-library/{id}/resource` | `ResourceLibraries.Build` |
| POST | `/api/app/teaching-resource-library/{id}/course` | `ResourceLibraries.Build` |
| POST | `/api/app/teaching-resource-library/{id}/practicum` | `ResourceLibraries.Build` |

> 归属学校通过 `Host: {slug}.localhost` / 子域名解析（`SchoolDomain` 租户解析器），
> 或走 `Abp-TenantId` 请求头。

---

## 6. 权限

| 权限 | SchoolAdmin | Teacher | Student | host admin |
|------|:-----------:|:-------:|:-------:|:----------:|
| `ResourceLibraries.Default` | ✅ | ✅ | ✅ | ✅ |
| `.Create` / `.Edit` / `.Delete` | ✅ | ❌ | ❌ | ✅ |
| `.ManageMembers` | ✅ | ❌ | ❌ | ✅ |
| `.AssignTeachers` | ✅ | ❌ | ❌ | ✅ |
| `.Build` | ✅ | ✅ | ❌ | ✅ |

授权由 `RolePermissionSeeder` 播种，`RolePermissionEnsureHostedService` 在**每次 API 启动时**
对所有租户自愈补齐，新增权限无需手工迁移授权数据。

---

## 7. 数据库迁移

`20261025145049_AddTeachingResourceLibraryVisibility`

- `AppResourceLibraries.Visibility`（smallint，默认 0=私有）
- `AppResourceLibraryTeachers`（新表，含 `(LibraryId, UserId)` 唯一索引）
- `AppPracticumProjects.LibraryId`（+ 索引）
- **历史数据回填**：带关联学校的库 → `CoBuilt(2)`，其余 → `Public(1)`，
  避免升级后存量库对教师突然不可见。

---

## 8. 验证结论（2026-10-25，开发环境实测）

| 场景 | 结果 |
|------|------|
| 院校管理员列表 | 主建的 + 共建参与的，共 3 个库 ✅ |
| 教师列表 | 仅与本人相关的库；非共建方的库不可见 ✅ |
| 私有库：分配前教师不可见 → 分配后可见 → 取消分配后不可见 | ✅ |
| 公开库：教师无需分配即可见、可共享 | ✅ |
| 共建库：关联学校教师可见，且能看到主建方共享的内容 | ✅ |
| 跨租户共享：国州教师共享的资源归属国州租户，启迪教师可见并显示「来自 国州学院」 | ✅ |
| 共建库未设关联学校 → 拒绝创建 | ✅ |
| 教师越权建库 / 删库 / 分配教师 | 全部 403 ✅ |
| 删除库 → 内容 `LibraryId` 置空、内容保留 | ✅ |

### 8.1 资源共享改造后复测（2026-10-25 晚）

| 场景 | 结果 |
|------|------|
| 资源列表返回共建库共享进来的资源，标注「来自 XX 学院」 | ✅ |
| 资源列表无重复项（本校共享进别校库的资源不重复计入） | ✅ |
| 共建库共享进来的资源详情可跨租户打开（HTTP 200） | ✅ |
| 教师新建资源：不带库 → `LibraryId` 为空；带库 → 正确写入 | ✅ |
| 工作台：自有 19 / 共享进来 1 / 共享出去 12（库维度统计） | ✅ |
| 旧共享接口全部 404（`shares/{id}`、`shared-to-me`、`shared-by-me`） | ✅ |
| 旧共享表 `AppResourceShares` 已删除 | ✅ |
| Angular 编译通过（资源页 / 管理端库页 / 学生端资源标签） | ✅ |

---

## 9. 前端入口（菜单与路由）

教学资源库在**教师端**与**管理端**各有一个入口：

| 端 | 菜单 | 路由 | 页面 | 权限 |
|----|------|------|------|------|
| 教师端 | 系统工作台 → **教学资源库工作台** | `/teaching-resource-libraries`（另有工作台下方区块） | `TeachingLibraryWorkbenchPageComponent` | `KnowledgeHub.ResourceLibraries` |
| 管理端 | 搜索和租户管理 → **教学资源库管理** | `/admin/libraries` | `ResourceLibraryManagementComponent` | `KnowledgeHub.ResourceLibraries` |

- 菜单注册在 `angular/src/app/route.provider.ts`（教师端挂在 `::Menu:ResourceManagement` 分组下 order=4；
  管理端挂在 `::Menu:SearchAndTenantManagement` 分组下 order=5，`租户信息` 顺延为 order=6）。
  两者都是**分组内**条目，不影响 `sidebar-divider.config.ts` 的分组分隔线。
- 路由注册在 `angular/src/app/app.routes.ts`，均带 `authGuard + nonStudentGuard + permissionGuard`。
- 教师端页面基于新接口 `TeachingResourceLibraryService`：
  「我的教学资源库」列表 → 库内内容（文件资源 / 课程 / 实训，可按类型与关键字筛选、分页）
  → 共享内容进库 / 把内容移出库。
- 管理端页面（`/admin/libraries`）此前**只有路由没有菜单入口**，本次补上菜单，
  并补充可见性（私有 / 公开 / 院校共建）选择器与可见性、教师数展示。
- 权限策略 `KnowledgeHub.ResourceLibraries` 同时授予 `SchoolAdmin` 与 `Teacher`，
  因此两端菜单都会显示；`Student` 无此权限。

---

## 10. 教学资源库工作台（教师端）

> 需求：「教师端应该能看到教学资源库工作台，包含教师能看到的所有资源库、资源库的数据汇总；
> 资源库（尤其是共建资源库）对应的课程能跳转到课程 / 实训 / 资源，对应资源库能上传等操作；
> 所有资源库相关的数据和操作都要在工作台能看到和完成；UI 参考管理端页面风格；
> 放在系统工作台的下方。」

### 10.1 位置与组成

- **两个入口，同一套组件**：
  | 入口 | 位置 | 说明 |
  |------|------|------|
  | 侧边栏菜单「教学资源库工作台」 | `/teaching-resource-libraries` | 独立页面，页头带「返回工作台」 |
  | 系统工作台页面下方 | `/admin/workbench` | 同一组件嵌入为区块（区块内标题可隐藏） |
- 组件：`angular/src/app/resources/teaching-library-workbench/`
  - `teaching-library-workbench.component.ts`（standalone + signals + OnPush）—— 区块本体，
    复用管理端样式原子 `kh-card / kh-metric / kh-chip / kh-tag`；
  - `teaching-library-workbench-page.component.ts` —— 独立页面外壳（页头 + 返回工作台）。
- **侧边栏排序**：菜单注册在 `angular/src/app/route.provider.ts`，`order: 1.5`
  （ABP 侧边栏先按 order 排序，1.5 恰好落在「系统工作台」(1) 与「资源中心」(2) 之间，
  无需整体重排其它栏目）；`requiredPolicy: 'KnowledgeHub.ResourceLibraries'`（教师 / 管理员可见，学生不可见）。
  本地化键 `Menu:WorkbenchTeachingLibrary`（zh-Hans：教学资源库工作台）。
- **展示范围**：`GET /api/app/teaching-resource-library/my-libraries` —— 即「我能看到的全部库」
  （被分配的私有库 + 本校公开库 + 参与共建的库）；host 全局管理员不显示该区块（无租户上下文）。
- **数据汇总**：区块顶部 4 张指标卡 —— 可见资源库数（私有 / 公开 / 共建分项）、库内资源数、
  库内课程数、库内实训数；每张库卡片再展示该库的 资源 / 课程 / 实训 计数与归属信息
  （主建学校、共建学校、主建专业、建设负责人）。
- **卡片布局**：库卡片为**两栏**网格（`repeat(2, minmax(0, 1fr))`，≤1080px 折为单栏）。

### 10.2 快捷访问

| 入口 | 行为 |
|------|------|
| 库卡片「资源 / 课程 / 实训」计数块 | 点击直达对应列表并按库过滤：`/resources?libraryId=`、`/learning/course-list?libraryId=`、`/practicum/projects?libraryId=` |
| 库卡片「库内容」 | 弹窗查看库内三类内容（类型 / 关键字 / 只看我创建的筛选 + 分页）；**课程不逐个打开**，直接跳到该库的课程列表（同卡片「课程」计数块的跳法），实训跳实训详情，资源跳资源中心并打开资源抽屉；可「移出库」 |
| 全局快速导航（Ctrl/Cmd + K） | 新增「教学资源库工作台」项 → `/admin/workbench?section=libraries`，自动滚动到该区块 |

> 深链参数统一为 `libraryId`。三个列表页均已支持：
> 资源页（`resource.ts` 读 `queryParamMap.libraryId` → `selectedLibraryId`）、
> 课程列表（`course-list.component.ts` → `libraryFilter`）、
> 实训列表（`practicum-list.component.ts` → `libraryId` 请求参数 + 顶部「资源库：xxx」筛选标签）。

### 10.3 库内操作（写权限 `KnowledgeHub.ResourceLibraries.Build`）

> 卡片操作区只保留 **共享资源**（进库）与 **库内容**（查看 / 移出）两个入口；
> 「上传资源 / 新建课程 / 新建实训」按需求已从卡片上移除（如需建库内容，走资源中心 / 课程中心 / 实训管理）。

| 操作 | 接口 |
|------|------|
| 共享资源（文件资源 / 课程 / 实训）进库 | `POST /api/app/teaching-resource-library/{id}/attach` |

> **共享列表的数据口径**：与**文件资源表单**里能选用的教学资源库完全同一套数据
> （同一接口 `GET /api/app/resource/library-facets`，即资源中心侧边栏的库列表）：
> - 「所属资源库」下拉的选项 = 该 facets 集合（不含「未分类」伪项）；
> - 候选内容只保留「所属库都在该集合内」或「未关联任何库」的内容 ——
>   只挂在他校库（如某校私有库）上的内容属于「多出来的」，不在共享列表出现。
| 查看库内容 / 把内容移出库 | `GET /api/app/teaching-resource-library/{id}/contents`、`POST /api/app/teaching-resource-library/{id}/detach/{contentId}` |
| （后端已就绪、卡片未开放）库内新建资源 / 课程 / 实训 | `POST /api/app/teaching-resource-library/{id}/resource｜course｜practicum` |

### 10.4 后端配合

- `TeachingResourceLibraryAppService.ToLibraryDtoAsync` 的资源计数口径改为
  「主库字段 `LibraryId` + 资源—库关联表」的**并集**，与 `GetContentsAsync` 保持一致
  （此前只统计主库字段，多对多登记的资源会漏计）。
- **取消「库内内容专业必须与库主建专业一致」的约束**（2026-10-26）：
  `ResourceLibraryAttachmentValidator.ValidateAsync` 只校验挂载权限
  （本校主建 / 参与共建），不再校验专业；`ValidateAsync` 签名去掉 `majorId` 参数。
  库的 `PrimaryMajorId` 降级为**展示标签**，课程 / 实训可多专业、资源专业可缺省，均可入库。
  此前共享实训到「智能制造专业教学资源库」会报「请先为内容设置专业」，现已可正常共享。

### 10.5 实测（2026-10-26，租户 qidi / 教师 wwq）

| 场景 | 结果 |
|------|------|
| 教师工作台下方出现「教学资源库工作台」 | ✅ 可见 4 个库（私有 1 / 公开 1 / 共建 2） |
| 侧边栏菜单「教学资源库工作台」 | ✅ 紧跟在「系统工作台」下方，跳转 `/teaching-resource-libraries` |
| 数据汇总 | ✅ 资源 3 · 课程 4 · 实训 0（含关联表资源，计数口径已修正） |
| 卡片两栏布局 | ✅ 每行 2 张库卡片（≤1080px 折单栏） |
| 卡片操作区 | ✅ 仅保留「共享资源」+「库内容」 |
| 共建库「国州·多校共建测试库」库内容 | ✅ 跨租户可见启迪学院 111 共享的 2 门课程，可「移出库」 |
| 深链 `/resources?libraryId=` | ✅ 资源列表收敛到该库，左栏高亮 |
| 深链 `/learning/course-list?libraryId=` | ✅ 只显示该库课程 |
| 深链 `/practicum/projects?libraryId=` | ✅ 顶部显示「资源库：xxx」+ 清除筛选 |
| 快速导航「教学资源库工作台」 | ✅ 跳转并滚动到区块 |
| 库内容弹窗内课程行 | ✅ 直接跳该库课程列表（不再打开课程详情） |
| 共享实训到「智能制造专业教学资源库」（限定专业） | ✅ 成功（不再报「请先为内容设置专业」）；自测数据已清理 |

---

## 11. 待办（前端）

1. 管理端「教学资源库管理」页：教师分配（可分配教师列表 / 分配 / 取消）已完成 UI；
   库的管理（建库 / 共建成员 / 可见性 / 归档）也已完成。
2. 教师端：独立页面 `/teaching-resource-libraries` + 系统工作台下方区块（同一组件）；
   卡片上只保留「共享资源 / 库内容」，建资源 / 建课程 / 建实训仍走资源中心 / 课程中心 / 实训管理
   （后端 `POST /{id}/resource｜course｜practicum` 已就绪，需要时可再开放入口）。
3. 代理生成：`abp generate-proxy -t ng -u https://localhost:44305 -a default -s KnowledgeHub`
   （**不要手改** `angular/src/app/proxy/`）。
   ⚠️ 全量重生成会覆盖仓库里手工加过的代理方法（如 `users/user.service.ts` 的 `changePassword`、
   `teaching-agents/agent-run.service.ts` 的 `submitAssignment`）并引入生成器 bug
   （`request<any, any<T>>`），重新生成后需从 git 还原这几处。
