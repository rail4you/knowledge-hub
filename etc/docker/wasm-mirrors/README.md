# WASM 仿真实训镜像（WASM 仿真 materialType=4）

本目录是**仿真镜像解压后的运行期存储位置**，不再直接放源镜像。

教师/管理员在后台把 Unity WebGL 的 ZIP 上传 → 后端 `PracticumSimulationAppService.CreateAsync`
解压到本目录的 `{slug}/` 子目录 → Kestrel（开发）/ Nginx（生产）通过 `/wasm/{slug}/` 静态托管。

## 目录约定

```
wasm-mirrors/
├── README.md                          ← 当前文件
├── .gitignore / .gitkeep               ← 占位
├── anatomy-mice/                       ← 本地打包源（可空，pack-wasm.sh 输入）
└── {slug}/                            ← 后端上传解压的目标目录（运行时填充）
    ├── index.html
    ├── Build/                          ← *.loader.js / *.wasm.unityweb …
    ├── StreamingAssets/                ← Unity StreamingAssets
    └── TemplateData/                   ← Unity 模板资源
```

- `slug` 决定 URL 路径 `/wasm/{slug}/index.html`，由后端自动生成（name slugify + 短 GUID 后缀）。
- 二进制不进 git，`.gitignore` 兜底白名单仅保留 `mirror.json / README.md / .gitkeep / .gitignore`。
- 上传目录**不再**校验 `mirror.json`（已删除该机制）。

## 教师端添加新仿真

```bash
# 1. 把 Unity WebGL 构建产物打成 zip（本地准备好源目录后）
bash scripts/pack-wasm.sh etc/docker/wasm-mirrors/anatomy-mice anatomy-mice.zip

# 2. 教师端 UI：实训管理 → 选目标 Project → 「仿真镜像」Tab → 上传 anatomy-mice.zip
#    填写名称/描述/封面 URL；后端自动解压到 wasm-mirrors/{slug}/，记录入库。
```

> 后端会自动校验：必须存在 `.unityweb` 或 `.wasm`（否则 `Status=Invalid`）。

## 学生端入口

| 入口 | 路径 | 数据来源 |
|------|------|----------|
| 学生实训详情 | `/student/practicums/:id`（仿真 Tab） | `GET /api/app/practicum-simulation/list-by-project/{projectId}` |
| 学生资源中心 | `/student/wasm-center`（全局） | `GET /api/app/practicum-simulation/all`，仅显示 Ready |
| 学生仿真全屏 | `/student/wasm-center/:slug` | iframe `/wasm/{slug}/{entryPath}`，经 `safeResourceUrl` 管道 |

## 教师/管理员管理入口

| 入口 | 数据来源 |
|------|----------|
| 教务后台 → WASM 镜像管理 | `GET /api/app/practicum-simulation/all`（全部状态；当前为只读总览） |
| 教务后台 → 实训管理 → 项目 → 仿真镜像 Tab | `GET /api/app/practicum-simulation/list-by-project/{projectId}` + 上传/编辑/删除 |

## 静态托管

- 开发：Kestrel（`KnowledgeHubHttpApiHostModule.cs` 已配 `app.UseStaticFiles("wasm-mirrors")`，
  COOP/COEP 头由另一中间件统一加）。
- 生产：Nginx `location ^~ /wasm/` 优先匹配，`alias /usr/share/nginx/html/wasm/`，COOP/COEP 已开。

## 故障排查

| 现象 | 原因 | 修复 |
|------|------|------|
| iframe 加载 200 HTML 而非 .unityweb | Nginx `/wasm/` 缺失被 SPA 兜底 | 确认 `nginx-proxy.conf` `location ^~ /wasm/` 存在 |
| 浏览器 `crossOriginIsolated === false` | COOP/COEP 缺失 | `curl -skI /wasm/{slug}/index.html` 应见 `Cross-Origin-*` 头 |
| 学生 wasm-center 看不到某镜像 | `Status != Ready`（缺 `.unityweb`） | 用 `pack-wasm.sh` 重新打包并上传；或教师后台删除 |
| 教师无法上传大文件 | 分片上传未走完 | 前端 `chunk-upload.service` `complete` 失败检查 `/api/app/chunk-upload/complete` 日志 |
| `wasm-mirrors/{slug}` 残留 | 后端删除记录但目录未清 | 看后端日志；手工 `rm -rf` 后重启 API |
