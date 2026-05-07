# KnowledgeHub 本地开发环境配置

## 目录

- [环境要求](#环境要求)
- [首次配置](#首次配置)
- [服务管理](#服务管理)
- [开发工具](#开发工具)
- [常见问题](#常见问题)

---

## 环境要求

### 必需软件

| 软件 | 版本 | 说明 |
|------|------|------|
| .NET SDK | .NET 8+ | 后端 API 开发 |
| Node.js | 18+ | Angular 开发 |
| npm / yarn / pnpm | 最新 | 包管理工具 |
| PostgreSQL | 13+ | 数据库 |
| Meilisearch | 1.x | 搜索引擎 |
| Redis | 6+ | 缓存（可选） |

### macOS 安装命令

```bash
# 安装 .NET SDK
brew install dotnet

# 安装 Node.js
brew install node

# 安装 PostgreSQL
brew install postgresql@16
brew services start postgresql@16

# 安装 Meilisearch
brew install meilisearch
brew services start meilisearch

# 安装 Redis（可选）
brew install redis
brew services start redis
```

### Windows/Linux

参考各软件官方文档进行安装。

---

## 首次配置

### 1. 克隆代码

```bash
git clone <repository-url>
cd KnowledgeHub
```

### 2. 安装依赖

```bash
# 安装 ABP CLI 和库
dotnet tool restore
abp install-libs

# 安装前端依赖
cd angular
npm install
# 或使用 yarn: yarn install
# 或使用 pnpm: pnpm install
cd ..
```

### 3. 配置数据库

```bash
# 创建本地 PostgreSQL 数据库
createdb -h localhost -p 5433 -U postgres KnowledgeHub

# 或通过 psql
PGPASSWORD=postgres psql -h localhost -p 5433 -U postgres -c "CREATE DATABASE KnowledgeHub;"
```

### 4. 启动 Meilisearch

```bash
# macOS
brew services start meilisearch

# 或手动启动
meilisearch --master-key="KnowledgeHubMeiliSearch" --http_addr=127.0.0.1:7700
```

### 5. 配置环境变量

```bash
# 复制环境变量模板
cp src/KnowledgeHub.HttpApi.Host/appsettings.json src/KnowledgeHub.HttpApi.Host/appsettings.Development.json
```

编辑 `appsettings.Development.json`：

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5433;Database=KnowledgeHub;Username=postgres;Password=postgres"
  },
  "App": {
    "SelfUrl": "https://localhost:44305"
  },
  "Redis": {
    "IsEnabled": false,
    "Configuration": "localhost:6379"
  },
  "MeiliSearch": {
    "Url": "http://localhost:7700",
    "MasterKey": "KnowledgeHubMeiliSearch"
  }
}
```

### 6. 生成 Angular API 代理

```bash
cd angular
abp generate-proxy -t ng
cd ..
```

### 7. 数据库迁移

```bash
# 使用 DbMigrator
dotnet run --project src/KnowledgeHub.DbMigrator

# 或使用 dev.sh
./dev.sh migrate
```

### 8. 启动开发服务

```bash
# 启动所有服务
./dev.sh start

# 或单独启动
./dev.sh start api        # 仅 API
./dev.sh start angular    # 仅 Angular
./dev.sh start meilisearch # 仅 Meilisearch
```

---

## 服务管理

### dev.sh 命令

| 命令 | 说明 |
|------|------|
| `./dev.sh start` | 启动所有服务 |
| `./dev.sh start api` | 仅启动 API |
| `./dev.sh start angular` | 仅启动 Angular |
| `./dev.sh start meilisearch` | 仅启动 Meilisearch |
| `./dev.sh stop` | 停止所有服务 |
| `./dev.sh restart` | 重启所有服务 |
| `./dev.sh status` | 查看服务状态 |
| `./dev.sh log api` | 查看 API 日志 |
| `./dev.sh log angular` | 查看 Angular 日志 |
| `./dev.sh tail api` | 实时查看 API 日志 |
| `./dev.sh migrate` | 执行数据库迁移 |

### 默认端口

| 服务 | URL | 说明 |
|------|-----|------|
| API | https://localhost:44305 | 后端接口 |
| Swagger | https://localhost:44305/swagger | API 文档 |
| Angular | http://localhost:4200 | 前端开发服务器 |
| Meilisearch | http://localhost:7700 | 搜索引擎 |
| PostgreSQL | localhost:5433 | 数据库 |
| Redis | localhost:6379 | 缓存 |

### 默认账号

| 用户名 | 密码 |
|--------|------|
| admin | 1q2w3E* |

---

## 开发工具

### API 开发

```bash
# 启动 API（带热重载监视）
cd src/KnowledgeHub.HttpApi.Host
dotnet watch run --no-hot-reload

# 或使用 dev.sh
./dev.sh start api
```

### Angular 开发

```bash
cd angular
ng serve
# 访问 http://localhost:4200
```

### 数据库操作

```bash
# 创建新迁移
dotnet ef migrations add <MigrationName> \
  --project src/KnowledgeHub.EntityFrameworkCore \
  --startup-project src/KnowledgeHub.HttpApi.Host

# 应用迁移
./dev.sh migrate

# 查看迁移状态
dotnet ef database update --project src/KnowledgeHub.EntityFrameworkCore --startup-project src/KnowledgeHub.HttpApi.Host
```

### 重新生成 API 代理

当后端接口变化时，重新生成 Angular 代理：

```bash
cd angular
abp generate-proxy -t ng
```

### 构建生产镜像

```bash
# Angular
docker build -f angular/Dockerfile.prod -t knowledgehub-angular:latest angular/

# API
docker build -f src/KnowledgeHub.HttpApi.Host/Dockerfile.prod -t knowledgehub-api:latest .

# DbMigrator
docker build -f src/KnowledgeHub.DbMigrator/Dockerfile.prod -t knowledgehub-db-migrator:latest .
```

---

## 常见问题

### 1. API 启动失败

**问题**: 端口 44305 被占用

```bash
# 查看占用端口的进程
lsof -ti:44305

# 杀死进程
lsof -ti:44305 | xargs kill -9

# 或重启服务
./dev.sh restart api
```

**问题**: HTTPS 证书错误

```bash
# 检查证书
dotnet dev-certs https --check

# 重新生成证书
dotnet dev-certs https --trust
```

### 2. 数据库连接失败

```bash
# 检查 PostgreSQL 是否运行
brew services list | grep postgresql

# 启动 PostgreSQL
brew services start postgresql@16

# 测试连接
PGPASSWORD=postgres psql -h localhost -p 5433 -U postgres -d KnowledgeHub -c "SELECT 1;"
```

### 3. Meilisearch 连接失败

```bash
# 检查 Meilisearch 是否运行
curl http://localhost:7700/health

# 启动 Meilisearch
meilisearch --master-key="KnowledgeHubMeiliSearch"
```

### 4. 前端构建失败

```bash
# 清除缓存
cd angular
rm -rf node_modules/.cache
npm cache clean --force

# 重新安装依赖
rm -rf node_modules
npm install
```

### 5. 迁移文件未生效

```bash
# 使用完整编译再迁移
dotnet run --project src/KnowledgeHub.DbMigrator
```

> 注意: `./dev.sh migrate` 使用 `--no-build` 参数，只运行已编译的 DbMigrator。如果刚创建了新迁移文件，需要完整编译后再迁移。

---

## 文件清理说明

本项目已从 Git 中移除以下大文件以优化仓库大小：

| 文件 | 大小 | 如何获取 |
|------|------|----------|
| `document-parser.jar` | ~38 MB | 运行 `src/KnowledgeHub.HttpApi.Host/utils/pdf-parser/build.sh` 构建 |
| `eng.traineddata` | ~5 MB | 从 Tesseract 下载或构建 document-parser 时自动下载 |
| `meilisearch` | ~122 MB | `brew install meilisearch` |
| `data.ms/` | ~11 MB | Meilisearch 运行后自动生成 |
| `node_modules/` | ~900 MB | `npm install` 安装 |

克隆仓库后，运行以下命令恢复：

```bash
# 安装前端依赖
cd angular && npm install && cd ..

# 安装后端依赖
dotnet restore

# 构建 PDF 解析器
./src/KnowledgeHub.HttpApi.Host/utils/pdf-parser/build.sh
```
