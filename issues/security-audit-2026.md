# 安全审计与修复记录（2026-09）

本文件记录 2026-09 对管理端及全项目做的两轮安全审计结论、修复动作与进度。
状态图例：`TODO` 待修 / `DOING` 修复中 / `DONE` 已修复 / `WONTFIX` 暂不处理（附原因）。

> 说明：第一轮由子代理扫描产出，第二轮由主流程逐文件复核。部分第一轮结论的严重度已修正，见「结论修正」。

---

## 一、结论修正（第一轮 → 第二轮）

| 编号 | 第一轮结论 | 修正 | 原因 |
|------|-----------|------|------|
| admin/majors 路由 | 严重：任意非学生可 CRUD | 降为 Medium | 后端 `MajorAppService.cs:67/86/101` 已加 `[Authorize(Majors.Create/Edit/Delete)]`，仅前端路由缺守卫 |
| H-7 GrantAllPoliciesMiddleware | High | 降为 Medium | 只重写 `/api/abp/application-configuration` 的 `grantedPolicies`，不参与服务端鉴权，属前端授权失真/纵深防御问题 |
| H-4 ResourceFile 预览 | 任意登录用户可预览未审核 | 部分成立 | 未审核资源仍要求登录（`ResourceFileController.cs:1034`），跨租户影响主要落在未审核资源 |
| H-5 SummaryGeneration | 匿名可利用 | 部分成立 | 类级有 `[Authorize(Search.ManageIndex)]`（`SummaryGenerationAppService.cs:30`），非匿名 |
| H-6 GetHotWords | 任意登录用户 | 上调为 High | 类无 `[Authorize]` 且无 `CheckPolicyAsync`，实为匿名可达 |

---

## 二、问题清单与修复状态

### P0 Critical

#### C-1 开放 SSRF 代理（匿名可打内网）
- 位置：`src/KnowledgeHub.HttpApi/Controllers/HttpProxyController.cs:17-19,52`
- 现状：`[AllowAnonymous]` + 目标 host/path 完全来自 URL，无白名单。
- 修复：加 `[Authorize]`；新增 `HttpProxy:AllowedHosts` 白名单；未配置白名单时拦截私有/回环/链路本地/保留地址与 `localhost/.internal/.local`。
- 状态：`DONE`（2026-09-13）

#### C-2 OSS 上传匿名可写
- 位置：`src/KnowledgeHub.HttpApi/Controllers/OssUploadController.cs:13,55-60`
- 现状：无 `[Authorize]`，`/api/oss-upload/file` 不限类型 50MB，ContentType/文件名客户端可控。
- 修复：类上加 `[Authorize(Resources.Create)]`；`UploadFile` 增加扩展名白名单；按扩展名强制服务端 ContentType。
- 状态：`DONE`（2026-09-13）

#### C-3 协议校验形同虚设 + 匿名安装 = 预置管理员被抢占
- 位置：`src/KnowledgeHub.Domain/Install/FixedLicenseValidator.cs:12-21`、`src/KnowledgeHub.Application/Install/InstallAppService.cs:19,53-83`
- 现状：`Validate` 只判断 `KH-STANDARD-` 前缀；`InstallAsync` 无鉴权，可用攻击者指定管理员密码创建 admin。
- 修复：`FixedLicenseValidator` 支持配置 `Install:ValidLicenseKeys` 精确匹配 / HMAC 签名；未配置时回退旧前缀但记录警告。新增 `InstallAccessGuard`：配置 `Install:Token` 时校验令牌，未配置时仅允许回环来源。`InstallAsync` 已接入。
- 状态：`DONE`（2026-09-13，需注意：生产安装需配置 `Install:Token` 或在服务器本机执行）

### P1 High

#### H-8 匿名升级版本/开启付费能力
- 位置：`src/KnowledgeHub.Application/Edition/EditionAppService.cs:18,56-77`
- 修复：方法加 `[Authorize]` + 宿主上下文校验（`CurrentTenant.Id.HasValue` 拒绝）+ `InstallAccessGuard` 令牌/回环校验。
- 状态：`DONE`（2026-09-13）

#### H-9 匿名可增删课程章节资源
- 位置：`src/KnowledgeHub.Application/Courses/ChapterResourceAppService.cs:14,62,96`
- 修复：`GetByChapterAsync` 加 `[Authorize(Courses.Default)]`，`CreateAsync/DeleteAsync` 加 `[Authorize(Courses.Edit)]`。
- 状态：`DONE`（2026-09-13）

#### H-10 搜索管理接口被整体匿名
- 位置：`src/KnowledgeHub.Application/Search/SearchAppService.cs:16`
- 修复：去掉类级 `[AllowAnonymous]`；检索/埋点/历史/热门词保留方法级 `[AllowAnonymous]`；索引写入与统计改为 `[Authorize(Search.Default)]` + `CheckPolicyAsync(Search.ManageIndex)`。
- 状态：`DONE`（2026-09-13）

#### H-11 仓库提交了真实密钥
- 位置：`etc/docker/.env`（git 已跟踪）
- 修复：`git rm --cached etc/docker/.env`；修正 `.gitignore` 中「行内注释导致规则失效」的问题（`.env`/`.env.*`/`uploads`/`redis_data`/`certs` 现均生效），放行 `.env.example`。
- 状态：`DONE`（2026-09-13）— ⚠️ 仍需人工轮换历史泄露的 Postgres/Meili/Qwen/OSS/JWT/加密口令

#### H-12 AI 会话线程越权（IDOR）
- 位置：`src/KnowledgeHub.Application/AI/ChatAppService.cs:126-136`
- 修复：线程已存在且 `thread.UserId != userId` 时抛 `AbpAuthorizationException`。
- 状态：`DONE`（2026-09-13）

#### H-13 匿名导出学生学习数据
- 位置：`src/KnowledgeHub.Application/Learning/StudentExerciseRecordAppService.cs:299,323,472,613,746,801`
- 修复：类加 `[Authorize]`；统计方法 `[Authorize(Learning.ViewStatistics)]`，导出方法 `[Authorize(Learning.ExportData)]`。
- 状态：`DONE`（2026-09-13）

### P2 / P3

| 编号 | 位置 | 问题 | 状态 |
|------|------|------|------|
| H-1 | `ImageProxyController.cs` | 匿名 SSRF + 任意 Content-Type | `DONE`：保持匿名（`<img>` 无法带头），新增公网 IP 校验 + host 白名单 + 仅 `image/*` + 32MB |
| H-2 | `LocalFileStorageService.cs` | 分片上传路径穿越 | `DONE`：`uploadId` 强制 Guid、文件名去路径、`EnsureUnderRoot` 越界断言 |
| H-3 | `PracticumChatController.cs` | 匿名 SSE 订阅聊天 | `DONE`：加 `[Authorize]` + 订阅前项目成员校验；EventSource 改由 `?access_token=` 传 JWT（仅 stream 生效） |
| H-4 | `ResourceFileController.cs` | 未审核资源跨租户预览 | `DONE`：未审核资源增加同租户校验（宿主不受限），已审核公开预览不变 |
| H-5 | `SummaryGenerationAppService.cs` | 摘要生成跨租户 | `DONE`：单条校验资源归属，批量强制 `TenantId == CurrentTenant.Id` |
| H-6 | `MeiliSearchAdminAppService.cs:326` | GetHotWords 漏鉴权 | `DONE`：加 `CheckPolicyAsync(Search.ManageIndex)` |
| M-1 | `KnowledgeHubHttpApiHostModule.cs` | 生产开启 PII/令牌日志 | `DONE`：`App:DisablePII` 默认改为 `true` |
| M-2 | `HangfireDashboardAuthorizationFilter.cs` | 空 IP 放行 | `DONE`：`remoteIp == null` 改为 fail-closed |
| M-3 | `KnowledgeHubHttpApiHostModule.cs:630` | 生产 Swagger 常开 | `TODO`（AGENTS 将生产 Swagger 列为预期能力，需产品确认后再按环境关闭） |
| M-4 | `KnowledgeHubHttpApiHostModule.cs:179-187` | 密码策略过弱 | `TODO`（收紧会与现有默认口令 `1q2w3E*` 冲突，需与种子/文档协同） |
| M-5 | `/uploads` 静态目录 | 上传可致同源 XSS | `DONE`：`OnPrepareResponse` 对 html/svg/js/xml/wasm 强制 `attachment` + nosniff + CSP sandbox |
| M-6 | `TenantListController.cs` | 匿名租户统计元数据 | `DONE`（部分）：`tenants-with-stats`/`tenant-stats` 加 `[Authorize]`；`/tenants` 仍公开（登录页/下拉需要） |
| M-6b | `TenantInfoAppService.cs:211` | 匿名跨租户知识图谱 | `WONTFIX`（公开租户主页 `/tenant/:id` 依赖 `getKnowledgeGraph`，加鉴权会破坏公开门户；如需收紧应改为按公开字段脱敏） |
| L-1 | `LogoutController.cs:16` | GET 登出 CSRF | `DONE`：加 `[Authorize]`（匿名 img 触发将 401） |
| L-2 | `AIController.cs` | `Guid.Parse` 未校验 500 | `DONE`：改 `Guid.TryParse` + 友好错误 |

### 新增配置项（部署需知）

| 配置 | 默认 | 说明 |
|------|------|------|
| `Install:Token` | 空 | 非空时安装/升级必须携带该令牌；为空时仅允许服务器本机（回环）调用 |
| `Install:ValidLicenseKeys` | 空 | 精确许可证白名单（逗号/分号分隔） |
| `Install:LicenseSigningKey` | 空 | 配置后启用 HMAC 签名许可证 `KH-STANDARD-{payload}.{sig}` |
| `HttpProxy:AllowedHosts` | 空 | 为空时仅允许公网目标；Unity 仿真若用内网/指定 host，需加入白名单 |
| `ImageProxy:AllowedHosts` | 空 | 同上 |
| `App:DisablePII` | `true` | 默认关闭 PII/安全令牌日志 |

---

## 三、修复进度日志

### 2026-09-13（第一轮修复）
- 建立本审计文档。
- C-1 HttpProxyController：鉴权 + host 白名单 + 私有/回环/保留地址拦截（新增共享 `ProxyHostGuard`）。
- C-2 OssUploadController：`[Authorize(Resources.Create)]` + 扩展名白名单 + 服务端按扩展名推断 ContentType（不再信任客户端）。
- C-3 安装/许可证：
  - `FixedLicenseValidator` 支持 `Install:ValidLicenseKeys` 精确匹配 / `Install:LicenseSigningKey` HMAC 签名，未配置时回退并警告。
  - 新增 `InstallAccessGuard`（令牌或回环），接入 `InstallAppService.InstallAsync`。
  - `InstallInputDto`/`EditionUpgradeInputDto` 增加 `InstallToken`；Install 前端表单增加令牌输入。
- H-8 `EditionAppService.UpgradeToStandardAsync`：`[Authorize]` + 宿主校验 + 安装访问守卫。
- H-9 `ChapterResourceAppService`：读/写方法分别加权限。
- H-10 `SearchAppService`：拆分类级 `[AllowAnonymous]`，管理方法加权限与策略校验。
- H-11 `.env`：`git rm --cached` + 修复 `.gitignore` 行内注释失效问题。
- H-12 `ChatAppService.ChatStreamingAsync`：校验线程归属。
- H-13 `StudentExerciseRecordAppService`：统计/导出方法鉴权。
- H-1 `ImageProxyController`：SSRF 公网校验 + 白名单 + `image/*` + 32MB。
- H-2 `LocalFileStorageService`：上传/合并路径穿越防护。
- H-3 `PracticumChatController`：`[Authorize]` + 成员校验；`GrantAllPoliciesMiddleware` 仅对 stream 支持 `?access_token=`；前端 `practicum-chat.service.ts` 附加令牌。
- H-6 `MeiliSearchAdminAppService.GetHotWordsAsync`：补策略校验。
- H-4 `ResourceFileController`：未审核资源增加同租户校验。
- H-5 `SummaryGenerationAppService`：单条/批量跨租户收紧。
- M-1 `App:DisablePII=true`；M-2 Hangfire fail-closed；M-5 `/uploads` 危险类型强制下载；M-6 租户统计接口鉴权；L-1 登出接口鉴权；L-2 `Guid.TryParse`。
- 后端 `dotnet build src/KnowledgeHub.HttpApi.Host` 通过（0 error）。
- 前端 `npx ng build --configuration development` 通过（仅 Sass @import 弃用警告，产出 `angular/dist/KnowledgeHub`）。

### 2026-09-13（前端路由策略补齐 H-7）
- `app.routes.ts` 对齐 `route.provider.ts` 菜单策略，补齐 `permissionGuard` + `requiredPolicy`：
  - 新增/收紧：`/resources` → `Resources`；`/ai/chat` → `AI`（并加 `nonStudentGuard`，学生走 `/student/ai/chat`）；`/ai/model-management` → `AI.LessonPlan`；`/admin/majors` → `Majors.Create`；`/admin/micro-majors` → `MicroMajors.Create`；`/admin/news` → `News.Create`；`/admin/practicum/projects` → `Practicum.Create`；`/admin/recruitment-live/:id` → `RecruitmentLive.Create`；`/document-viewer/:id` → `Resources`；`/learning` 父路由 → `Courses`。
  - 修正：`/admin/indexing-jobs` 由 `Resources` 改为 `Search.ManageIndex`（与菜单一致）。
- 说明：前端守卫仅提升纵深防御；本轮已核查相关后端 AppService 均有鉴权。
- `npx ng build --configuration development` 通过。
- 遗留：`/admin/exercise` 引用了未定义的 `KnowledgeHub.Exercises`（且无菜单入口），当前 fail-closed，待产品确认后统一。

### 2026-09-13（AI AppService 鉴权补齐）
- `ChatAppService`：类级 `[Authorize]`（仅登录即可，保持学生可用；不再匿名）。
- `LessonPlanAppService`/`CaseAnalysisAppService`/`CareerGuidanceAppService`：
  - 被 `AiGenerationJob`（Hangfire，无用户上下文）直接调用，不能加类级 `[Authorize]`；
  - 因此将其全部公开方法（含 `ExportDocx`/`ExportMultiChapterDocx`）标注 `[RemoteService(false)]`，使其不再作为约定控制器端点暴露；HTTP 访问统一走 `AIController` 的细粒度 `[Authorize(AI.*)]` 路由。
- `LearningAppService`、`PracticumChatAppService`：类级 `[Authorize]`（方法内部另有自校验）。
- `MeiliSearchAdminAppService`：类级 `[Authorize(Search.ManageIndex)]`（方法内保留 `CheckPolicyAsync` 双保险）。
- `InstallAppService`：`GetStatusAsync`/`InstallAsync` 显式 `[AllowAnonymous]`，访问控制由 `InstallAccessGuard` 负责。
- 复核命令已无「既无 Authorize 又无 AllowAnonymous」的 AppService（三个 AI 生成服务为有意例外，已加注释说明）。
- `dotnet build` 通过（0 error）。

### 待办（下一轮）
- M-3 生产 Swagger 按环境开关（需产品确认）。
- M-4 密码策略收紧（需与种子数据/文档协同）。
- M-6b `TenantInfoAppService.GetKnowledgeGraphAsync` 鉴权（公开租户主页可能依赖，需确认后处理）。
- 前端 `app.routes.ts` 补齐 `permissionGuard`/`requiredPolicy`（H-7 前端授权失真，后端多已鉴权，属纵深防御）。
- 人工轮换历史泄露密钥（Postgres/Meili/Qwen/OSS/JWT/加密口令），并清理 git 历史。
