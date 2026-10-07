# KnowledgeHub 部署到 iketong 服务器（47.99.245.49）

## 📋 概述

本文档记录 KnowledgeHub 在 `47.99.245.49`（域名 `https://www.iketong.com`）的部署。

**与生产服务器 119.45.170.4 的区别：**
- 不同服务器、不同域名
- 通过宝塔面板终结 HTTPS（容器内 Nginx 只听 4200）
- 端口完全隔离（避开 kg_edu 占用的 5432/4000）

## 🌐 站点信息

| 项目 | 值 |
|------|----|
| 公网 IP | 47.99.245.49 |
| 域名 | iketong.com / www.iketong.com |
| HTTPS 入口 | https://www.iketong.com |
| SSL 证书 | Let's Encrypt（自动续签，3 个月） |
| 宝塔面板 | https://bt.yikecheng.com:16375 |
| 部署目录 | ~/knowledgehub-iketong/ |
| SSH 用户 | root（免密已配置） |

## 🔌 端口规划

| 服务 | 容器端口 | 宿主机端口 | 说明 |
|------|---------|-----------|------|
| Angular | 80 | **4200** | 宝塔反代到此 |
| API | 44354 | 无 | 仅内部网络 |
| PostgreSQL | 5432 | **5433** | 避开 kg_edu 的 5432 |
| Redis | 6379 | **6380** | - |
| Meilisearch | 7700 | **7701** | - |
| LiteParse | 5707 | **15001** | 避开 kg_edu 的 15000 |
| Gotenberg | 3000 | **3001** | - |

## 🏗️ 部署架构

```
浏览器 (HTTPS)
    ↓
宝塔 Nginx (80/443, SSL 终结) — iketong.com.conf
    ↓ proxy_pass 127.0.0.1:4200
knowledgehub-angular (4200) — 容器内 Nginx 反代
    ↓ proxy_pass knowledgehub-api:44354
knowledgehub-api (44354)
    ↓
postgres (5433) / redis (6380) / meilisearch (7701) / liteparse (15001) / gotenberg (3001)
```

## 📁 远程目录结构

```
~/knowledgehub-iketong/
├── docker-compose.yml       # 容器编排（iketong 版本，端口已调整）
├── .env                     # 从 .env.iketong 复制
├── .env.iketong             # iketong 专用环境变量
├── nginx-proxy.conf         # 容器内 Nginx 配置（只听 80，宝塔终结 TLS）
├── dynamic-env.json         # Angular 运行时环境（deploy.sh 自动生成）
├── dynamic-env.template.json
├── deploy.sh                # 远程管理脚本
├── postgres_data/           # PostgreSQL 数据
├── redis_data/             # Redis 数据
├── meilisearch_data/       # 搜索引擎数据
├── uploads/                # 用户上传文件（从 119.45.170.4 迁移）
└── wasm-mirrors/           # 仿真实训 WASM 镜像（从 119.45.170.4 同步）
```

## 📝 本地新增文件

| 文件 | 用途 |
|------|------|
| `etc/docker/.env.iketong` | iketong 专用环境变量 |
| `etc/docker/docker-compose-iketong.yml` | iketong 专用 compose（端口已调整） |
| `etc/docker/nginx-proxy-iketong.conf` | iketong 专用容器 Nginx（只听 80） |
| `etc/docker/deploy-iketong.sh` | iketong 专用部署脚本（独立） |

## 🚀 部署命令

```bash
cd /Users/bai/projects/KnowledgeHub/etc/docker

# 完整部署（首次）
./deploy-iketong.sh all

# 仅更新前端
./deploy-iketong.sh angular

# 仅更新后端
./deploy-iketong.sh api

# 仅更新数据库迁移工具并执行迁移
./deploy-iketong.sh migrator

# 同步配置文件
./deploy-iketong.sh sync

# 宝塔站点 + SSL 操作
./deploy-iketong.sh bt-site     # 仅创建宝塔站点
./deploy-iketong.sh bt-ssl      # 仅申请/续签 SSL

# 数据迁移（从 119.45.170.4）
./deploy-iketong.sh migrate-data

# WASM 镜像同步
./deploy-iketong.sh wasm

# 状态检查
./deploy-iketong.sh status
```

## 🔑 与原 deploy-to-remote.sh 的对比

| 项 | deploy-to-remote.sh | deploy-iketong.sh |
|----|-------------------|---------------------|
| 目标 IP | 119.45.170.4 | 47.99.245.49 |
| SSH 用户 | ubuntu | root |
| 远程目录 | ~/knowledgehub | ~/knowledgehub-iketong |
| 域名 | IP 地址 | https://www.iketong.com |
| HTTPS 终结 | 容器内 Nginx | 宝塔面板 |
| 端口 | 默认 5432/80/443 | 5433/4200 |
| 配置模板 | `.env.production` | `.env.iketong` |
| Compose 文件 | `docker-compose.yml` | `docker-compose-iketong.yml` |
| Nginx 配置 | `nginx-proxy.conf` | `nginx-proxy-iketong.conf` |

**两个部署脚本完全独立，可并行使用**（生产服务器和 iketong 服务器互不影响）。

## 🔧 维护任务

### 申请 Let's Encrypt 证书

acme.sh 已安装在 `~/.acme.sh/`，并配置了自动续签 cron job。

如果需要手动续签：

```bash
ssh root@47.99.245.49
~/.acme.sh/acme.sh --renew -d iketong.com --force
cp ~/.acme.sh/iketong.com_ecc/fullchain.cer /www/server/panel/vhost/cert/iketong.com/fullchain.pem
cp ~/.acme.sh/iketong.com_ecc/iketong.com.key /www/server/panel/vhost/cert/iketong.com/privkey.pem
nginx -s reload
```

### 查看远程服务状态

```bash
ssh root@47.99.245.49 'cd ~/knowledgehub-iketong && ./deploy.sh status'
```

### 数据库迁移工具单独运行

```bash
ssh root@47.99.245.49 'cd ~/knowledgehub-iketong && ./deploy.sh migrate'
```

## 📊 数据迁移来源

部署时从生产服务器 `119.45.170.4` 迁移了以下数据：

| 数据 | 大小 | 状态 |
|------|------|------|
| PostgreSQL 数据库（KnowledgeHub） | 51MB（dump） | ✅ 已导入 |
| uploads（用户上传文件） | 4.5GB | 🔄 异步同步中 |
| wasm-mirrors（仿真实训 WASM） | 2.6GB | 🔄 异步同步中 |

## ⚠️ 注意事项

1. **不要修改宝塔的 `iketong.com.conf`**（除了 cert 路径），否则反代会失效
2. **容器端口冲突**：本服务器已运行 kg_edu 项目（4000 端口），KnowledgeHub 容器必须用 5433/4200 等不同端口
3. **COTURN**：本服务器也启用了 coturn 容器（host 网络，UDP 3478），与 kg_edu 的 coturn 各自独立
4. **TURN_SECRET**：每个服务器（生产 119.45.170.4 和 iketong 47.99.245.49）有独立的 TURN_SECRET，互不通用

## 📞 联系信息

部署日期：2026-10-07
部署人：Claude (pi agent)
证书有效期：2026-10-07 ~ 2027-01-05
下次续签时间：自动（acme.sh cron）