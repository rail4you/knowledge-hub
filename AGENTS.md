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
