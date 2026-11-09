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

| Environment | Script | Database | API | Angular | Meilisearch | Gotenberg |
|-------------|--------|----------|-----|---------|-------------|-----------|
| Development | `./dev.sh` | localhost:5433 (local PostgreSQL) | https://localhost:44305 | http://localhost:4200 | http://localhost:7700 | http://localhost:3000 (Docker) |
| Production | `./etc/docker/deploy-to-remote.sh` | postgres:5432 (Docker container) | https://localhost:44354 | http://localhost:4200 | - | Docker 内网 http://gotenberg:3000 |

---

## Development (dev.sh)

**IMPORTANT: Agent should NOT start services automatically.** User manages services via `dev.sh`.

### Commands

```bash
./dev.sh start              # Start API + Angular + Meilisearch + Gotenberg
./dev.sh start api          # Start only API
./dev.sh start angular      # Start only Angular
./dev.sh start meilisearch  # Start only Meilisearch
./dev.sh start gotenberg    # Start only Gotenberg (Docker, Office 预览转 PDF)
./dev.sh stop               # Stop all services
./dev.sh stop meilisearch   # Stop only Meilisearch
./dev.sh restart            # Restart all services
./dev.sh status             # Show service status
./dev.sh log api            # View API logs (last 100 lines)
./dev.sh log angular        # View Angular logs (last 100 lines)
./dev.sh log meilisearch    # View Meilisearch logs (last 100 lines)
./dev.sh log gotenberg      # View Gotenberg logs (docker logs)
./dev.sh tail api           # Tail API logs in real-time
./dev.sh tail angular       # Tail Angular logs in real-time
./dev.sh tail meilisearch   # Tail Meilisearch logs in real-time
./dev.sh tail gotenberg     # Tail Gotenberg logs (docker logs -f)
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
- **Gotenberg**: http://localhost:3000
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
   curl -sk 'http://服务器IP:44354/health-status'

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

## iketong 服务器（资源库，47.99.245.49）

### 概述

| 服务器 | 用途 | 域名 | 部署脚本 |
|--------|------|------|---------|
| 119.45.170.4 | 生产环境 | IP 直接访问 | `etc/docker/deploy-to-remote.sh` |
| **47.99.245.49** | **资源库（宝塔托管）** | **https://www.iketong.com** | **`etc/docker/deploy-iketong.sh`** |

**两个服务器完全独立，互不干扰**。iketong 服务器与现有 kg_edu（yikecheng.com）共存，端口隔离。

### iketong 服务器信息

- **公网 IP**: 47.99.245.49
- **域名**: https://www.iketong.com / https://iketong.com
- **SSH**: `ssh root@47.99.245.49`（免密已配置）
- **远程目录**: `~/knowledgehub-iketong/`
- **宝塔面板**: https://bt.yikecheng.com:16375
- **SSL**: Let's Encrypt（acme.sh 自动续签，3 个月）

### 端口隔离规划（避开 kg_edu 占用的 5432/4000）

| 服务 | 容器端口 | 宿主机端口 |
|------|---------|-----------|
| Angular | 80 | **4200**（宝塔反代到此） |
| PostgreSQL | 5432 | **5433** |
| Redis | 6379 | **6380** |
| Meilisearch | 7700 | **7701** |
| LiteParse | 5707 | **15001** |
| Gotenberg | 3000 | **3001** |

### 本地配置文件（iketong 专用）

| 文件 | 用途 |
|------|------|
| `etc/docker/.env.iketong` | iketong 专用环境变量（PUBLIC_URL=https://www.iketong.com） |
| `etc/docker/docker-compose-iketong.yml` | 端口已调整的 compose |
| `etc/docker/nginx-proxy-iketong.conf` | 容器内 Nginx，只听 80（TLS 由宝塔终结） |
| `etc/docker/deploy-iketong.sh` | 独立部署脚本（用法同 deploy-to-remote.sh） |
| `etc/docker/DEPLOY-IKETONG.md` | iketong 部署文档 |

### 常用命令

```bash
cd /Users/bai/projects/KnowledgeHub/etc/docker

# 完整部署
./deploy-iketong.sh all

# 仅更新前端/后端/迁移工具
./deploy-iketong.sh angular
./deploy-iketong.sh api
./deploy-iketong.sh migrator

# 同步配置
./deploy-iketong.sh sync

# 宝塔站点 + SSL
./deploy-iketong.sh bt-site     # 创建/更新宝塔站点 www.iketong.com
./deploy-iketong.sh bt-ssl      # 申请/续签 Let's Encrypt

# 数据迁移（从 119.45.170.4）
./deploy-iketong.sh migrate-data

# 状态检查
./deploy-iketong.sh status
```

### 远程管理（已部署后）

```bash
ssh root@47.99.245.49 'cd ~/knowledgehub-iketong && ./deploy.sh status'
ssh root@47.99.245.49 'cd ~/knowledgehub-iketong && ./deploy.sh logs knowledgehub-api'
ssh root@47.99.245.49 'cd ~/knowledgehub-iketong && ./deploy.sh restart knowledgehub-angular'
```

---

## 资讯下架 / 上架

**入口**：`/admin/news` → 「资讯内容」Tab → 操作列。

| 状态 | 后端枚举 | 操作按钮 | 说明 |
|------|---------|---------|------|
| 草稿 | `Draft` | 发布 | 首次发布 |
| 待审核 | `PendingReview` | 发布 | 审核通过后发布 |
| 已发布 | `Published` | **下架** | 已发布 → 已下架，前台隐藏 |
| 已下架 | `Archived` | **上架** | 已下架 → 已发布，回到列表顶部 |
| 已驳回 | `Rejected` | 发布 | 修正后重新发布 |

**后端端点**（`NewsArticleAppService`）：

| 方法 | 路由 | 权限 | 状态守卫 |
|------|------|------|---------|
| `ArchiveAsync` | `POST /api/app/news-article/{id}/archive` | `News.Publish` | 仅 `Published` 可下架 |
| `RepublishAsync` | `POST /api/app/news-article/{id}/republish` | `News.Publish` | 仅 `Archived` 可上架 |
| `PublishAsync` | `POST /api/app/news-article/{id}/publish` | `News.Publish` | 拒绝重复发布 / 已下架（引导用「上架」） |

**前台隐藏范围**（全部只取 `Status == Published`）：
`/news` 列表、`/student/news`、热门列表、门户首页「最新资讯」、资讯详情直链
（`GetAsync` 对无管理权限的用户抛「资讯不存在或已下架」）。

**重新上架会刷新发布时间**（`PublishedAt = Clock.Now`），资讯因此回到列表顶部、
且能被「本周/本月」筛到；历史上下架记录保留在 `NewsAudit` 审计表。

**管理端全量列表** `GET /api/app/news-article` 权限是 `News.Edit`（不是 `News.Default`），
否则学生会拿到草稿与已下架内容。

---

## 就业管理（工作台 + 闭环）

**入口**：`/admin/employment/workbench`（菜单「就业管理工作台」，就业分组第 1 项）。

- 五张闭环卡片：岗位发布 → 岗位投递 → 面试 → 就业去向 → 就业统计，每张可点击跳转到对应页面。
- 顶部「学生覆盖」：本校学生数 / 已投递 / 有去向 / 去向已核实 / 已落实就业 + 就业率。
- 三块待办：待审岗位、面试待办、待核实去向。
- 统计口径：**只统计当前租户内拥有 `Student` 角色且 `TenantId != null` 的学生**，
  不统计社会学校 / 外部学员。就业率 = 已核实且状态属「签约/就业/升学/创业」的学生数 ÷ 学生总数。
- 接口：`GET /api/app/employment/workbench`（`GetWorkbenchAsync`，权限 `Employment.ViewStatistics`）。
- 菜单顺序（`route.provider.ts`，parent `::Menu:TrainingManagement`）：
  工作台(1) → 岗位管理(2) → 岗位投递和面试管理(3) → 就业去向管理(4) → 就业统计(20，放最后)。
- ⚠️ 现存闭环问题与推荐方案见 `docs/就业管理闭环分析与建议.md`：
  面试通过不会自动生成去向；`GetStatisticsAsync` 误用投递数据；多个主要去向缺数据库约束（库中已有脏数据）。

## 企业工作台（只给企业用户）

**入口**：`/enterprise/workbench`（菜单「企业工作台」，企业用户登录后直接落到这里）。

⚠️ **权限不能用 `KnowledgeHub.Cooperation`**：那是产教合作业务权限，**老师 / 院校管理员也有**，
用它在菜单里放开会导致教师能看到并进入企业工作台（历史 bug：教师 wwq 能看到「企业工作台」）。

现已新增企业侧专用权限：

| 权限 | 用途 | 授权角色 |
|---|---|---|
| `KnowledgeHub.Enterprise` | 企业侧权限组 | EnterpriseUser、host admin |
| `KnowledgeHub.Enterprise.Workbench` | 企业工作台入口 | EnterpriseUser、host admin |

- 菜单与路由的 `requiredPolicy` 都改成 `KnowledgeHub.Enterprise.Workbench`；
- RolePermissionSeeder 里显式给 Teacher / SchoolAdmin / Student **收回**这两个权限（防历史脏授权）；
- 企业工作台的数据接口本来就按当前用户的企业主体过滤（`GetMyEnterpriseIdAsync`），非企业用户进去也看不到企业数据。

## 专业和课程管理（菜单按顶级分组组织）

侧边栏改成**三个顶级分组**，所有课程相关页面都保留在导航里（与原来风格一致）：

| 顶级分组 | 菜单项 |
|---|---|
| **专业和课程管理** | 课程工作台、课程列表、学生选课、专业管理、微专业管理、学习统计、课程直播 |
| **课程内容** | 章节管理、章节习题、课程资源、章节资源、教案管理、案例库 |
| **习题与试卷** | 习题管理、试卷管理 |

> 主题（LeptonX）只支持两级菜单，做不了三级子菜单，所以分组只能用顶级菜单来做。
> 分组依据：课程档案与运营 / 课程内容生产 / 题库与组卷。

**课程工作台**（`/learning/course-workbench`）是课程内容的总入口：
选一门课 → 8 个内容模块卡片（章节 / 习题 / 章节习题 / 课程资源 / 章节资源 / 教案 / 案例 / 试卷），
点击带 `?courseId=` 直达对应页面。

**「当前课程」跨页签保持**（`CourseContextService`，`angular/src/app/learning/course-context.service.ts`）：

- 课程工作台选中课程 → 写入全局上下文（同时持久化到 localStorage `kh_current_course_id`）；
- 进入任意课程内容页 → 没有 `?courseId=` 时沿用全局的当前课程；
- 在内容页里换了课程 → 全局同步跟着换（回到工作台也是同一门课）；
- 优先级：`?courseId=`（深链接）> 全局「当前课程」。

涉及页面：课程工作台 + 章节管理 / 习题管理 / 章节习题 / 课程资源 / 章节资源 / 教案管理 / 案例库 / 试卷管理。
持久化到 localStorage 是为了让**整页刷新 / 新标签页打开**时也能保持选中的课程。

课程详情页有「课程内容」按钮直达工作台。

> 图标注意：`nz-icon` 的动态 `[nzType]` 只有在 `app.config.ts` 的 `provideNzIcons(icons)` 里
> **注册过**才会渲染，否则是空白（本次补注册了 UnorderedListOutline / SignatureOutline / FileDoneOutline）。

**课程资源页**（`/learning/course-resource`）的操作列与资源中心一致：

| 按钮 | 行为 |
|---|---|
| 预览（eye） | 复用资源中心的 `FilePreviewComponent`（PDF / Office / 图片 / 音视频 / 文本）；扩展名缺失时从 `originalFileName` 兜底 |
| 下载（download） | `<a download>` + `AuthFetchService.withAccessToken`，原生下载（断点续传 / 可取消）；`isDownloadable=false` 或未过审且非创建者时不显示 |
| 详情（file-search） | 跳 `/resources?resourceId=xxx`，打开资源中心详情抽屉（编辑 / 审核 / 版本等完整管理操作） |
| 移除（disconnect） | 解除「课程 ↔ 资源」关联，不删资源文件；需要 `KnowledgeHub.Courses.Edit` |
| 删除（delete） | 仅创建者可删自己未过审的资源（原逻辑保留） |

⚠️ 后端 `CourseResourceAppService.DeleteAsync` 必须在 `DataFilter.Disable<IMultiTenant>()` 里执行：
列表 `GetByCourseAsync` 本身就是跨租户 join，宿主管理员能看到别校的关联；
保留租户过滤器时 `FindAsync` 找不到实体，ABP 的 `DeleteAsync(id)` 会**静默返回 204 但没删**，
前端却提示「已从课程中移除」。

## 智学分析（工作台 + 闭环顺序）

**入口**：`/learning/analysis/workbench`（菜单「智学分析工作台」，智学分析分组第 1 项）。

四个环节按**依赖顺序**串成一条链（工作台卡片可点击直达）：

| 顺序 | 环节 | 页面 | 为什么在这个位置 |
|---|---|---|---|
| ① | 知识点治理 | `/learning/knowledge-points` | OCR 匹配题目、掌握率与章节达成度都按知识点统计；灰区未确认会让口径不稳 → **前置** |
| ② | 课程联动设置 | `/learning/analysis/linkage` | 课程前后关系 + 前置课程复习包推送，是「加强前置课程」诊断建议的落地依据 → **结构配置** |
| ③ | 试卷采集与校对 | `/learning/analysis/paper` | 上传 → OCR → 人工校对 → 推送；未校对的低置信度题目不能进分析 → **数据采集** |
| ④ | 分析报告与推送 | `/learning/analysis/report` | 依赖已确认批次产出薄弱点 / 诊断 / 学生报告，再推送、导出、留档 → **结果消费** |

- 接口：`GET /api/app/learning-analytics/workbench`（`GetWorkbenchAsync`，权限沿用 `KnowledgeHub.Courses`）。
- 工作台给出：四张环节卡（进度 + 待办告警）、6 个关键指标、三块待办（待校对批次 / 灰区知识点 / 待推送批次）、
  以及「为什么是这个顺序」的依赖说明。
- ⚠️ 表单对齐坑：`.upload-form` 用 `align-items: flex-end` 时，字段高度不一致（如「试卷所属学生」多一行提示）
  会把其它输入框顶偏 —— 已改为顶对齐 + 提示行 `min-height` 占位。

## 章节多选组件（共享）+ 组卷限定章节多选

**共享组件**：`angular/src/app/shared/chapter-picker/chapter-picker.component.ts`

多选章节的「嵌套表格」实现（章节管理页同款：逐级缩进 + 父子联动），
现在 **AI 生成习题** 页与 **试卷管理「组卷」弹窗** 共用：

| 页面 | 用法 |
|---|---|
| `/ai/exercise-generate` | 直接内联 `<app-chapter-picker>`（选中章节用于 AI 出题范围） |
| `/learning/exam-management` 组卷弹窗 | 按钮打开「选择限定章节」弹窗（多选），选中章节用于抽题范围 |
| `/learning/lesson-plan` AI 生成教案 | 步骤① 内联 `[cascade]="false"`（只选父章节，子章节内容随父章节进提示词） |

组件约定：

- 选中集合由**父组件持有**：`[selectedIds]` 传入，`(selectedChange)` 回写；
  组件内部只管展开 / 搜索 / 勾选状态，不持久化选中项。
- `[chapters]` 传章节树（`ChapterDto[]`，含 children），`[selectedIds]` 传 id 数组；
- `[maxHeight]` 控制表格区高度（弹窗内可调小），`[showToolbar]` 控制搜索 + 展开层级工具条；
- **`[cascade]`（默认 true）**：勾父节点是否自动带上全部子孙。
  - `true`：勾父带子，勾子时父节点显示半选（AI 出题 / 组卷限定章节）；
  - `false`：只勾当前节点；父节点未勾选但有子孙被勾选时**仍显示半选**（仅提示，不联动）。
- 展开层级下拉：**收起全部 / 仅展开一级 / 展开到二级 / 展开到三级 / 展开全部**；
  章节树加载后默认「展开全部」（组件内部 `effect` 处理，父组件不用管展开状态）。

**组卷支持多章节**（后端）：

- `GenerateExamInput` 新增 `List<Guid>? ChapterIds`（`src/KnowledgeHub.Application.Contracts/Exams/Dtos/ExamDtos.cs`）；
  `ChapterId` 保留为「主章节」（试卷实体只有一个章节字段，用于列表按章节筛选）。
- `ExamAppService.LoadExercisePoolAsync` 支持章节集合：习题主章节命中**任一**章节，
  或章节多对多关联（`ChapterExercise`）命中**任一**章节，都入池；
  `ChapterIds` 为空时退化为整门课程。
- 前端 `exam.service.ts` 的 `GenerateExamInput` 同步加了 `chapterIds`。

## 课程作业（教师发布 / 批阅，学生提交 / 看反馈）

按 5 张原型实现：**教师端 · 作业列表 / 批阅** + **学生端 · 作业列表 / 提交作业 / 成绩与反馈**。

### 后端（新增 Assignments 模块）

| 层 | 位置 | 内容 |
|---|---|---|
| Domain | `src/KnowledgeHub.Domain/Assignments/` | `CourseAssignment`（作业）、`AssignmentCriterion`（评分细则）、`AssignmentFile`（要求附件 / 提交件 / 批注版）、`AssignmentSubmission`（提交）、`SubmissionScore`（得分明细） |
| Domain.Shared | `src/KnowledgeHub.Domain.Shared/Assignments/Enums/` | `AssignmentType`（个人/小组）、`AssignmentStatus`（草稿/已发布/已结束）、`SubmissionStatus`（草稿/已提交/已批阅）、`AssignmentFileType` |
| Contracts | `src/KnowledgeHub.Application.Contracts/Assignments/` | `IAssignmentAppService` + `Dtos/AssignmentDtos.cs` |
| Application | `src/KnowledgeHub.Application/Assignments/AssignmentAppService.cs` | 全部业务逻辑 |
| EF | `src/KnowledgeHub.EntityFrameworkCore/Assignments/` | `AssignmentDbModelCreatingExtensions`（5 张表）+ DbContext `DbSet<>`（**不加 DbSet 则 ABP 不注册 `IRepository<,>`，注入时报 500**） |

接口（`/api/app/assignment/...`，均已出现在 `api-definition`）：

| 方法 | 路由 | 说明 |
|---|---|---|
| GET | `assignment?courseId=&status=&filter=` | 教师列表（含提交进度 / 批阅进度 / 迟交人次） |
| GET | `assignment/statistics/{courseId}` | 统计卡片：进行中 / 待批阅 / 平均分 / 迟交 |
| GET/POST/PUT/DELETE | `assignment/{id}` | 详情 / 新建 / 编辑 / 删除 |
| POST | `assignment/{id}/publish`、`/{id}/close` | 发布、结束 |
| GET | `assignment/export-scores/{assignmentId}` | 导出成绩 CSV（带 BOM，Excel 不乱码） |
| GET | `assignment/submissions?assignmentId=` | 提交名单（含**未提交**学生，用于「缺交名单」） |
| GET | `assignment/submission/{submissionId}` | 提交详情（文件 + 得分明细） |
| POST | `assignment/grade/{submissionId}` | 批阅：得分明细 + 总分 + 评语 + 是否允许重交 + 迟交扣分 |
| POST | `assignment/upload-file/{assignmentId}?purpose=requirement\|submission\|annotated` | 上传文件（multipart，字段名 `file`） |
| GET | `assignment/file/{fileId}` | 下载 / 预览（返回 base64） |
| GET | `assignment/my-assignments/{courseId}` | 学生端列表 |
| GET | `assignment/my-assignment/{assignmentId}` | 学生端详情（要求 + 细则 + 我的提交） |
| POST | `assignment/submit` | 学生提交（`isDraft` 区分草稿 / 正式提交） |
| GET | `assignment/my-feedback/{assignmentId}` | 成绩与反馈（得分明细 / 评语 / 提交时间线） |

### 前端

| 端 | 路由 | 组件 |
|---|---|---|
| 教师 | `/learning/assignment-management` | `angular/src/app/learning/assignment-management/assignment-management.component.ts` |
| 教师 | `/learning/assignment-management/:id/grading` | 同目录 `assignment-grading.component.ts` |
| 学生 | `/student/assignments` | `angular/src/app/student/assignments/student-assignment-list.component.ts` |
| 学生 | `/student/assignments/:id/submit` | 同目录 `student-assignment-submit.component.ts` |
| 学生 | `/student/assignments/:id/feedback` | 同目录 `student-assignment-feedback.component.ts` |

- 客户端：手写 `assignment.service.ts`（`RestService`），**不走 proxy**（上传 / 二进制下载不适合生成的 proxy）。
- 菜单：教师端 `route.provider.ts` 挂在「习题与试卷」分组（`::Menu:AssignmentManagement`）；
  学生端 `student-navigation.ts` 新增 `assignments`（侧边栏「我的作业」）。

### 关键规则

- 权限 `KnowledgeHub.Assignments`（Default / Manage / Grade / Submit）：
  Teacher + SchoolAdmin + admin 持 Manage/Grade；Student 持 Default/Submit；
  **必须同时在 `KnowledgeHubPermissionDefinitionProvider` 里定义**，
  否则运行时抛「The AuthorizationPolicy named 'KnowledgeHub.Assignments.Manage' was not found」。
- 「提交进度」= 已提交人数 / 课程选课人数（`StudentCourse`，排除 Dropped）；「批阅进度」= 已批阅 / 已提交。
- 迟交：`提交时间 > 截止时间` 判定；是否扣分由教师在批阅时勾选，按作业配置的百分比自动扣。
- 学生必须**选修该课程**才能看到 / 提交作业（后端 `EnsureEnrolledAsync` 校验）。
- ⚠️ ABP 仓储默认 `autoSave=false`：同一次请求内「先插入 / 更新，再统计」会数不到数据 ——
  关联提交文件后必须先 `SaveNowAsync()`（`CurrentUnitOfWork.SaveChangesAsync()`）再统计。
- 文件走 `IFileStorageService`（`assignments/{assignmentId}` 目录），扩展名与大小按作业配置校验。

## 教案管理 · AI 多章节生成（勾选章节 → 预览确认 → 后台逐章生成）

**入口**：`/learning/lesson-plan` → 「AI 生成教案」（弹窗向导，3 步）。

| 步骤 | 内容 |
|---|---|
| ① 选择章节 | 复用共享组件 `app-chapter-picker`（**复选框多选**）；勾选**只影响当前章节**，不会自动带上子章节（`[cascade]="false"`） |
| ② 预览确认 | **只列出被勾选的章节**，逐章可编辑：**章节标题**、**内容要点**、**学时**、**参考资源**、**附加要求**；含子章节的章节**只标一个「含 N 个子章节」标签**（不展开子章节列表）。顶部有「统一设置」（学时 / 参考资源 / 附加要求）+ **应用到全部章节** |
| ③ 生成进度 | **后台任务**，逐章进度（进度条 + 「第 X/Y 章」+ 每章 done/active/pending 状态 + 已用时）；**全部章节完成后自动关闭进度面板回到教案列表** |

**进度显示口径**（避免最后一章永远停在“生成中”）：

- 前端维护「已完成章节集合」`aiDoneChapters`：解析后端进度文案
  - `正在生成第 X/Y 章：标题` → 当前章 = X；
  - `第 X/Y 章已完成：标题` → 该章标记完成，当前章推进到**下一个未完成**的章节（最后一个完成时当前章 = 0）。
- 任务状态变为 `Completed` 时，后端最终进度文案只保留「已完成」，所以前端在完成分支里
  **把全部章节一次性标记为已完成**（`markAllAiChaptersDone`），并显示「全部 N 章已完成」。
- 完成后：刷新教案列表 → toast 提示 → 1.5s 后自动关闭进度弹窗（`scheduleAiPanelClose`），
  页面回到教案列表。

- **章节标题可编辑**：预览步骤里每章的标题是一个输入框，改动后随章节一起进入生成提示词
  （「- 章节：{title}」）；
- **内容要点**：预填章节描述，章节没有描述时**用章节标题兜底**（避免多章节时后面的章节是空白），
  最终作为「章节要点」进入提示词；
- **学时 / 参考资源 / 附加要求以章节为单位**：逐章单独设置，顶部的「统一设置」可一键应用到全部章节；
  后端按 `章节级值 ?? 输入级默认值` 生效（`duration` / `resourceId` / `customPrompt`）。

**勾选父章节时，子章节内容会作为本章要点一起进入生成提示词**（不是逐个生成子章节）：

- 前端把选中章节的全部子孙（任意层级）按相对路径收集进 `subChapters`（title / path / summary）随任务提交
  （**预览界面只显示子章节个数，不展示子章节内容**）；
- 后端 `BuildCourseChapterUserPrompt` 会输出「本章包含的子章节（本章教案需覆盖以下内容）」段落，
  无参考资源时这些要点也会作为教学依据回退文本；
- 指令里已加约束：**子章节清单不得遗漏**。

> `ChapterPickerComponent` 的 `[cascade]` 入参：默认 `true`（AI 出题 / 组卷限定章节仍保持「勾父带子」），
> 教案生成传 `false`。非级联模式下，父节点未勾选但有子孙被勾选时仍显示**半选**提示（仅提示，不联动）。

- 弹窗可随时关闭 → 任务继续在后台跑；页面顶部显示「有进行中的 AI 教案生成任务」横幅，
  切页回来点「查看进度」即可恢复进度面板（同样支持 `?taskId=` 深链）。
- 与 `/ai/lesson-plan`（文档 → 多章节整体教案）是**两套**：
  本功能章节来自**课程章节树**，产出**每章一份独立教案**并直接落库到「教案管理」。

### 后端

| 位置 | 内容 |
|---|---|
| `src/KnowledgeHub.Domain/AI/AiGenerationTask.cs` | 新任务类型 `AiTaskType.LessonPlanCourseMulti = 11`（与 `LessonPlanMulti=10` 区分） |
| `src/KnowledgeHub.Application.Contracts/AI/Dtos/LessonPlanDtos.cs` | `CourseMultiChapterLessonPlanInputDto` / `CourseChapterLessonPlanItemDto`（含 `SubChapters`）/ `CourseSubChapterDto` |
| `src/KnowledgeHub.Application/AI/LessonPlanAppService.cs` | `GenerateCourseChapterPlanAsync(input, chapter, ct)`：按章节生成单章教案（复用 `CourseLessonPlanInstructions`），返回规范化 JSON |
| `src/KnowledgeHub.Application/AI/Tasks/AiGenerationJob.cs` | `GenerateCourseLessonPlanMultiAsync`：逐章生成 → 每章生成完立即落库；进度文案「正在生成第 X/Y 章：标题」 |
| `src/KnowledgeHub.Application/AI/AiGenerationTaskAppService.cs` | 权限映射 `AI.LessonPlan`、用量分组 `AiFeatureGroups.LessonPlan` |

- ⚠️ **一个任务会落多条教案**（每章一条）→ `AppCourseLessonPlans.AiTaskId` 索引已从「唯一」改为
  「非唯一 + (AiTaskId, ChapterId) 联合唯一」（迁移 `AllowMultiChapterCourseLessonPlans`）。
  重复消费同一任务同一章节时按 `AiTaskId + ChapterId` 去重。
- 前端提交：`POST /api/app/ai-generation-task`，`taskType=11`，
  `inputJson = { courseId, courseTitle, duration, customPrompt?, resourceId?,
  chapters:[{chapterId, order, title, summary?, duration?, resourceId?, customPrompt?,
  subChapters?:[{title, path, summary?}]}] }`。

### 前端

- `angular/src/app/ai/services/ai-task.service.ts`：`AiTaskType.LessonPlanCourseMulti = 11`、
  标签「课程多章节教案」、结果页路由 → `/learning/lesson-plan`。
- `angular/src/app/learning/lesson-plan/course-lesson-plan.component.*`：向导 UI 与逻辑。
- ⚠️ `nz-modal` 的 `nzTitle` 在打开时快照，**运行时改标题不会生效** → 标题用静态文案，
  当前步骤靠 `nz-steps` 表达。

## 案例库 · AI 多章节生成（与教案同一套交互）

**入口**：`/learning/course-case` → 「AI 生成案例」（弹窗向导，3 步，交互与教案管理完全一致）。

| 步骤 | 内容 |
|---|---|
| ① 选择章节 | 共享 `app-chapter-picker`，`[cascade]="false"`（只选当前章节，子章节内容进提示词） |
| ② 预览确认 | 只列被勾选章节，逐章可编辑：**章节标题 / 内容要点 / 主题提示 / 关注点 / 附加要求**；含子章节的章节只标「含 N 子章节」；顶部「统一设置」（主题提示 / 关注点 / 附加要求 / 参考资源）+ **应用到全部章节** |
| ③ 生成进度 | **后台任务**逐章生成（进度条 + 「第 X/Y 章」+ 每章 done/active/pending + 已用时）；**全部完成后自动关闭进度面板回到案例列表** |

- 任务类型：`AiTaskType.CaseAnalysisCourseMulti = 21`（与单章节 `CaseAnalysis = 20` 区分）。
- 后端：`AiGenerationJob.GenerateCourseCaseMultiAsync` 逐章调用
  `CaseAnalysisAppService.GenerateStreamingAsync`（新增可选 `CancellationToken`，课程模式复用
  `CourseCaseLibraryInstructions` + `BuildCourseCaseUserPrompt`），每章生成完立即落库一条 `CourseCase`。
- 输入 DTO：`CourseMultiCaseAnalysisInputDto` / `CourseCaseChapterItemDto`（含 `SubChapters`）；
  `CaseAnalysisGenerationInputDto` 也补了 `SubChapters`（子章节要点会作为「案例需覆盖以下内容」进提示词；
  章节无描述时用子章节要点兵底）。
- ⚠️ **一个任务会落多条案例** → `AppCourseCases.AiTaskId` 索引改为「非唯一 + (AiTaskId, ChapterId) 联合唯一」
  （迁移 `AllowMultiChapterCourseCases`），重复消费按 `AiTaskId + ChapterId` 去重。
- 前端：`angular/src/app/learning/case-library/course-case.component.*`；
  `ai-task.service.ts` 新增类型 21（标签「课程多章节案例」、结果路由 → `/learning/course-case`）。
- 支持 `?taskId=` 深链与「查看进度」横幅（切页回来继续跟进后台任务）。

## 资源库口径（教学资源库目录 / 共建资源库 / 学校门户）

三个公开页面都列资源库，但**口径不同，不要混用**：

| 页面 | 接口 | 口径 |
|---|---|---|
| `/explore/courses`（教学资源库目录） | `GET /api/app/teaching-library-catalog` | 校内 + 公共共享**全部**库，带 scope 筛选（`school` / `public` / `all`） |
| `/explore/library`（共建资源库） | `GET /api/app/portal/public-library-list` | **只给跨校共建库**：`Visibility == CoBuilt` + 至少一个参建学校 + `IsPublic` + `Active` |
| `/school/{id}`（学校门户） | `GET /api/app/school/profile/{tenantId}` | 本校**主建 + 参建**的跨校共建库（`Visibility == CoBuilt` + `IsPublic` + `Active`） |

**关键概念**（`ResourceLibrary`）：

- `Visibility`：`Private`(0 校级私有) / `Public`(1 校级共享) / `CoBuilt`(2 校际共建)；
- `IsPublic`：与 `Visibility` **正交** —— 管「门户上能不能被访客看到」。
  `IsPublic=false` 的库即使 `Visibility=CoBuilt` 也不上公开门户（学校门户同理）；
- 参建学校在 `ResourceLibraryMember`（`Role=Partner`），主建在 `OwnerTenantId`；
  判定「本校参与」要用 `IResourceLibraryRepository.GetByMemberTenantAsync(tenantId)`（含主建 + 参建，跨租户），
  **不能只查 `OwnerTenantId == tenantId`**（那样「他校主建、本校参建」的库在本校门户缺失）。

⚠️ 历史 bug：`public-library-list` 曾只过滤 `IsPublic && Active`，把校级私有 / 校级共享库
一起列进「共建资源库」页（生产表现为「页面显示所有教学资源库」）；学校门户又反过来只玩家主建库，
参建库看不到。两处已按上表口径修正。

---

## 教学资源库闭环 E2E 脚本

**位置**：`scripts/e2e/resource-library-loop/`（本地 / 远程服务器同一套脚本）

```bash
cd scripts/e2e/resource-library-loop
./preflight.sh     # 只读预检：API 可达 / 租户 / 四账号 / LibraryReview 开关
./run.sh           # 14 步闭环：建私有库 → 审核 → 分配老师 → 建课 → 传资源 → 章节 → 章节资源
                   # → 资源校审 → 课程资源转库资源 → 库准入审核 → 发课 → 导学生 → 学生学习
./verify-db.sh     # 数据库侧独立交叉验证（七段查询，不依赖上面的断言）
./cleanup.sh       # 清理本次产物（资源硬删 / 课程软删 / 资源库归档）
```

生产（iketong，47.99.245.49 / www.iketong.com）：
```bash
./run-iketong.sh              # 跑一次（保留数据供人工核对）
CLEANUP=1 ./run-iketong.sh    # 跑完顺便清理
```

远程：`API_BASE=http://<IP>:44354 ./remote.sh <host> <ssh-user>`（scp + ssh，输出在本地）。

默认账号：租户 `qidi`，`qidi-admin`(院校管理员) / `wwq`(老师) / `zmq`(学生) / `admin`(宿主审核人)。

**回归时必须知道的几条业务规则**：

| 规则 | 后果 |
|---|---|
| 审核内核「回避本人」 | 建库的审核人 ≠ 建库人；单管理员租户要用宿主 admin 审，否则 `CannotReviewOwn` |
| 校级私有库学习闸门 | 仅老师已导入（已有选课记录）的学员可学；学生自助选课返回「已经选修该课程」 |
| 库准入审核独立于资源校审 | 互转进库要再过一道 `ResourcePublication` 审核 |
| 学生只看得到已审核资源 | 资源必须先 `SchoolApproved` |
| 章节资源必须先关联课程 | 否则 `ChapterResourceAppService` 抛「该资源尚未关联到当前课程」 |
| 建资源带 `courseIds` = 关联课程 | 与 `POST /api/app/course-resource` 是同一张表，重复关联返回 403 |
| 资源库教师 DTO 的 `userName` 是「显示名」 | 生产 `wwq` 显示名是「王琬青」，按 `userName` 断言会失败 → 一律按 `userId` 匹配 |

详细清单与踩坑记录见 `scripts/e2e/resource-library-loop/README.md`。

---

## 评估管理（双高评估 / 教师填报）

**入口**：`/assessment/manage` → 菜单「评估管理」（老师自己创建评估项目、配置指标与权重、填报、导出资料包）。
旧页面「双高评估项目 / 详情 / 报表中心」路由仍保留，菜单入口已改为新页面。

四个页签（同一个组件 `angular/src/app/assessment/assessment-management.component.ts`）：
**评估项目**（列表 + 选模板新建 + 当前项目选择器）→ **项目配置**（指标、权重、评分规则、必备佐证、设置依据）→
**填报指标**（系统取数 / 填写数值 / 填写说明 / 上传佐证）→ **资料导出**（zip 资料包）。

页面顶部三张**流程卡片**（① 创建项目 / ② 配置指标 / ③ 填报并提交）可点击跳转到对应页签，
当前所在步骤高亮；「当前评估项目」跨页签保持（存在 `AssessmentContextService` 单例里，
因为切页签会重建组件，只放组件里会丢选择）。选择器**四个页签共用同一套 UI**，
固定在页签下方单独一行；评估模板只在「新建评估项目」弹窗里选。

后端沿用 `DoubleHigh` 模块（`src/KnowledgeHub.Application/DoubleHigh/`），新增能力：

| 能力 | 接口 |
|------|------|
| 模板目录 | `GET /api/app/double-high/templates` |
| 按模板建项目 | `POST /api/app/double-high/from-template` |
| 开始填报 | `POST /api/app/double-high/{id}/start-filling` |
| 提交填报 | `POST /api/app/double-high/{id}/submit` |
| 填报表内容 | `POST /api/app/double-high/save-reporting` |
| 上传佐证文件 | `POST /api/app/double-high/evidence-file`（multipart，含 projectId / indicatorId / evidenceName / file） |
| 导出资料包 | `POST /api/app/double-high/export-package/{id}`、`POST /api/app/double-high/export-all-package` |

**评估对象 = 自动取值的上下文范围**（`ObjectType` 资源库 / 专业 / 课程 / 手动填写）：

- 选资源库 / 专业 / 课程：自动取数按该对象范围内统计（库内课程资源、专业下的课程、课程自身的学生与资源）。
- 选「手动填写」：对象名自由填，老师为**每个系统取数指标**单独确认取值范围
  （指标上的 `ScopeType/ScopeId/ScopeName`，为空 = 随项目对象）。
- 范围解析在 `DoubleHighScopeResolver`；**范围查询必须关闭租户过滤器**（宿主管理员 + 共建库都要能取到数据）。
- 对象候选项：`GET /api/app/double-high/object-options?objectType=1|2|3`。

**关键规则**

- 指标新增 `IndicatorType`（系统取数 / 填写数值 / 填写说明 / 仅佐证），连同**权重**（0~1，合计 = 1）
  在「项目配置」里设好。配置页是**卡片布局**（每个指标一张卡片），不是宽表格。
- **当前阶段只采集信息**：界面已去掉「评分规则 / 完成度 / 加权总分」，`ScoringRule`、`Progress`、`Score`、
  `TotalScore` 字段保留兼容但不上界面；评分与总分留到后续评审环节再算。
- 评估模板包含一个 `custom`「无模板（自行添加指标）」：不预置指标，项目建出来指标列表为空。
- 指标的「设置依据」可以是手写文本，也可以**引用项目依据文件**（`BasisDocumentId`）。
  项目依据文件（政策文件等）可上传多个：`POST /api/app/double-high/project-document/{projectId}`，
  导出资料包会放进 `依据文件/` 目录。
- 没有配「必备佐证」的指标（新增指标 / 仅佐证指标）在填报页也有「其他佐证材料」上传区；
  项目列表有「佐证材料」列显示份数。
- 新实体必须加 `DbSet<>`，否则 ABP 不注册 `IRepository<,>`，注入时运行时报 500。
- 必备佐证清单 `RequiredEvidences`：每项都至少上传一个文件，该指标才算完成。
- 保存项目配置**不再清空已填报数据**：指标按编码原地更新（旧实现是全删重建，改权重会丢填报内容）。
- 导出 zip：项目说明 + 指标体系 / 填报内容 / 佐证材料清单 / 指标完成情况 CSV + 依据文件 + 佐证文件原件
  （按「指标 / 佐证名称」分目录）。
- 达标制指标允许超过目标值（「实际值 ≤ 目标值」只对比例制生效）。

详细说明见 `docs/评估管理模块说明.md`。

---

## 前端缓存与数据新鲜度

本项目有**三层**缓存，写操作后必须确保学生端能立即看到变化：

| 层 | 位置 | 失效方式 |
|----|------|---------|
| ① 前端内存缓存 | `ClientCacheService`（60s TTL，按 namespace 隔离） | `clearMany(ns)`；**写操作后由 `cacheInvalidationInterceptor` 自动失效** |
| ② 浏览器 HTTP 缓存 | 浏览器对 API GET 响应的缓存 | **后端已改为 `Cache-Control: no-store`**（见 `KnowledgeHubHttpApiHostModule`） |
| ③ 后端响应头 | 曾经是 `private, max-age=60` | 已移除，**不要改回来** |

**关键规则：**

- 公共列表接口（资讯 published-list / hot-list / 分类树、资源 filtered-list / categories、
  微专业 published、实训 published）**不得**再加 `max-age`：浏览器会缓存 60s，
  后台的写操作无法失效它，用户最长要等 60s 才看到变化。
- 新增写操作后**不需要**手动清缓存：`cacheInvalidationInterceptor`（注册在
  `app.config.ts`）会在 2xx 写请求后，按 URL 命中的业务域自动失效对应 namespace。
  业务域 → namespace 映射维护在 `shared/cache/cache-domains.ts`。
- 需要让**其它标签页/浏览器**也立即生效时，`ClientCacheService.clearMany()` 会通过
  localStorage 广播（`kh:cache-invalidate`）通知同浏览器其它标签页清缓存。
- 跨浏览器/跨设备仍受 60s TTL 限制（前端内存缓存是浏览器级的）；要彻底解决需要
  服务端缓存失效（如 Redis），目前未做。

**排查缓存问题**：先看响应头 `curl -skI <api>` 是否带 `max-age`；再看
`angular/src/app/shared/cache/cache-domains.ts` 是否覆盖了该业务域。

---

## 多租户 / 域名绑定（`__tenant` 残留 cookie 坑）

**症状**：同一个页面在**不同浏览器**显示的数据不一样 —— 例如学校门户
`/school/{A校}` 在 Firefox 里显示成**另一所学校**的课程（数量对不上），
Chrome / Safari 正常。

**根因**：ABP 会把 `__tenant` **cookie** 解析成租户上下文。登录页选机构时会写这个 cookie，
退出登录才清；浏览器保留 cookie 的策略不同（Firefox 常留着，Chrome/Safari 可能已被清）。
一旦带着残留 cookie 请求平台域名下的公开接口，`CurrentTenant.Id` 就是 cookie 里那所学校，
于是「按 URL 租户过滤」的公开门户被错误收窄。

**判定「域名绑定」必须看 Host，不能只看 `CurrentTenant.Id`**：

```csharp
// ✅ 正确：只有「当前 Host 是学校专属域名」才用租户上下文覆盖调用方传入的租户
if (CurrentTenant.Id.HasValue && await _schoolDomainContext.IsCurrentHostSchoolDomainAsync())
{
    tenantId = CurrentTenant.Id.Value;
}
```

- `ISchoolDomainContext`（`src/KnowledgeHub.Application/Schools/SchoolDomainContext.cs`）用
  「当前 Host 能不能解析出某所学校」判定，与域名解析器同一套规则。
- **不能用** `ITenantResolveResultAccessor.Result.AppliedResolvers`：ABP 会把每个**尝试过**的
  解析器名字都记进去，该判断在平台域名下恒为真。
- 已有正确实现可参照：`TenantListController.GetTenants`（登录页机构下拉）、
  `PortalAppService.GetScopeTenantsAsync`。

**排查手法**（对比「带 cookie / 不带 cookie」的响应）：

```bash
# 不带租户上下文（正常口径）
curl -sk "https://<域名>/api/app/portal/public-browse?tenantId=<URL里的租户>&skipCount=0&maxResultCount=24"
# 带残留 cookie（复现用户看到的错误口径）
curl -sk "https://<域名>/api/app/portal/public-browse?tenantId=<URL里的租户>&skipCount=0&maxResultCount=24" \
  --cookie "__tenant=<cookie里那所学校的租户Id>"
```

**给用户的临时绕过**：清掉 `__tenant` cookie（Firefox：设置 → 隐私 → Cookie → 删除
`__tenant`），或用无痕窗口打开。

---

## ABP Angular 高级用法

### 路由布局（layout）：深链页默认会掉到 empty 布局

ABP 的 `DynamicLayoutComponent` 取布局的顺序是：

1. Angular 路由 `data.layout`；
2. **ABP 路由树**（`route.provider.ts` 里 `RoutesService.add([...])` 的节点）里，
   按当前路径**向上找最近的带 `layout` 的祖先节点**；
3. 都没有才用 `app.component.ts` 的 `defaultLayout`（本项目 = `application`）。

⚠️ 坑：只有 `app.routes.ts` 里有、`route.provider.ts` 里**没有菜单节点**的深链页
（如 `/ai/exercise-generate`、`/ai/image-generation` 等 AI 生成页），
第2 步会一路向上落到 `path: '/'` 的门户首页节点（`layout: eLayoutType.empty`），
于是这些页被渲染成**脱离管理端外壳的独立页**：没有侧边栏、没有面包屑、没有返回入口。

**修法**：在 `route.provider.ts` 补一个 `invisible: true` 的节点（不进侧边栏菜单），
只为把布局钉成 application：

```typescript
{
  path: '/ai/exercise-generate',
  name: '::Menu:AIExerciseGenerateHidden',
  layout: eLayoutType.application,
  requiredPolicy: 'KnowledgeHub.AI.ExerciseGenerate',
  invisible: true,
},
```

注意 `invisible` 的节点仍会进 `RoutesService` 的路由树（只有 `visible$` 会过滤掉），
所以 `findRoute()` 找得到它。

**AI 生成页还必须有「返回」按钮**（它们是管理页的深链页，用户不是从菜单进来的）：

| 页面 | 返回到 |
|---|---|
| `/ai/exercise-generate` | `/learning/exercise-management`（习题管理） |
| `/ai/lesson-plan` | `/learning/lesson-plan`（教案管理） |
| `/ai/case-analysis` | `/learning/course-case`（案例库） |
| `/ai/image-generation`、`/ai/video-generation` | `/learning/course-resource`（课程资源） |

实现要点：方法名用 `backToManage()`（`lesson-plan` 里已有一个 `goBack()` 是多章节流程的「上一步」，不要重名）；
带上 `courseId` 查询参数，目标页会自动选中同一门课；
图片 / 短视频生成组件还有 `embedded` 模式（课程资源页弹窗内嵌），
返回按钮要放在 `@if (!embedded())` 的页头块里，嵌入时不渲染。

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
| 控制台报 `the icon xxx-o does not exist or is not registered`，且该按钮上的 popconfirm 点不动 | `nzType="down-circle" nzTheme="outline"` 需要 `DownCircleOutline`（名字带 `-o` 后缀），未在 `provideNzIcons` 注册时 ng-zorro 抛错并打断该子树的绑定 | 在 `app.config.ts` 的 `icons` 数组里补 `DownCircleOutline` / `UpCircleOutline` 等对应 Outline 变体 |

### ASP.NET Core / API 常见问题

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| POST 请求返回 400 错误，日志显示 "Antiforgery token validation failed" | 使用 JWT 认证的 API Controller 的 POST 方法被 antiforgery 中间件拦截，Angular `RestService` 不自动处理 antiforgery token | 在 Controller 或 Action 上添加 `[IgnoreAntiforgeryToken]` 属性 |

### React 学生端 API 调用模式

#### 1. POST AppService 公开访问（需要 AllowAnonymous + IgnoreAntiforgeryToken）

当 React 前端需要公开调用 ABP AppService 的 POST 方法时（无需登录），必须同时加两个属性：

```csharp
// src/KnowledgeHub.Application/Search/SearchAppService.cs
[IgnoreAntiforgeryToken]  // 防止浏览器端 POST 被 ABP antiforgery 中间件拦截返回 400
[AllowAnonymous]          // 允许未登录用户访问
public class SearchAppService : KnowledgeHubAppService, ISearchAppService
{
    public async Task<SearchResultDto> SearchAsync(SearchQueryDto input) { ... }
}
```

**为什么 curl 正常但浏览器 400？**
- curl 不发送 cookies，ABP antiforgery 检查跳过
- 浏览器可能携带 cookies（即使是跨端口），触发 antiforgery 验证
- Vite proxy 将请求从 `http://localhost:3000` 转发到 `https://localhost:44305`

**排查方法：**
```bash
# 用 curl 测试 POST API（模拟前端请求）
curl -sk "http://localhost:3000/api/app/search/search" -X POST \
  -H "Content-Type: application/json" \
  -d '{"query":"test","skipCount":0,"maxResultCount":5}'
```

#### 2. ABP AppService 路由规则

ABP 根据方法名自动生成路由，前端需要精准匹配：

| 方法签名 | 生成的路由 | HTTP 方法 |
|----------|-----------|-----------|
| `GetAsync(Guid id)` | `/api/app/{service}/{id}` | GET |
| `GetListAsync(...)` | `/api/app/{service}` | GET |
| `CreateAsync(...)` | `/api/app/{service}` | POST |
| `GetByCourseAsync(Guid courseId)` | `/api/app/{service}/by-course/{courseId}` | GET |
| `GetFilteredListAsync(...)` | `/api/app/{service}/filtered-list` | GET |
| `SearchAsync(SearchQueryDto)` | `/api/app/{service}/search` | POST |
| `GetHomeDataAsync(Guid tenantId)` | `/api/app/{service}/home-data/{tenantId}` | GET |

**关键规则：**
- 方法名去掉 `Async` 后缀，转为 kebab-case
- 简单类型参数（Guid, string）放在路径中：`/api/app/{service}/{action}/{id}`
- 复杂类型参数（DTO）作为 query string（GET）或 request body（POST）
- `ICrudAppService` 自动生成完整 CRUD 路由

**验证路由是否正确：**
```bash
curl -sk https://localhost:44305/api/abp/api-definition | python3 -c "
import json, sys
d = json.load(sys.stdin)
for k, v in d['modules']['app']['controllers'].items():
    if 'Search' in k:
        for name, action in v['actions'].items():
            print(f'{action[\"httpMethod\"]} {action[\"url\"]}')"
```

#### 3. React 前端 API 调用模式

```typescript
// student-react/src/lib/api.ts

// GET with path params
const { data } = await api.get<CourseDetail>(`/api/app/course/${courseId}/detail`);

// GET with query params
const { data } = await api.get<PagedResult<Resource>>('/api/app/resource', {
  params: { skipCount: 0, maxResultCount: 20 },
});

// POST with body
const { data } = await api.post('/api/app/search/search', {
  query: 'keyword',
  skipCount: 0,
  maxResultCount: 12,
});

// POST with path params (one-arg methods)
const { data } = await api.post(`/api/app/resource/collect/${resourceId}`);
```

#### 4. 登录后才能调用的 API

部分 API（收藏、习题提交等）需要认证。前端调用模式：

```typescript
const toggleCollection = (r: Resource) => {
  if (!auth.isAuthenticated) return;  // 未登录静默跳过
  api.post(`/api/app/resource/collect/${r.id}`)
    .then(() => { /* success */ })
    .catch(() => { /* silent fail */ });
};
```

请求拦截器自动附加 Bearer token（`student-react/src/lib/api.ts`）：
```typescript
api.interceptors.request.use(async (config) => {
  const token = await getAccessTokenFromStorage();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});
```

#### 5. 租户上下文传递

```typescript
import { setTenantId } from '../lib/api';

useEffect(() => {
  if (tenantId) setTenantId(tenantId);  // 设置 __tenant header
}, [tenantId]);
```

#### 6. 常见 400 错误排查清单

| 现象 | 可能原因 | 检查方法 |
|------|---------|----------|
| curl 返回 200，浏览器 400 | antiforgery 拦截 | 给 AppService 加 `[IgnoreAntiforgeryToken]` |
| 所有 API 返回 400 | `Content-Type` 缺失 | 确认 axios 请求带 `application/json` |
| 路径参数 API 返回 400/404 | 参数在路径还是 query？ | 用 api-definition 确认路由，GET 简单类型用路径，复杂类型用 query |
| POST 返回 400 "The input field is required" | JSON key 大小写不对 | ABP 接受 camelCase，检查 payload key 名 |
| 返回 302 重定向 | 需登录但未登录 | 给 AppService 或方法加 `[AllowAnonymous]` |
| 租户数据为空 | 未传 `__tenant` header | 调用 `setTenantId(tenantId)` |
