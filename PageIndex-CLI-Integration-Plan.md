# PageIndex CLI 集成方案

## 概述

将 `src/pageindex-cli/` Python CLI 工具集成到 KnowledgeHub 平台的两个环节：
1. 文档上传后自动生成 PageIndex JSON（后台作业）
2. AI Agent 通过 Tool Call 搜索 PageIndex 数据回答用户问题

---

## 架构设计

### 数据模型

```
Resource (1) ──→ (N) ResourceVersion (1) ──→ (1) ResourcePageIndex
```

- 每个 `ResourceVersion` 对应一个文件，生成一个 PageIndex JSON
- `ResourcePageIndex` 通过 `ResourceVersionId` 关联到具体版本
- 同时保留 `ResourceId` 方便按资源查询（当前版本的 PageIndex）
- JSON 存为 `jsonb` 类型，可利用 PostgreSQL JSON 查询能力

### 表结构：`KhResourcePageIndices`

| 列 | 类型 | 说明 |
|---|---|---|
| Id | uuid | PK |
| TenantId | uuid? | 租户 |
| ResourceId | uuid | FK → AppResources |
| ResourceVersionId | uuid | FK → AppResourceVersions（唯一索引） |
| PageIndexJson | jsonb | PageIndex 输出的完整 JSON |
| SourceFormat | varchar(32) | PDF/DOCX/PPTX/XLSX |
| Model | varchar(64) | 使用的 LLM 模型（qwen-long） |
| NodeCount | int | 树节点总数 |
| CreatedAt | timestamp | 创建时间 |

---

## 已完成的变更

### 新建文件

| 文件 | 用途 |
|------|------|
| `src/pageindex-cli/test_pageindex.sh` | Shell 测试脚本，验证 CLI 对 DOCX 的端到端处理 |
| `src/KnowledgeHub.Domain/Search/ResourcePageIndex.cs` | PageIndex 实体，关联 Resource 和 ResourceVersion |
| `src/KnowledgeHub.Application.Contracts/Search/IPageIndexService.cs` | 服务接口（Generate/Get/Search） |
| `src/KnowledgeHub.Application.Contracts/Search/IPageIndexAppService.cs` | ABP AppService 接口 + SearchInput DTO |
| `src/KnowledgeHub.Application.Contracts/Search/Dtos/PageIndexDtos.cs` | ResourcePageIndexDto + PageIndexSearchResultDto |
| `src/KnowledgeHub.Application/Search/PageIndexOptions.cs` | 配置 Options（CliPath/PythonPath/Timeout/Model） |
| `src/KnowledgeHub.Application/Search/PageIndexService.cs` | 核心实现：调用 Python CLI、解析 JSON、数据库 CRUD、搜索 |
| `src/KnowledgeHub.Application/Search/PageIndexAppService.cs` | REST API 端点暴露 |
| `src/KnowledgeHub.Application/AI/Tools/PageIndexTools.cs` | AI Tool Call 函数（SearchPageIndex + GetDocumentStructure） |

### 修改文件

| 文件 | 变更 |
|------|------|
| `src/KnowledgeHub.EntityFrameworkCore/Search/SearchDbModelCreatingExtensions.cs` | 新增 ResourcePageIndex 表配置（jsonb + 唯一索引） |
| `src/KnowledgeHub.EntityFrameworkCore/EntityFrameworkCore/KnowledgeHubDbContext.cs` | 新增 `DbSet<ResourcePageIndex>` 以注册 ABP 仓储 |
| `src/KnowledgeHub.Application/Search/DocumentIndexingBackgroundJob.cs` | 注入 IPageIndexService，Meilisearch 索引后自动生成 PageIndex |
| `src/KnowledgeHub.Application/Resources/ResourceAppService.cs` | `EnqueueDocumentIndexingJobAsync` 传入 `ResourceVersionId` |
| `src/KnowledgeHub.HttpApi.Host/appsettings.json` | 新增 PageIndex 配置节 |
| `src/KnowledgeHub.Application/AI/ChatAppService.cs` | 重构为 `FunctionInvokingChatClient` + Tool Call 自动处理 |

### 自动生成

| 文件 | 用途 |
|------|------|
| `Migrations/20260406045125_AddResourcePageIndex.cs` | 创建 `KhResourcePageIndices` 表的 EF 迁移 |

---

## 核心流程

### 1. 文档上传 → PageIndex 生成

```
用户上传 DOCX/PDF/PPTX/XLSX
  → ResourceAppService.CreateAsync()
    → EnqueueDocumentIndexingJobAsync(resource)
      → BackgroundJobManager.EnqueueAsync(DocumentIndexingJobArgs { ResourceVersionId })
        → DocumentIndexingBackgroundJob.ExecuteAsync()
          → 解析文档 → Meilisearch 索引
          → [新增] PageIndexService.GeneratePageIndexAsync(versionId)
            → python3 pageindex_cli.py <file> --qwen-api-key <key> -o <output.json>
            → 保存 JSON 到 KhResourcePageIndices 表
```

### 2. AI Agent Tool Call 流程

```
用户在聊天界面提问："帮我搜索关于 XX 的文档章节"
  → ChatAppService.ChatStreamingAsync()
    → FunctionInvokingChatClient（自动处理 tool call）
      → LLM 决定调用 SearchPageIndex(query="XX")
        → PageIndexTools.SearchPageIndex()
          → PageIndexService.SearchPageIndexAsync("XX")
            → 查询 KhResourcePageIndices 表
            → 解析 JSON，递归匹配 title/summary
            → 返回匹配节点列表
      → LLM 基于工具结果生成回答
      → 流式输出到前端（SSE）
```

---

## API 端点

由 ABP Conventional Controllers 自动生成：

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/app/page-index/{resourceId}` | 获取资源的 PageIndex |
| POST | `/api/app/page-index/search` | 搜索 PageIndex（query + maxResults） |
| POST | `/api/app/page-index/generate` | 手动触发 PageIndex 生成 |

---

## 配置

```json
{
  "PageIndex": {
    "CliPath": "src/pageindex-cli/pageindex_cli.py",
    "PythonPath": "python3",
    "TimeoutMinutes": 5,
    "Model": "qwen-long"
  }
}
```

---

## 验证清单

### 阶段 1：PageIndex CLI 测试

```bash
cd src/pageindex-cli
git clone https://github.com/Vectifyai/PageIndex.git
pip install -r requirements.txt
bash test_pageindex.sh
```

预期：生成 JSON 文件，包含 `structure` 数组和非零节点数。

### 阶段 2-3：上传流程

1. `./dev.sh migrate` 应用迁移
2. `./dev.sh start` 启动服务
3. 通过 Angular 前端上传一个 DOCX 文件
4. 检查 `KhResourcePageIndices` 表中是否生成了对应记录
5. 通过 API 验证：
   ```bash
   curl -sk https://localhost:44305/api/abp/api-definition | jq '.modules.app.controllers | keys'
   # 确认 PageIndex API 出现
   ```

### 阶段 4：AI Tool Call

1. 在聊天界面发送："帮我搜索关于 XX 的文档章节"
2. 观察 AI 是否调用了 SearchPageIndex 工具
3. 验证返回的章节信息来自 PageIndex 数据
4. 检查 SSE 流中是否包含基于 PageIndex 的回答
