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
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string FileExtension { get; set; }
    public string OriginalFileName { get; set; }
    public ResourceStatus Status { get; set; }
    public int CurrentVersion { get; set; }
    public string? Keywords { get; set; }
    public string? CopyrightInfo { get; set; }
    public bool IsDownloadable { get; set; }
    /// <summary>P1-13：是否作为简历使用。AI 职业规划下拉会列出当前用户已审核通过的简历资源。</summary>
    public bool IsResume { get; set; }
    public int CollectionCount { get; set; }
    public int DownloadCount { get; set; }
    public int ViewCount { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public Guid CreatorId { get; set; }
    public string? CreatorName { get; set; }
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
    public Guid CreatorId { get; set; }
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
    public Guid? MajorId { get; set; }
    public string? Keywords { get; set; }
    public string? CopyrightInfo { get; set; }
    public bool IsDownloadable { get; set; } = true;
    /// <summary>P1-13：上传"文档"类型时可勾选"作为简历使用"，职业规划下拉按此过滤。</summary>
    public bool IsResume { get; set; }
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
