# KnowledgeHub - Project Guidelines

## Project Overview

This is a layered startup solution based on Domain Driven Design (DDD) using [ABP Framework](https://abp.io). The project consists of:

- **Backend**: ASP.NET Core with ABP Framework (.NET)
- **Frontend**: Angular (TypeScript)
- **Architecture**: Layered monolith

---

## Development Rules

For detailed development rules, see: `angular/.claude/CLAUDE.md`

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
./dev.sh migrate            # Run database migration
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

---

## Production (Docker)

### Commands

```bash
cd etc/docker
./run-docker.sh            # Start all Docker services
docker compose down        # Stop all services
docker compose logs -f api # View API logs
docker compose logs -f angular # View Angular logs
```

### Production URLs

- **API**: https://localhost:44354
- **Swagger**: https://localhost:44354/swagger
- **Angular**: http://localhost:4200

---

## Key Commands

- **Install dependencies**: `abp install-libs` (in solution root)
- **Generate API proxy**: `abp generate-proxy -t ng` (in `angular/` folder) - ALWAYS use this to generate frontend API services, NEVER manually edit proxy files
- **Run tests**: `dotnet test` (in solution root)
