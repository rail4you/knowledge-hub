# 阿里云 OSS 接入与 AccessKey 申请方案

适用版本：2026-11 起，OSS 凭证由**数据库动态配置**承载（不再依赖 appsettings / 环境变量里的明文 Key），
管理入口：**管理 → 对象存储设置**（路由 `/admin/oss-settings`，权限 `KnowledgeHub.Oss`）。

---

## 一、当前项目是怎么保存上传文件的

项目里有两条并存的上传链路，本次改造只动了第二条：

| 链路 | 实现 | 存储位置 | 用途 |
|---|---|---|---|
| ① 资源库本地存储 | `IFileStorageService` / `LocalFileStorageService`<br>`src/KnowledgeHub.Application/Resources/FileStorage/` | 本地磁盘 `Storage__RootPath`（容器内 `/data/uploads`） | 资源文件本体、分片上传、缩略图、Office→PDF 转换、Range 视频代理 |
| ② OSS 直传 | `OssUploadController` / `ResumeUploadController`<br>`src/KnowledgeHub.HttpApi/Controllers/` | 阿里云 OSS Bucket | 图片上传、简历附件、实训资料/章节目录等通用文件 |

链路 ① 目前**没有**改成 OSS：资源文件的读取路径大量依赖本地绝对路径
（`ResourceFileController` 有 10+ 处 `Path.Combine(FileStorageService.RootPath, ...)`，
另有 Gotenberg 转 PDF、FFmpeg 缩略图需要本地可读文件）。
如需把资源本体也迁到 OSS，应作为独立二期改造（引入"本地缓存 + OSS 源站"或全量重写读路径），不要与凭证治理混在一起。

链路 ② 原来直接读 `_configuration["Oss:AccessKeyId"]` 等 4 个配置项，本次已改为读数据库。

---

## 二、凭证落库是否可行：结论与依据

**可行，且本项目已有成熟先例，本次直接复用同一套模式。**

已有先例：
- Qwen API Key：`KnowledgeHub.AI.QwenApiKey`（`src/KnowledgeHub.Domain/Settings/KnowledgeHubSettings.cs`），
  由 `QwenCredentialProvider` 解析，AI 使用管理页维护。
- SMTP 密码：`KnowledgeHub.Smtp.Password`，由 `SmtpConfigAppService` + `SmtpSettingsComponent` 维护。

ABP 的 `ISettingProvider` 天然提供 **租户 → 全局 → 默认值** 的三级解析，
`SettingDefinition(isEncrypted: true)` 会用 `StringEncryption:DefaultPassPhrase` 做 AES 加密后写入 `AbpSettings` 表，
所以密钥既不进代码仓库，也不进 `appsettings.json` / 容器环境变量。

### 本次落地的内容

| 文件 | 作用 |
|---|---|
| `src/KnowledgeHub.Domain/Settings/KnowledgeHubSettings.cs` | 新增 `KnowledgeHub.Oss.*` 6 个键（Endpoint / AccessKeyId / AccessKeySecret / BucketName / UploadPath / UrlPrefix） |
| `src/KnowledgeHub.Domain/Settings/KnowledgeHubSettingDefinitionProvider.cs` | 注册定义；AK/SK 标记 `isEncrypted: true`，全部 `isVisibleToClients: false` |
| `src/KnowledgeHub.Application/Resources/Oss/OssCredentialProvider.cs` | 统一出口：逐字段 数据库 → appsettings → 默认值；按租户缓存 5 分钟；`CreateClient()` 未配置时抛可读业务异常 |
| `src/KnowledgeHub.Application.Contracts/Resources/Oss/IOssConfigAppService.cs` | `GetAsync` / `UpdateAsync` / `TestAsync` 契约与 DTO |
| `src/KnowledgeHub.Application/Resources/Oss/OssConfigAppService.cs` | 读写设置 + 连通性测试（写 1 字节探测对象后删除） |
| `OssUploadController` / `ResumeUploadController` / `ResumePreviewController` | 改为依赖 `IOssCredentialProvider`，不再读 `IConfiguration` |
| `angular/src/app/admin/oss-settings/*` | 管理页（区域预设、密钥掩码、连通性测试、来源提示） |
| `KnowledgeHubPermissions.Oss` + `RolePermissionSeeder` | 授予 host `admin` 与租户 `SchoolAdmin` |

### 关键设计取舍

1. **逐字段回退而不是整体切换**
   `Endpoint` 可以先在数据库改、`Bucket` 仍走配置文件，方便灰度；任一字段为空/是占位符
   `YOUR_OSS_ACCESS_KEY_ID_HERE` 时自动回退 `appsettings` 的 `Oss:*`。
   → **现网部署文件不用动也能继续跑**，属于向前兼容的增量改造。

2. **租户级 + 全局回退**
   多校场景下不同学校可能用不同 Bucket：租户管理员写租户级 Setting，host admin 写全局；
   租户没配就用全局。这与 SMTP 完全一致，运维心智统一。

3. **密钥不回显**
   后端只返回 `hasAccessKeySecret: boolean`；`UpdateAsync` 里密钥为空表示"保持不变"，
   `ClearAccessKeySecret: true` 才清空。避免"打开页面就把密钥拉到浏览器"。

4. **保存即生效**
   `OssCredentialProvider` 缓存 5 分钟且按租户隔离，保存后显式 `ClearCache()`；
   `OssClient` 每次请求新建，**换 Key 不需要重启 API**。

5. **连通性测试用 PutObject+DeleteObject，不用 ListBuckets**
   这样 RAM 策略只需授到对象级最小权限就能测通，不会出现"能列目录但传不上去"的假成功；
   同时验证"能写能删"，因为本项目存在物理删除逻辑（`KnowledgeHubPermissions.Resources.PhysicalDelete`）。

### 必须注意的坑（务必记下）

- **加密依赖 `StringEncryption:DefaultPassPhrase`**（生产由 `STRING_ENCRYPTION_PASSPHRASE` 注入）。
  这个值一改，库里已存的 AK/SK、SMTP 密码、Qwen Key **全部无法解密**，只能重新填。
  它必须和数据库备份一起备份，且**不能提交到仓库**。
- 本地开发的 `appsettings.json` 里 `Oss` 节点保留占位符即可；密钥填到管理页或 `dotnet user-secrets`。
- 数据库里的凭证意味着**能读库的人就能拿到 OSS 权限**。因此：
  - `KnowledgeHub.Oss` 权限只授予 host `admin` 与 `SchoolAdmin`（已按此配置）；
  - 数据库访问权限、备份文件权限要与 `STRING_ENCRYPTION_PASSPHRASE` 同级别保护；
  - 定期在阿里云侧轮换 AccessKey（轮换后回管理页更新即可，见第五节）。

---

## 三、阿里云账号开通与 AccessKey 申请方案（新账号）

### 0. 前置：账号实名认证

1. 用手机号注册阿里云账号，完成**实名认证**（个人或企业认证，建议企业认证，Bucket 数量/实名限制更宽）。
2. 登录 [阿里云控制台](https://ram.console.aliyun.com/) → 右上角头像 → **AccessKey 管理**。

> ⚠️ 主账号的 AccessKey 权限等同于超级管理员，**禁止**用于本项目上传。
> 现在起只走下面的 RAM 子账号方案。

### 1. 创建用于上传的 RAM 用户

路径：控制台 → **访问控制 RAM** → 身份管理 → **用户** → 创建用户

| 项 | 建议值 |
|---|---|
| 登录名称 | `knowledgehub-oss-uploader` |
| 显示名称 | 知识资源库-OSS上传 |
| 控制台登录 | **不创建**（程序调用不需要控制台密码） |

创建完成后立刻弹出 **AccessKey / Secret**（这是唯一一次完整展示 Secret，务必当场存入密码管理器）。
若已关闭弹窗：用户详情 → **AccessKey** → 创建 AccessKey。

### 2. 授权：自定义最小权限策略（推荐）

不要直接挂 `AliyunOSSFullAccess`。做法：

1. RAM → **权限管理** → 权限策略 → **创建权限策略** → 脚本编辑：

```json
{
  "Version": "1",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "oss:PutObject",
        "oss:GetObject",
        "oss:DeleteObject",
        "oss:AbortMultipartUpload"
      ],
      "Resource": [
        "acs:oss:*:*:kg-edu",
        "acs:oss:*:*:kg-edu/*"
      ]
    }
  ]
}
```

> - `kg-edu` 换成实际 Bucket 名；上线前用新 Bucket 名（如 `kg-edu-prod`）则同步替换。
> - 这 4 个 Action 覆盖了本项目的上传、下载/预览、删除、分片上传取消。
> - **不要**给 `oss:PutObjectAcl`、`oss:DeleteBucket`、`oss:*`。

2. 回到该用户 → **权限管理** → 新增授权 → 选择刚建的策略 → 确定。

> 若运维希望省事，也可先挂系统策略 `AliyunOSSFullAccess` 打通链路，
> 上线前再换成上面的自定义策略（收窄权限不影响功能）。

### 3. 创建 Bucket

路径：控制台 → **对象存储 OSS** →  Bucket 列表 → 创建 Bucket

| 项 | 建议值 |
|---|---|
| Bucket 名称 | 全站唯一、小写。例：`kg-edu-prod`（与环境变量/管理页填写一致） |
| 区域 | 与服务器同区（如 `华北2（北京）`，Endpoint `oss-cn-beijing.aliyuncs.com`） |
| 存储类型 | **标准存储**（归档/冷归档取回慢，不适合在线预览） |
| 冗余类型 | 本地冗余（LRS）即可；有更高要求再上同城冗余（ZRS，费用翻倍） |
| 版本控制 | 关闭 |
| 服务端加密 | 可选（开启后读对象自动解密，注意与图片处理兼容） |
| 访问权限 | 见下方说明 |

**访问权限三选一：**

| 方案 | 配置 | 代价 | 适用 |
|---|---|---|---|
| A. 公共读（当前项目默认假设） | Bucket 权限 → 公共读 | 图片/简历 URL 任何人可访问、无流量保护 | ✅ 先用这个，快速跑通 |
| B. 私有 + CDN 回源私有 | 私有读写 + 绑 CDN（CDN 开启私有回源，配置 Referer/URL 鉴权） | 需备案域名、有 CDN 费用 | 有域名后升级 |
| C. 私有 + 应用签名 URL | 私有读写，后端按需签名 | 改动大（当前前端直接存 URL） | 长期方案 |

> 简历附件含个人信息，方案 A 长期有隐私风险，建议上线前切到 B 或 C。

### 4. CORS（浏览器直传/预览需要）

Bucket → 数据安全 → **跨域访问(CORS)** → 创建规则：

- 来源 Origin：`http://localhost:4200`（开发）、`https://www.iketong.com`、`https://iketong.com`（生产，逐一添加）
- 允许 Methods：`GET`、`POST`、`PUT`、`HEAD`
- 允许 Headers：`*`
- 暴露 Headers：`ETag`、`x-oss-request-id`
- 缓存：600 秒

### 5. 费用与安全兜底

1. **费用中心 → 预算管理**：新建月度预算告警（如 200 元），超 80% / 100% 短信通知。
2. **Bucket 防护**：开启防盗链（Referer 白名单：你的域名 + 空 Referer 放行）、限制流量/并发。
3. **生命周期规则**：对 `.healthcheck/`、`chunks/` 目录设置 1 天后自动删除（防探测对象残留）。
4. **AccessKey 轮换**：建议每 6 个月或人员离职时轮换。
   RAM 用户 → AccessKey → **禁用旧 AK** + **创建新 AK** → 回本项目「对象存储设置」更新 → 点"连通性测试"验证 → 再禁用旧 AK。
5. **不要**做的事：不把 AK/SK 写进 `appsettings.json` 提交仓库、不写进 docker-compose 明文、不在日志打印。

---

## 四、在本项目里填什么

管理页 **管理 → 对象存储设置**（`/admin/oss-settings`）：

| 字段 | 填入 | 备注 |
|---|---|---|
| Endpoint | `oss-cn-beijing.aliyuncs.com` | 与 Bucket 同区域，可点区域预设按钮 |
| Bucket 名称 | `kg-edu-prod` | 必须与控制台一致 |
| 上传路径前缀 | `knowledgehub` | **保持默认值**，改了会导致新旧对象分散 |
| 自定义访问域名 / CDN | 先留空 | 上 CDN 后再填 `https://cdn.example.com` |
| AccessKeyId | RAM 子账号的 AK | |
| AccessKeySecret | RAM 子账号的 SK | 只写一次，之后不回显 |

保存后：

- 页面顶部状态从「不可用 / 未配置」变为「可用 / 来源：数据库设置」；
- 点 **写入并删除测试对象** → 提示"连通性正常"即完成；
- 回到「对象存储设置」旁边的资源页面，随便传一张图片验证 URL 域名是否正确。

容器部署（`etc/docker/.env.production`）中的 `OSS_ACCESS_KEY_ID` / `OSS_ACCESS_KEY_SECRET`
可以留空占位——数据库配置优先级更高；保留它们只是作为"数据库未配置时的兜底"。

---

## 五、常见故障对照

| 现象 | 原因 | 处理 |
|---|---|---|
| `AccessKeyId 无效` | AK 已禁用 / 属于别的阿里云账号 | 换成本账号 RAM 用户的 AK |
| `AccessKeyId 与 AccessKeySecret 不匹配` | Secret 填错，或 Endpoint 与 AK 不同区域 | 核对区域一致后重填 |
| `403 AccessDenied` | RAM 未授权该 Bucket，或 Bucket 名错 | 补 `oss:PutObject/GetObject/DeleteObject` 资源授权；核对 Bucket 名 |
| `NoSuchBucket` | Bucket 不存在或不在当前账号 | 控制台核对 Bucket 名与区域 |
| `SignatureExpired` | 服务器时间偏差 | `timedatectl set-ntp true`，校准后重试 |
| 上传成功但前端 403/跨域失败 | Bucket CORS 未配 | 按第三节配置 CORS |
| 前端能显示、但简历预览报"仅支持预览本站简历附件" | 预览走 Bucket 域名校验 | 确认未改 Bucket 名；用了自定义域名也没问题（已同时放行自定义域名 Host） |
| 保存后不生效 | 多副本部署，内存缓存各自独立（最多 5 分钟） | 等待 5 分钟或重启 API；如需立即生效可缩短缓存 TTL |
