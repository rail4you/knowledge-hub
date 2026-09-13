# Progress

## Status
In Progress

## Tasks
- [x] 全项目安全审计（两轮），详见 `issues/security-audit-2026.md`
- [x] 修复 P0：C-1 SSRF 代理、C-2 匿名 OSS 上传、C-3 匿名安装/弱许可证
- [x] 修复 P1：H-1/H-2/H-3/H-4/H-5/H-6/H-8~H-13
- [x] 修复部分 P2：M-1 PII 日志、M-2 Hangfire fail-closed、M-5 上传目录 XSS、M-6 租户统计鉴权、L-1 登出鉴权、L-2 Guid 校验
- [x] AI AppService 鉴权补齐（Chat/Learning/PracticumChat/MeiliSearchAdmin 类级；三个 AI 生成服务改 `[RemoteService(false)]` 隐藏 HTTP 暴露）
- [x] 前端路由 `permissionGuard`/`requiredPolicy` 补齐（H-7）
- [ ] 剩余：M-3 Swagger、M-4 密码策略、M-6b、`/admin/exercise` 无效策略
- [ ] 人工轮换历史泄露密钥（`.env`）

## Files Changed
- 安全文档：`issues/security-audit-2026.md`、`issues/README.md`
- 后端：
  - `src/KnowledgeHub.HttpApi/Controllers/HttpProxyController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/ImageProxyController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/ProxyHostGuard.cs`（新增）
  - `src/KnowledgeHub.HttpApi/Controllers/OssUploadController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/PracticumChatController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/AIController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/TenantListController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/ResourceFileController.cs`
  - `src/KnowledgeHub.HttpApi/Controllers/LogoutController.cs`
  - `src/KnowledgeHub.Application/Search/SummaryGenerationAppService.cs`
  - `src/KnowledgeHub.Application/Install/InstallAccessGuard.cs`（新增）
  - `src/KnowledgeHub.Application/Install/InstallAppService.cs`
  - `src/KnowledgeHub.Application/Edition/EditionAppService.cs`
  - `src/KnowledgeHub.Application/Courses/ChapterResourceAppService.cs`
  - `src/KnowledgeHub.Application/Search/SearchAppService.cs`
  - `src/KnowledgeHub.Application/Search/MeiliSearchAdminAppService.cs`
  - `src/KnowledgeHub.Application/AI/ChatAppService.cs`
  - `src/KnowledgeHub.Application/Learning/StudentExerciseRecordAppService.cs`
  - `src/KnowledgeHub.Application/Resources/FileStorage/LocalFileStorageService.cs`
  - `src/KnowledgeHub.Application.Contracts/Install/Dto/InstallDtos.cs`
  - `src/KnowledgeHub.Domain/Install/FixedLicenseValidator.cs`
  - `src/KnowledgeHub.HttpApi.Host/GrantAllPoliciesMiddleware.cs`
  - `src/KnowledgeHub.HttpApi.Host/KnowledgeHubHttpApiHostModule.cs`
  - `src/KnowledgeHub.HttpApi.Host/HangfireJobs/HangfireDashboardAuthorizationFilter.cs`
  - `src/KnowledgeHub.HttpApi.Host/appsettings.json`
- 前端：
  - `angular/src/app/install/install.component.ts`、`angular/src/app/install/models.ts`
  - `angular/src/app/practicum/practicum-chat.service.ts`
  - `angular/src/app/app.routes.ts`（路由策略补齐）
- 配置：`.gitignore`（修复规则失效）、`git rm --cached etc/docker/.env`

## Notes
- 后端 `dotnet build src/KnowledgeHub.HttpApi.Host` 通过（0 error）。
- 新增部署配置见 `issues/security-audit-2026.md` 文末「新增配置项」。
- `etc/docker/.env` 已从 git 索引移除但仍保留在本地磁盘；历史提交中的密钥必须轮换。
