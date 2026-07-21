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

### WASM 镜像开发流程（仿真实训 materialType=4）

把外部 Unity WebGL 仿真实训资源镜像到 `etc/docker/wasm-mirrors/{slug}/`，通过 Nginx（生产）/ Kestrel（开发）静态托管，前端把 sourceUrl 改写到本地路径实现「秒开」。

**目录约定：**

```
etc/docker/wasm-mirrors/{slug}/
├── mirror.json         ← 元信息，必须存在且 status=ready 才生效
├── index.html
├── Build/
├── StreamingAssets/    ← 递归镜像
└── TemplateData/       ← 递归镜像
```

二进制不进 git；仅 `mirror.json` / `README.md` / `.gitkeep` / `.gitignore` 受 `.gitignore` 白名单保护。

**添加新仿真：**

```bash
# 1. 下载脚本会下载 index.html、loader.js、*.unityweb、StreamingAssets、TemplateData，
#    并写 mirror.json；staging → 原子 mv 到 {slug}/
#    可选参数：[title] [cover-url] [description] → 写入 mirror.json 供前端展示
bash scripts/fetch-wasm.sh http://外部源站/anatomyMice/ anatomy-mice "小鼠解剖" "https://cdn.example.com/c.png" "3D 交互式"

# 2. 重启 API 让 WasmMirrorAppService 扫描新的 mirror.json
./dev.sh restart api

# 3. 验证
curl -sk https://localhost:44305/api/app/wasm-mirror/all | jq '.[] | {slug, title, cover, description, status}'
curl -skI https://localhost:44305/wasm/anatomy-mice/index.html
```

**生产部署：**

```bash
cd etc/docker
bash ../../scripts/fetch-wasm.sh http://外部源站/anatomyMice/ anatomy-mice
docker compose restart knowledgehub-angular
```

`nginx-proxy.conf` 已经 `location ^~ /wasm/` 优先匹配，二进制通过 `alias /usr/share/nginx/html/wasm/` 静态托管；COOP/COEP 头已开启（Unity 多线程 build 需要 SharedArrayBuffer）。

**回退 / 禁用：**

- 临时回退某个仿真：编辑 `{slug}/mirror.json` 的 `status` 为 `"missing"`（60s 缓存后生效）
- 全局禁用：`appsettings.json` / Docker env 设 `WasmMirror__Enabled=false`；前端会回退到原 URL → `/api/proxy/http/...`

**镜像元信息 (`mirror.json`)：**

| 字段 | 必填 | 说明 |
|------|------|------|
| `slug` | ✅ | 目录名，全小写短横线 |
| `sourceUrl` | ✅ | 外部源站 URL |
| `entryPath` | ❌ | 入口 HTML，默认 `index.html` |
| `status` | ✅ | `ready` / `missing` / `syncing` / `invalid` |
| `mirroredAt` | ❌ | ISO 8601 时间戳 |
| `coopCoepRequired` | ❌ | 默认 `true` |
| `buildSha` / `files` / `totalBytes` / `fileCount` | ❌ | 由脚本自动写入 |
| **`title`** | ❌ | 展示用标题；缺失时由后端用 slug 美化（`anatomy-mice` → `Anatomy Mice`） |
| **`cover`** | ❌ | 封面图 URL；必须是 `http(s)`，不允许本地路径 |
| **`description`** | ❌ | 描述文本 |

> 三个 `**xxx**` 字段为可选，向后兼容；旧 manifest 自动 fallback 到 slug 美化标题。

**两端入口：**

| 入口 | 路径 | 角色 | 数据来源 |
|------|------|------|----------|
| 学生资源中心 | `/student/wasm-center` | 学生 | `GET /api/app/wasm-mirror/all`，**只展示 `status==='ready'`** |
| 学生仿真全屏 | `/student/wasm-center/:slug` | 学生 | 同上，命中后 iframe `/wasm/{slug}/index.html` |
| 教师镜像管理 | `/admin/wasm-mirrors` | 教师/管理员 | 同上，**展示所有状态**；提供「重新同步」按钮 → 复制 shell 命令到剪贴板 |

学生端 navbar 「实训」Tab 之后新增「仿真实训」入口（图标 `play-circle`）。
教师端侧边栏在「搜索和租户管理」之后新增独立分组「WASM 镜像管理」（图标 `cube`）。

**常见排查：**

| 现象 | 原因 | 修复 |
|------|------|------|
| iframe 加载 200 HTML 而非 .unityweb | Nginx `/wasm/` 缺失被 SPA 兜底 | 确认 `nginx-proxy.conf` 的 `location ^~ /wasm/` 存在 |
| 浏览器 DevTools: `crossOriginIsolated === false` | COOP/COEP 缺失 | `curl -skI /wasm/{slug}/index.html` 应见两个 `Cross-Origin-*` 头 |
| API 启动日志 `WasmMirror root not found` | 新机器没拉镜像 | 执行 `fetch-wasm.sh`，或忽略（仅影响仿真功能） |
| `Map<sourceUrl, publicUrl>` 大小为 0 | 后端 mapping 端点被网关拦截 | `curl -sk https://localhost/api/abp/api-definition \| grep wasm-mirror` |
| 学生 wasm-center 看不到某个镜像 | `mirror.json` 的 `status` 不是 `ready` | 改为 `"ready"` 后 60s 生效；或用教师管理页排查 |
| 教师页表格 60s 才看到新镜像 | 后端 60s 缓存 | 等 60s 或手动点「刷新」 |

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

---

## API 日志排查指南

### 日志位置

API 日志文件位于项目根目录下的 `.dev/logs/api.log`：
```bash
# 查看 API 日志
tail -f .dev/logs/api.log

# 或者使用 dev.sh
./dev.sh log api
./dev.sh tail api
```

### 排查 API 500 错误的步骤

**1. 首先确认 API 是否是最新代码**
代码修改后必须重启 API 才能生效：
```bash
./dev.sh restart api
```

**2. 等待 API 完全启动后再测试**
重启后等待 5 秒以上：
```bash
sleep 5 && curl -sk https://localhost:44305/health-status
```

**3. 触发请求后查看日志**
```bash
# 方法一：实时跟踪日志
./dev.sh tail api

# 方法二：先清空日志，触发请求，再查看
> .dev/logs/api.log  # 清空日志（需手动或用其他方式）
# 然后在浏览器触发请求
tail -100 .dev/logs/api.log
```

**4. 搜索错误关键词**
```bash
grep -i "ERR\|Exception" .dev/logs/api.log | tail -50
grep -i "search-statistics" .dev/logs/api.log | tail -20
```

**5. 查看完整的异常信息**
```bash
# 查看 PostgreSQL 错误（如 TenantId 模糊引用、UUID 类型错误等）
grep -B10 -A10 "MessageText:" .dev/logs/api.log | head -50
```

### 本次排查学到的经验

1. **重启时机问题**：API 重启后需要等待足够时间（5秒以上）才能完全就绪
2. **日志捕获时机**：需要在用户操作之前就开始捕获日志
3. **PostgreSQL UUID 类型问题**：`TenantId` 是 UUID 类型，不能和空字符串 `''` 比较
4. **LEFT JOIN + WHERE 问题**：在 `LEFT JOIN` 后，`WHERE` 子句中对右表的条件会导致左表数据被过滤
5. **动态 SQL 调试**：复杂 SQL 错误需要查看 PostgreSQL 的 `MessageText` 获取详细信息
