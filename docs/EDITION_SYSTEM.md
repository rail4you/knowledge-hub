# 知识中心平台 - 版本管理系统

## 概述

平台支持两种版本：
- **基础版（Basic）**：单租户模式，不支持联盟，单级审批
- **标准版（Standard）**：多租户模式，支持联盟，两级审批

## 功能架构

```
┌─────────────────────────────────────────────────────────────┐
│                     超级管理员                               │
│  - 安装系统时选择版本                                        │
│  - 基础版可升级到标准版                                      │
│  - 标准版不可降级                                           │
└─────────────────────────────────────────────────────────────┘

基础版                          标准版
┌──────────────────┐           ┌──────────────────┐
│ MaxTenantCount=1 │           │ MaxTenantCount=-1│
│ Alliance=false   │           │ Alliance=true    │
│ TwoLevelApproval │           │ TwoLevelApproval │
│    =false        │           │    =true         │
└──────────────────┘           └──────────────────┘
```

## API 端点

### 安装 API

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/app/install/status` | GET | 获取安装状态和当前版本 |
| `/api/app/install/install` | POST | 执行系统安装 |

### 版本 API

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/app/edition/current` | GET | 获取当前版本配置 |
| `/api/app/edition/upgrade` | POST | 升级到标准版 |

## Feature 定义

| Feature Key | 说明 | Basic 默认值 | Standard 默认值 |
|-------------|------|--------------|----------------|
| `KnowledgeHub.Edition` | 版本类型 | "Basic" | "Standard" |
| `KnowledgeHub.MaxTenantCount` | 最大租户数 | 1 | -1 (无限制) |
| `KnowledgeHub.Alliance` | 联盟管理 | false | true |
| `KnowledgeHub.TwoLevelApproval` | 两级审批 | false | true |

## 许可证验证

基础版：无需许可证
标准版：需要以 `KH-STANDARD-` 开头的许可证密钥

## 测试流程

### 重置数据库

```bash
# 1. 删除现有数据库
psql -h localhost -p 5433 -U postgres -c "DROP DATABASE \"KnowledgeHub\";"
psql -h localhost -p 5433 -U postgres -c "CREATE DATABASE \"KnowledgeHub\";"

# 2. 运行迁移
cd /Users/bai/projects/KnowledgeHub
./dev.sh migrate

# 3. 启动 API
./dev.sh start api

# 4. 访问安装向导
# 浏览器打开: https://localhost:44305/install
```

### 安装向导

1. **步骤 1 - 许可证验证**
   - 基础版：直接点击"验证许可证"继续
   - 标准版：输入许可证密钥 `KH-STANDARD-2024-FREE`

2. **步骤 2 - 选择版本**
   - 选择"基础版"或"标准版"

3. **步骤 3 - 创建管理员**
   - 设置管理员用户名、邮箱、密码

4. **完成**
   - 跳转登录页面

### 测试升级（基础版 → 标准版）

```bash
# 1. 以管理员身份登录

# 2. 调用升级 API（通过 Swagger 或 curl）
curl -X POST "https://localhost:44305/api/app/edition/upgrade" \
  -H "Content-Type: application/json" \
  -d '{"licenseKey": "KH-STANDARD-2024-FREE"}'

# 3. 刷新页面，检查版本配置
```

## 数据库表

### 核心配置表

| 表名 | 说明 |
|------|------|
| `AbpSettings` | 系统设置（IsInstalled, InstalledEdition, LicenseKey） |
| `AbpFeatureValues` | 功能开关配置 |
| `AbpTenants` | 租户表（标准版使用） |

### 相关设置

```sql
-- 查看当前配置
SELECT * FROM "AbpSettings" WHERE "Name" LIKE 'KnowledgeHub%';
SELECT * FROM "AbpFeatureValues" WHERE "Name" LIKE 'KnowledgeHub%';

-- 手动重置安装状态
DELETE FROM "AbpSettings" WHERE "Name" LIKE 'KnowledgeHub%';
DELETE FROM "AbpFeatureValues" WHERE "Name" LIKE 'KnowledgeHub%';
```

## 开发说明

### 核心文件

| 文件 | 说明 |
|------|------|
| `KnowledgeHubFeatures.cs` | Feature 常量定义 |
| `KnowledgeHubEditions.cs` | 版本常量 |
| `KnowledgeHubFeatureDefinitionProvider.cs` | Feature 定义 |
| `EditionConfigService.cs` | 版本配置检查服务 |
| `InstallAppService.cs` | 安装服务 |
| `EditionAppService.cs` | 版本管理服务 |
| `TenantCreationEventHandler.cs` | 租户创建事件拦截 |
| `InstallComponent.ts` | 安装向导前端 |
| `EditionInfoComponent.ts` | 版本信息与升级组件 |
| `EditionService.ts` | Angular 版本检查服务 |

### 版本升级流程

```
1. 调用 GET /api/app/edition/current 确认当前版本
2. 调用 POST /api/app/edition/upgrade
   - 验证许可证
   - 更新 AbpSettings.InstalledEdition = "Standard"
   - 更新 AbpFeatureValues
   - 创建默认租户（如果不存在）
```

## 注意事项

1. **升级不可逆**：标准版创建后无法降级到基础版
2. **数据隔离**：基础版所有数据在同一租户内，标准版支持多租户数据隔离
3. **审批流程**：
   - 基础版：提交审核 → 校级审批 → 直接发布（学生可见）
   - 标准版：提交审核 → 校级审批 → 联盟审批 → 发布（学生可见）

## 已完成功能

- [x] 安装向导（Web UI）
- [x] 安装 API（保存 Edition 和 Feature 配置）
- [x] 版本升级 API
- [x] 版本升级前端 UI（EditionInfoComponent）
- [x] 审批流程根据版本控制
- [x] EditionService（Angular 版本检查服务）
- [x] 租户创建拦截（TenantCreationEventHandler）
- [x] 联盟管理前端（AllianceManagementComponent）
- [x] 联盟审批流程（AllianceAppService.LeagueAuditAsync）
- [x] 联盟菜单项（/admin/alliance）

## 后续功能

- [ ] 菜单根据版本动态显示/隐藏（Basic 版隐藏联盟菜单）
