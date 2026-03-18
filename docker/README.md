# KnowledgeHub Docker Deployment

Docker 部署方案，包含：
- PostgreSQL 数据库
- ABP .NET API
- Angular 前端 (Nginx)

## 快速开始

### 方式一：直接运行 (需要 Docker)

```bash
cd docker
docker compose up -d
```

访问 http://localhost

### 方式二：构建镜像后部署

```bash
# 1. 构建镜像
cd docker
./build.sh

# 2. 导出镜像为 tar 文件
./export-images.sh
```

### 方式三：离线部署到远程服务器

```bash
# 在构建机器上：
cd docker
./build.sh
./export-images.sh
# 将 knowledgehub-images 文件夹传输到远程服务器

# 在远程服务器上：
cd knowledgehub-images
docker load -i postgres-*.tar
docker load -i knowledgehub-api-*.tar
docker load -i knowledgehub-web-*.tar

# 启动
cd docker
cp .env.example .env
# 编辑 .env 修改密码和URL
docker compose -f docker-compose.prod.yml up -d
```

## 环境变量

| 变量 | 默认值 | 说明 |
|------|--------|------|
| DB_PASSWORD | KnowledgeHub2024! | 数据库密码 |
| API_URL | http://localhost | API 地址 |
| WEB_URL | http://localhost | Web 地址 |

## 端口

- 80: Angular 前端
- 5433: PostgreSQL (可选暴露)
- 5000: API (可选暴露)

## 常用命令

```bash
# 查看日志
docker compose logs -f

# 停止
docker compose down

# 重启
docker compose restart

# 查看状态
docker compose ps
```
