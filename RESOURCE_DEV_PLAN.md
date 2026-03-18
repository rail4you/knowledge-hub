# 资源管理系统 - 开发文档

## 一、项目概述

### 背景
教育资源共享平台，支持文档、视频、音频、图片、PPT等主流格式资源的上传、审核、版本管理。

### 功能需求
1. **资源上传**：支持批量上传与断点续传，需填写元数据
2. **二级审核**：院校初审 → 联盟终审
3. **资源分类**：自定义分类体系，支持树形结构
4. **版本管理**：完整版本历史，更新需重新审核
5. **资源删除**：逻辑删除（可恢复）+ 物理删除（需审批）
6. **权限管控**：联盟管理员、院校管理员、教师、学生、企业用户

### 技术选型
- 框架：ABP 10.1.0 + ASP.NET Core + Angular
- 数据库：PostgreSQL + Entity Framework Core
- 分片上传：simple-uploader.js（1MB-5MB分片）
- 文件存储：本地存储（接口可替换S3/MinIO）

---

## 二、数据模型

### 2.1 Resource（资源表）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 主键 |
| Name | string(256) | 资源名称 |
| Description | string(2000) | 资源描述 |
| ResourceType | ResourceType | 资源类型 |
| CategoryId | Guid? | 所属分类 |
| FilePath | string(512) | 文件存储路径 |
| FileSize | long | 文件大小 |
| FileExtension | string(32) | 文件扩展名 |
| OriginalFileName | string(256) | 原始文件名 |
| Status | ResourceStatus | 资源状态 |
| CurrentVersion | int | 当前版本号 |
| Keywords | string(500) | 关键词 |
| CopyrightInfo | string(500) | 版权信息 |
| IsDownloadable | bool | 是否可下载 |
| CollectionCount | int | 收藏数 |
| DownloadCount | int | 下载数 |
| ViewCount | int | 浏览数 |
| OrganizationId | Guid? | 所属组织 |
| CreatorId | Guid | 上传人 |
| TenantId | Guid? | 租户 |
| IsDeleted | bool | 逻辑删除 |
| DeletionTime | DateTime? | 删除时间 |

### 2.2 ResourceVersion（版本历史）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 主键 |
| ResourceId | Guid | 资源ID |
| Version | int | 版本号 |
| FilePath | string | 文件路径 |
| FileSize | long | 文件大小 |
| UpdateContent | string(500) | 更新内容说明 |
| IsCurrentVersion | bool | 是否当前版本 |
| CreatorId | Guid | 创建人 |
| CreationTime | DateTime | 创建时间 |

### 2.3 ResourceCategory（分类表）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 主键 |
| Name | string(128) | 分类名称 |
| ParentId | Guid? | 父分类（树形） |
| Code | string(64) | 分类编码 |
| SortOrder | int | 排序 |
| IsActive | bool | 是否启用 |
| TenantId | Guid? | 租户 |

### 2.4 ResourceAudit（审核记录）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 主键 |
| ResourceId | Guid | 资源ID |
| AuditType | AuditType | 审核类型 |
| Status | AuditStatus | 审核结果 |
| Comment | string(1000) | 审核意见 |
| AuditorId | Guid | 审核人 |
| CreationTime | DateTime | 审核时间 |

### 2.5 ResourceCollection（收藏表）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 主键 |
| ResourceId | Guid | 资源ID |
| UserId | Guid | 用户ID |
| CreationTime | DateTime | 收藏时间 |

---

## 三、枚举定义

```csharp
// 资源类型
public enum ResourceType 
{ 
    Document,  // doc/docx/pdf
    Video,     // mp4/avi  
    Audio,     // mp3
    Image,     // jpg/png
    PPT        // ppt/pptx
}

// 资源状态
public enum ResourceStatus 
{ 
    Draft,             // 草稿
    PendingReview,    // 待初审
    SchoolApproved,   // 院校审核通过
    LeagueApproved,   // 联盟审核通过
    Rejected,         // 审核拒绝
    Hidden            // 已隐藏
}

// 审核类型
public enum AuditType { Initial, Final }

// 审核状态
public enum AuditStatus { Pending, Approved, Rejected }
```

---

## 四、文件存储设计

### 4.1 常量定义

```csharp
public static class AppFileUploadConsts
{
    public const long MaxFileSize = 500 * 1024 * 1024;  // 500MB
    public const int ChunkSize = 2 * 1024 * 1024;        // 2MB
    public static readonly string[] AllowedExtensions = 
        { ".doc", ".docx", ".pdf", ".mp4", ".avi", ".mp3", ".jpg", ".jpeg", ".png", ".ppt", ".pptx" };
}
```

### 4.2 存储路径规则

```
/{TenantId}/{OrganizationCode}/{UserId}/{Year}/{Guid}.{Extension}
```

---

## 五、权限设计

```csharp
public static class ResourcePermissions
{
    public const string GroupName = "KnowledgeHub.Resources";
    
    public const string Default = GroupName;
    public const string Create = Default + ".Create";
    public const string Edit = Default + ".Edit";
    public const string Delete = Default + ".Delete";
    public const string Download = Default + ".Download";
    public const string SchoolAudit = Default + ".SchoolAudit";
    public const string LeagueAudit = Default + ".LeagueAudit";
    public const string ManageCategory = Default + ".ManageCategory";
    public const string PhysicalDelete = Default + ".PhysicalDelete";
    public const string ViewStatistics = Default + ".ViewStatistics";
}
```

### 角色权限映射

| 角色 | 权限 |
|------|------|
| 联盟管理员 | 全部权限 |
| 院校管理员 | Create, Edit, Delete, Download, SchoolAudit, ManageCategory |
| 教师 | Create, Edit(自己的), Download |
| 学生 | Download(可下载标记), Collection |
| 企业用户 | Create, 查看教学资源 |

---

## 六、前端 UI 设计

### 6.1 三栏布局

```
┌─────────────┬─────────────────────────┬──────────────────────┐
│  文档列表   │      文档预览           │   版本历史面板       │
│             │                         │                      │
│ [文档1] v1 │  文件名: xxx.pdf        │  ┌────────────────┐  │
│ [文档2] v2 │  版本: v3 上传人: xxx    │  │ 拖拽上传区     │  │
│ [文档3] v1 │  时间: 2026-03-17        │  │ (上传=新建版本)│  │
│             │                         │  └────────────────┘  │
│             │  [下载] [分享] [新版本]  │                      │
│             │                         │  ● v3 当前版本      │
│             │                         │    2026-03-17       │
│             │                         │    [下载] [预览]     │
│             │                         │                      │
│             │                         │  ○ v2               │
│             │                         │    2026-03-15       │
│             │                         │    [下载] [预览]     │
│             │                         │                      │
└─────────────┴─────────────────────────┴──────────────────────┘
```

### 6.2 功能说明

| 区域 | 功能 |
|------|------|
| 左侧 | 文档列表、版本号标签、点击切换当前文档 |
| 中间 | 预览、顶栏信息（文件名/版本/上传人/时间）、操作按钮 |
| 右侧 | 拖拽上传（自动生成新版本）、时间线历史版本、版本操作 |

---

## 七、目录结构

```
src/KnowledgeHub.Domain/
├── Resources/
│   ├── Resource.cs
│   ├── ResourceVersion.cs
│   ├── ResourceCategory.cs
│   ├── ResourceAudit.cs
│   ├── ResourceCollection.cs
│   ├── IResourceRepository.cs
│   └── Enums/
│       ├── ResourceType.cs
│       ├── ResourceStatus.cs
│       ├── AuditType.cs
│       └── AuditStatus.cs

src/KnowledgeHub.Application/
├── Resources/
│   ├── ResourceAppService.cs
│   ├── ResourceAuditAppService.cs
│   ├── ResourceCategoryAppService.cs
│   └── FileStorage/
│       ├── IFileStorageService.cs
│       └── LocalFileStorageService.cs

src/KnowledgeHub.EntityFrameworkCore/
├── Resources/
│   ├── EfCoreResourceRepository.cs
│   └── Configurations/
│       └── ...

angular/src/app/
├── resources/
│   ├── resource-list/
│   ├── resource-preview/
│   ├── version-history/
│   ├── resource-upload/
│   ├── category-manage/
│   └── audit-manage/
```

---

## 八、开发步骤

| 阶段 | 步骤 | 内容 |
|------|------|------|
| 一 | 1.1 | 删除 Document、AppUser 相关代码 |
| | 1.2 | 创建枚举：ResourceType, ResourceStatus, AuditType |
| | 1.3 | 创建常量：AppFileUploadConsts |
| 二 | 2.1 | 创建实体：Resource, ResourceVersion, ResourceCategory, ResourceAudit, ResourceCollection |
| | 2.2 | 创建仓储接口：IResourceRepository |
| 三 | 3.1 | 创建 IFileStorageService 接口 |
| | 3.2 | 创建 LocalFileStorageService 实现 |
| 四 | 4.1 | EF Core 配置（实体映射、索引） |
| | 4.2 | 添加数据库迁移 |
| 五 | 5.1 | 更新权限定义：ResourcePermissions |
| | 5.2 | 创建应用服务 |
| 六 | 6.1 | 安装 simple-uploader.js |
| | 6.2 | 创建前端组件 |
| | 6.3 | 注册菜单和多国语言 |

---

## 九、开发进度

### ✅ 已完成后端开发

| 阶段 | 状态 | 说明 |
|------|------|------|
| 一 | ✅ | 删除Document/AppUser，创建枚举和常量 |
| 二 | ✅ | 创建领域实体（Resource, ResourceVersion等） |
| 三 | ✅ | 创建文件存储服务（LocalFileStorageService） |
| 四 | ✅ | EF Core配置和数据库迁移 |
| 五 | ✅ | 权限定义和应用服务 |
| 六 | ✅ | 注册菜单和多国语言 |

### 🔄 开发中

| 内容 | 说明 |
|------|------|
| 前端 | 创建Resource组件（三栏布局）已完成基本功能 |

---

## 十、技术实现细节

### 10.1 后端API端点

```
GET    /api/app/resource              - 获取资源列表
POST   /api/app/resource              - 创建资源
PUT    /api/app/resource/{id}         - 更新资源
DELETE /api/app/resource/{id}         - 删除资源
GET    /api/app/resource/{id}         - 获取资源详情
POST   /api/app/resource/create-with-file - 带文件创建资源
GET    /api/app/resource/{id}/versions   - 获取版本历史
POST   /api/app/resource/upload-version   - 上传新版本
POST   /api/app/resource/{id}/collect     - 收藏
DELETE /api/app/resource/{id}/collect     - 取消收藏
GET    /api/app/resource/{id}/file-url    - 获取文件URL
GET    /api/app/resource/{id}/download    - 下载文件
```

### 10.2 权限定义

```csharp
KnowledgeHub.Resources.Default
KnowledgeHub.Resources.Create
KnowledgeHub.Resources.Edit
KnowledgeHub.Resources.Delete
KnowledgeHub.Resources.Download
KnowledgeHub.Resources.SchoolAudit
KnowledgeHub.Resources.LeagueAudit
KnowledgeHub.Resources.ManageCategory
KnowledgeHub.Resources.PhysicalDelete
KnowledgeHub.Resources.ViewStatistics
```

### 10.3 数据库表

- `AppResources` - 资源表
- `AppResourceVersions` - 版本历史
- `AppResourceCategories` - 分类表
- `AppResourceAudits` - 审核记录
- `AppResourceCollections` - 收藏表

---

## 九、历史记录

| 日期 | 版本 | 说明 |
|------|------|------|
| 2026-03-17 | v1.0 | 初始版本 |
| 2026-03-17 | v1.1 | 完成数据库迁移，后端API开发完成 |
| 2026-03-17 | v1.2 | 前端组件开发完成，三栏布局完成 |
| 2026-03-17 | v1.3 | 编译修复完成，前端可运行 |
