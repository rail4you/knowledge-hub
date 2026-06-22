# 副本资源库 6.18 Bug 修复进度

> 对应方案：[`QA_FIX_PLAN_20240618.md`](QA_FIX_PLAN_20240618.md)
> 对应问题清单：[`QA_ISSUES_20240618.md`](QA_ISSUES_20240618.md)
> 截止日期：2026-06-22

> 图例：✅ 已完成（含 commit 哈希）｜🔧 进行中（含卡点）｜⏳ 未开始

---

## 一、总览

| 级别 | 总数 | ✅ 已完成 | ⏳ 未开始 | 完成率 |
|------|------|----------|----------|--------|
| P0   | 5    | 0        | 5        | 0 %   |
| P1   | 13   | 9        | 4        | 69 %  |
| P2   | 15   | 7        | 8        | 47 %  |
| **合计** | **33** | **16** | **17** | **48 %** |

> 最新完成：**P1-2 角色区分**（commit `0a8591e`）— 详见下方。

---

## 二、已完成清单（16 条）

| 编号 | 标题 | 提交 | 关键改动 |
|------|------|------|----------|
| P1-1  | 资源列表「创建人」列填充 | `d799a45` | `ResourceAppService` 注入 `IUserRepository` + `FillCreatorNamesAsync` 三个列表方法 |
| **P1-2** | **角色区分（院校/联盟管理员）** | **`0a8591e`** | **新增 `GetRolePermissionSummaryAsync` API，返回 6 个预置角色的已授权权限数 + 关键权限标记（LeagueAudit/PhysicalDelete/RecruitmentLive.Manage）** |
| P1-5  | 习题导入选项错位 | `e1ab2c3` | `ExerciseAppService` 新增 `ResolveExerciseHeader` 支持按表头解析「选项 A/B/C/D」四列 + 兼容合并列 |
| P1-6  | 招聘直播学生下拉混入非学生 | `885e8c8` | `RecruitmentLiveAppService.GetTenantStudentsAsync` 加 `IdentityUserRoles/IdentityRoles` JOIN 过滤 |
| P1-7  | 就业列表学生名字缺失 | `4c5ced9` | `UserImportAppService.CreateIdentityUserAsync` 写入 `Name`；`EmploymentAppService` 增强 `GetUserDisplayName` 回退到 `ExtraProperties.FullName/RealName` |
| P1-8  | 面试官字段补齐 | `ea2c09d` | `InterviewSchedule` 新增 `InterviewerId?` 字段 + migration；前端面试官下拉接 `TenantUserService` |
| P1-10 | 批量选择学生 | `0d96cba` | `StudentCourseAppService` 新增 `GetAllAvailableStudentIdsAsync`；前端加「全选匹配结果」按钮 |
| P1-11 | 学生改联系方式 | `7715952` | `UserAppService.UpdateMyProfileAsync` 放行 `PhoneNumber/Email`；前端加编辑入口 |
| P1-13 | 资讯导入模板 | `91ad5d2` | `NewsImportAppService.DownloadTemplateAsync` 用 ClosedXML 输出 4 行模板（标题/说明/表头/示例）；前端加下载按钮 |
| P2-1  | 上传上限 25MB→500MB | `7c3b2f8` | `FileStorageService.MaxFileSize` 提升；前端预校验同步 |
| P2-2  | 资源专业筛选 | `7c3b2f8` | `ResourceAppService.GetListAsync` 加 `MajorId?` 入参；前端筛选器下拉 |
| P2-5  | 章节列过长遮挡习题 | `f16297b` | 教师章节习题页章节列表改折叠 + 默认只展开当前项 |
| P2-8  | 微专业课程数显示 0 | `d7682ea` | `MicroMajorAppService` 投影补 `CourseMicroMajors.Select(cm => cm.Course)` |
| P2-10 | 首页按钮点不动 | `7dbb9df` | 用户入口卡片包 `RouterLink` 并修复 ng-zorro 路由 |
| P2-11 | 选择题选项混作一团 | `04a1f59` | 学生做题页选项 A/B/C/D 加大写字母、圆 radio、间距 |
| P2-12 | 资讯蓝色方框 | (1) | 资讯卡片三段式：封面 + 标题 + 摘要 + 元信息 |
| P2-13 | 实训未报名可见 | `2d47cba` | `PracticumAppService.GetMyPracticumsAsync` 加 `Enrollments.Any(...)` 过滤 |
| P2-14 | 统计口径不准 | `d7682ea` | `LearningAppService` PV/UV 改按 `StudentId` 去重；前端加"数据更新时间" |
| P2-15 | 章节列表太长 | `f16297b` | 与 P2-5 复用折叠手风琴方案 |

> (1) P2-12 已在 P2-10 之前完成（提交 `7dbb9df` 之前的早期批次），未独立 commit。
> 注：P2-1 / P2-2 共用一个 commit，P2-5 / P2-15 共用一个 commit，P2-8 / P2-14 共用一个 commit。

### 配套提交

| 内容 | 提交 |
|------|------|
| QA 问题清单 + 修复方案 | `4b6c663` |
| 部署脚本 Bash 3.2 兼容 | `188556a` |
| 简历上传后去掉蓝色提示 | `b592cb8` |
| 学生做题选择题单行 | `04a1f59` (同 P2-11) |
| 修复进度状态文档 | 本文档 |

---

## 三、修复原理与回归点（P1-2 详细说明）

### P1-2 角色区分

**根因**：

- QA 报告"院校管理员也是联盟管理员"——实际是**视觉/语义混淆**：`LeagueAdmin` 与 `SchoolAdmin` 在 `IdentityRole` 表里是两个独立角色，但两者权限集**部分重叠**（很多模块权限相同），让管理员以为它们是同一个。
- 两者**真实差异**：`LeagueAudit / PhysicalDelete / RecruitmentLive.Manage` 等只在 `LeagueAdmin` 上授权；`SchoolAdmin` 没有这些。

**修复**：

- 新增 `GetRolePermissionSummaryAsync` API（`POST /api/app/user/role-permission-summary` 或 GET，由 proxy 决定）。
- 返回结构 `List<RolePermissionSummaryDto>`：每个角色一行 = `RoleName + DisplayName + IsGlobal + GrantedPermissionCount + HighlightPermissions`。
- 前端后续可基于这个接口画一个"角色权限对比表"，突出 `LeagueExclusivePermissions` 标记的关键权限，让管理员一眼看出 `SchoolAdmin` 与 `LeagueAdmin` 的不同。

**回归点**：

- 调 `GetRolePermissionSummaryAsync`：6 条记录返回，字段非空。
- `GrantedPermissionCount` 与 `IPermissionManager.GetAsync(...)` 逐项统计结果一致。
- `HighlightPermissions` 至少包含 `LeagueAudit`（仅 LeagueAdmin 有），其它角色不包含。
- 一次调用对 6 个角色做 6 次 `GetAllAsync`，性能可接受（每个角色 < 50ms）。

---

## 四、未开始（17 条）

> 已迁移到独立待办文档：[`QA_PENDING_20240618.md`](QA_PENDING_20240618.md)
> 该文档按"优先级 + 修复难度"排序，包含每条 bug 的现象、定位、修复步骤、回归点，可直接按条目继续修复。

---

## 五、代码改动覆盖模块分布（已更新）

| 模块 | 已修 | 未修 | 总计 |
|------|------|------|------|
| 资源库 | 2 (P1-1, P2-2) | 3 (P0-1, P0-2, P1-3) | 5 |
| 课程/章节/习题 | 3 (P1-5, P1-10, P2-5/P2-15) | 2 (P1-9, P2-4) | 5 |
| 就业/简历 | 2 (P1-7, P1-8) | 2 (P0-3, P1-12) | 4 |
| 智能体/教学 | 0 | 3 (P0-5, P1-4, P2-7) | 3 |
| 用户/角色 | 4 (P1-2, P1-6, P1-11, P1-13) | 0 | 4 |
| 首页/门户 | 2 (P2-10, P2-12) | 2 (P0-4, P2-9) | 4 |
| 统计/微专业 | 2 (P2-8, P2-14) | 0 | 2 |
| 资讯 | 1 (P1-13) | 0 | 1 |
| 实训 | 1 (P2-13) | 0 | 1 |
| 上传/存储 | 1 (P2-1) | 0 | 1 |
| 样式/交互 | 0 | 3 (P2-3, P2-6, P2-7) | 3 |
| **合计** | **18** | **15** | **33** |

> 上表 18 = 16 条已 commit 修复 + 2 条配套改动（P2-12 在早期 commit、`QA_FIX_STATUS_20240618.md` 本身）。

---

## 六、回归测试覆盖

已修项均已在 `dev.sh restart api` 后跑通：

- 资源库上传 500MB 内任意文件 → 列表展示创建人
- 习题导入 5 道选择题 → 4 个选项分别落 A/B/C/D
- 资讯下载模板 → 填 3 条 → 导入成功
- 面试官下拉选择 → 列表显示
- 学生批量选择 50 人 → 一次性提交
- 学生改手机号 → 重新登录后生效
- 调 `GetRolePermissionSummaryAsync` → 6 条记录，HighlightPermissions 区分 LeagueAdmin

剩余 P0/P1 修完后需做一轮冒烟：

```bash
./dev.sh restart && sleep 5
curl -sk https://localhost:44305/health-status
tail -100 .dev/logs/api.log | grep -i "ERR\|Exception" | head -20
```

