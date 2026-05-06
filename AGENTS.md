# KnowledgeHub - Project Guidelines

## Project Overview

This is a layered startup solution based on Domain Driven Design (DDD) using [ABP Framework](https://abp.io). The project consists of:

- **Backend**: ASP.NET Core with ABP Framework (.NET)
- **Frontend**: Angular (TypeScript)
- **Architecture**: Layered monolith

---

## Development Rules

For detailed development rules, see: `CLAUDE.md`

## Codex Workflow

This repository already contains `AGENTS.md` and `CLAUDE.md` at the root. There is currently no `angular/.claude/CLAUDE.md`, so do not rely on that path.

### Primary Goal

The main day-to-day task in this project is:

1. Start from a route URL
2. Find the Angular page/component that owns the route
3. Find the service calls used by that page
4. Map those calls to backend AppService or Controller code
5. Modify the smallest correct set of frontend, contracts, application, domain, and EF files

### Route To Angular Lookup

When a request mentions a route such as `/search`, `/resources`, `/learning/course-detail/123`, or `/identity/roles`, use this order:

1. Check `angular/src/app/app.routes.ts`
2. Check menu registration files:
   - `angular/src/app/route.provider.ts`
   - `angular/src/app/alliance-route.provider.ts`
3. Check ABP replaceable component registration in:
   - `angular/src/app/app.config.ts`
   - `angular/src/app/identity-roles.config.ts`
   - `angular/src/app/identity-users.config.ts`
4. If the route is under an ABP package module such as `/account`, `/identity`, `/tenant-management`, or `/setting-management`, assume the route shell comes from the ABP package first, then verify whether this repo replaces the concrete page component locally

Useful commands:

```bash
rg -n "path: 'search'|path: 'resources'|path: 'identity'|path: 'learning'" angular/src/app/app.routes.ts angular/src/app/route.provider.ts angular/src/app/alliance-route.provider.ts
rg -n "ReplaceableComponentsService|eIdentityComponents" angular/src/app
```

### Angular To Backend Lookup

After locating the component:

1. Open the component `.ts` file
2. Inspect injected services and imports
3. Prefer this mapping:
   - `angular/src/app/proxy/**` => generated from `src/KnowledgeHub.Application.Contracts/**`
   - matching implementation usually lives in `src/KnowledgeHub.Application/**`
4. If the component uses a hand-written Angular service with `RestService.request(...)`, search the URL string in `src/KnowledgeHub.Application/**`, `src/KnowledgeHub.HttpApi/**`, and `src/KnowledgeHub.AI.Api/**`
5. If the component uses ABP built-in modules, also inspect local replacement config and wrapper services before assuming the code is external

Useful commands:

```bash
rg -n "inject\\(|private readonly .*Service|constructor\\(" angular/src/app/<feature>
rg -n "/api/app/|/api/knowledge-hub/|RemoteService\\(|Route\\(" src
rg -n "interface I.*AppService|class .*AppService" src/KnowledgeHub.Application.Contracts src/KnowledgeHub.Application
```

### Backend Mapping Rules

For conventional ABP endpoints:

- `src/KnowledgeHub.Application.Contracts/**/I*AppService.cs` defines the contract
- `src/KnowledgeHub.Application/**/**/*AppService.cs` contains the implementation
- `src/KnowledgeHub.HttpApi/KnowledgeHubHttpApiModule.cs` registers conventional controllers from the Application assembly
- Angular generated proxies under `angular/src/app/proxy/**` are derived from these contracts

For manual endpoints:

- Look in `src/KnowledgeHub.HttpApi/Controllers/**`
- Match `apiName` in Angular with `[RemoteService(Name = "...")]` on the controller when present
- If Angular uses a hard-coded URL and there is no generated proxy, do not edit proxy files; edit the manual controller or the underlying service instead

### Route Lookup Cheat Sheet

- `/` => `angular/src/app/home/home.component.ts`
- `/resources` => `angular/src/app/resources/resource.ts`
  Backend starts from `angular/src/app/proxy/resources/resource.service.ts`
  Then check `src/KnowledgeHub.Application.Contracts/Resources/IResourceAppService.cs` and `src/KnowledgeHub.Application/Resources/ResourceAppService.cs`
- `/student/resources` => `angular/src/app/student/resources/student-resources.component.ts`
  Primarily uses `ResourceService`, `ResourceReviewService`, and recommendation services
- `/search` => `angular/src/app/search/search.component.ts`
  This page uses a hand-written Angular service `angular/src/app/search/search.service.ts`
  Search its request URLs in backend code instead of assuming a generated proxy
- `/admin/indexing-jobs` => `angular/src/app/admin/indexing-jobs/indexing-jobs.component.ts`
- `/admin/meilisearch` => `angular/src/app/admin/meilisearch/meilisearch-dashboard.component.ts`
- `/identity/users` => ABP identity route, but the page is replaced locally through `angular/src/app/identity-users.config.ts`
- `/identity/roles` => ABP identity route, but the page is replaced locally through `angular/src/app/identity-roles.config.ts` with `angular/src/app/admin/identity-roles/identity-roles.component.ts`
  Backend is not the default ABP role page here; inspect `src/KnowledgeHub.Application/Identity/TenantRoleAppService.cs` and `src/KnowledgeHub.HttpApi/Controllers/TenantPermissionController.cs`
- `/learning/**` => children are declared directly in `angular/src/app/app.routes.ts`

### Proxy Rules

- Never manually edit files under `angular/src/app/proxy/**`
- If an Application.Contracts interface or DTO changes, regenerate Angular proxies:

```bash
cd angular
abp generate-proxy -t ng
```

- Some older hand-written Angular services still exist next to generated proxies. If both exist, first verify which one the component actually imports before making changes

### Route Change Checklist

When changing behavior for a route, check all of these before finishing:

1. Route entry in `app.routes.ts`
2. Menu entry in `route.provider.ts` or `alliance-route.provider.ts`
3. Guards and `requiredPolicy`
4. Angular component, template, and local service imports
5. Generated proxy or manual `RestService` calls
6. Matching AppService contract and implementation, or manual HttpApi controller
7. DTO changes across Contracts, Application, and Angular proxy regeneration
8. Localization keys if text changed

### ABP-Specific Reminders

- App service interfaces must end with `AppService` and inherit `IApplicationService`
- App service implementations must be `public class`, end with `AppService`, and inherit `KnowledgeHubAppService`
- If a new API does not appear, verify naming, DI registration, and `ConventionalControllers.Create(typeof(KnowledgeHubApplicationModule).Assembly)`
- ABP built-in pages may be overridden through `ReplaceableComponentsService`; do not assume `/identity/*` is untouched framework code
- For JWT-authenticated custom POST endpoints that return antiforgery errors, add `[IgnoreAntiforgeryToken]` on the controller or action when appropriate

### Quick Reference

**ABP / .NET:**
- Follow ABP's standard folder structure: `*.Application`, `*.Domain`, `*.EntityFrameworkCore`, `*.HttpApi`
- Use C# 10+ features, LINQ, and lambda expressions
- Follow Microsoft C# Coding Conventions
- Use async/await for I/O operations
- Implement pagination with `PagedResultDto`

**ABP App Service 规范 — 自动注册 & 自动生成 API**

Interface (Contracts 层):
```csharp
// 文件名：IBookAppService.cs
// 命名空间：YourApp.Application.Contracts.{Module}
public interface IBookAppService : IApplicationService
{
    Task<BookDto> GetAsync(Guid id);
    Task CreateAsync(CreateBookDto input);
}
```
- 接口必须继承 `IApplicationService`
- 接口名必须以 `AppService` 结尾（`IXxxAppService`）
- 文件放在 `*.Application.Contracts` 项目

Implementation (Application 层):
```csharp
// 文件名：BookAppService.cs
// 命名空间：YourApp.Application.{Module}
public class BookAppService : KnowledgeHubAppService, IBookAppService
{
    public BookAppService(IRepository<Book, Guid> repository) { ... }
}
```
- 必须是 `public class`
- 类名必须以 `AppService` 结尾
- 继承项目基类 `KnowledgeHubAppService`，不要直接继承 `ApplicationService`

注册 Conventional Controllers (HttpApi 层):
```csharp
// KnowledgeHubHttpApiModule.cs
options.ConventionalControllers
    .Create(typeof(KnowledgeHubApplicationModule).Assembly); // Application 层的 Module
```

HTTP 动词约定:
| 方法名前缀 | HTTP 动词 |
|---|---|
| `Get` / `GetList` | GET |
| `Create` / `Insert` | POST |
| `Update` / `Put` | PUT |
| `Delete` / `Remove` | DELETE |

常见错误:
| 错误 | 后果 |
|---|---|
| 接口名用 `IXxxService`（缺少 `App`） | API 不生成 |
| 实现类不是 `public` | ABP 扫描不到 |
| DI 注册失败（Repository 未注册） | 不报错但 API 不出现 |
| `ConventionalControllers.Create()` 传错 Assembly | 整个模块 API 不生成 |

排查 API 不出现:
```bash
curl -sk https://localhost:44305/api/abp/api-definition | jq -r '.modules.app.controllers | keys[]'
```

强制指定（临时验证命名问题）:
```csharp
[RemoteService(Name = "IndexingJob")]
public class IndexingJobAppService : ...
```

**Angular:**
- Prefer standalone components (default in Angular 14+)
- Use signals for state management
- Use `input()` and `output()` functions instead of decorators
- Always set `changeDetection: ChangeDetectionStrategy.OnPush`
- Use native control flow (`@if`, `@for`, `@switch`)
- Use the `inject()` function instead of constructor injection

---

## Environments

| Environment | Script | Database | API | Angular | Meilisearch |
|-------------|--------|----------|-----|---------|-------------|
| Development | `./dev.sh` | localhost:5433 (local PostgreSQL) | https://localhost:44305 | http://localhost:4200 | http://localhost:7700 |
| Production | `./etc/docker/run-docker.sh` | postgres:5432 (Docker container) | https://localhost:44354 | http://localhost:4200 | - |

---

## Development (dev.sh)

**IMPORTANT: Agent should NOT start services automatically.** User manages services via `dev.sh`.

### Commands

```bash
./dev.sh start              # Start API + Angular + Meilisearch
./dev.sh start api          # Start only API
./dev.sh start angular      # Start only Angular
./dev.sh start meilisearch  # Start only Meilisearch
./dev.sh stop               # Stop all services
./dev.sh stop meilisearch   # Stop only Meilisearch
./dev.sh restart            # Restart all services
./dev.sh status             # Show service status
./dev.sh log api            # View API logs (last 100 lines)
./dev.sh log angular        # View Angular logs (last 100 lines)
./dev.sh log meilisearch    # View Meilisearch logs (last 100 lines)
./dev.sh tail api           # Tail API logs in real-time
./dev.sh tail angular       # Tail Angular logs in real-time
./dev.sh tail meilisearch   # Tail Meilisearch logs in real-time
./dev.sh migrate            # Run database migration (使用 DbMigrator)
```

### 数据库迁移 (Database Migration)

**使用 DbMigrator 进行迁移：**
```bash
./dev.sh migrate            # 使用 --no-build（快速，但新建迁移文件后不生效）
dotnet run --project src/KnowledgeHub.DbMigrator  # 完整编译后再迁移（确保新建迁移文件被应用）
```

**为什么 `./dev.sh migrate` 有时不能同步数据库？**
- `dev.sh migrate` 使用 `--no-build` 参数，只运行已编译的 DbMigrator
- 如果刚创建了新的迁移文件（如 `AddXXX`），但尚未编译，新迁移不会被应用
- 解决：直接运行 `dotnet run --project src/KnowledgeHub.DbMigrator` 完整编译后再迁移

**创建新迁移：**
```bash
dotnet ef migrations add <MigrationName> --project src/KnowledgeHub.EntityFrameworkCore --startup-project src/KnowledgeHub.HttpApi.Host
```

**验证迁移是否生效：**
```bash
# 检查迁移历史
PGPASSWORD=postgres psql -h localhost -p 5433 -U postgres -d KnowledgeHub -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 5;"

# 检查表结构
PGPASSWORD=postgres psql -h localhost -p 5433 -U postgres -d KnowledgeHub -c "\d \"表名\""
```

### Development URLs

- **API**: https://localhost:44305
- **Swagger**: https://localhost:44305/swagger
- **Angular**: http://localhost:4200
- **Meilisearch**: http://localhost:7700
- **Database**: localhost:5433 (PostgreSQL)

### Default Credentials

- **Username**: admin
- **Password**: 1q2w3E*

### 进程管理与代码修改

**API 使用 `--no-hot-reload` 运行**：
- 修改 C# 代码后，`dotnet watch` 会检测到变化并**完全重启** API 进程
- 这比热重载更稳定，因为 ABP 框架的复杂性使热重载效果不稳定
- 重启通常需要 3-5 秒

**何时需要重启 API**：
- 修改了任何 C# 代码（Controller、Service、Entity、Dto 等）
- 修改了后端配置文件（如 appsettings.json）
- 修改了 Application.Contracts 或 Domain 层代码
- 添加/修改了 API 路由或 DTO

**何时不需要重启（Angular 自动更新）**：
- 只修改了 Angular 前端代码（TypeScript、HTML、SCSS）
- 只修改了静态资源

**端口占用问题处理**：
如果 API 重启失败并报端口占用，执行：
```bash
./dev.sh restart api
```
或手动清理：
```bash
lsof -ti:44305 | xargs kill -9 2>/dev/null || true
```

---

## Production (Docker)

### 部署脚本 (deploy-to-remote.sh)

```bash
# 完整部署（构建 + 推送 + 启动）
./etc/docker/deploy-to-remote.sh --env production

# 仅构建镜像（不推送）
./etc/docker/deploy-to-remote.sh --env production --skip-push

# 仅推送镜像（不构建）
./etc/docker/deploy-to-remote.sh --env production --skip-build
```

### 生产环境部署流程

**重要：部署前检查项**

1. **确认远程服务器状态**
   ```bash
   sshpass -p '密码' ssh ubuntu@服务器IP 'docker ps'
   ```

2. **确认 .env.production 配置正确**
   ```bash
   cat etc/docker/.env.production | grep -E "APP_SELF_URL|APP_ANGULAR_URL"
   ```

3. **确认 dynamic-env.json 配置正确**（关键！）
   - Angular 的 `dynamic-env.json` 必须包含正确的 API 端口
   - API URL 格式：`http://服务器IP:44354`

**标准部署步骤**

1. **本地构建并推送镜像**
   ```bash
   ./etc/docker/deploy-to-remote.sh --env production
   ```

2. **远程服务器拉取并启动**
   ```bash
   # 拉取新镜像
   docker-compose -f docker-compose.registry.yml --env-file .env.production pull

   # 启动服务
   docker-compose -f docker-compose.registry.yml --env-file .env.production up -d
   ```

3. **验证服务状态**
   ```bash
   # 检查容器状态
   docker ps

   # 检查 API 健康状态
   curl -sk 'http://服务器IP:44354/api/app/install/status'

   # 检查 Angular 首页
   curl -sk 'http://服务器IP/'
   ```

### 常见部署问题

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| Angular 容器启动失败 "exec format error" | 镜像架构不匹配（arm64 vs amd64） | 本地使用 `docker build --platform linux/amd64` 构建 |
| 登录页一直 "busy" | `dynamic-env.json` 配置错误 | 检查 API URL 是否包含端口 44354 |
| API 返回 404 | 远程 `dynamic-env.json` 未更新 | 重新复制正确配置到远程并重启容器 |
| Docker Hub 拉取失败 | 网络问题 | 等待重试或使用阿里云镜像 |

### 远程服务器管理

```bash
# SSH 连接
sshpass -p '密码' ssh ubuntu@服务器IP

# 查看容器状态
docker ps --format "table {{.Names}}\t{{.Status}}"

# 查看日志
docker logs knowledgehub-api --tail 100
docker logs knowledgehub-angular --tail 100

# 重启容器
docker restart knowledgehub-api
docker restart knowledgehub-angular

# 完全重建
docker-compose -f docker-compose.registry.yml --env-file .env.production down
docker-compose -f docker-compose.registry.yml --env-file .env.production up -d
```

### Production URLs

- **API**: http://服务器IP:44354
- **Swagger**: http://服务器IP:44354/swagger
- **Angular**: http://服务器IP

---

## Key Commands

- **Install dependencies**: `abp install-libs` (in solution root)
- **Generate API proxy**: `abp generate-proxy -t ng` (in `angular/` folder) - ALWAYS use this to generate frontend API services, NEVER manually edit proxy files
- **Run tests**: `dotnet test` (in solution root)

---

## ABP Angular 高级用法

### 替换内置组件 (Replaceable Components)

ABP 允许替换框架内置组件（如角色管理、用户管理等），而无需修改路由配置。

**步骤：**

1. 创建配置文件（如 `identity-roles.config.ts`）：
```typescript
import { provideAppInitializer, inject } from '@angular/core';
import { ReplaceableComponentsService } from '@abp/ng.core';
import { eIdentityComponents } from '@abp/ng.identity';
import { MyCustomRolesComponent } from './my-custom-roles.component';

function initCustomComponent() {
  const replaceableComponents = inject(ReplaceableComponentsService);
  replaceableComponents.add({
    key: eIdentityComponents.Roles,  // 要替换的组件 key
    component: MyCustomRolesComponent,  // 自定义组件
  });
}

export const CUSTOM_COMPONENT_PROVIDER = [
  provideAppInitializer(() => {
    initCustomComponent();
  }),
];
```

2. 在 `app.config.ts` 中引入 provider：
```typescript
import { CUSTOM_COMPONENT_PROVIDER } from './identity-roles.config';

export const appConfig: ApplicationConfig = {
  providers: [
    // ... 其他 providers
    CUSTOM_COMPONENT_PROVIDER,
  ]
};
```

**可替换的 Identity 组件：**
- `eIdentityComponents.Roles` - 角色管理
- `eIdentityComponents.Users` - 用户管理

**可替换的 Theme 组件：**
- `eThemeLeptonXComponents.Footer` - 页脚
- `eThemeLeptonXComponents.Header` - 页头
- `eThemeLeptonXComponents.Sidebar` - 侧边栏

### Localization 本地化

**1. 后端添加翻译**

在 `src/{Project}.Domain.Shared/Localization/{Project}/` 目录下编辑 JSON 文件：

```json
// zh-Hans.json
{
  "culture": "zh-Hans",
  "texts": {
    "MyKey": "我的翻译",
    "RoleName:Admin": "管理员"
  }
}
```

**2. 前端使用翻译**

模板中使用 `abpLocalization` pipe：
```html
<!-- 基本用法 -->
<span>{{ '::MyKey' | abpLocalization }}</span>

<!-- 带参数 -->
<span>{{ '::HelloMessage' | abpLocalization:{0: userName} }}</span>
```

TypeScript 中使用 `LocalizationService`：
```typescript
import { LocalizationService } from '@abp/ng.core';

// 注入服务
private readonly localization = inject(LocalizationService);

// 获取翻译
const text = this.localization.instant('::MyKey');

// 动态 key
const roleName = 'Admin';
const translated = this.localization.instant(`::RoleName:${roleName}`);
```

**3. Localization Key 前缀规则**

| 前缀 | 说明 |
|------|------|
| `::Key` | 从所有资源中查找（推荐） |
| `Key` | 仅从默认资源查找 |
| `ResourceName::Key` | 从指定资源查找 |

**4. 查找顺序**

ABP 按以下顺序查找翻译：
1. 项目自定义资源（如 `KnowledgeHub`）
2. 模块资源（如 `AbpIdentity`）
3. 框架基础资源（如 `AbpValidation`）

**5. 查看可用的 Localization Keys**

```bash
# 查看所有资源
curl -sk "https://localhost:44305/api/abp/application-localization?cultureName=zh-Hans" | jq '.resources | keys'

# 查看特定资源的翻译
curl -sk "https://localhost:44305/api/abp/application-localization?cultureName=zh-Hans" | jq '.resources.AbpIdentity.texts'
curl -sk "https://localhost:44305/api/abp/application-localization?cultureName=zh-Hans" | jq '.resources.KnowledgeHub.texts'
```

### 常见问题排查

**问题：路由被 ABP 模块覆盖**

当自定义路由与 ABP 模块的 `loadChildren` 冲突时（如 `/identity/roles`），有两种解决方案：

1. 使用不同的路径（如 `/admin/identity-roles`）
2. 使用 `ReplaceableComponentsService` 替换组件（推荐）

**问题：翻译不显示**

1. 检查 key 是否正确（使用 `::` 前缀）
2. 确认 API 已重启加载新的翻译文件
3. 使用 curl 命令验证 API 返回的 localization 数据
4. 检查 key 是否在正确的资源文件中

**问题：ABP Permission Management 组件打不开，报 "Provider Key and Provider Name are required"**

这是 Angular 变更检测顺序问题。ABP 的 `abp-permission-management` 组件在 `visible` 变为 `true` 时**同步**调用 `openModal()` 检查 `providerKey`。当使用 `[(visible)]` 双向绑定时，`visible` 和 `providerKey` 在同一变更检测周期内更新，但 `visible` 的变化触发了同步检查，此时 `providerKey` 可能尚未更新。

**解决方案（模板）：**

```html
<!-- 使用 *ngIf 控制渲染，[visible] 单向绑定，permissionProviderKey 初始值为 '' -->
<abp-permission-management
  *ngIf="permissionProviderKey"
  [visible]="isPermissionModalOpen"
  providerName="R"
  [providerKey]="permissionProviderKey"
  [entityDisplayName]="permissionEntityDisplayName"
  (visibleChange)="isPermissionModalOpen = $event"
></abp-permission-management>
```

**解决方案（TypeScript）：**

```typescript
// 初始值为空字符串（不是 null，null 在 *ngIf 中是 truthy）
permissionProviderKey = '';

// 用 setTimeout 推迟 visible 的设置
openPermissions(role: IdentityRoleDto) {
  this.permissionProviderKey = role.name;
  setTimeout(() => {
    this.isPermissionModalOpen = true;
  });
}
```

**通用原则**：当组件的某个属性变化会触发依赖于其他属性的同步逻辑时，用 `setTimeout` 将该属性的变化推迟到下一个事件循环，确保其他属性已先期完成更新。

---

### 常见的 ABP UI 配置问题

本项目积累的 ABP UI 配置问题汇总：

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| Permission Management 报 "Provider Key and Provider Name are required" | Angular 变更检测顺序问题，`visible` 双向绑定触发同步检查时 `providerKey` 未就绪 | 模板用 `*ngIf` + `[visible]`，TS 用 `setTimeout` 延迟设置 `visible` |

### ASP.NET Core / API 常见问题

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| POST 请求返回 400 错误，日志显示 "Antiforgery token validation failed" | 使用 JWT 认证的 API Controller 的 POST 方法被 antiforgery 中间件拦截，Angular `RestService` 不自动处理 antiforgery token | 在 Controller 或 Action 上添加 `[IgnoreAntiforgeryToken]` 属性 |
