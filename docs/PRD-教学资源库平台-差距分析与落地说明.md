# 教学资源库平台 PRD — 差距分析与本次落地说明

> 依据文档：`~/Desktop/附件：1023教学资源库平台 · 产品需求文档（PRD）.docx`
> 对照实现：本仓库（ABP .NET + Angular）
> 本次落地：① 共享资源库的「仅限校内」约束（后端强制 + UI 约束）② 结业证书功能（申请 → 审核 → 发放 → 核验 → 归档）

---

## 一、PRD 形态与平台现状的映射

PRD 第 2.1 节把资源库分为三种形态，平台枚举 `ResourceLibraryVisibility` 与之对应：

| PRD 形态 | 平台枚举 | 平台语义（改造前） | 目标语义（本次） |
|---|---|---|---|
| 校级私有库（非共享资源库） | `Private = 0` | 仅被分配教师可见的「教师集合」 | 前台所有人可见可浏览；**仅课程老师导入班级的学员可学习**；不允许参建单位；不对外开放自学 |
| 校级共享库（单校共享） | `Public = 1` | 本校全体教师可见 + 可跨租户选课 | **对外开放学习**（其他学员需手机号 + 实名认证）；不允许参建单位；**库内课程仅限校内共享** |
| 校际共建库（共建共享资源库） | `CoBuilt = 2` | 主校 + 关联校教师共享 | 主建 + 参建单位；可共享给**参建单位**；同样对外开放自学 |

> 关键结论：平台原有的 `Visibility` 表达的是「**教师之间**的横向共享范围」，PRD 表达的是「**对学员**的开放范围 + 对外共享范围」。两者语义不同，不能直接复用同一个开关。
> 本次改造保留 `Visibility` 表达教师侧范围，另加 `ResourceLibrary.OpenToExternalLearners`（对外开放自学）表达学员侧范围，
> 并引入 `ResourceLibrarySharingScope`（跨校共享范围）统一表达「课程/资源能否共享给其它学校」。

---

## 二、逐条差距矩阵

| PRD 章节 | 要求 | 当前实现（改造前） | 差距 | 本次处理 |
|---|---|---|---|---|
| 3. 资源库创建与形态选择 | 选私有/共享；共享时可开关「允许跨校共建」 | 表单只有「私有/公开/院校共建」三选一，无「共享」与「共建」两级语义 | 形态语义不清 | ✅ 管理端改为 PRD 形态名（校级私有库/校级共享库/校际共建库）+ 形态说明；新增「对外开放自学」开关 |
| 4.1 校级私有库 | 前台可见；仅导入班级学员可学；不允许参建单位 | 前台可见 ✅；但**任何本校学生都能自助选课**；参建单位已限制 ✅ | ❌ 学习权限未拦截 | ✅ 选课闸门：私有库课程仅「已有选课记录（老师导入/分配）」的学员可学，否则提示「请联系课程老师加入班级」 |
| 4.2 校级共享库 | 对外开放学习（手机号+实名）；不允许参建单位；数据归入主建老师「其他学员」目录 | 对外开放学习**无任何校验**（任何学校学生都能跨租户选课）；**无其他学员目录**；手机号/实名**全缺** | ❌❌ 核心缺失 | ✅ 新增「其他学员」档案 + **账号级实名/手机认证** + 学习闸门 + 教师端目录/导出；跨校共享被禁止（第二轮：实名口径改为账号级、档案改为首次学习自动建档） |
| 4.3 校际共建库 | 参建单位由**平台管理员**挂载；主建老师授权外校老师参与；参建老师导入本校学生 | 参建单位由**主建学校的租户管理员**添加；外校老师通过 `ResourceLibraryTeacher` 分配 | ⚠️ 挂载主体不符；参建老师可导入学生 ✅ | ⚠️ 仍保持租户管理员挂载（未改），已列为后续项；共建库共享限制为「仅参建单位」 |
| 5.1/5.2 数据归属矩阵 | 主建/参建/其他学员三类归属；跨校物理隔离；导入学员必须归属班级 | `TeachingClass` + `TeachingClassMember` 已有班级模型；**无「其他学员」概念**；主建方可见参建方明细（无汇总/下钻控制） | ❌ 其他学员缺失；⚠️ 主建方可见参建方学员明细 | ✅ 新增 `ResourceLibraryLearner`（来源区分 自学/班级导入）；其他学员按库隔离、未被分配到该库的教师看不到（实测 qa-teacher 不可见、库内教师可见）；⚠️ 主建方「只见汇总数」仍待做 |
| 5.3 「其他学员」文件夹 | 系统级默认文件夹；首个其他学员学习时自动创建；空态不展示 | 无 | ❌ | ✅ `ResourceLibraryLearner` 在**首次选课时自动建档**（第二轮由「逐库登记」改为「自动建档」）；教师端列表空态显示「暂无记录」不占位 |
| 6. 账号有效期 | 单位/老师账号有效期；到期只读；参建单位到期退回仅主建方维护 | `AccountValidity` + `AccountValidityCheckHostedService` + 权限熔断已实现；参建单位到期联动未实现 | ⚠️ 部分满足 | 未改（已覆盖「老师账号只读」，参建单位联动待做） |
| 7. 院校独立主页 | 每校独立主页 + 富文本装修 + 独立域名绑定 | `School`（徽标/简介/Banner/联系方式/Domain/Slug）+ `SchoolDomainResolver` 已实现 | ✅ 基本满足 | 未改 |
| 8. 前台搜索 | 校名搜索直达院校主页 | 门户/学校档案已实现 | ✅ | 未改 |
| 9. 结业证书 | 申请 → 平台管理员审核 → 发放；模板平台统一配置；记录归入主建老师「其他学员」目录 | 仅有**微专业**证书（管理员直接发证，无申请/审核环节），无资源库证书 | ❌ 全新 | ✅ 新增 `Certificates` 模块（模板/申请/证书）+ 申请-审核-发放-核验-归档全链路 + 公开防伪核验页；**第二轮**：审核权下放到库所有者（租户管理员），跨租户越权发证被拒绝 |
| 10. 异常与边界 | 未登录访问私有库学习页 → 引导登录 → 提示联系老师；未实名点击学习 → 拦截并跳认证流程 | 无 | ❌ | ✅ 后端闸门返回明确文案；前台资源库详情页提供「实名认证后加入学习」入口 |

### 顺带修复的既有缺陷

| 缺陷 | 现象 | 修复 |
|---|---|---|
| `KnowledgeHub.Courses.Share` / `ManageShare` 未在权限定义中注册 | 课程共享接口 500：`The AuthorizationPolicy named 'KnowledgeHub.Courses.Share' was not found` | ✅ 在 `KnowledgeHubPermissionDefinitionProvider` 中补齐（含本地化文案） |
| 平台管理员看不到跨租户的证书申请 | host 上下文 ABP 租户过滤只返回 `TenantId IS NULL` 的行 | ✅ 审核相关查询显式关闭租户过滤器 |
| `ResourceLibrary.OpenToExternalLearners` 存量数据 | 新增字段默认 false | ✅ 迁移中按形态回填（私有库 false，其余 true） |

---

## 三、需求一：共享资源库「仅限校内」的约束落地

### 3.1 规则矩阵

| 库形态 | 共享给其它学校 | 共享给「社会学校」（对外自学通道） | 校外学员直接选课 | 校内学员自助选课 |
|---|---|---|---|---|
| 校级私有库 | ❌ 拒绝 | ❌ 拒绝（不对外开放自学） | ❌ 拒绝 | ❌ 仅老师导入/分配的学员 |
| 校级共享库 | ❌ 拒绝 | ✅ 允许（需实名认证） | ⚠️ 需先登记为其他学员 | ✅ 允许 |
| 校际共建库 | ✅ 仅限参建单位 | ✅ 允许（需实名认证） | ✅ 参建单位学员允许 | ✅ 允许 |

### 3.2 后端实现

- `src/KnowledgeHub.Domain.Shared/ResourceLibraries/ResourceLibrarySharingScope.cs`：新增共享范围枚举（`Unrestricted` / `SchoolOnly` / `CoBuildMembersOnly`）。
- `src/KnowledgeHub.Domain.Shared/ResourceLibraries/ResourceLibraryVisibilityExtensions.cs`：形态名、参建单位许可、对外开放自学默认值、共享范围推导。
- `src/KnowledgeHub.Application/ResourceLibraries/ResourceLibraryLearningPolicy.cs`（新）：**PRD 第 4 章的集中实现**
  - `EvaluateLearningAsync`：学习闸门（允许 / 需班级导入 / 需实名登记 / 拒绝），返回可直接展示给用户的原因文案；
  - `EnsureShareToTenantAllowedAsync`：跨校共享闸门；
  - `EnsurePublishAsSharedCourseAllowedAsync`：「共享课」发布闸门；
  - `GetSharingScopeAsync` / `GetSharingScopeReasonAsync`：供前端做 UI 约束。
- 接入点：
  - `CourseAppService.EnrollAsync` → 学习闸门；
  - `CourseAppService.PublishAsync` → 共享课闸门；
  - `CourseShareAppService.ShareAsync` / `ShareToPublicSchoolAsync` → 跨校共享闸门。

实测（`curl`）：

```
POST /api/app/course-share/share  (校级共享库课程 → 其它学校)
→ 「社会学校公开资源」是校级共享库，按需求库内课程仅限校内共享，不能共享给其他学校。
```

### 3.3 UI 约束

| 位置 | 约束 |
|---|---|
| 课程发布弹窗（`learning/course-list`） | 「共享课」标签在受限课程上**禁用**，并给出原因文案（`CourseDto.sharingScope` / `sharingScopeReason`） |
| 资源库管理（`admin/libraries`） | 形态下拉改为 PRD 形态名 + 形态说明；新增「对外开放自学」开关（私有库强制关闭）；列表展示形态与「对外开放自学」标签 |
| 资源库详情页（前台 `home/library`） | 对外开放的库展示「实名认证后加入学习」入口 + 说明；私有库不展示 |

---

## 四、需求二：结业证书功能设计

### 4.1 领域模型（`src/KnowledgeHub.Domain/Certificates/`）

| 实体 | 职责 | PRD 对应 |
|---|---|---|
| `CertificateTemplate` | **平台级**证书模板（宿主级实体，不带 `TenantId`）：底图 + 占位符图层 JSON + 默认/启用标记 | 第 9 节「证书模板：由平台侧统一定义，院校/老师不可自定义」 |
| `CertificateApplication` | 学员申请：库/课程/学员/实名快照/学习完成/考核通过/状态/审核人/意见/归档指针 | 第 9 节「申请」「审核发放」 |
| `Certificate` | 已发放证书：模板/申请/库/课程/学员/证书编号/防伪码/图片/结业时间/有效期/归档指针 | 第 9 节「证书」「记录归档」 |

配套：`ResourceLibraryLearner`（`src/KnowledgeHub.Domain/ResourceLibraries/`）承载「其他学员」档案：
库、用户、实名/手机号验证状态、主建老师、来源（自学 / 班级导入）、首次学习时间、已发证书数。

### 4.2 流程

```
学员（其他学员）                     平台管理员                        主建老师
  │ ① 手机号验证 + 实名认证登记 ─────────────────────────────────────────► 档案自动归入「其他学员」目录
  │ ② 完成课程学习（全部习题作答完毕）                                    
  │ ③ 通过考核（课程配置考试时须有通过的考试记录；未配置考试视为通过）       
  │ ④ POST /api/app/certificate/apply ──► 待审核申请 ──────────────────► 审核列表
  │                                        ⑤ prepare：生成证书编号 + 防伪码
  │                                        ⑥ 前端按模板图层合成证书图片（canvas）→ 上传 OSS
  │                                        ⑦ approve：写入证书 + 归档到主建老师「其他学员」目录
  │ ◄── ⑧ 我的证书列表（含图片 / 编号 / 防伪核验入口） ◄────────────────
  │                                                     ⑨ 「其他学员」目录查看 / 导出 Excel
```

### 4.3 接口

| 接口 | 权限 | 说明 |
|---|---|---|
| `GET /api/app/certificate/templates` | `Certificates.ManageTemplate` | 模板列表（平台统一） |
| `POST /api/app/certificate/template` / `PUT /api/app/certificate/{id}/template` / `DELETE` | `Certificates.ManageTemplate` | 模板维护 |
| `GET /api/app/certificate/applications` | `Certificates.Review` | 申请列表（跨院校） |
| `POST /api/app/certificate/prepare/{applicationId}` | `Certificates.Review` | 生成证书编号 + 防伪码（供合成图片） |
| `POST /api/app/certificate/approve` / `reject` | `Certificates.Review` | 审核发放 / 驳回 |
| `GET /api/app/certificate/apply-eligibility/{courseId}` | 登录用户 | 申请资格（含不可申请原因） |
| `POST /api/app/certificate/apply` | `Certificates.Apply` | 提交申请 |
| `GET /api/app/certificate/my-applications` / `my-certificates` | 登录用户 | 我的申请 / 我的证书 |
| `POST /api/app/certificate/verify?code=` | 匿名 | **防伪核验**（证书编号 / 防伪码） |
| `POST /api/app/resource-library-learner/register` | 登录用户 | 登记为「其他学员」（手机号 + 实名） |
| `GET /api/app/resource-library-learner/my-profile/{libraryId}` | 登录用户 | 我的其他学员档案 |
| `GET /api/app/resource-library-learner/learners` | `ResourceLibraries.ManageLearners` | 教师端「其他学员」目录 |
| `POST /api/app/resource-library-learner/export-learners` | `ResourceLibraries.ManageLearners` | 导出 Excel（ClosedXML） |

### 4.4 前端

| 页面 | 路由 | 说明 |
|---|---|---|
| 平台结业证书中心 | `/admin/certificates` | 模板管理（底图上传 + 占位符图层编辑器）+ 申请审核与发证（canvas 合成 + OSS 上传） |
| 学生端结业证书 | `/student/certificates` | 我的证书 / 我的申请 / 可申请课程 |
| 公开防伪核验 | `/verify/certificate` | 输入证书编号或防伪码核验真伪 |
| 教师端「其他学员」目录 | `/admin/other-learners` | 按库查看 + 导出 Excel |

可复用组件抽到 `angular/src/app/shared/certificate/`：
`certificate-layer-editor`（占位符图层编辑器）、`certificate-issue-composer`（发证合成）、`certificate-canvas.util`（画布绘制 / 导出）。

### 4.5 实测结果（本地开发库）

```
① register            → 张三 / 13800000001 / 1101************34，owner=wwq
② apply               → status=0（待审核），phoneMasked=138****0001
③ 平台审核列表         → total=1（跨院校可见）
④ approve             → certificateNo=KH20261026-TEST01，verifyCode=V8F3A2C1D9E7B605
⑤ 学生「我的证书」      → 返回该证书
⑥ 公开核验             → {"isValid":true,"message":"证书真实有效。"}
⑦ 教师「其他学员」目录  → 张三 / 13800000001 / 社会学校公开资源 / cert=1
```

---

## 五、第二轮：实名 / 手机认证、学习权限放开、库所有者审核（2026-10-27）

### 5.1 账号级实名认证 + 手机认证

| 项 | 实现 |
|---|---|
| 存储 | `IdentityUser` 扩展属性：`RealName` / `IdCardNumber` / `IsRealNameVerified` / `RealNameVerifiedAt`（迁移 `AddUserRealNameVerification`）；手机认证复用 ABP 内置 `PhoneNumberConfirmed` |
| 接口 | `POST /api/app/user/verify-real-name`（真实姓名 + 18 位身份证，含 GB 11643 校验位）、`POST /api/app/user/verify-phone`（与账号绑定手机号一致） |
| 个人中心 UI | `student/profile`：新增「手机认证」「实名认证」两项，显示 **已认证 / 未认证**、**已实名 / 未实名** 状态标签与身份证脱敏展示 |
| 口径 | 短信网关与权威实名核验**未接入**：手机号以「与账号绑定手机号一致」为准，身份证做格式校验后置为已实名（后续接入需收紧写入路径） |

### 5.2 学习权限（按 PRD 第 4 章 + 本轮口径统一）

| 学习者 | 校级私有库 | 校级共享库 | 校际共建库 |
|---|---|---|---|
| 本校学生 | 仅课程老师导入/分配的学员 | ✅ | ✅ |
| 社会学校 / 他校学生 | ❌ 一律不能学 | ✅ **实名认证后**可学 | ✅ **实名认证后**可学 |

关键改动：

1. **实名口径统一到账号级**：学习闸门、证书申请资格、证书归档全部用 `IsRealNameVerified`（此前用「每个库单独登记的学员档案」，导致同一用户换个库就要重新实名）。
2. **「其他学员」档案自动建档**（PRD 5.3）：校外学员首次选课时，系统自动在主建老师账号下建档（姓名取实名、手机号取账号），不再要求逐库登记。
3. **「对外开放学习」库集合统一**（`IResourceLibraryLearningPolicy.GetOpenToExternalLibraryIdsAsync`）：= 校级共享库 + 校际共建库，且 `Status=Active && IsPublic=true`。原先三处口径不一致（课程列表只算共享库、学习看板只算共享库、跨租户选课闸门只算共享库），导致**共建库课程对外校学生不可见**。
4. **共享资源库前台可见**：公开资源库目录（`/explore/library`、课程中心「公共课程」）现已同时包含共享库与共建库课程。

实测（社会学校用户 `bai`）：

```
选课（共享库课程，未实名） → 「社会学校公开资源」对外开放学习，请先在个人账户完成实名认证后再学习。
选课（私有库课程）        → 不能跨租户选课，该课程属于其他院校
选课（共建库课程，实名后）→ 成功，并自动建「其他学员」档案
公共课程列表              → 9 门（qidi 共享库 1 + qidi 共建库 6 + guozhou 共建库 2）
```

### 5.3 证书申请与审核下放到库所有者

| 项 | 实现 |
|---|---|
| 申请条件 | 习题进度 100%（该课程全部可学习习题都已作答 → `StudentCourse.Status=Completed`）+ 通过考核（课程配置考试时须有通过的考试记录）+ **已完成实名认证** |
| 申请入口 | ① 课程学习页头部「申请结业证书」按钮（可申请时显示 / 已申请时显示状态）；② 学生端「结业证书」页 |
| 审核主体 | **库所有者**（资源库归属租户的租户管理员）可审核本机构主建库的申请并发放证书；平台管理员可审核全部 |
| 隔离 | 租户管理员只看到本租户名下资源库的申请；越权发证被拒绝：「只能审核本机构主建的资源库所发起的证书申请。」 |
| 权限 | `Certificates.Review` 授予 `SchoolAdmin`（租户）+ `admin`（平台）；模板管理 `Certificates.ManageTemplate` 仅平台；模板**读取**放开给有证书权限的账号（库所有者发证时也要选模板） |
| 菜单 | 「结业证书中心」从「多校协同」（host 专属）移到「资源库管理」分组下 → 平台管理员与租户管理员都可见；租户管理员登录时**只显示「申请审核」页签**，模板页签隐藏 |

实测：

```
bai（社会学校）申请 中药药剂学（qidi 共享库） → status=0
qidi 租户管理员 审核列表 → 只看到本机构库的申请（2 条），看不到 guozhou 库的申请
qidi 租户管理员 approve  → 证书编号 KH20261027-BF4DBD，防伪码 7EF359FB39DE476F，归档=True
qidi 管理员越权 approve guozhou 库的申请 → 「只能审核本机构主建的资源库所发起的证书申请。」
公开核验 → 证书真实有效。
```

---

## 六、本地验证数据（供手动验证）

| 数据 | 说明 |
|---|---|
| 社会学校用户 | `bai` / `123456`（租户：社会学校），已完成实名认证（白冰 / 1101************17）与手机认证 |
| qidi（启迪学院111）共享库 | 「社会学校公开资源」（Visibility=1，OpenToExternalLearners=true）→ 关联课程「中药药剂学」（补 3 道习题，已挂章节） |
| qidi 共建库 | 「智能制造专业教学资源库」「数字创意专业教学资源库」「国州·多校共建测试库」 |
| guozhou（国州学院）共享库 | **新建**「国州学院共享专业资源库」（Visibility=1，对外开放自学）→ 关联「工业机器人基础（共建共享课）」（补 2 道习题 + 新建章节）、「共享课示例：面向社会学校开放」（1 道习题 + 章节） |
| guozhou 共建库 | 「工业机器人专业教学资源库」「国州·多校共建测试库」 |
| bai 的学习记录 | 5 门课程习题进度均已置 100%：中药药剂学 3/3、工业机器人基础 2/2、共享课示例 1/1、测试课程 8/8、计算机网络技术基础 12/12 |
| 私有库示例课程 | 「中医专业资源库·校级私有示例课程」（qidi 私有库）→ 用于验证校外学生不可选课 |
| 证书模板 | 「教学资源库结业证书」（默认模板，含学员姓名 / 课程名称 / 学校名称 / 结业时间 / 证书编号 5 个占位符图层） |
| 已发放证书 | KH20261027-BF4DBD（bai / 中药药剂学 / qidi 共享库） |

手动验证路径：

1. 社会学校登录 `bai` → 个人中心看「手机认证：已认证」「实名认证：已实名」。
2. 课程中心 → 公共课程 9 门 → 任一课程学习页 → 右上「申请结业证书」。
3. qidi 用租户管理员登录 → 资源库管理 → 结业证书中心 → 申请审核 → 审核并发证。
4. 公开核验页 `/verify/certificate?code=<证书编号>`。

---

## 七、尚未覆盖 / 已知限制（后续项）

| # | 项 | 说明 | 建议 |
|---|---|---|---|
| 1 | **手机号验证未接短信网关** | 当前口径为「手机号与账号绑定手机号一致」即视为已验证；实名认证为用户自填 + 脱敏存储，无权威核验 | 接入短信验证码 + 实名核验服务（公安/三要素）后再收紧 `IsPhoneVerified` / `IsRealNameVerified` 的写入路径 |
| 2 | **考核流程未落地** | `Exam` / `StudentExam` 仅有实体与表，**没有任何 AppService**；因此「通过考核」目前只能以「课程配置了考试且存在通过记录」判定，否则视为通过 | 补齐考试/判卷流程后，`IsAssessmentPassed` 才有真实数据源 |
| 3 | **参建单位挂载主体不符** | PRD 要求由**平台管理员**挂载，现为主建学校的租户管理员添加 | 增加平台管理员侧的参建单位挂载入口 |
| 4 | **主建方只见汇总数** | PRD 5.2 要求主建方默认只见汇总数、不下钻参建方学生明细；当前主建方可见参建方明细 | 在学习分析类接口按「库角色」降维 |
| 5 | **导入学员必须归属班级** | 私有库闸门目前按「有选课记录」判定，未强绑 `ClassId` | 批次导入时强制写入班级来源 |
| 6 | **参建单位到期联动** | 账号到期只读已实现；参建单位到期后共建库自动退回「仅主建方维护」未实现 | 在 `AccountValidityCheckHostedService` 中联动共建库状态 |
| 7 | **证书二维码** | 证书模板支持 `verifyCode` 图层，但二维码**图形**需在底图或前端生成 | 前端在合成时生成二维码贴图，或底图预留位置 + 图层贴二维码 |
| 8 | **证书模板图片直链** | 模板底图为公网 OSS 直链，未走站内图片代理 | 与其它证书模块保持一致，复用 `/api/image-proxy` |
| 9 | **实名认证仍为自填** | 账号级实名已上线，但身份证仅做格式校验，无权威核验；「手机认证」未接短信 | 接公安 / 三要素核验 + 短信验证码后，收紧 `VerifyRealNameAsync` / `VerifyPhoneAsync` 的通过条件 |
| 10 | **私有库「仅导入学员」判定较宽** | 私有库闸门按「已有选课记录」判定（老师导入 / 分配），未强绑班级来源 | 批次导入时强制写入 `StudentCourse.ClassId`，闸门改为「必须归属自建班级」 |
| 11 | **主建方只见汇总数** | 共建库中主建方仍可看到参建方的学员明细 | 在学习分析类接口按「库角色」降维 |

---

## 六、本次改动清单

**后端（新增）**
- `src/KnowledgeHub.Domain.Shared/ResourceLibraries/ResourceLibrarySharingScope.cs`
- `src/KnowledgeHub.Domain.Shared/ResourceLibraries/ResourceLibraryVisibilityExtensions.cs`
- `src/KnowledgeHub.Domain.Shared/ResourceLibraries/ResourceLibraryLearnerStatus.cs` / `ResourceLibraryLearnerSource.cs`
- `src/KnowledgeHub.Domain/Certificates/{CertificateTemplate,CertificateApplication,Certificate}.cs`
- `src/KnowledgeHub.Domain/ResourceLibraries/ResourceLibraryLearner.cs`
- `src/KnowledgeHub.Application/ResourceLibraries/ResourceLibraryLearningPolicy.cs`
- `src/KnowledgeHub.Application/ResourceLibraries/ResourceLibraryLearnerAppService.cs`
- `src/KnowledgeHub.Application/Certificates/CertificateAppService.cs`
- `src/KnowledgeHub.Application.Contracts/Certificates/**`、`.../ResourceLibraries/{IResourceLibraryLearnerAppService,ResourceLibraryLearnerDtos}.cs`
- `src/KnowledgeHub.EntityFrameworkCore/Certificates/CertificateDbModelCreatingExtensions.cs`
- 迁移 `AddResourceLibraryCertificatesAndLearners`

**后端（修改）**
- `ResourceLibrary`（新增 `OpenToExternalLearners`）、`ResourceLibraryAppService`（创建/更新支持该开关，按形态联动）
- `CourseAppService`（学习闸门、共享课闸门、DTO 输出共享范围）、`CourseShareAppService`（跨校共享闸门）
- `PortalAppService` / `IPortalAppService`（公开库视图输出形态与开放自学标记）
- `KnowledgeHubPermissions` + `KnowledgeHubPermissionDefinitionProvider`（新增证书与「其他学员」权限；补齐缺失的 `Courses.Share`）
- `RolePermissionSeeder`（admin / SchoolAdmin / Teacher / Student 的权限授予）
- `IResourceLibraryRepository` + EF 实现（`GetTeacherIdsAsync`）

**前端（新增）**
- `angular/src/app/shared/certificate/**`（画布工具 / 图层编辑器 / 发证合成器 / 模型）
- `angular/src/app/admin/certificates/certificate-management.component.*`
- `angular/src/app/admin/libraries/other-learners.component.*`
- `angular/src/app/student/certificates/student-certificates.component.*`
- `angular/src/app/certificates/certificate-verify.component.*`

**前端（修改）**
- `learning/course-list`（共享课标签约束）、`admin/libraries`（形态命名 + 对外开放自学开关）、`home/library`（实名认证入口）
- `app.routes.ts` / `route.provider.ts` / `student.routes.ts` / `student-navigation.ts`（路由与菜单）
- 代理：`angular/src/app/proxy/certificates/**`、`resource-libraries/resource-library-learner*.ts`、`resource-library-sharing-scope.enum.ts`（`abp generate-proxy -t ng` + `scripts/fix-generated-proxy.mjs`）
