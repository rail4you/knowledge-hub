# 教学资源库 —— 架构设计说明

> 状态：后端已落地（管理端 + 教师端 API），前端页面待补。
> 关联需求：《软件需求功能清单和技术参数要求说明》 + 补充说明「租户 = 学校」。

---

## 1. 核心概念定义

| 概念 | 落点 | 说明 |
|------|------|------|
| **租户 = 学校** | `School`（全局实体，`TenantId` 唯一索引） | 徽标 `LogoUrl`、横幅 `BannerUrl`、简介 `Introduction`、联系方式、域名 / 子域名。由**院校管理员**（`SchoolAdmin`）维护，`Schools.Edit` 权限。 |
| **教学资源库** | `ResourceLibrary`（租户级实体） | 归属某所学校（`OwnerTenantId` = 主学校）。是**文件资源 + 课程 + 实训**的关联集合。**不包含**微专业、就业、咨询、双高等数据。 |
| **专业维度** | `ResourceLibrary.PrimaryMajorId`（可选） | 留空 = 不限专业；设置后库内内容的专业必须一致（`ResourceLibraryAttachmentValidator` 校验）。 |

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

> 刻意**不复用**「限定专业」的老库 —— 迁移过来的资源专业可能与该库主建专业不一致，
> 挂进去会破坏「库内内容专业须与库一致」的约束。

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
| 教师端 | 资源中心 → **教学资源库** | `/teaching-resource-libraries` | `TeachingResourceLibraryComponent` | `KnowledgeHub.ResourceLibraries` |
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

## 10. 待办（前端）

1. 管理端「教学资源库管理」页：**教师分配**交互（可分配教师列表 / 分配 / 取消）尚未做 UI，
   后端接口 `GET /assignable-teachers`、`POST /{id}/assign-teacher`、`DELETE /{id}/teacher/{userId}` 已就绪。
2. 教师端「教学资源库建设」页：目前支持浏览与「共享已有内容」，
   **库内直接新建课程 / 实训**的表单尚未做 UI（后端 `POST /{id}/course`、`/{id}/practicum` 已就绪）。
3. 代理生成：`abp generate-proxy -t ng -u https://localhost:44305 -a default -s KnowledgeHub`
   （**不要手改** `angular/src/app/proxy/`）。
   ⚠️ 全量重生成会覆盖仓库里手工加过的代理方法（如 `users/user.service.ts` 的 `changePassword`、
   `teaching-agents/agent-run.service.ts` 的 `submitAssignment`）并引入生成器 bug
   （`request<any, any<T>>`），重新生成后需从 git 还原这几处。
