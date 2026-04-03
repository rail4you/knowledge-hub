# KnowledgeHub 远程部署手册

## 目录

- [架构概览](#架构概览)
- [服务器与凭据](#服务器与凭据)
- [镜像仓库](#镜像仓库)
- [目录结构](#目录结构)
- [环境变量说明](#环境变量说明)
- [构建与推送镜像](#构建与推送镜像)
- [部署流程](#部署流程)
- [运维命令](#运维命令)
- [URL 路由说明](#url-路由说明)
- [故障排查](#故障排查)

---

## 架构概览

```
浏览器 (:80)
  │
  ▼
┌─────────────────────────────────────────┐
│  Angular 容器 (nginx:80)                │
│  ┌─────────────────────────────────────┐ │
│  │ nginx-proxy.conf                    │ │
│  │  /              → 静态文件          │ │
│  │  /getEnvConfig → dynamic-env.json   │ │
│  │  /api/*        → proxy → API:44354  │ │
│  │  /connect/*    → proxy → API:44354  │ │
│  │  /.well-known/*→ proxy → API:44354  │ │
│  └─────────────────────────────────────┘ │
└──────────────────┬──────────────────────┘
                   │ Docker 内部网络 (abp-network)
                   ▼
┌──────────────────────────────────────────┐
│  API 容器 (dotnet :44354)                │
│  不对外暴露端口，仅内部网络可达          │
└──────┬──────────┬──────────┬────────────┘
       │          │          │
       ▼          ▼          ▼
  PostgreSQL    Redis    Meilisearch
  (:5432)       (:6379)  (:7700)
```

**关键设计**：API 不对外暴露端口，所有外部请求通过 Angular 容器内的 Nginx 反向代理转发。

---

## 服务器与凭据

### 远程服务器

| 项目 | 值 |
|------|-----|
| IP 地址 | `119.45.170.4` |
| SSH 用户 | `ubuntu` |
| SSH 密码 | `5[4j:nfqE)S~s;t` |
| 操作系统 | Linux x86_64 |
| 部署目录 | `~/knowledgehub/` |
| docker-compose 版本 | v1.29.2（注意：不支持 `docker compose --env-file`） |

### SSH 连接

```bash
ssh ubuntu@119.45.170.4
# 密码含特殊字符，如需在脚本中使用建议用 SSH 密钥认证
```

> **注意**：密码包含 `[`, `)`, `~`, `;` 等特殊字符，在 shell 中传递需要仔细转义。建议配置 SSH 密钥免密登录。

### 应用默认账号

| 项目 | 值 |
|------|-----|
| 管理员用户名 | `admin` |
| 管理员密码 | `1q2w3E*` |

---

## 镜像仓库

### 阿里云容器镜像服务 (ACR)

| 项目 | 值 |
|------|-----|
| 仓库地址 | `registry.cn-zhangjiakou.aliyuncs.com/myelixir` |
| 登录命令 | `docker login registry.cn-zhangjiakou.aliyuncs.com` |

### 镜像列表

| 镜像名 | Dockerfile | 说明 |
|--------|-----------|------|
| `knowledgehub-angular` | `angular/Dockerfile.prod` | Angular 前端 + Nginx |
| `knowledgehub-api` | `src/KnowledgeHub.HttpApi.Host/Dockerfile.prod` | .NET API 服务 |
| `knowledgehub-db-migrator` | `src/KnowledgeHub.DbMigrator/Dockerfile.prod` | 数据库迁移工具 |

完整镜像地址格式：`registry.cn-zhangjiakou.aliyuncs.com/myelixir/<镜像名>:latest`

---

## 目录结构

### 本地项目 (`etc/docker/`)

```
etc/docker/
├── docker-compose.yml            # Docker Compose 配置（生产环境）
├── nginx-proxy.conf              # Nginx 代理配置（覆盖 Angular 容器内的默认配置）
├── dynamic-env.template.json     # Angular 运行时环境模板（变量占位符）
├── dynamic-env.json              # 生成的运行时配置（deploy.sh 自动生成）
├── .env.example                  # 环境变量模板
├── deploy.sh                     # 远程服务器管理脚本
├── certs/
│   └── localhost.pfx             # 本地开发证书
└── DEPLOY.md                     # 本文档
```

### 远程服务器 (`~/knowledgehub/`)

```
~/knowledgehub/
├── docker-compose.yml            # 同步自 etc/docker/
├── nginx-proxy.conf              # 同步自 etc/docker/
├── dynamic-env.template.json     # 同步自 etc/docker/
├── dynamic-env.json              # 由 deploy.sh 自动生成
├── deploy.sh                     # 同步自 etc/docker/
├── .env                          # 从 .env.example 复制并填写真实值
├── uploads/                      # 文件上传目录（API 挂载）
└── meilisearch_data/             # 搜索引擎数据目录
```

---

## 环境变量说明

### `.env` 文件配置

所有变量定义在 `.env` 中，通过 `deploy.sh` 加载并传递给 docker-compose。

```bash
# ============================================================
# 核心变量 — 浏览器访问地址
# 格式: http://IP 或 http://域名（不带尾部斜杠、不带端口号）
# ============================================================
PUBLIC_URL=http://119.45.170.4

# ============================================================
# 数据库
# ============================================================
POSTGRES_PASSWORD=<数据库密码>

# ============================================================
# Meilisearch 搜索引擎
# ============================================================
MEILISEARCH_MASTER_KEY=<Meilisearch Master Key>

# ============================================================
# Embedding API（向量搜索）
# ============================================================
EMBEDDING_API_KEY=<Embedding API Key>

# ============================================================
# Qwen AI API
# ============================================================
QWEN_API_KEY=<Qwen API Key>
QWEN_BASE_URL=https://dashscope.aliyuncs.com/compatible-mode/v1
QWEN_MODEL=qwen-plus

# ============================================================
# 安全配置
# ============================================================
JWT_CERTIFICATE_PASSWORD=<JWT 证书密码>
STRING_ENCRYPTION_PASSPHRASE=<字符串加密密钥>

# ============================================================
# Docker 镜像仓库
# ============================================================
REGISTRY=registry.cn-zhangjiakou.aliyuncs.com/myelixir
IMAGE_TAG=latest
```

### 变量用途映射

| 变量 | 使用位置 | 说明 |
|------|---------|------|
| `PUBLIC_URL` | API、Angular、DbMigrator | 浏览器访问地址，影响 OAuth 回调、CORS、API 路由 |
| `POSTGRES_PASSWORD` | API、DbMigrator、Postgres | 数据库连接密码 |
| `MEILISEARCH_MASTER_KEY` | API、Meilisearch | 搜索引擎密钥 |
| `EMBEDDING_API_KEY` | API | 向量化 API 密钥 |
| `QWEN_API_KEY` | API | 通义千问 API 密钥 |
| `JWT_CERTIFICATE_PASSWORD` | API、DbMigrator | JWT 签名证书密码 |
| `STRING_ENCRYPTION_PASSPHRASE` | API、DbMigrator | ABP 字符串加密密钥 |
| `REGISTRY` | docker-compose | 镜像仓库地址前缀 |
| `IMAGE_TAG` | docker-compose | 镜像标签 |

### API 容器内部通信

API 容器内部使用 `http://knowledgehub-api:44354` 进行容器间通信（`AuthServer__Authority`），不走外部 Nginx。浏览器端则通过 `PUBLIC_URL`（80 端口）访问。

---

## 构建与推送镜像

### 前提条件

- 本地已安装 Docker（支持 buildx）
- 已登录阿里云镜像仓库

```bash
docker login registry.cn-zhangjiakou.aliyuncs.com
```

### 构建命令

**所有镜像必须指定 `--platform linux/amd64`**，因为本地 macOS 是 arm64 架构，远程服务器是 x86_64。

#### 1. Angular 前端镜像

```bash
# 在项目根目录执行
docker buildx build --platform linux/amd64 \
  -f angular/Dockerfile.prod \
  -t registry.cn-zhangjiakou.aliyuncs.com/myelixir/knowledgehub-angular:latest \
  --push \
  angular/
```

构建上下文为 `angular/` 目录。Dockerfile.prod 会：
- 用 `node:20-alpine` 编译 Angular 项目
- 用 `nginx:alpine` 托管静态文件
- 内置默认的 `nginx.conf` 和 `dynamic-env.json`（运行时被挂载覆盖）

#### 2. API 后端镜像

```bash
# 在项目根目录执行
docker buildx build --platform linux/amd64 \
  -f src/KnowledgeHub.HttpApi.Host/Dockerfile.prod \
  -t registry.cn-zhangjiakou.aliyuncs.com/myelixir/knowledgehub-api:latest \
  --push \
  .
```

构建上下文为项目根目录 `.`。Dockerfile.prod 会：
- 安装 Java 17（用于 Apache POI 文档解析）
- 编译 .NET 项目
- 复制 `utils/pdf-parser/document-parser.jar`

#### 3. DbMigrator 镜像

```bash
# 在项目根目录执行
docker buildx build --platform linux/amd64 \
  -f src/KnowledgeHub.DbMigrator/Dockerfile.prod \
  -t registry.cn-zhangjiakou.aliyuncs.com/myelixir/knowledgehub-db-migrator:latest \
  --push \
  .
```

### 一键构建全部

```bash
REGISTRY=registry.cn-zhangjiakou.aliyuncs.com/myelixir
TAG=latest

# Angular
docker buildx build --platform linux/amd64 \
  -f angular/Dockerfile.prod -t ${REGISTRY}/knowledgehub-angular:${TAG} --push angular/

# API
docker buildx build --platform linux/amd64 \
  -f src/KnowledgeHub.HttpApi.Host/Dockerfile.prod -t ${REGISTRY}/knowledgehub-api:${TAG} --push .

# DbMigrator
docker buildx build --platform linux/amd64 \
  -f src/KnowledgeHub.DbMigrator/Dockerfile.prod -t ${REGISTRY}/knowledgehub-db-migrator:${TAG} --push .
```

---

## 部署流程

### 首次部署

```bash
# 1. SSH 登录远程服务器
ssh ubuntu@119.45.170.4

# 2. 创建部署目录
mkdir -p ~/knowledgehub && cd ~/knowledgehub

# 3. 上传部署文件（从本地执行）
# 在本地 etc/docker/ 目录下：
scp docker-compose.yml nginx-proxy.conf dynamic-env.template.json deploy.sh \
    ubuntu@119.45.170.4:~/knowledgehub/

# 4. 创建环境变量文件
cp .env.example .env
vim .env
# 填写所有真实密钥

# 5. 赋予执行权限
chmod +x deploy.sh

# 6. 拉取镜像并启动
./deploy.sh up
```

### 更新部署

```bash
# 1. 本地构建并推送新镜像（参见"构建与推送镜像"）

# 2. 同步配置文件到远程
scp docker-compose.yml nginx-proxy.conf dynamic-env.template.json deploy.sh \
    ubuntu@119.45.170.4:~/knowledgehub/

# 3. SSH 登录远程
ssh ubuntu@119.45.170.4

# 4. 拉取新镜像并重启
cd ~/knowledgehub
./deploy.sh pull
./deploy.sh up

# 5. 如有数据库变更，执行迁移
./deploy.sh migrate
```

---

## 运维命令

`deploy.sh` 是远程服务器上的管理脚本，支持以下命令：

| 命令 | 说明 |
|------|------|
| `./deploy.sh up` | 启动所有服务（自动生成 dynamic-env.json、拉取镜像） |
| `./deploy.sh down` | 停止所有服务 |
| `./deploy.sh restart [svc]` | 重启服务（可选指定服务名） |
| `./deploy.sh migrate` | 执行数据库迁移 |
| `./deploy.sh pull` | 拉取最新镜像 |
| `./deploy.sh logs [svc]` | 查看日志（默认全部，可指定服务） |
| `./deploy.sh status` | 查看容器和健康检查状态 |
| `./deploy.sh gen-env` | 生成 dynamic-env.json 并输出内容 |

### 服务名列表

| 服务名 | 说明 |
|--------|------|
| `knowledgehub-angular` | Angular 前端 |
| `knowledgehub-api` | 后端 API |
| `meilisearch` | 搜索引擎 |
| `postgres` | 数据库 |
| `redis` | 缓存 |
| `db-migrator` | 数据库迁移（仅 migrate 时运行） |

### 示例

```bash
# 查看所有容器状态
./deploy.sh status

# 查看 API 日志
./deploy.sh logs knowledgehub-api

# 重启 API 服务
./deploy.sh restart knowledgehub-api

# 执行数据库迁移
./deploy.sh migrate
```

---

## URL 路由说明

所有外部访问通过 80 端口（Nginx 代理），以下是路由规则：

| URL 路径 | 目标 | 说明 |
|----------|------|------|
| `/` | Angular 静态文件 | 前端页面 |
| `/getEnvConfig` | `dynamic-env.json` | Angular 运行时配置 |
| `/api/*` | `http://knowledgehub-api:44354` | API 接口（上传限制 200MB） |
| `/connect/*` | `http://knowledgehub-api:44354` | OpenIddict（登录、Token） |
| `/.well-known/*` | `http://knowledgehub-api:44354` | OIDC 配置发现 |
| `/health-status` | `http://knowledgehub-api:44354` | 健康检查 |

### dynamic-env.json 的作用

Angular 应用启动时通过 `/getEnvConfig` 获取运行时配置，包含：
- OAuth 配置（issuer、clientId、redirectUri）
- API 基础 URL
- 应用名称

该文件由 `deploy.sh` 从 `dynamic-env.template.json` 生成，自动替换 `${PUBLIC_URL}` 变量。

---

## 故障排查

### 1. Angular 容器报 `exec format error`

**原因**：镜像架构不匹配。本地 macOS (arm64) 构建的镜像无法在 x86_64 服务器运行。

**解决**：构建时指定 `--platform linux/amd64`：

```bash
docker buildx build --platform linux/amd64 \
  -f angular/Dockerfile.prod \
  -t registry.cn-zhangjiakou.aliyuncs.com/myelixir/knowledgehub-angular:latest \
  --push angular/
```

### 2. Meilisearch 报版本不兼容

**原因**：数据目录由更高版本的 Meilisearch 创建，与当前镜像版本不兼容。

**解决**：清除数据目录并重建索引：

```bash
cd ~/knowledgehub
docker-compose down
sudo rm -rf meilisearch_data/*
docker-compose up -d meilisearch
# 然后通过 API 触发索引重建
```

### 3. OAuth 登录失败

**排查步骤**：

```bash
# 1. 检查 OIDC 发现端点
curl http://119.45.170.4/.well-known/openid-configuration

# 2. 确认 dynamic-env.json 中的 issuer 正确
curl http://119.45.170.4/getEnvConfig

# 3. 检查 API 日志
./deploy.sh logs knowledgehub-api
```

常见问题：
- `issuer` 不匹配：确保 `dynamic-env.json` 和 API 的 `App__SelfUrl` 一致
- CORS 错误：检查 API 的 `App__CorsOrigins` 是否包含 `PUBLIC_URL`

### 4. 数据库连接失败

```bash
# 检查 PostgreSQL 容器状态
docker ps | grep postgres

# 查看数据库日志
./deploy.sh logs postgres

# 验证连接
docker exec -it postgres psql -U postgres -d KnowledgeHub -c "SELECT 1"
```

### 5. docker-compose v1 兼容性

远程服务器使用 docker-compose v1.29.2，不支持 `docker compose --env-file`。`deploy.sh` 已做兼容处理：

```bash
# deploy.sh 自动检测版本
# v2: docker compose --env-file .env -f docker-compose.yml up -d
# v1: source .env && docker-compose -f docker-compose.yml up -d
```

手动操作时需要先加载环境变量：

```bash
cd ~/knowledgehub
set -a && source .env && set +a
docker-compose -f docker-compose.yml up -d
```

### 6. 镜像拉取失败

```bash
# 确认已登录镜像仓库
docker login registry.cn-zhangjiakou.aliyuncs.com

# 手动拉取测试
docker pull registry.cn-zhangjiakou.aliyuncs.com/myelixir/knowledgehub-api:latest
```
