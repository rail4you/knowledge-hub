# Document Indexing Job - Implementation Status

## Overview

Implementing a document indexing system using **liteparse** CLI for parsing PDF/DOCX/PPTX files, with **Meilisearch** for full-text search, and async background jobs with retry logic.

---

## Completed Tasks

### Backend - Domain Layer
- [x] `DocumentIndexingJob.cs` - Entity for tracking job status with retry fields
- [x] `PageContent.cs` - Stores parsed page content with text positions
- [x] `IndexingJobStatus` enum - Pending, Parsing, Indexing, Completed, Failed, Cancelled

### Backend - Database
- [x] DbContext updated with `DocumentIndexingJobs` and `PageContents` DbSets
- [x] EF Core migration created and applied
- [x] Tables verified in PostgreSQL: `KhDocumentIndexingJobs`, `KhPageContents`

### Backend - Application Layer
- [x] `LiteparseService.cs` - Wraps `lit parse --format json` CLI command
- [x] `DocumentIndexingBackgroundJob.cs` - Background job with exponential backoff retry (30s, 2m, 10m, max 3 retries)
- [x] `IndexingJobAppService.cs` - CRUD operations for indexing jobs
- [x] `ResourceAppService.CreateAsync` - Modified to trigger indexing on upload

### Backend - Contracts Layer
- [x] `IIndexingJobAppService` interface
- [x] `GetIndexingJobsInput` - Pagination and filter DTO
- [x] `CreateIndexingJobInput` - Create job DTO
- [x] `IndexingJobDto` - Response DTO with all job fields

### Frontend
- [x] `indexing-jobs.component.ts` - Admin UI component for job monitoring
- [x] Route added: `/admin/indexing-jobs`
- [x] `search.service.ts` - Updated with indexing job API methods
- [x] Menu item added: "索引任务" under "知识库"
- [x] Removed legacy menu items: "文档管理", "用户管理"

---

## ✅ API Issue Fixed (2026-03-22)

### Root Cause
ABP 命名约定不匹配。接口被命名为 `IIndexingJobService`（缺少 `App`），但 ABP 要求：
- 接口名必须以 `AppService` 结尾（如 `IXxxAppService`），或
- 实现类以 `AppService` 结尾

### Fix
将接口从 `IIndexingJobService` 改回 `IIndexingJobAppService`

### Verification
```bash
curl -sk https://localhost:44305/api/abp/api-definition | jq -r '.modules.app.controllers | keys[]' | grep -i indexing
# Output: KnowledgeHub.Application.Search.IndexingJobAppService ✅
```

---

## ✅ Background Job Issues Fixed (2026-03-22)

### Problem
索引任务创建成功，但后台任务执行失败：
- `AbpBackgroundJobs` 表中所有任务 `IsAbandoned = true`，`TryCount = 1`
- 任务状态始终为 `Pending`（status=0），进度为 0

### Root Causes & Fixes

| # | 问题 | 位置 | 修复 |
|---|------|------|------|
| 1 | `IFileStorageService` 接口缺少 `RootPath` 属性 | `IFileStorageService.cs:6` | ✅ 添加 `string RootPath { get; }` |
| 2 | `LiteparseService` 未实现 `ITransientDependency` | `LiteparseService.cs:20` | ✅ 添加 `ITransientDependency` |
| 3 | 异常被 catch 吞掉，ABP 无法重试 | `DocumentIndexingBackgroundJob.cs:75-79` | ✅ `catch` 后添加 `throw;` |
| 4 | 重复代码（fullPath 定义和检查重复） | `DocumentIndexingBackgroundJob.cs:95-106` | ✅ 删除重复代码 |
| 5 | 数据库 Status 值与 enum 不匹配 | `KhDocumentIndexingJobs` 表 | ✅ `UPDATE ... SET "Status" = 40` |

### Fix Details

#### 1. IFileStorageService.cs
```csharp
public interface IFileStorageService
{
    string RootPath { get; }  // 新增
    Task<string> SaveAsync(Stream stream, string fileName, string directory);
    // ...
}
```

#### 2. LiteparseService.cs
```csharp
public class LiteparseService : IDocumentExtractionService, ITransientDependency  // 新增 ITransientDependency
```

#### 3. DocumentIndexingBackgroundJob.cs
```csharp
public async Task ExecuteAsync(DocumentIndexingJobArgs args)
{
    // ...
    try
    {
        await ExecuteJobAsync(job, args);
    }
    catch (Exception ex)
    {
        await HandleFailureAsync(job, ex);
        throw;  // 新增：触发 ABP 重试机制
    }
}
```

### Verification Commands
```bash
# 检查后台任务状态
PGPASSWORD="postgres" psql -h localhost -p 5433 -U postgres -d KnowledgeHub -c \
  "SELECT \"Id\", \"TryCount\", \"IsAbandoned\" FROM \"AbpBackgroundJobs\" ORDER BY \"CreationTime\" DESC LIMIT 5;"

# 检查索引任务状态
PGPASSWORD="postgres" psql -h localhost -p 5433 -U postgres -d KnowledgeHub -c \
  "SELECT \"Id\", \"Status\", \"Progress\", \"ErrorMessage\" FROM \"KhDocumentIndexingJobs\";"

# 手动重试失败任务
curl -sk -X POST "https://localhost:44305/api/app/indexing-job/{jobId}/retry"
```

### Status Enum Values
```
Pending   = 0
Parsing   = 10
Indexing  = 20
Completed = 30
Failed    = 40
Cancelled = 50
```

---

## 🟡 Pending Verification

### Next Steps
1. **重启 API** - 确保代码修复生效
   ```bash
   ./dev.sh restart api
   ```

2. **通过前端上传文件测试** - 触发新的索引任务
   - 访问 http://localhost:4200
   - 上传 PDF/DOCX/PPTX 文件
   - 观察任务状态变化

3. **检查任务执行日志**
   ```bash
   ./dev.sh tail api
   # 查找关键字: DocumentIndexing, Parsing, Completed, Failed
   ```

4. **验证任务状态更新**
   - Pending → Parsing → Indexing → Completed
   - 失败时 Status = 40, ErrorMessage 有值

### Expected Behavior After Fix
- ✅ 后台任务不再 abandoned
- ✅ TryCount 正常增加（如果失败会重试）
- ✅ 任务状态正确更新（Pending → Parsing → Indexing → Completed）
- ✅ Progress 从 0 → 100
- ✅ 失败任务可以通过 `/retry` API 重试

---

## ⚠️ ABP Background Job 开发注意事项

### 1. 必须实现 `ITransientDependency`
```csharp
// ❌ 错误：不会被 DI 注册
public class MyBackgroundJob : IAsyncBackgroundJob<MyArgs>

// ✅ 正确：添加 ITransientDependency
public class MyBackgroundJob : IAsyncBackgroundJob<MyArgs>, ITransientDependency
```

### 2. 不要吞掉异常
```csharp
// ❌ 错误：ABP 认为任务成功，不会重试
public async Task ExecuteAsync(MyArgs args)
{
    try
    {
        await DoWorkAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed");
        // 没有 throw，ABP 认为任务成功
    }
}

// ✅ 正确：抛出异常触发 ABP 重试
public async Task ExecuteAsync(MyArgs args)
{
    try
    {
        await DoWorkAsync();
    }
    catch (Exception ex)
    {
        await HandleErrorAsync(ex);
        throw;  // 关键：重新抛出异常
    }
}
```

### 3. 依赖注入要完整
```csharp
// ❌ 错误：注入具体类
public MyBackgroundJob(LiteparseService liteparse)

// ✅ 正确：注入接口
public MyBackgroundJob(IDocumentExtractionService liteparse)
```

### 4. 接口属性要定义完整
```csharp
// ❌ 错误：接口没有 RootPath，但实现类有
public interface IFileStorageService
{
    Task<string> SaveAsync(...);
}
public class LocalFileStorageService : IFileStorageService
{
    public string RootPath => _rootPath;  // 接口没有这个属性
}

// ✅ 正确：接口包含所有需要的属性
public interface IFileStorageService
{
    string RootPath { get; }
    Task<string> SaveAsync(...);
}
```

### 5. 数据库 Status 值要匹配 Enum
```sql
-- 如果 enum 定义 Failed = 40，数据库中也要是 40
-- 检查: SELECT DISTINCT "Status" FROM "KhDocumentIndexingJobs";
-- 修复: UPDATE "KhDocumentIndexingJobs" SET "Status" = 40 WHERE "Status" = 4;
```

---

## File Locations

### The Problem Service
```
src/KnowledgeHub.Application/Search/IndexingJobAppService.cs
src/KnowledgeHub.Application/Search/DocumentIndexingBackgroundJob.cs
src/KnowledgeHub.Application/Search/LiteparseService.cs
src/KnowledgeHub.Application.Contracts/Search/IndexingJobAppService.cs
src/KnowledgeHub.Application.Contracts/Search/Dtos/IndexingJobDto.cs
```

### ABP Configuration
```
src/KnowledgeHub.HttpApi/KnowledgeHubHttpApiModule.cs
src/KnowledgeHub.Application/KnowledgeHubApplicationModule.cs
```

---

## Local Development

```bash
# Start services
./dev.sh start api
./dev.sh start angular
./dev.sh start meilisearch

# Check indexing jobs
curl -sk https://localhost:44305/api/app/indexing-job | jq .

# Check background jobs in database
PGPASSWORD="postgres" psql -h localhost -p 5433 -U postgres -d KnowledgeHub \
  -c "SELECT * FROM \"AbpBackgroundJobs\" ORDER BY \"CreationTime\" DESC LIMIT 5;"
```

---

## Known Working Components

- ✅ API endpoints generated correctly
- ✅ Indexing job records created in database
- ✅ Background job enqueued to `AbpBackgroundJobs` table
- ✅ Frontend UI displays job list
- ✅ Liteparse CLI works: `lit parse --format json <file>`
- ✅ **DI 激活问题已修复** (`LiteparseService` + `ITransientDependency`)
- ✅ **接口定义问题已修复** (`IFileStorageService.RootPath`)
- ✅ **异常处理已修复** (添加 `throw` 触发 ABP 重试)
- 🟡 **待验证**: 后台任务执行是否正常

---

## Quick Start Testing

```bash
# 1. 启动所有服务
./dev.sh start

# 2. 监控 API 日志
./dev.sh tail api

# 3. 上传测试文件（通过前端或 API）
# 访问 http://localhost:4200 上传 PDF/DOCX 文件

# 4. 检查任务状态
curl -sk https://localhost:44305/api/app/indexing-job | jq .

# 5. 检查后台任务
PGPASSWORD="postgres" psql -h localhost -p 5433 -U postgres -d KnowledgeHub \
  -c "SELECT \"Id\", \"TryCount\", \"IsAbandoned\" FROM \"AbpBackgroundJobs\" ORDER BY \"CreationTime\" DESC LIMIT 5;"
```
