# 测试执行报告（2026-09-13）

- 环境：Development（`./dev.sh`），API `https://localhost:44305`，Angular `:4200`，PostgreSQL `:5433`，Meilisearch `:7700`，Gotenberg `:3000`
- 提交：`a20cf8a1`（测试方案与脚本）+ 本轮测试中修复的补丁
- 执行人：自动化（opencode）
- 说明：AI 场景按用户要求**未做压测**，仅保留脚本备用，避免消耗 Qwen 额度。

---

## 1. 结果总览

| 套件 | 命令 | 结果 |
|------|------|------|
| 认证取 token | `scripts/test/get-token.sh` | ✅ 通过（admin / host） |
| 安全回归 | `scripts/test/security-regression.sh` | ✅ 10/10 通过 |
| API 冒烟 | `scripts/test/api-smoke.sh` | ✅ 131 PASS / 15 WARN / 0 FAIL |
| DB 压力（只读） | `pgbench read.sql` | 665 tps @ 50 clients，avg 75ms |
| DB 压力（读写） | `pgbench read-write.sql` | 14,632 tps @ 30 clients，avg 2ms |
| 应用并发（列表） | `ab c=50` | 87 RPS，p95 875ms |
| 应用并发（列表） | `ab c=100` | 79 RPS，p95 1780ms |
| 应用并发（分类） | `ab c=50` | 92 RPS，p95 815ms |
| 应用并发（搜索） | `ab c=50` | 22.6 RPS，p95 3391ms，**0.57% 5xx** |

> 重要前提：以上性能数字来自**单台开发机**（同时运行 API(Debug)、Angular dev server、Meilisearch、Gotenberg、PostgreSQL），仅代表开发环境相对基线与瓶颈定位，不代表生产容量。

---

## 2. 安全回归（10/10）

| 用例 | 期望 | 实测 |
|------|------|------|
| C-1 匿名 `/api/proxy/http` 内网 | 拒绝 | 302 |
| C-2 匿名 `/api/oss-upload/file` | 拒绝 | 302 |
| H-8 匿名 `/api/app/edition/upgrade-to-standard` | 拒绝 | 302 |
| H-9 匿名 `POST /api/app/chapter-resource` | 拒绝 | 302 |
| H-10 匿名 `POST /api/app/search/index-resource` | 拒绝 | 302 |
| H-13 匿名学习统计导出 | 拒绝 | 302 |
| H-3 匿名 practicum-chat SSE | 拒绝 | 302 |
| H-6 匿名 meili hot-words | 拒绝 | 302 |
| H-1 已登录 image-proxy 访问内网 | 403 | 403 |
| C-1 已登录 proxy/http 访问内网 | 403 | 403 |

结论：上一轮安全修复在运行环境中已验证生效（匿名请求被 401/302 拒绝，SSRF 内网目标被 403）。

---

## 3. 冒烟测试发现并修复的缺陷

冒烟首轮 `FAIL=5`，均为**既有缺陷**（非本轮安全改动引入），已定位并修复：

### 3.1 `search-analytics` 重复控制器 500（3 个端点）
- 现象：`/api/app/search-analytics/{popular-searches,search-stats,top-resources}` → 500
- 根因：`SearchAnalyticsService` 实现 `ISearchAnalyticsService : IApplicationService`，被 ABP 暴露为约定控制器，但只按接口注册、未注册具体类型 → `ComponentNotRegisteredException`。
- 修复：`SearchAnalyticsService` 加 `[RemoteService(false)]`，对外统一走 `/api/app/search/*`（已有策略鉴权），同时避免匿名暴露统计接口。

### 3.2 遗留 `AppUser` CRUD 500
- 现象：`GET /api/app/user` → 500（`Cannot create a DbSet for 'AppUser' ... not included in the model`）
- 根因：`UserAppService` 的 Get/List/Create/Update/Delete 使用已废弃的 `AppUser` 仓储，该实体未映射且库中无表；用户管理实际已迁移到 ABP Identity。
- 修复：在 `IUserAppService` 与实现方法上将这些遗留方法标注 `[RemoteService(false)]` 隐藏（保留在用的 `my-profile`）。现返回 404 而非 500。

### 3.3 资源推荐接口并发 500（连接/命令占用）
- 现象：`/api/app/resource-recommendation/*` → `Connection already open` / `A command is already in progress`
- 根因：手动 `GetDbConnection().OpenAsync()` 未判断状态；`GetCategoryBasedRecommendationsAsync` 在 `reader` 未释放时又执行第二个命令。
- 修复：
  - 4 处 `OpenAsync` 前增加 `State != Open` 判断；
  - 将 reader 用 `using(...)` 提前释放，再做 trending 兜底查询。
- 结果：三个推荐端点均 200。

> 冒烟 15 条 WARN 均为「缺少必填参数(400) / 缺少权限或特性未开通(403) / 需要路径参数(404)」的预期行为，非缺陷。

---

## 4. 性能测试详情

### 4.1 数据库压力（pgbench）
```
READ  : -c 50 -j 4 -T 60  →  39,894 txns, tps=665.5, avg latency=75.1ms
R-W   : -c 30 -j 4 -T 60  → 877,086 txns, tps=14,632, avg latency=2.05ms
```

### 4.2 应用并发（ab，含鉴权头）
| 场景 | 并发 | RPS | p50 | p95 | p99 | Error | DB 峰值连接 |
|------|------|-----|-----|-----|-----|-------|------------|
| resource/filtered-list | 50 | 87.2 | 563ms | 875ms | 1029ms | 0 | 61 |
| resource/filtered-list | 100 | 79.3 | 1239ms | 1780ms | 2334ms | 0 | 58 |
| resource/categories | 50 | 92.1 | 523ms | 815ms | 917ms | 0 | - |
| search/search (POST) | 50 | 22.6 | 2111ms | 3391ms | 4112ms | 0.57% | - |

### 4.3 瓶颈结论
1. **吞吐平台 ~80–90 RPS**：c=50→100 时吞吐不再增长、时延近线性上升，说明 API/单机已饱和（Debug 构建 + 多服务同机）。
2. **数据库连接池打满**：峰值 58–61 连接，逼近 API 侧 `Maximum Pool Size=50`，出现排队。建议：调大 `Default` 连接串 Pool Size，或减少单请求连接占用（推荐接口存在多次往返）。
3. **搜索是最慢链路**：22.6 RPS、p95 3.4s，且出现 0.57% 500，日志显示为对 Meilisearch 的 `HttpRequestException/HttpIOException/SocketException`（并发下连接被拒/重置）。建议：检查 Meili 并发与连接复用、为搜索 HttpClient 调优连接池/超时、减少每请求的双索引查询。
4. `resource/categories` 与 `filtered-list` 时延接近，疑似存在 N+1 / 全量聚合，建议用 `EXPLAIN ANALYZE` + 日志慢查询定位。

---

## 5. 未执行 / 后续

- **AI（SSE）压测**：按用户要求跳过，脚本 `scripts/perf/k6/ai.js` 已就绪，建议正式压测时 `VUS<=5` 且单独排期。
- **k6 混合场景**：本机未安装 k6，正式压测建议 `brew install k6` 后按 `docs/TEST_PLAN.md` 执行。
- **E2E（Playwright）**：尚未搭建，建议按方案 §5.4 补充。
- **前端路由守卫回归**：需用 teacher/student 账号跑 `api-permission-matrix.sh`（当前仅 admin 令牌，teacher/student 未配置）。
- **测试数据清理**：已执行清理，库恢复为 2 租户 / 73 资源 / 9 课程；后续可用 `scripts/test/cleanup-perf-data.sh`。

---

## 6. 第二轮：权限矩阵、越权与多租户隔离验证

### 6.1 单元/集成测试现状
- 修复前：`dotnet test` 仅有 **3 个** EF Core 样例测试，Application/Domain 测试项目为空壳。
- 本轮补充 **11 个** 安全原语单元测试，`dotnet test` 现为 **14/14 通过**：
  - `FixedLicenseValidatorTests`（4）：空值、前缀回退、精确密钥白名单、HMAC 签名/篡改。
  - `InstallAccessGuardTests`（2）：令牌匹配、无令牌仅回环、无 HttpContext fail-closed。
  - `LocalFileStorageServiceTests`（5）：文件名清洗、`../` 穿越不越界、非法 uploadId 拒绝、分片合并顺序、越界读取拒绝。
- 仍缺：业务 AppService/Domain 的集成用例（含真实 DB）。

### 6.2 测试账号
通过 `POST /api/app/tenant-user/user-for-tenant` 在 `qidi` 租户创建：
- `qa-teacher / 1q2w3E*`（Teacher）
- `qa-student / 1q2w3E*`（Student）

（如需清理，可用 admin 调 `DELETE /api/app/tenant-user/{id}`。）

### 6.3 权限矩阵（`api-permission-matrix.sh`，8/8 通过）
| 端点 | admin | teacher | student |
|------|-------|---------|---------|
| `/api/app/resource/filtered-list` | 200 | 200 | — |
| `/api/app/major/lookup-list` | 200 | — | — |
| `/api/app/meili-search-admin/indexes` | 200 | 200 | 403 |
| `/api/app/meili-search-admin/dashboard` | 200 | — | 403 |

> 结论：Teacher 确实持有 `Search.ManageIndex`（由 `RolePermissionSeeder` 授予），因此能看到 Meili 管理端点；Student 被正确拒绝。矩阵脚本最初把该端点期望写成 teacher=403，已验证并修正为 teacher=2xx。

### 6.4 越权 / 隔离负向验证
| 用例 | 构造 | 结果 |
|------|------|------|
| H-4 宿主未审资源 | qa-teacher（qidi）预览 HOST 未审核资源 | **200（越权，已修复）→ 403** |
| H-4 同租户未审资源 | qa-teacher 预览 qidi 未审核资源 | 200（符合预期） |
| H-4 宿主管理员 | admin 预览 HOST 未审核资源 | 200（符合预期） |
| H-5 摘要跨租户 | qa-teacher 对 HOST 资源触发摘要 | **403** |
| H-12 AI 会话 IDOR | qa-student 用 qa-teacher 的 threadId 发消息 | 拒绝「无权访问该会话」，插入 0 条消息，未调用 AI |
| H-2 上传路径穿越 | `fileName=../../pwned.txt` 上传并 complete | 落点为 `uploads/20260913/pwned.txt`，未越界 |
| M-6 租户统计 | 匿名 `/api/public/tenants-with-stats` | **200（越权，已修复）→ 拒绝**；`/api/public/tenants` 仍匿名可用 |
| L-1 登出 CSRF | 匿名 `GET /api/app/logout/clear-session` | 拒绝（302） |

### 6.5 本轮新发现并修复的缺陷
1. **`TenantListController` 类级 `[AllowAnonymous]` 覆盖方法级 `[Authorize]`** —— ASP.NET Core 中 `[AllowAnonymous]` 优先，导致 `tenants-with-stats` 仍匿名可访问。改为移除类级、`GetTenants` 单独 `[AllowAnonymous]`。
2. **`ResourceFileController` 未审核资源隔离不完整** —— 租户用户可预览宿主（TenantId=null）未审核资源。收紧为「租户用户仅可访问本租户未审核资源」。

## 7. 异步闭环验证（上传 → 处理 → 索引 → 搜索 → 删除）

用带唯一关键词 `ZEBRAFISH-QA-9137` 的最小 DOCX（1KB）走完整链路，逐阶段核对 DB/Meili/文件系统/接口。

### 7.1 成功路径
| 阶段 | 观测 | 结果 |
|------|------|------|
| 上传 | `POST /api/app/chunk-upload/{initiate,upload,complete}` | `uploads/20260913/qa-workflow.docx`，落点正确 |
| 建资源 | `POST /api/app/resource`（带 filePath） | 资源+初始版本创建，入队 `KhDocumentIndexingJobs` + `AppResourceMediaJobs` |
| 文本解析 | LiteParse（localhost:15000） | `KhPageContents` 1 页 |
| 索引 | Meilisearch `documents` 索引 | 文档含 `pageContent` / `tenantId` |
| 媒体 | Gotenberg（localhost:3000） | `converted/{rid}.pdf` + `thumbnails/{versionId}_400.jpg`，artifacts State=Ready |
| 搜索 | `POST /api/app/search/search` | 关键词命中，total=1 |
| 预览 | `preview-pdf-info` / `thumbnail` | `ready=true` / HTTP 200 |

### 7.2 版本更新（换版本重索引）
- `POST /api/app/resource/upload-version` 上传 v2（关键词 `ZEBRAFISH-QA-V2-4471`）。
- 新索引任务 Completed；Meili 该资源仅剩 1 页且为 v2 内容 → **旧正文已被替换，无残留**。
- 搜索 v2 关键词命中；v1 关键词仅命中的是资源名（资源名未改），正文已更新。

### 7.3 失败路径
- 建资源时指向不存在的文件：索引任务 → **Failed(40)**，错误 `File not found: ...`，无无限重试。

### 7.4 删除路径
- host 资源由 host 删除：`204` + `IsDeleted=true`。
- 租户资源被 host 上下文删除：修复后返回 **404**，资源保留（不再假成功）。

### 7.5 本轮新发现并修复的缺陷
1. **媒体任务假成功**：源文件缺失时 `ResourceMediaProcessor` 把“路径已配置但文件不存在”与“仅正文无文件”混为一谈，直接标记 `Ready/Completed`，导致“处理完成却无任何生成物”。修复：区分二者，前者标记 `Failed`（错误“源文件不存在”）。
2. **删除假成功 + 状态不一致**：`ResourceAppService.DeleteAsync` 先做 Meili/媒体清理再删除；且 ABP `DeleteAsync(id)` 找不到实体时**静默返回**，导致跨租户/不存在时返回 204、资源未删但 Meili 索引已被清掉。修复：先 `GetAsync`（不存在抛 404）再删除，最后清理。

> 说明：host 用户即使带 `__tenant: qidi` 头，删除租户资源仍按 host 上下文处理（审计 TenantId 为空），因此返回 404 属正确隔离行为。

## 8. 本轮代码改动
- `src/KnowledgeHub.Application/Search/SearchAnalyticsService.cs`：`[RemoteService(false)]`
- `src/KnowledgeHub.Application/Users/UserAppService.cs` + `IUserAppService.cs`：遗留 CRUD 隐藏
- `src/KnowledgeHub.Application/Search/ResourceRecommendationAppService.cs`：连接/命令占用修复
- `src/KnowledgeHub.HttpApi/Controllers/ResourceFileController.cs`：未审核资源租户/宿主隔离收紧
- `src/KnowledgeHub.HttpApi/Controllers/TenantListController.cs`：移除类级 `[AllowAnonymous]`，仅 `GetTenants` 匿名
- `src/KnowledgeHub.Application/Resources/Media/ResourceMediaProcessor.cs`：源文件缺失时媒体任务标记 Failed（修复假成功）
- `src/KnowledgeHub.Application/Resources/ResourceAppService.cs`：DeleteAsync 先确认存在再删除，再清理（修复假成功与索引不一致）
- `scripts/test/seed-perf-data.sh`、`scripts/perf/pgbench/read.sql`：表名修正为 `AppResources/AppCourses`
- `scripts/test/security-regression.sh`：SSE 超时保护、`Accept: application/json`、REJECT 状态集合、变量花括号修复、新增 M-6/L-1 用例
- `scripts/test/api-permission-matrix.sh`：修正 major 路径与 teacher 期望
- `scripts/test/cleanup-perf-data.sh`：新增清理脚本

## 9. 后续仍待补的测试
1. **业务集成测试**：权限 Seeder、租户隔离、AI 配额等（安全原语单测已补 11 个）。
2. **E2E（Playwright）**：管理端/教师/学生关键旅程与前端路由守卫（当前仅有 API 级验证）。
3. **k6 混合/搜索压测**：本机无 k6；`ai.js` 按用户要求暂缓。
4. **视频链路**：`VideoIndexingJob`（ffmpeg 抽帧 + Qwen VL 索引）尚未走完整闭环。
5. **契约测试**：对 api-definition 全量 action 做参数化调用（当前冒烟仅覆盖无参 GET）。
6. **Hangfire 重试/恢复**：人为让转换/索引失败，验证异常队列、重试与恢复任务。
