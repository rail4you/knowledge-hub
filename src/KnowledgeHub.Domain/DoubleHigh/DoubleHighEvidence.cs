using System;
using KnowledgeHub.DoubleHigh.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

public class DoubleHighEvidence : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid IndicatorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DoubleHighEvidenceType EvidenceType { get; set; } = DoubleHighEvidenceType.ResourceLink;
    public Guid? ResourceId { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? ExternalLink { get; set; }
    public int SortOrder { get; set; }

    /// <summary>
    /// 佐证名称（对应指标「必备佐证」清单里的某一项，如「课程标准」）。
    /// 同一指标可以按不同佐证名称分别上传多个文件。
    /// </summary>
    public string? EvidenceName { get; set; }

    /// <summary>上传文件的原始文件名。</summary>
    public string? FileName { get; set; }

    /// <summary>上传文件大小（字节）。</summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 文件存储路径：<c>oss:{objectKey}</c> / <c>local:{relativePath}</c>。
    /// 导出资料包时按此路径把原始文件打进 zip。
    /// </summary>
    public string? StoredPath { get; set; }

    public DoubleHighEvidence()
    {
    }

    public DoubleHighEvidence(Guid id, Guid projectId, Guid indicatorId, string title)
        : base(id)
    {
        ProjectId = projectId;
        IndicatorId = indicatorId;
        Title = title;
    }
}
