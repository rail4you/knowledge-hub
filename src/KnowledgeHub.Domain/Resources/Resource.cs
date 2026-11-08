using System;
using System.Collections.Generic;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Resources;

public class Resource : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public ResourceType ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? FileExtension { get; set; }
    public string? OriginalFileName { get; set; }
    /// <summary>
    /// AI 生成的资源摘要（用于语义搜索和展示）
    /// </summary>
    public string? Summary { get; set; }
    public ResourceStatus Status { get; set; }
    public int CurrentVersion { get; set; }
    public string? Keywords { get; set; }
    public string? CopyrightInfo { get; set; }
    public bool IsDownloadable { get; set; }
    public int CollectionCount { get; set; }
    public int DownloadCount { get; set; }
    public int ViewCount { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? MajorId { get; set; }
    /// <summary>
    /// 所属资源库（多校共建）。非空表示该资源归属某个资源库，
    /// 其专业维度须落在该库的主建专业上；合作学校通过库成员关系获得可见性。
    /// </summary>
    public Guid? LibraryId { get; set; }
    public Guid? TenantId { get; set; }

    // ── 来源（课程资源 ⇄ 教学资源库资源 互转） ──────────────────────
    /// <summary>来源类型：新建 / 从课程转换 / 从资源库转换。</summary>
    public ResourceSourceType SourceType { get; set; } = ResourceSourceType.Created;

    /// <summary>转换前的源资源 Id（两段都是副本时用于溯源，不做同步）。</summary>
    public Guid? SourceResourceId { get; set; }

    /// <summary>转换来源课程（SourceType = FromCourse 时有效，用于「来源」列展示）。</summary>
    public Guid? SourceCourseId { get; set; }

    /// <summary>转换来源资源库（SourceType = FromLibrary 时有效，用于「来源」列展示）。</summary>
    public Guid? SourceLibraryId { get; set; }

    /// <summary>媒体处理状态（缩略图/预览生成），供列表页快速渲染。</summary>
    public ResourceMediaStatus MediaStatus { get; set; } = ResourceMediaStatus.None;

    /// <summary>
    /// 是否为 AI 生成内容（教学图片 / 短视频）。
    /// AI 生成的媒体资源与上传资源走同一套审核流程（初始 PendingReview），
    /// 仅用于列表标识与统计区分，不影响可用性判定。
    /// </summary>
    public bool IsAiGenerated { get; set; }

    public ICollection<ResourceVersion> Versions { get; set; }
    public ICollection<ResourceAudit> Audits { get; set; }
    public ICollection<ResourceCollection> Collections { get; set; }
    public ICollection<DocumentIndex> DocumentIndices { get; set; }
    public ICollection<ResourceViewLog> ViewLogs { get; set; }
    public ResourceExposure? Exposure { get; set; }
    public ICollection<ResourceReview> Reviews { get; set; }

    public Resource()
    {
        Versions = new List<ResourceVersion>();
        Audits = new List<ResourceAudit>();
        Collections = new List<ResourceCollection>();
        DocumentIndices = new List<DocumentIndex>();
        ViewLogs = new List<ResourceViewLog>();
        Reviews = new List<ResourceReview>();
    }
}
