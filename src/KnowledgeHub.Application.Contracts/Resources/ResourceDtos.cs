using System;
using System.Collections.Generic;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Resources;

public class ResourceDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public ResourceType ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    /// <summary>资源归属的全部专业 Id（主专业排第一）；与 <see cref="MajorId"/> / <see cref="MajorName"/> 兼容并双写。</summary>
    public List<Guid> MajorIds { get; set; } = new();
    /// <summary>与 <see cref="MajorIds"/> 一一对应的专业名（主专业排第一）。</summary>
    public List<string> MajorNames { get; set; } = new();
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string FileExtension { get; set; }
    public string OriginalFileName { get; set; }
    public ResourceStatus Status { get; set; }
    public int CurrentVersion { get; set; }
    public string? Keywords { get; set; }
    /// <summary>AI 生成的资源摘要（由 DocumentSummaryBackgroundJob 写入）。</summary>
    public string? Summary { get; set; }
    public string? CopyrightInfo { get; set; }
    public bool IsDownloadable { get; set; }
    public int CollectionCount { get; set; }
    public int DownloadCount { get; set; }
    public int ViewCount { get; set; }

    /// <summary>媒体处理状态（缩略图/预览生成），前端可据此展示"处理中"。</summary>
    public ResourceMediaStatus MediaStatus { get; set; }

    /// <summary>是否文件缺失：FilePath 已配置，但磁盘上对应物理文件不存在。</summary>
    /// <remarks>前端据此禁用下载/预览按钮并给出提示；后端资源列表据此标记。由 AppService 填充。</remarks>
    public bool IsFileMissing { get; set; }

    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }    // CreatorId 复用基类 FullAuditedEntityDto<Guid>.CreatorId（Guid?）。
    // 此前这里曾用 `public Guid CreatorId` 隐藏基类（CS0108），导致 Mapperly 在
    // Guid? -> Guid 映射时处理脆弱、历史数据的 NULL 变成 Guid.Empty，
    // FillCreatorNamesAsync 按 Guid.Empty 过滤后直接返回，列表创建人全空。
    public string? CreatorName { get; set; }

    /// <summary>
    /// 是否来自其他租户的共享资源。
    /// true：资源属于其他租户，由该租户共享给当前租户；
    /// false：资源属于当前租户（自己创建的或本租户内他人创建的）。
    /// 前端资源列表据此显示「共享者」列和详情「共享信息」块。
    /// </summary>
    public bool IsShared { get; set; }

    /// <summary>本资源（本租户拥有）已共享给多少个其它租户。仅对 <see cref="IsShared"/>=false 的资源有意义。</summary>
    public int OutgoingShareCount { get; set; }

    /// <summary>已共享的目标租户名列表（用于表格列悬停提示）。仅 <see cref="OutgoingShareCount"/> > 0 时有值。</summary>
    public List<string> OutgoingShareTenantNames { get; set; } = new();

    /// <summary>源租户 Id（仅 IsShared=true 时有意义）。</summary>
    public Guid? SourceTenantId { get; set; }

    /// <summary>源租户名称（仅 IsShared=true 时有意义）。</summary>
    public string? SourceTenantName { get; set; }

    /// <summary>共享时间（仅 IsShared=true 时有意义）。</summary>
    public DateTime? SharedAt { get; set; }

    /// <summary>共享人用户 Id（仅 IsShared=true 时有意义）。</summary>
    public Guid? SharedByUserId { get; set; }

    /// <summary>共享人显示名（仅 IsShared=true 时有意义）。</summary>
    public string? SharedByUserName { get; set; }

    /// <summary>共享备注（仅 IsShared=true 时有意义）。</summary>
    public string? ShareNote { get; set; }
}

public class ResourceVersionDto : EntityDto<Guid>
{
    public Guid ResourceId { get; set; }
    public int Version { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string? UpdateContent { get; set; }
    public bool IsCurrentVersion { get; set; }
    public DateTime CreationTime { get; set; }
    // 与源实体 AuditedEntity<Guid>.CreatorId（Guid?）保持一致，避免 Guid? -> Guid 映射问题。
    public Guid? CreatorId { get; set; }
    public string? CreatorName { get; set; }
}

public class ResourceCategoryDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    /// <summary>该分类下的资源总数（包括子分类下的资源）。</summary>
    public int ResourceCount { get; set; }
    public List<ResourceCategoryDto> Children { get; set; } = new();
}

public class ResourceAuditDto : EntityDto<Guid>
{
    public Guid ResourceId { get; set; }
    public AuditType AuditType { get; set; }
    public AuditStatus Status { get; set; }
    public string? Comment { get; set; }
    public Guid AuditorId { get; set; }
    public string? AuditorName { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateUpdateResourceDto
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public ResourceType ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    /// <summary>主专业（兼容老字段；与 <see cref="MajorIds"/> 同时传时以后端归一化结果为准）。</summary>
    public Guid? MajorId { get; set; }
    /// <summary>资源归属的全部专业（主专业排第一）；为空表示“无专业归属”。</summary>
    public List<Guid> MajorIds { get; set; } = new();
    public string? Keywords { get; set; }
    public string? CopyrightInfo { get; set; }
    public bool IsDownloadable { get; set; } = true;
    public Guid? OrganizationId { get; set; }

    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? FileExtension { get; set; }
    public string? OriginalFileName { get; set; }
}

public class CreateUpdateResourceCategoryDto
{
    public string Name { get; set; }
    public Guid? ParentId { get; set; }
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AuditResourceDto
{
    public Guid ResourceId { get; set; }
    public AuditStatus Status { get; set; }
    public string? Comment { get; set; }
}

public class UploadVersionDto
{
    public Guid ResourceId { get; set; }
    public string? UpdateContent { get; set; }
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? FileExtension { get; set; }
    public string? OriginalFileName { get; set; }
}

public class InitiateUploadDto
{
    public string FileName { get; set; }
    public long TotalSize { get; set; }
    public int ChunkSize { get; set; } = 1024 * 1024; // 1MB default
}

public class InitiateUploadResultDto
{
    public string UploadId { get; set; }
    public int ChunkSize { get; set; }
    public int TotalChunks { get; set; }
}

public class UploadChunkDto
{
    public string UploadId { get; set; }
    public string FileName { get; set; }
    public int ChunkNumber { get; set; }
    public bool IsLastChunk { get; set; }
}

public class CompleteUploadDto
{
    public string UploadId { get; set; }
    public string FileName { get; set; }
    public int TotalChunks { get; set; }
}

public class CompleteUploadResultDto
{
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string FileExtension { get; set; }
    public string OriginalFileName { get; set; }
}

public class PhysicalDeleteRequestDto : EntityDto<Guid>
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; }
    public string Reason { get; set; }
    public int Status { get; set; }
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; }
    public Guid? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? ApprovalTime { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreatePhysicalDeleteRequestDto
{
    public Guid ResourceId { get; set; }
    public string Reason { get; set; }
}

public class ResourceShareDto : EntityDto<Guid>
{
    public Guid ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public Guid SourceTenantId { get; set; }
    public string? SourceTenantName { get; set; }
    public Guid TargetTenantId { get; set; }
    public string? TargetTenantName { get; set; }
    public Guid SharedByUserId { get; set; }
    public string? SharedByUserName { get; set; }
    public DateTime SharedAt { get; set; }
    public string? Note { get; set; }
}

public class CreateResourceShareDto
{
    public Guid ResourceId { get; set; }
    public List<Guid> TargetTenantIds { get; set; } = new();
    public string? Note { get; set; }
}

public class SharedResourceDto : EntityDto<Guid>
{
    public Guid ShareId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public ResourceType ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    /// <summary>资源归属的全部专业 Id（主专业排第一）；与 <see cref="MajorId"/> / <see cref="MajorName"/> 兼容并双写。</summary>
    public List<Guid> MajorIds { get; set; } = new();
    /// <summary>与 <see cref="MajorIds"/> 一一对应的专业名（主专业排第一）。</summary>
    public List<string> MajorNames { get; set; } = new();
    public string? FileExtension { get; set; }
    public ResourceStatus Status { get; set; }
    public string? Summary { get; set; }
    public int CollectionCount { get; set; }
    public int DownloadCount { get; set; }
    public int ViewCount { get; set; }
    public Guid SourceTenantId { get; set; }
    public string? SourceTenantName { get; set; }
    public DateTime SharedAt { get; set; }
    public Guid SharedByUserId { get; set; }
    public string? SharedByUserName { get; set; }
}

public class SharedResourceListQueryDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public ResourceType? ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? MajorId { get; set; }
}
