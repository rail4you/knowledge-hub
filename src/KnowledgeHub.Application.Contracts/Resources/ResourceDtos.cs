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
    /// <summary>
    /// 所属教学资源库 Id（教学资源库 = 文件资源 + 课程 + 实训的关联集合）。
    /// 非空表示该资源归属某个教学资源库；共建库中其它学校的资源通过库成员关系对本校可见。
    /// </summary>
    public Guid? LibraryId { get; set; }
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

    /// <summary>
    /// 是否已建页面/视频索引（可用于 AI 文档问答）。
    /// true：资源有 PageContent（文档/PDF/PPT 等）或视频已完成索引；
    /// false：未建立索引或索引中。
    /// 前端据此决定是否显示「基于资源提问」入口并跳转到 AI 聊天。
    /// 仅 <see cref="GetWithVersionsAsync"/> 与 <see cref="GetListAsync"/> 等详情/列表 DTO 出口填充。
    /// </summary>
    public bool IsIndexed { get; set; }

    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }    // CreatorId 复用基类 FullAuditedEntityDto<Guid>.CreatorId（Guid?）。
    // 此前这里曾用 `public Guid CreatorId` 隐藏基类（CS0108），导致 Mapperly 在
    // Guid? -> Guid 映射时处理脆弱、历史数据的 NULL 变成 Guid.Empty，
    // FillCreatorNamesAsync 按 Guid.Empty 过滤后直接返回，列表创建人全空。
    public string? CreatorName { get; set; }

    /// <summary>所属教学资源库名称。</summary>
    public string? LibraryName { get; set; }

    /// <summary>
    /// 是否为「共建库共享进来」的资源。
    /// true：资源归属其它学校，通过院校共建库对当前学校可见；
    /// false：资源属于当前学校（自己创建的或本租户内他人创建的）。
    /// 前端资源列表据此显示「来源」列与详情「资源库」信息块。
    /// </summary>
    public bool IsSharedIn { get; set; }

    /// <summary>资源归属学校 Id（共建库中可能不是当前学校）。</summary>
    public Guid? OwnerTenantId { get; set; }

    /// <summary>资源归属学校租户 Id（由 Resource.TenantId 映射，共建库中可能不是当前学校）。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>资源归属学校名称（共建库中可能不是当前学校）。</summary>
    public string? OwnerTenantName { get; set; }
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

    /// <summary>
    /// 所属资源库（多校共建）。非空表示该资源归属某个资源库，
    /// 其专业必须与该库的主建专业一致（后端强制校验）。
    /// </summary>
    public Guid? LibraryId { get; set; }

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


