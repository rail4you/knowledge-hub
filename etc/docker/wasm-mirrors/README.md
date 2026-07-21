# WASM 仿真实训镜像

本目录存放 `materialType=4` 的 Unity WebGL 仿真实训资源本地镜像，用于：

- **冷启动加速**：浏览器直连仓库（首次 ≈ 源站），后续命中 HTTP 缓存
- **降低跨域/限流风险**：不再走 `/api/proxy/http/...` 反向代理链路

## 目录约定

```
wasm-mirrors/
├── README.md                       ← 当前文件
├── .gitignore 占位文件              ← .gitkeep
└── {slug}/                         ← 每份仿真一个 slug 小写子目录
    ├── mirror.json                 ← 元信息（必须，下载脚本自动生成）
    ├── index.html                  ← Unity loader 入口
    ├── Build/                      ← *.loader.js / *.wasm.unityweb / *.framework.js.unityweb
    ├── StreamingAssets/            ← Unity StreamingAssets（递归）
    └── TemplateData/               ← Unity 模板资源（图片、样式等）
```

`slug` 必须满足：`^[a-z0-9][a-z0-9-]{0,63}$`，由 `scripts/fetch-wasm.sh` 强制校验。

## mirror.json 契约

```json
{
  "slug": "anatomy-mice",
  "sourceUrl": "http://124.222.92.99:8003/anatomyMice/",
  "entryPath": "index.html",
  "status": "ready",
  "mirroredAt": "2026-07-21T10:00:00Z",
  "buildSha": "可选，Unity build 的 git sha",
  "totalBytes": 123456789,
  "fileCount": 42,
  "coopCoepRequired": true,
  "files": [
    "index.html",
    "Build/小鼠解剖实验.loader.js",
    "Build/小鼠解剖实验.data.unityweb",
    "Build/小鼠解剖实验.wasm.unityweb",
    "Build/小鼠解剖实验.framework.js.unityweb"
  ]
}
```

- `status=ready` 时 `WasmMirrorAppService.GetMappingAsync()` 才会把 sourceUrl 映射到 publicUrl
- 否则前端管线下落到原 URL，走 `/api/proxy/http/...`
- 修改 `status` 可即时回退（无需重启 API，扫描有 mtime 失效缓存）

## 添加新仿真

```bash
bash scripts/fetch-wasm.sh http://外部源站/anatomyMice/ anatomy-mice
./dev.sh restart api    # 让 API 重新扫描根目录
```

生产环境：

```bash
cd etc/docker
bash ../../scripts/fetch-wasm.sh http://外部源站/anatomyMice/ anatomy-mice
docker compose restart knowledgehub-angular   # 让 nginx 重新读取静态目录
```

> 镜像不进入 git。新机器拉取仓库后需要重新执行 `fetch-wasm.sh` 同步。

## 故障排查

| 现象 | 原因 | 排查 |
|------|------|------|
| iframe 仍走源站 | `mirror.json.status` ≠ `"ready"` 或文件缺失 | `curl -s http://localhost:44305/api/app/wasm-mirror/mapping` |
| 浏览器 DevTools: `Refused to display ... COOP/COEP` | 未启用 COOP/COEP 头，Unity 多线程 build 失败 | 检查 Nginx `location ^~ /wasm/` 是否生效（`curl -I /wasm/{slug}/index.html`） |
| `.wasm.unityweb` 被当成 HTML 返回 200 | Nginx `/wasm/` 缺失，被 Angular SPA 兜底 | `location ^~ /wasm/` 优先级匹配 + `try_files $uri =404` |
| 移动端 iOS Safari SharedArrayBuffer 失败 | iOS Safari SharedArrayBuffer 限制 | 设 `mirror.json.coopCoepRequired=false`，或使用单线程 Unity build |
| `WasmMirror root not found` warning | 目录不存在 | 新机器首次运行属正常；`fetch-wasm.sh` 同步后即消失 |
