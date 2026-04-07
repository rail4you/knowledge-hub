# KnowledgeHub 生产环境部署问题总结

## 一、问题概述

部署过程中遇到的核心问题是：**POST 请求失败（新建资源分类/资源保存失败）**。

表现为浏览器返回 500 错误页面，日志显示 `AbpAutoValidateAntiforgeryTokenAuthorizationFilter` 验证失败。

---

## 二、根本原因分析

### 1. XSRF/Anti-Forgery Token 问题

ABP 框架默认启用防伪令牌（XSRF/Anti-Forgery）验证。浏览器的 Cookie 中设置了 `SameSite=None`，但由于应用运行在 **HTTP** 协议下，浏览器拒绝接受没有 `Secure` 属性的 Cookie。

**错误日志：**
```
因为设置了"SameSite=None"属性，但缺少"Secure"属性，已拒绝 Cookie "XSRF-TOKEN"
```

**影响：** 导致 POST 请求被 ABP 的 `AbpAutoValidateAntifyssTokenAuthorizationFilter` 拦截，返回 302 重定向，最终显示 400 错误页面。

### 2. Issuer 地址不一致问题

**问题描述：** ABP 的 OIDC discovery document 返回的 `issuer` 与 Angular 配置的 `issuer` 必须完全匹配，否则 token 验证失败。

**实际情况：**
- API 配置 `AuthServer__Authority=http://119.45.170.4`（无尾部斜杠）
- 但 ABP 框架会自动在 issuer 后面添加斜杠，discovery 返回 `http://119.45.170.4/`（有尾部斜杠）
- Angular dynamic-env.json 配置 `issuer: "http://119.45.170.4"`（无尾部斜杠）
- **结果：** issuer 不匹配，token 验证失败

**错误日志：**
```
invalid_token: The issuer associated to the specified token is not valid
```

---

## 三、解决方案

### 方案：启用 HTTPS

**必要性：**
1. 解决 XSRF Cookie 的 `Secure` 属性问题（现代浏览器要求 SameSite=None 必须配合 Secure）
2. 解决 issuer 地址规范化问题（HTTPS 环境下 ABP 的 issuer 行为更一致）
3. 生产环境安全标准要求

### 实施步骤

#### 1. 生成自签名 SSL 证书（或使用正式证书）

```bash
# 在远程服务器执行
cd /home/ubuntu/knowledgehub/certs
openssl req -x509 -newkey rsa:2048 -keyout localhost.key -out localhost.crt -days 365 -nodes -subj "/CN=119.45.170.4"
openssl pkcs12 -export -out localhost.pfx -inkey localhost.key -in localhost.crt -passout pass:您的证书密码
```

#### 2. 修改 docker-compose.yml

**关键配置点：**

```yaml
knowledgehub-angular:
  ports:
    - "80:80"
    - "443:443"          # 新增 HTTPS 端口
  volumes:
    - ./certs:/etc/nginx/certs:ro   # 挂载证书

knowledgehub-api:
  environment:
    - App__SelfUrl=https://119.45.170.4          # HTTPS
    - App__AngularUrl=https://119.45.170.4       # HTTPS
    - App__CorsOrigins=https://119.45.170.4      # HTTPS
    - App__RedirectAllowedUrls=https://119.45.170.4  # HTTPS
    - AuthServer__Authority=https://119.45.170.4/  # HTTPS + 尾部斜杠
```

#### 3. 修改 nginx-proxy.conf

```nginx
server {
    listen       80;
    listen  [::]:80;
    server_name  _;
    return 301 https://$host$request_uri;  # HTTP 重定向到 HTTPS
}

server {
    listen       443 ssl http2;
    listen  [::]:443 ssl http2;
    server_name  _;

    ssl_certificate /etc/nginx/certs/localhost.crt;
    ssl_certificate_key /etc/nginx/certs/localhost.key;

    # API 反向代理（内部仍用 HTTP）
    location /api/ {
        proxy_pass http://knowledgehub-api:44354;  # 内部用 HTTP
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        client_max_body_size 200m;
    }

    # OpenIddict Connect 端点
    location /connect/ {
        proxy_pass http://knowledgehub-api:44354;
        # ...
    }

    # OIDC 发现文档
    location /.well-known/ {
        proxy_pass http://knowledgehub-api:44354;
        # ...
    }
}
```

#### 4. 修改 dynamic-env.json（Angular 运行时配置）

```json
{
  "production": true,
  "application": {
    "baseUrl": "https://119.45.170.4",
    "name": "易课通知识库系统"
  },
  "oAuthConfig": {
    "issuer": "https://119.45.170.4/",
    "redirectUri": "https://119.45.170.4",
    "clientId": "KnowledgeHub_App",
    "responseType": "code",
    "scope": "offline_access openid profile email phone KnowledgeHub"
  },
  "apis": {
    "default": {
      "url": "https://119.45.170.4",
      "rootNamespace": "KnowledgeHub"
    },
    "AbpAccountPublic": {
      "url": "https://119.45.170.4",
      "rootNamespace": "AbpAccountPublic"
    }
  }
}
```

#### 5. 修改 .env 文件

```bash
# 生产环境公开访问地址
PUBLIC_URL=https://119.45.170.4
```

---

## 四、配置清单

### 4.1 地址格式规范

| 配置项 | 值 | 说明 |
|--------|-----|------|
| `App__SelfUrl` | `https://119.45.170.4` | 应用公开 URL |
| `App__AngularUrl` | `https://119.45.170.4` | Angular 前端 URL |
| `App__CorsOrigins` | `https://119.45.170.4` | CORS 允许的源 |
| `AuthServer__Authority` | `https://119.45.170.4/` | **必须带尾部斜杠** |
| `dynamic-env.json issuer` | `https://119.45.170.4/` | **必须带尾部斜杠** |
| `dynamic-env.json baseUrl` | `https://119.45.170.4` | 不带尾部斜杠 |

### 4.2 Issuer 地址匹配规则

ABP 框架行为：
- OpenIddict 的 discovery document 中 `issuer` 字段**始终带尾部斜杠**
- Token 中包含的 `iss` claim 与 `Authority` 配置相关
- Angular 的 `oAuthConfig.issuer` 必须与 discovery 返回的 `issuer` **完全一致**

**匹配矩阵：**

| API AuthServer__Authority | Discovery issuer | Angular issuer | 是否匹配 |
|---------------------------|-------------------|----------------|----------|
| `http://119.45.170.4` | `http://119.45.170.4/` | `http://119.45.170.4` | ❌ 不匹配 |
| `http://119.45.170.4/` | `http://119.45.170.4/` | `http://119.45.170.4/` | ✅ 匹配 |
| `https://119.45.170.4/` | `https://119.45.170.4/` | `https://119.45.170.4/` | ✅ 匹配 |

---

## 五、其他部署注意事项

### 5.1 Nginx 代理配置要点

1. **HTTP 重定向到 HTTPS**
   ```nginx
   server {
       listen 80;
       return 301 https://$host$request_uri;
   }
   ```

2. **API 代理使用 HTTP 内部通信**
   - Nginx 容器与 API 容器在内部网络通过 HTTP 通信
   - 对外暴露的是 HTTPS

3. **必要的请求头转发**
   ```nginx
   proxy_set_header X-Forwarded-Proto $scheme;
   proxy_set_header X-Real-IP $remote_addr;
   proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
   ```

### 5.2 Docker 网络

确保所有容器在同一个网络中：
```yaml
networks:
  abp-network:
    driver: bridge
```

### 5.3 健康检查端口

API 的健康检查 URL 应该使用容器内部地址：
```yaml
App__HealthCheckUrl: http://knowledgehub-api:44354/health-status
```

---

## 六、故障排查

### 6.1 POST 请求返回 302 或 400

**检查项：**
1. 浏览器控制台是否有 XSRF Cookie 警告
2. 是否使用了 HTTPS
3. API 日志是否有 `AbpAutoValidateAntiforgeryTokenAuthorizationFilter` 错误

### 6.2 Token 验证失败 (invalid_token)

**检查项：**
1. Discovery document 的 issuer：`/.well-known/openid-configuration`
2. Angular dynamic-env.json 的 issuer 配置
3. 两者是否完全一致（包括尾部斜杠）

### 6.3 容器启动失败 (端口占用)

**检查项：**
```bash
# 检查端口占用
ss -tlnp | grep 44354

# 检查容器状态
docker ps -a | grep knowledgehub
```

### 6.4 Nginx 502 Bad Gateway

**检查项：**
1. API 容器是否正常运行
2. Nginx 日志：`docker logs knowledgehub-angular`
3. proxy_pass 配置是否正确（应使用 `http://` 而非 `https://`）

---

## 七、最终配置汇总

### 7.1 docker-compose.yml 关键环境变量

```yaml
knowledgehub-api:
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ASPNETCORE_URLS=http://+:44354
    - ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
    - App__SelfUrl=https://119.45.170.4
    - App__HealthCheckUrl=http://knowledgehub-api:44354/health-status
    - App__AngularUrl=https://119.45.170.4
    - App__CorsOrigins=https://119.45.170.4
    - App__RedirectAllowedUrls=https://119.45.170.4
    - AuthServer__Authority=https://119.45.170.4/
    - AuthServer__RequireHttpsMetadata=false
```

### 7.2 Nginx 配置要点

- HTTP 监听 80 端口，重定向到 HTTPS
- HTTPS 监听 443 端口
- API 代理使用 `proxy_pass http://knowledgehub-api:44354`
- 正确设置 SSL 证书路径

### 7.3 验证命令

```bash
# 验证 HTTPS 和 issuer
curl -k https://119.45.170.4/.well-known/openid-configuration | jq .issuer

# 验证登录
curl -k -X POST https://119.45.170.4/connect/token \
  -d "grant_type=password&username=admin&password=1q2w3E*&client_id=KnowledgeHub_App&scope=offline_access openid profile email phone KnowledgeHub"

# 验证创建分类
TOKEN=$(curl -k -s -X POST https://119.45.170.4/connect/token \
  -d "grant_type=password&username=admin&password=1q2w3E*&client_id=KnowledgeHub_App&scope=offline_access openid profile email phone KnowledgeHub" | jq -r .access_token)

curl -k -X POST https://119.45.170.4/api/app/resource/category \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"测试分类","parentId":null}'
```

---

## 八、总结

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| POST 请求失败 | XSRF Cookie 的 Secure 属性缺失 | 启用 HTTPS |
| Token 验证失败 | issuer 地址不匹配 | 统一 issuer 格式（带尾部斜杠） |
| API 容器启动失败 | 端口占用或证书配置错误 | 检查端口、简化配置 |
| Nginx 502 | proxy_pass 协议错误 | 使用 http:// 而非 https:// |

---

## 九、2026-04-07 部署记录

### 9.1 韶署新功能

本次部署新增了以下功能到远程服务器：
- 职业指导规划（Career Guidance）组件
- AI 敩案教案生成（Lesson Plan）
- AI 聊天服务更新
- 资源库管理更新（PageIndex-based AI 生成）
- Python PageIndex CLI 集成（含 litellm, pymupdf, python-docx 等）

### 9.2 部署中遇到的问题

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| Docker Hub 连接超时/重置 | GFW 导致无法访问 Docker Hub | 配置 `~/.orbstack/config/docker.json` 镜像加速器 |
| buildx docker-container driver 不继承镜像配置 | buildx 的 docker-container driver 使用独立 daemon | 使用 OrbStack 原生 driver（`orbstack`），或先手动 `docker pull` 基础镜像再构建 |
| `eclipse-temurin:17-jdk` 镜像加速器找不到 | 部分镜像加速器未同步该镜像 | 先 `docker pull --platform linux/amd64 eclipse-temurin:17-jdk` 手动拉取 |
| `deploy.sh` CRLF 行尾符 | scp 从 macOS 传输时保留了 CRLF | 远程执行 `sed -i 's/\r$//' deploy.sh` |
| 远程 Docker Hub 拉取超时 | 远程服务器同样无法访问 Docker Hub | 基础镜像已有本地缓存，跳过拉取即可 |
| DbMigrator 报 `libgssapi_krb5.so.2` 警告 | Kerberos 库未安装 | 不影响数据库迁移，可忽略 |
| **scp 覆盖 HTTPS 配置** | `scp` 同步时将本地的纯 HTTP `nginx-proxy.conf` 覆盖了远程的 HTTPS 版本 | 将本地 `nginx-proxy.conf` 更新为包含 HTTPS 443 配置，避免再次被覆盖 |

### 9.3 部署注意事项

1. **本地 `nginx-proxy.conf` 已更新为 HTTPS 版本**（HTTP 80 重定向到 HTTPS 443），以后 `scp` 同步不会再丢失 HTTPS 配置
2. **`docker-compose.yml` 已添加 443 端口映射和证书挂载**（`./certs:/etc/nginx/certs:ro`）
3. **`.env` 中 `PUBLIC_URL` 需要设为 `https://119.45.170.4`**（当前远程仍为 `http://`，需按需更新）
4. **镜像加速器配置**：本地 `~/.orbstack/config/docker.json` 已添加镜像加速器，构建时优先使用 OrbStack 原生 driver
5. **远程服务器证书文件**位于 `~/knowledgehub/certs/` 目录