# KnowledgeHub 全链路测试方案（2026-09）

本文档定义 KnowledgeHub（ABP + Angular 分层单体）从认证到业务模块的完整测试策略，以及面向多用户并发与数据库压力的性能测试方案。目标是可执行、可回归、可纳入 CI。

---

## 1. 测试目标与范围

### 1.1 目标
- 保证「匿名 → 登录 → 租户上下文 → 权限判定 → 业务读写 → 后台任务」整条链路正确。
- 保证多租户隔离、权限最小化、文件与上传安全不回退。
- 量化系统在目标并发下的吞吐、延迟与数据库负载，找出瓶颈。

### 1.2 覆盖范围（链路分层）

| 层 | 组件 | 关键验证点 |
|----|------|-----------|
| 认证 | OpenIddict `/connect/token` | 密码/授权码/刷新令牌；租户选择；令牌失效 |
| 网关/中间件 | `KnowledgeHubHttpApiHostModule` | CORS、ForwardedHeaders、限流、`GrantAllPoliciesMiddleware` |
| 授权 | ABP Permission + `RolePermissionSeeder` | 角色权限矩阵、宿主 vs 租户 |
| 多租户 | `__tenant`、`IMultiTenant` | 数据隔离、跨租户越权拒绝 |
| 业务 | Resources / Courses / Learning / Employment / MicroMajors / News / Practicum / DoubleHigh / SpecialEdu | CRUD、状态流转、审核 |
| 文件 | `ResourceFileController`、OSS、Gotenberg | 上传、预览、转换、下载、路径安全 |
| 搜索 | Meilisearch + 索引任务 | 建索引、检索、热词、统计 |
| AI | Qwen + RAG tools + 配额 | 流式 SSE、文档问答、额度/限流 |
| 异步 | Hangfire（default/conversion/ai/media） | 任务入队、重试、恢复、看板 |
| 前端 | Angular（管理端/门户/学生端） | 路由守卫、菜单、表单、错误提示 |

### 1.3 不在本轮范围
- 第三方外部系统（阿里云 OSS/短信）真实计费验证，使用沙箱或 mock。
- 浏览器兼容性全矩阵（仅覆盖 Chrome/Edge 最新版）。

---

## 2. 环境矩阵

| 环境 | 启动方式 | API | Angular | DB | Meili | Gotenberg |
|------|---------|-----|---------|----|-------|-----------|
| Development | `./dev.sh start` | https://localhost:44305 | http://localhost:4200 | localhost:5433 | http://localhost:7700 | http://localhost:3000 |
| Staging/Docker | `etc/docker/docker-compose.yml` | http://<host>:44354 | http://<host> | postgres:5432 | meilisearch:7700 | gotenberg:3000 |

**约定**
- 功能/集成/契约/安全测试在 Development。
- 性能测试在 **独立 staging**（或 Docker）上跑，禁止直接压生产；生产压测需审批并在业务低峰。
- 性能测试前必须关闭 `dotnet watch` 并固定 API 版本（`--no-hot-reload` 的 release/`dotnet run -c Release`）。

---

## 3. 测试数据与账号

### 3.1 标准账号（由 Seeder 创建）
| 账号 | 密码 | 角色 | 上下文 |
|------|------|------|--------|
| `admin` | `1q2w3E*` | admin | Host（全局） |
| `league-admin` | `123456` | LeagueAdmin | Host（联盟审核） |
| Teacher/Student/SchoolAdmin/EnterpriseUser | 由 `UserImportAppService` / 身份管理创建 | 对应角色 | 租户内 |

### 3.2 租户
- `Default`（默认租户，Standard 版创建）。
- 建议额外建 2 个租户（如 `tenant-a`、`tenant-b`）用于隔离测试。

### 3.3 数据准备要求
- 每个租户至少：1 个专业、2 门课程、每课 2 章节、每章 1 资源 + 3 习题。
- 资源类型覆盖：`pdf` / `docx` / `pptx` / `jpg` / `mp4`。
- 审核状态覆盖：待审、校级通过、联盟通过、已拒绝。
- 性能库需专门脚本批量造数（见 §8.4）。

---

## 4. 认证与令牌获取（所有 API 测试的基础）

`KnowledgeHub_App` 是 public client，允许 `password` 授权，可用于自动化测试。

```bash
# 1) 获取 host 管理员令牌
curl -sk -X POST 'https://localhost:44305/connect/token' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password' \
  -d 'client_id=KnowledgeHub_App' \
  -d 'username=admin' \
  -d 'password=1q2w3E*' \
  -d 'scope=KnowledgeHub offline_access'

# 2) 租户用户令牌：加 __tenant（值可为租户名或 Id）
curl -sk -X POST 'https://localhost:44305/connect/token' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -H '__tenant: Default' \
  -d 'grant_type=password&client_id=KnowledgeHub_App&username=<teacher>&password=<pwd>&scope=KnowledgeHub offline_access'

# 3) 调用 API
curl -sk 'https://localhost:44305/api/app/resource/filtered-list?skipCount=0&maxResultCount=10' \
  -H "Authorization: Bearer $TOKEN" -H '__tenant: Default'
```

**要点**
- 分页统一 `skipCount` / `maxResultCount`。
- 复杂对象 POST 用 JSON body，简单类型走路径/query（见 `AGENTS.md`）。
- 脚本化令牌获取见 §8.1 `scripts/test/get-token.sh`。

---

## 5. 分层测试策略

### 5.1 单元测试（Domain / Application）

命令：`dotnet test`

现有 `test/KnowledgeHub.Application.Tests`、`Domain.Tests`、`EntityFrameworkCore.Tests` 仅有 Base，需补用例。建议优先覆盖：

| 用例 | 目标 |
|------|------|
| `ProxyHostGuard.IsHostAllowed` | 公网放行、私网/回环/userinfo/IPv6 拒绝、白名单精确匹配 |
| `FixedLicenseValidator` | 精确密钥、HMAC 签名、前缀回退、空值拒绝 |
| `InstallAccessGuard.IsAccessAllowed` | 有令牌校验、无令牌仅回环 |
| `LocalFileStorageService` | `../` 穿越拒绝、文件名清洗、`uploadId` 非 Guid 拒绝 |
| `RolePermissionSeeder` | host/租户角色权限集合与宿主级权限不授予租户 |
| `AccountValidityPermissionHandler` | 到期账号权限回收 |
| `AiQuotaService` | 配额耗尽拒绝 |
| `MeiliFilter` | tenantId 过滤、status 过滤、ResourceId 精确 |

### 5.2 集成测试（Application / EF）

基于 `KnowledgeHubApplicationTestBase` 与 `KnowledgeHubTestBaseModule`，使用真实 DB（或 Testcontainers PostgreSQL，推荐与生产一致）。

关键场景：
- 多租户隔离：租户 A 的 token 查询 B 的数据必须为空/403。
- 授权矩阵：对每个敏感 AppService 方法按角色断言 200/403。
- 事务与 UoW：失败回滚、Hangfire job 重试幂等。
- 安全回归（见 §5.5）。

### 5.3 API 契约与冒烟测试

1. 拉取并校验 API 定义：
```bash
curl -sk https://localhost:44305/api/abp/api-definition -o api-definition.json
jq -r '.modules.app.controllers | keys[]' api-definition.json | sort
```
2. 对每个 controller 的每个 action 生成冒烟用例：
   - 无 token → 期望 401（或按 `[AllowAnonymous]` 期望 2xx）。
   - 有效低权限 token → 期望 200 或 403（与策略表比对）。
   - 有效高权限 token → 期望 2xx。
3. 权限矩阵脚本见 §8.2 `scripts/test/api-permission-matrix.sh`。

### 5.4 端到端测试（Playwright）

工具：本仓库已具备 `playwright-cli` skill。建议新增 `e2e/` 工程（@playwright/test）。

关键用户旅程：
| 角色 | 旅程 |
|------|------|
| 管理员 | 登录 → /admin/workbench → 资源管理 → 审核通过 → 索引任务 → 搜索 → 用户/角色 |
| 教师 | 登录 → /resources 上传 → /learning 建课/章节/习题 → 发布 |
| 学生 | 登录(门户) → 选课 → 学习 → 做题 → 收藏 → 微专业报名 |
| 未登录 | 公开门户/租户主页可访问；管理路由跳登录 |

断言点：菜单可见性、路由守卫重定向、表单校验、错误提示、关键接口 2xx。

### 5.5 安全回归测试（对应 `issues/security-audit-2026.md`）

以 curl 固化以下负向用例，全部应通过（拒绝或鉴权）：

| 编号 | 用例 | 期望 |
|------|------|------|
| C-1 | 匿名 `GET /api/proxy/http/169.254.169.254/latest/meta-data/` | 401/403 |
| C-2 | 匿名 `POST /api/oss-upload/file` | 401 |
| C-3 | 匿名 `POST /api/app/install/install`（非回环、无令牌） | 403 |
| H-8 | 匿名/租户用户 `POST /api/app/edition/upgrade-to-standard` | 401/403 |
| H-9 | 匿名 `POST /api/app/chapter-resource` | 401 |
| H-10 | 匿名 `POST /api/app/search/index-resource` | 401 |
| H-12 | 用户 A 用 B 的 threadId 调 `/api/learning/ai/chat` | 403 |
| H-13 | 匿名 `GET /api/app/student-exercise-record/export-learning-statistics` | 401 |
| H-1 | `GET /api/image-proxy?url=http://127.0.0.1:7700` | 403 |
| H-3 | 匿名 `GET /api/learning/practicum-chat/stream/<guid>` | 401 |
| H-6 | 匿名 `GET /api/app/meili-search-admin/hot-words?...` | 401/403 |

脚本见 §8.3 `scripts/test/security-regression.sh`。

### 5.6 前端/构建测试
- `cd angular && npx ng build --configuration development` 必须通过（已纳入）。
- `dotnet build KnowledgeHub.slnx` 必须 0 error。

---

## 6. 性能测试方案（多用户并发 + 数据库压力）

### 6.1 目标 SLO（基线，可按业务调整）

| 场景 | 并发 | p95 | 错误率 | 说明 |
|------|------|-----|--------|------|
| 登录 | 100 VU | < 500ms | < 0.5% | 含 OpenIddict 校验 |
| 资源列表 | 200 VU | < 300ms | < 0.5% | 读多写少 |
| 全局搜索 | 100 VU | < 800ms | < 1% | Meili 为主 |
| 资源详情/元数据 | 200 VU | < 300ms | < 0.5% | |
| 文档预览状态轮询 | 150 VU | < 200ms | < 1% | 高频短请求 |
| 管理统计 | 20 VU | < 1.5s | < 1% | 重聚合查询 |
| AI 问答（SSE） | 10 VU | 首字节 < 3s | < 2% | 受外部 Qwen 限制，单独跑 |

### 6.2 并发模型
- 阶梯加压：`10 → 50 → 100 → 200 → 300 VU`，每阶段 3–5 分钟，观察拐点。
- 混合场景权重（更贴近真实）：列表 40%、详情 25%、搜索 20%、登录 5%、预览轮询 10%。
- 持续压测：目标并发下跑 30–60 分钟，观察内存/连接池是否泄漏。

### 6.3 工具选型
- **k6**（推荐，脚本化、指标丰富）：`brew install k6`。
- **pgbench**（PostgreSQL 自带）：制造纯 DB 压力。
- **ab**（本机已有）：快速单接口冒烟，不作为正式结论。
- 监控：`pg_stat_activity`、`pg_stat_statements`、`docker stats`、API 日志、Hangfire 看板。

### 6.4 k6 脚本骨架

`scripts/perf/k6/lib.js`
```javascript
import http from 'k6/http';
import { check } from 'k6';

export const BASE = __ENV.BASE || 'https://localhost:44305';

export function login(username, password, tenant) {
  const headers = { 'Content-Type': 'application/x-www-form-urlencoded' };
  if (tenant) headers['__tenant'] = tenant;
  const res = http.post(`${BASE}/connect/token`,
    `grant_type=password&client_id=KnowledgeHub_App&username=${username}&password=${encodeURIComponent(password)}&scope=KnowledgeHub offline_access`,
    { headers });
  check(res, { 'login 200': r => r.status === 200 });
  return res.json('access_token');
}

export function authHeaders(token, tenant) {
  const h = { Authorization: `Bearer ${token}` };
  if (tenant) h['__tenant'] = tenant;
  return h;
}
```

`scripts/perf/k6/mixed.js`
```javascript
import http from 'k6/http';
import { sleep, group } from 'k6';
import { login, authHeaders, BASE } from './lib.js';

export const options = {
  stages: [
    { duration: '1m', target: 50 },
    { duration: '3m', target: 100 },
    { duration: '3m', target: 200 },
    { duration: '2m', target: 300 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{scenario:list}': ['p(95)<300'],
    'http_req_duration{scenario:search}': ['p(95)<800'],
  },
};

export function setup() {
  return { token: login(__ENV.USER || 'admin', __ENV.PASS || '1q2w3E*', __ENV.TENANT || '') };
}

export default function (data) {
  const h = authHeaders(data.token, __ENV.TENANT || '');
  group('list', () => {
    http.get(`${BASE}/api/app/resource/filtered-list?skipCount=0&maxResultCount=20`, { headers: h, tags: { scenario: 'list' } });
  });
  group('detail', () => {
    http.get(`${BASE}/api/app/resource/categories`, { headers: h, tags: { scenario: 'detail' } });
  });
  group('search', () => {
    http.post(`${BASE}/api/app/search/search`, JSON.stringify({ query: '课程', skipCount: 0, maxResultCount: 12 }),
      { headers: { ...h, 'Content-Type': 'application/json' }, tags: { scenario: 'search' } });
  });
  sleep(Math.random() * 1.5);
}
```

运行：
```bash
k6 run --env BASE=https://localhost:44305 --env USER=admin scripts/perf/k6/mixed.js
k6 run --out json=reports/k6-result.json scripts/perf/k6/mixed.js
```

### 6.5 数据库压力测试（pgbench）

`scripts/perf/pgbench/read.sql`（自定义只读脚本，模拟 API 的典型查询）
```sql
\set rid random(1, 100000)
SELECT id, name, status FROM "Resources" WHERE id = (
  SELECT id FROM "Resources" OFFSET :rid LIMIT 1
);
SELECT count(*) FROM "StudentExerciseRecords" WHERE "CourseId" IN (
  SELECT id FROM "Courses" OFFSET :rid LIMIT 5
);
```

运行：
```bash
# 初始化
pgbench -i -s 50 -h localhost -p 5433 -U postgres KnowledgeHub
# 只读并发（模拟 100 连接）
pgbench -h localhost -p 5433 -U postgres -c 100 -j 8 -T 120 -f scripts/perf/pgbench/read.sql KnowledgeHub
# 读写混合
pgbench -h localhost -p 5433 -U postgres -c 50 -j 8 -T 120 -f scripts/perf/pgbench/read-write.sql KnowledgeHub
```

### 6.6 数据库观测清单
- 连接数：`SELECT count(*) FROM pg_stat_activity;`（对比 `Maximum Pool Size`，默认 50）。
- 慢查询：先开启 `pg_stat_statements`，按 `total_exec_time` 排序。
- 锁等待：`SELECT * FROM pg_locks WHERE NOT granted;`
- 缓存命中：`pg_statio_user_tables`。
- 索引缺失：对 `Resources(TenantId,Status,CreationTime)`、`StudentExerciseRecords(StudentId,CourseId)` 等热查询 `EXPLAIN ANALYZE`。
- Hangfire 表增长（`hangfire.job`/`state`）对 DB 的影响。

### 6.7 关键风险点
- API 与 Hangfire 共用 PostgreSQL：后台任务（转换/AI/媒体）会与请求争抢连接。`ConnectionStrings:Hangfire` 已独立，但同库；压测时需同时观察 Hangfire worker。
- `ResourceFileController` 预览轮询有 5s 内存缓存，压测高频轮询会掩盖 DB 真实压力，需区分「缓存命中」与「缓存失效」。
- Meilisearch 是外部进程，搜索场景需单独监控其 CPU/内存。
- AI 场景受外部 Qwen 限流，不宜与其他场景混跑。

---

## 7. 执行流程与 CI 集成

| 阶段 | 触发 | 内容 |
|------|------|------|
| PR | 每次提交 | `dotnet build`、`dotnet test`（单元+集成）、`ng build`、API 冒烟（§5.3）、安全回归（§5.5） |
| Nightly | 每日 | 全量 E2E、契约、多租户隔离回归 |
| Release | 发版前 | 性能测试（§6）+ 报告归档 |

CI 建议命令：
```bash
dotnet test KnowledgeHub.slnx
cd angular && npx ng build --configuration development
bash scripts/test/security-regression.sh "$TOKEN"
```

---

## 8. 配套脚本（已创建）

| 路径 | 作用 | 用法 |
|------|------|------|
| `scripts/test/get-token.sh` | 获取指定用户/租户令牌 | `scripts/test/get-token.sh admin '1q2w3E*' [tenant]` |
| `scripts/test/api-smoke.sh` | 遍历 api-definition 对无参 GET 做冒烟 | `scripts/test/api-smoke.sh [token] [tenant]` |
| `scripts/test/api-permission-matrix.sh` | 多角色 × 多端点 2xx/403 断言 | 配置 `ADMIN_*/TEACHER_*/STUDENT_*` 后直接运行 |
| `scripts/test/security-regression.sh` | §5.5 安全负向用例 | `scripts/test/security-regression.sh` |
| `scripts/test/seed-perf-data.sh` | 克隆式批量造数（jsonb，免硬编码表结构） | `CONFIRM=yes COUNT=5000 TENANTS=3 scripts/test/seed-perf-data.sh` |
| `scripts/perf/k6/lib.js` | k6 公共库（登录/头/查询词） | 被下方场景引用 |
| `scripts/perf/k6/mixed.js` | 混合场景（列表/分类/搜索） | `k6 run scripts/perf/k6/mixed.js` |
| `scripts/perf/k6/search.js` | 搜索专项 | `k6 run scripts/perf/k6/search.js` |
| `scripts/perf/k6/ai.js` | AI SSE 专项（小并发） | `k6 run --env VUS=5 scripts/perf/k6/ai.js` |
| `scripts/perf/pgbench/setup.sql` | 创建独立压测表 `perf_kv` | `psql ... -f scripts/perf/pgbench/setup.sql` |
| `scripts/perf/pgbench/read.sql` | 只读压测 | `pgbench ... -f scripts/perf/pgbench/read.sql` |
| `scripts/perf/pgbench/read-write.sql` | 读写压测 | 先 run setup，再 `pgbench ... -f read-write.sql` |

### 8.1 快速开始
```bash
# 功能/安全
chmod +x scripts/test/*.sh
scripts/test/get-token.sh admin '1q2w3E*' | head -c 20; echo
scripts/test/security-regression.sh
scripts/test/api-smoke.sh

# 性能
brew install k6                       # 或见 https://k6.io/docs/get-started/installation/
CONFIRM=yes COUNT=10000 TENANTS=3 scripts/test/seed-perf-data.sh
psql -h localhost -p 5433 -U postgres -d KnowledgeHub -f scripts/perf/pgbench/setup.sql
k6 run --env BASE=https://localhost:44305 --env USER=admin --env PASS='1q2w3E*' scripts/perf/k6/mixed.js
pgbench -h localhost -p 5433 -U postgres -c 100 -j 8 -T 120 -f scripts/perf/pgbench/read.sql KnowledgeHub
```

> 注意：`seed-perf-data.sh` 需要库中已有至少 1 个租户、1 条 Resource、1 条 Course 作为模板；不存在时脚本会提示先通过 UI/API 创建。

---

## 9. 报告模板

每次执行输出一份 `reports/<date>-<env>-<type>.md`：
```
# 测试报告
- 环境 / 版本 / 提交号：
- 执行时间 / 人员：
- 用例总数 / 通过 / 失败 / 阻塞：
- 失败用例（编号、操作、期望、实际、日志、截图）：
- 性能指标（RPS、p50/p95/p99、错误率、DB 连接峰值、慢查询 Top5）：
- 结论与后续动作：
```

---

## 10. 验收标准（Definition of Done）
- 单元+集成测试通过率 100%，安全回归全绿。
- E2E 关键旅程通过，路由守卫与权限矩阵无越权。
- 性能达到 §6.1 SLO，无连接池耗尽、无明显内存增长、无慢查询恶化。
- 报告归档，遗留问题登记到 `issues/`。
