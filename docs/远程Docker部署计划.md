# KnowledgeHub 远程 Docker 部署计划

## 一、概述

本文档描述如何将 KnowledgeHub 项目通过 Docker 部署到远程服务器（119.45.170.4），包含以下核心需求：

1. 添加 Meilisearch 依赖及数据卷映射
2. 添加 Litparse 依赖（LibreOffice）用于解析 Office 文档
3. 实现本地打包镜像 → SSH 同步 → 远程构建服务的自动化部署流程

---

## 二、目标环境分析

### 远程服务器信息
- **IP**: 119.45.170.4
- **SSH 端口**: 22
- **用户名**: ubuntu
- **密码**: `5[4j:nfqE)S~s;t$`
- **开放端口**: 80（HTTP）、443（HTTPS）、其他自定义端口

### 本地开发环境
- **平台**: macOS (darwin)
- **工作目录**: `/Users/bai/projects/KnowledgeHub`

---

## 三、Docker 部署架构

### 3.1 服务组件

| 服务 | 镜像 | 端口 | 说明 |
|------|------|------|------|
| knowledgehub-angular | mycompany/knowledgehub-angular:latest | 4200 → 80 | Angular 前端 |
| knowledgehub-api | mycompany/knowledgehub-api:latest | 44305 → 443 | ASP.NET Core API |
| knowledgehub-db-migrator | mycompany/knowledgehub-db-migrator:latest | - | 数据库迁移（一次性） |
| postgres | postgres:16-alpine | 5432 | PostgreSQL 数据库 |
| redis | redis:alpine | 6379 | Redis 缓存 |
| meilisearch | getmeili/meilisearch:latest | 7700 | 全文搜索引擎 |
| libreoffice | 在 API 容器内安装 | - | Office 文档解析（litparse 依赖） |

### 3.2 卷映射

| 宿主机路径 | 容器内路径 | 说明 |
|-----------|-----------|------|
| ./certs | /root/certificate | HTTPS 证书 |
| ./dynamic-env.json | /usr/share/nginx/html/dynamic-env.json | Angular 环境变量 |
| ./meilisearch_data | /meili_data | Meilisearch 数据持久化 |
| ./uploads_data | /app/uploads | 用户上传文件 |
| （主机安装 libreoffice） | /usr/bin/soffice | LibreOffice 可执行文件 |

### 3.3 环境变量

#### API 服务环境变量
```yaml
environment:
  - ASPNETCORE_URLS=https://+:443;http://+:80
  - App__SelfUrl=https://your-domain.com
  - App__AngularUrl=http://your-domain.com
  - App__CorsOrigins=http://your-domain.com
  - ConnectionStrings__Default=Host=postgres;Port=5432;Database=KnowledgeHub;Username=postgres;Password=xxx
  - Redis__Configuration=redis
  - Meilisearch__Host=http://meilisearch:7700
  - Meilisearch__ApiKey=your-meilisearch-master-key
  - Meilisearch__IndexName=documents
  - Qwen__ApiKey=your-qwen-api-key
  - Qwen__BaseUrl=https://dashscope.aliyuncs.com/compatible-mode/v1
  - Qwen__Model=qwen-plus
```

#### Meilisearch 环境变量
```yaml
environment:
  - MEILI_MASTER_KEY=your-meilisearch-master-key
  - MEILI_ENV=production
  - MEILI_DB_PATH=/meili_data
```

---

## 四、Docker 配置修改

### 4.1 修改 docker-compose.yml

需要添加：
1. Meilisearch 服务定义
2. API 服务中添加 Meilisearch 和 LibreOffice 相关配置
3. 添加 meilisearch_data 卷

### 4.2 修改 Dockerfile.local (API)

需要添加：
1. 安装 LibreOffice 和 litparse CLI
2. 设置 litparse 的 PATH

### 4.3 环境变量同步策略

使用 `docker-compose.override.yml` 或环境变量文件：
- 本地开发：`docker-compose.yml` + `docker-compose.dev.yml`
- 远程部署：`docker-compose.yml` + `docker-compose.prod.yml`

---

## 五、部署脚本设计

### 5.1 脚本命名

| 脚本 | 用途 | 执行频率 |
|------|------|----------|
| `deploy-to-remote.sh` | 部署项目代码（Angular/API） | 每次代码更新时 |
| `package-database-images.sh` | 打包数据库镜像 | 只需执行一次或偶尔更新 |

### 5.2 deploy-to-remote.sh 功能

1. **本地构建阶段**
   - 构建 Angular 生产镜像
   - 构建 API (.NET) 生产镜像
   - 构建 DbMigrator 镜像
   - 打包镜像为 tar 文件

2. **SSH 同步阶段**
   - 通过 SSH 传输镜像 tar 文件到远程服务器
   - 使用 sshpass 实现密码登录

3. **远程部署阶段**
   - 在远程服务器加载镜像
   - 使用 docker-compose 启动服务
   - 执行数据库迁移

### 5.3 package-database-images.sh 功能

1. **拉取最新镜像**
   - PostgreSQL 16 Alpine
   - Redis Alpine
   - Meilisearch v1.6

2. **打包传输**
   - 打包为 tar 文件
   - 通过 SSH 传输到远程服务器
   - 加载到远程 Docker

### 5.4 部署流程

```bash
# 首次部署时（只需执行一次）
./package-database-images.sh

# 每次代码更新时
./deploy-to-remote.sh --env production
```

### 5.5 脚本参数

**deploy-to-remote.sh:**
```bash
./deploy-to-remote.sh [OPTIONS]

OPTIONS:
  --skip-build      跳过本地构建，直接使用已有镜像
  --skip-db-migrate 跳过数据库迁移
  --env ENV         指定环境 (production|development)
  --dry-run         演练模式，不实际执行
  --help            显示帮助
```

**package-database-images.sh:**
```bash
./package-database-images.sh
# 无需参数，直接执行
```

---

## 六、实施步骤

### Step 1: 创建 docker-compose.prod.yml
创建远程生产环境配置文件

### Step 2: 修改 Dockerfile.local (API)
添加 LibreOffice 安装

### Step 3: 创建部署脚本
实现本地打包 → SSH 同步 → 远程部署

### Step 4: 配置 SSH 免密码登录
使用 sshpass 或 SSH Key

### Step 5: 测试部署流程
验证各服务正常运行

---

## 七、API Key 和敏感信息管理

### 7.1 敏感信息清单

| 配置项 | 说明 | 来源 |
|--------|------|------|
| Meilisearch Master Key | Meilisearch 认证 | `dev.sh` 中定义 |
| Qwen API Key | 阿里通义千问 API | `appsettings.json` |
| PostgreSQL Password | 数据库密码 | 需单独设置 |
| JWT Certificate Password | JWT 证书密码 | `appsettings.json` |

### 7.2 管理策略

1. **开发环境**: 使用 `.env` 文件，通过 `docker-compose.dev.yml` 加载
2. **生产环境**: 使用 `docker-compose.prod.yml`，敏感值通过环境变量注入
3. **绝对禁止**: 将包含真实密钥的文件提交到 Git

---

## 八、远程服务器准备清单

在远程服务器执行：

```bash
# 1. 安装 Docker 和 Docker Compose
curl -fsSL https://get.docker.com | sh
sudo apt install docker-compose -y

# 2. 安装 SSH 客户端（本地）
brew install sshpass  # macOS

# 3. 创建必要目录
mkdir -p ~/knowledgehub/{certs,meilisearch_data,uploads}

# 4. 安装 LibreOffice（用于文档解析）
sudo apt update
sudo apt install libreoffice -y

# 5. 安装 lit CLI 工具（用于解析 PDF/DOCX/PPTX）
# lit 是 liteparse 项目提供的 CLI 工具
# 下载地址: https://github.com/nickthathasnotbeenusedyet/litparse/releases
wget https://github.com/nickthathasnotbeenusedyet/litparse/releases/latest/download/lit-linux-x64.tar.gz
tar -xzf lit-linux-x64.tar.gz
sudo mv lit /usr/local/bin/
sudo chmod +x /usr/local/bin/lit
rm lit-linux-x64.tar.gz

# 验证安装
lit --version

# 6. 配置防火墙（开放必要端口）
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 44305/tcp
```

---

## 九、部署流程详解

### 9.1 首次部署（数据库镜像）

数据库镜像（PostgreSQL、Redis、Meilisearch）不频繁更新，只需执行一次：

```bash
cd /Users/bai/projects/KnowledgeHub/etc/docker
./package-database-images.sh
```

### 9.2 每次代码更新部署

```bash
cd /Users/bai/projects/KnowledgeHub/etc/docker
./deploy-to-remote.sh --env production
```

### 9.3 手动分步部署（可选）

**构建项目镜像：**
```bash
# 构建 Angular
cd angular
docker build -f Dockerfile.local -t mycompany/knowledgehub-angular:latest .

# 构建 API
docker build -f src/KnowledgeHub.HttpApi.Host/Dockerfile.local \
  -t mycompany/knowledgehub-api:latest .

# 构建 DbMigrator
docker build -f src/KnowledgeHub.DbMigrator/Dockerfile.local \
  -t mycompany/knowledgehub-db-migrator:latest .
```

**打包并传输：**
```bash
# 打包项目镜像（不含数据库镜像）
docker save mycompany/knowledgehub-angular:latest \
  mycompany/knowledgehub-api:latest \
  mycompany/knowledgehub-db-migrator:latest \
  -o knowledgehub-images.tar

# SSH 传输
sshpass -p 'password' scp knowledgehub-images.tar \
  ubuntu@119.45.170.4:~/knowledgehub/
```

**远程部署：**
```bash
# SSH 远程执行
sshpass -p 'password' ssh ubuntu@119.45.170.4 << 'EOF'
  cd ~/knowledgehub
  
  # 加载镜像
  docker load -i knowledgehub-images.tar
  
  # 启动服务
  docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
  
  # 执行数据库迁移
  docker-compose -f docker-compose.yml -f docker-compose.prod.yml run --rm db-migrator
EOF
```

---

## 十、验证清单

部署完成后验证：

- [ ] Angular UI 可访问 (http://119.45.170.4)
- [ ] API Swagger 可访问 (https://119.45.170.4:44305/swagger)
- [ ] Meilisearch 可访问 (http://119.45.170.4:7700)
- [ ] 数据库连接正常
- [ ] 文档上传和索引功能正常
- [ ] Office 文档解析功能正常

---

## 十一、故障排查

### 常见问题

1. **镜像传输失败**: 检查网络连接和磁盘空间
2. **端口冲突**: 确认远程服务器端口未被占用
3. **数据库迁移失败**: 检查 PostgreSQL 连接和迁移脚本
4. **Meilisearch 连接失败**: 检查网络和服务健康状态
5. **镜像架构不匹配**: macOS (Apple Silicon) 构建的镜像是 arm64，远程服务器是 x86_64，需要使用 `package-database-images.sh` 重新拉取正确架构的镜像

### 架构问题排查

如果遇到 `exec format error` 或容器无法启动：

```bash
# 检查服务器架构
ssh ubuntu@119.45.170.4 uname -m

# 检查镜像架构
ssh ubuntu@119.45.170.4 docker inspect <image> --format '{{.Architecture}}'

# 解决方案：重新打包数据库镜像
./package-database-images.sh
```

### 日志查看

```bash
# 本地日志
docker-compose logs -f

# 远程日志
sshpass -p 'password' ssh ubuntu@119.45.170.4 \
  "docker-compose -f ~/knowledgehub/docker-compose.yml -f ~/knowledgehub/docker-compose.prod.yml logs -f"
```

---

## 十二、已知限制

### LibreOffice 安装限制

**问题**: 当前远程服务器（119.45.170.4）无法访问外网，导致 Docker 镜像无法从官方源拉取，LibreOffice 无法在容器内安装。

**影响**: Office 文档解析功能（litparse 依赖 LibreOffice）暂时不可用。

**解决方案**:

1. **方案一：配置国内镜像加速器**
   ```bash
   # 在远程服务器上配置
   sudo mkdir -p /etc/docker
   sudo tee /etc/docker/daemon.json << 'EOF'
   {
     "registry-mirrors": [
       "https://docker.mirrors.ustc.edu.cn",
       "https://hub-mirror.c.163.com",
       "https://mirror.baidubce.com"
     ]
   }
   EOF
   sudo systemctl restart docker
   ```

2. **方案二：使用 VPN 连接**
   - 配置 VPN 使远程服务器可以访问外网

3. **方案三：本地构建完整镜像**
   - 在本地（需配置为 linux/amd64 架构或使用 CI/CD）
   - 构建包含 LibreOffice 的 API 镜像
   - 通过 `docker save/load` 方式传输到远程服务器

**验证 LibreOffice 是否可用**:
```bash
ssh ubuntu@119.45.170.4
docker exec knowledgehub-api which soffice
docker exec knowledgehub-api soffice --version
```

---

## 十三、当前部署状态

**部署时间**: 2026-03-25

**已部署服务**:
- ✅ Angular UI (http://119.45.170.4)
- ✅ API (https://119.45.170.4:44354)
- ✅ PostgreSQL (5432)
- ✅ Redis (6379)
- ✅ Meilisearch (http://119.45.170.4:7700)
- ⚠️ LibreOffice (待解决外网访问问题)

---

## 十四、故障修复记录

### 14.1 SSL 证书问题 (ERR_CERT_COMMON_NAME_INVALID)

**问题描述**: 浏览器访问 https://119.45.170.4:44354 时报错 `ERR_CERT_COMMON_NAME_INVALID`，原因是证书 CN 为 localhost 而非服务器 IP。

**原因**: 远程服务器上的 SSL 证书是针对 localhost 生成的，Angular 访问时 IP 与证书不匹配。

**解决方案**:
```bash
# 在远程服务器上重新生成证书
cd /home/ubuntu/knowledgehub
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout certs/localhost.key \
  -out certs/localhost.crt \
  -subj "/CN=119.45.170.4" \
  -addext "subjectAltName=IP:119.45.170.4"
openssl pkcs12 -export -out certs/localhost.pfx \
  -inkey certs/localhost.key \
  -in certs/localhost.crt \
  -password pass:91f91912-5ab0-49df-8166-23377efaf3cc

# 重启 API 容器
docker stop knowledgehub-api && docker rm knowledgehub-api
docker run -d \
  --name knowledgehub-api \
  --hostname knowledgehub-api \
  --network knowledgehub_abp-network \
  -e ASPNETCORE_URLS="https://+:443;http://+:80" \
  -e Kestrel__Certificates__Default__Path=/app/localhost.pfx \
  -e Kestrel__Certificates__Default__Password=91f91912-5ab0-49df-8166-23377efaf3cc \
  -e AuthServer__Authority=https://119.45.170.4:44354 \
  -e App__CorsOrigins='http://119.45.170.4,https://119.45.170.4:44354' \
  -e ConnectionStrings__Default="Host=postgres;Port=5432;Database=KnowledgeHub;Username=postgres;Password=postgres" \
  -e Redis__Configuration=redis \
  -p 44354:443 \
  -v /home/ubuntu/knowledgehub/certs/localhost.pfx:/app/localhost.pfx:ro \
  -v /home/ubuntu/knowledgehub/uploads:/app/uploads \
  mycompany/knowledgehub-api:latest
```

**关键点**:
- 证书 CN 和 subjectAltName 都需要设置为服务器 IP
- `AuthServer__Authority` 必须与证书 IP 一致
- Chrome 会缓存证书错误，需要使用隐私模式或清除缓存

### 14.2 CORS 配置问题

**问题描述**: 浏览器控制台出现 `CORS error`，Angular 无法调用 API。

**原因**: API 服务未正确配置 `App:CorsOrigins`，导致 CORS 头未正确返回。

**解决方案**:
```bash
# 在 docker run 命令中添加环境变量
-e App__CorsOrigins='http://119.45.170.4,https://119.45.170.4:44354'
```

**注意**: ABP 框架使用 `__` (双下划线) 作为配置层级分隔符，所以 `App:CorsOrigins` 在环境变量中写作 `App__CorsOrigins`。

**验证 CORS 配置**:
```bash
curl -skv -X OPTIONS https://119.45.170.4:44354/.well-known/openid-configuration \
  -H "Origin: http://119.45.170.4" \
  -H "Access-Control-Request-Method: GET" \
  -H "Access-Control-Request-Headers: Authorization,Content-Type"
# 应返回 access-control-allow-origin: http://119.45.170.4
```

### 14.3 上传目录缺失 (/app/uploads)

**问题描述**: API 容器启动失败，错误信息 `System.IO.DirectoryNotFoundException: /app/uploads/`。

**原因**: API 代码在 `KnowledgeHubHttpApiHostModule.cs:317` 中引用了 `/app/uploads/` 目录，但容器镜像中不存在该目录。

**解决方案**:
```bash
# 在远程服务器创建上传目录
mkdir -p /home/ubuntu/knowledgehub/uploads

# 在 docker run 命令中添加卷挂载
-v /home/ubuntu/knowledgehub/uploads:/app/uploads
```

### 14.4 Angular dynamic-env.json 配置

**问题描述**: Angular 前端无法正确连接到 API。

**原因**: `dynamic-env.json` 中的 API 地址与实际部署不匹配。

**解决方案**:
更新远程服务器的 `/home/ubuntu/knowledgehub/dynamic-env.json`:
```json
{
  "production": true,
  "application": {
    "baseUrl": "http://119.45.170.4",
    "name": "KnowledgeHub"
  },
  "oAuthConfig": {
    "issuer": "https://119.45.170.4:44354/",
    "redirectUri": "http://119.45.170.4",
    "clientId": "KnowledgeHub_App",
    "responseType": "code",
    "scope": "offline_access openid profile email phone KnowledgeHub"
  },
  "apis": {
    "default": {
      "url": "https://119.45.170.4:44354",
      "rootNamespace": "KnowledgeHub"
    },
    "AbpAccountPublic": {
      "url": "https://119.45.170.4:44354",
      "rootNamespace": "AbpAccountPublic"
    }
  }
}
```

### 14.5 完整 API 容器启动命令

```bash
docker run -d \
  --name knowledgehub-api \
  --hostname knowledgehub-api \
  --network knowledgehub_abp-network \
  -e ASPNETCORE_URLS="https://+:443;http://+:80" \
  -e Kestrel__Certificates__Default__Path=/app/localhost.pfx \
  -e Kestrel__Certificates__Default__Password=91f91912-5ab0-49df-8166-23377efaf3cc \
  -e AuthServer__Authority=https://119.45.170.4:44354 \
  -e App__CorsOrigins="http://119.45.170.4,https://119.45.170.4:44354" \
  -e ConnectionStrings__Default="Host=postgres;Port=5432;Database=KnowledgeHub;Username=postgres;Password=postgres" \
  -e Redis__Configuration=redis \
  -p 44354:443 \
  -v /home/ubuntu/knowledgehub/certs/localhost.pfx:/app/localhost.pfx:ro \
  -v /home/ubuntu/knowledgehub/uploads:/app/uploads \
  mycompany/knowledgehub-api:latest
```

### 14.6 验证清单

部署完成后执行以下验证:

```bash
# 1. 验证容器运行状态
docker ps --filter name=knowledgehub-api

# 2. 验证 CORS 头
curl -skv -X OPTIONS https://119.45.170.4:44354/.well-known/openid-configuration \
  -H "Origin: http://119.45.170.4" \
  -H "Access-Control-Request-Method: GET"

# 3. 验证 API 健康
curl -sk https://119.45.170.4:44354/.well-known/openid-configuration | jq .issuer

# 4. 验证 Angular 可访问
curl -s http://119.45.170.4 | head -5
```
