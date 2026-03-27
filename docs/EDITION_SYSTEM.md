# 知识中心平台 - 版本管理系统

## 概述

平台支持两种版本：
- **基础版（Basic）**：单租户模式，不支持联盟，单级审批
- **标准版（Standard）**：多租户模式，支持联盟，两级审批

## 版本功能对比

| 功能 | 基础版 | 标准版 |
|------|--------|--------|
| 最大租户数 | 1 | 无限制 |
| 联盟管理 | ❌ | ✅ |
| 两级审批 | ❌ | ✅ |
| 许可证 | 免费 | 需要许可证 |

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

---

## 功能验证指南

### 前置条件

```bash
# 1. 重置数据库
psql -h localhost -p 5433 -U postgres -c "DROP DATABASE \"KnowledgeHub\";"
psql -h localhost -p 5433 -U postgres -c "CREATE DATABASE \"KnowledgeHub\";"

# 2. 运行迁移
./dev.sh migrate

# 3. 启动 API
./dev.sh start api

# 4. 启动 Angular
./dev.sh start angular
```

---

### 功能一：系统安装向导

**验证地址**: https://localhost:44305/install

**验证步骤**:
1. 浏览器打开安装向导
2. 步骤1：输入许可证（基础版直接继续，标准版输入 `KH-STANDARD-2024-FREE`）
3. 步骤2：选择版本（基础版/标准版）
4. 步骤3：创建管理员账号
5. 完成安装，跳转登录页面

**预期结果**:
- ✅ 安装向导正常显示
- ✅ 版本选择可用
- ✅ 管理员创建成功
- ✅ 安装后跳转登录页

---

### 功能二：版本信息显示与升级

**验证地址**: https://localhost:44305/admin/edition

**验证步骤**:
1. 以管理员身份登录
2. 进入"版本信息"页面
3. 查看当前版本配置
4. 基础版点击"升级到标准版"按钮
5. 输入许可证密钥 `KH-STANDARD-2024-FREE`
6. 确认升级

**预期结果**:
- ✅ 版本信息正确显示（Basic/Standard）
- ✅ 最大租户数正确（1 或 无限制）
- ✅ 联盟管理状态正确显示
- ✅ 两级审批状态正确显示
- ✅ 升级后页面刷新，版本变为"标准版"

---

### 功能三：租户创建限制（基础版）

**验证地址**: https://localhost:44305/tenant-management

**验证步骤**:
1. 确认当前为基础版
2. 进入租户管理页面
3. 点击"新建租户"
4. 填写租户信息并保存

**预期结果（基础版）**:
- ✅ 基础版限制创建租户数量为1
- ✅ 如果已有1个租户，再次创建时提示"当前版本已达最大租户数限制，请升级到标准版"

---

### 功能四：审批流程（版本差异）

**验证地址**: https://localhost:44305/resources

**验证步骤**:
1. 以学校管理员身份登录
2. 创建或提交一个资源
3. 以联盟管理员身份登录（标准版）
4. 查看资源审核状态

**预期结果**:

| 版本 | 提交后状态 | 学校审核后 | 联盟审核后 |
|------|-----------|-----------|-----------|
| 基础版 | PendingReview | SchoolApproved → 直接发布 | 不适用 |
| 标准版 | PendingReview | SchoolApproved | LeagueApproved → 发布 |

**验证命令**:
```bash
# 查看资源状态
curl -sk https://localhost:44305/api/app/resource/my-resources | jq
```

---

### 功能五：联盟管理（标准版专属）

**验证地址**: https://localhost:44305/admin/alliance

**验证步骤**:
1. 确认当前为标准版
2. 进入"联盟管理"页面（出现在管理菜单中）
3. 点击"创建联盟"
4. 填写联盟名称和描述
5. 添加成员学校（输入租户ID和名称）
6. 设置成员角色（Member/Admin）

**预期结果（标准版）**:
- ✅ 联盟管理菜单可见
- ✅ 可以创建联盟
- ✅ 可以添加/移除成员
- ✅ 可以修改成员角色

**预期结果（基础版）**:
- ✅ 联盟管理菜单不可见

---

### 功能六：联盟审批（标准版专属）

**验证步骤**:
1. 学校A提交资源 → 状态变为 `SchoolApproved`
2. 以联盟Admin身份登录
3. 进入"联盟管理" → "待审核"标签页
4. 可以看到其他学校提交的待审核资源
5. 点击"通过"或"拒绝"

**预期结果**:
- ✅ 联盟Admin可以看到所有成员的待审核资源
- ✅ 通过后资源状态变为 `LeagueApproved`
- ✅ 拒绝后资源状态变为 `Rejected`

---

### 功能七：菜单动态显示

**验证步骤**:
1. 基础版登录 → 检查管理菜单
2. 升级到标准版
3. 刷新页面 → 再次检查管理菜单

**预期结果**:
| 菜单项 | 基础版 | 标准版 |
|--------|--------|--------|
| 联盟管理 | ❌ 隐藏 | ✅ 显示 |

---

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

### 联盟 API

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/app/alliance` | GET/POST | 获取/创建联盟 |
| `/api/app/alliance/members` | GET | 获取联盟成员 |
| `/api/app/alliance/league-audit` | POST | 联盟审核资源 |
| `/api/app/alliance/pending-audits/{id}` | GET | 获取待审核列表 |

---

## 数据库表

### 核心配置表

| 表名 | 说明 |
|------|------|
| `AbpSettings` | 系统设置（IsInstalled, InstalledEdition, LicenseKey） |
| `AbpFeatureValues` | 功能开关配置 |
| `AbpTenants` | 租户表（标准版使用） |
| `Alliances` | 联盟表 |
| `AllianceMembers` | 联盟成员表 |
| `AllianceAudits` | 联盟审核记录表 |

### 相关设置

```sql
-- 查看当前配置
SELECT * FROM "AbpSettings" WHERE "Name" LIKE 'KnowledgeHub%';
SELECT * FROM "AbpFeatureValues" WHERE "Name" LIKE 'KnowledgeHub%';

-- 手动重置安装状态
DELETE FROM "AbpSettings" WHERE "Name" LIKE 'KnowledgeHub%';
DELETE FROM "AbpFeatureValues" WHERE "Name" LIKE 'KnowledgeHub%';
```

---

## 注意事项

1. **升级不可逆**：标准版创建后无法降级到基础版
2. **数据隔离**：基础版所有数据在同一租户内，标准版支持多租户数据隔离
3. **审批流程**：
   - 基础版：提交审核 → 校级审批 → 直接发布（学生可见）
   - 标准版：提交审核 → 校级审批 → 联盟审批 → 发布（学生可见）

---

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
- [x] 菜单根据版本动态显示（Basic 版隐藏联盟菜单）
