using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Studios;

/// <summary>
/// 工作室资料（成员上传 → 领衔人审核 → 发布）。
///
/// <para><b>为什么引用 Resource 而不是自带文件字段</b>：上传、分片、版本、
/// 多格式预览/下载/媒体处理在资源中心已经完整实现，这里只挂一个
/// <see cref="ResourceId"/>，避免把文件链路复制一遍。</para>
/// </summary>
public class StudioMaterial : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid StudioId { get; set; }

    /// <summary>资源中心里的文件资源 Id（图片/文档/视频）。</summary>
    public Guid ResourceId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public StudioMaterialStatus Status { get; set; } = StudioMaterialStatus.Draft;

    public Guid SubmittedByUserId { get; set; }

    public string SubmittedByName { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public Guid? ReviewedByUserId { get; set; }

    public string? ReviewedByName { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewComment { get; set; }

    public DateTime? PublishedAt { get; set; }

    private StudioMaterial()
    {
    }

    public StudioMaterial(Guid id, Guid? tenantId, Guid studioId, Guid resourceId, string title,
        string? description, Guid submittedByUserId, string submittedByName)
        : base(id)
    {
        TenantId = tenantId;
        StudioId = studioId;
        ResourceId = resourceId;
        Title = title?.Trim() ?? string.Empty;
        Description = description;
        SubmittedByUserId = submittedByUserId;
        SubmittedByName = submittedByName;
    }

    /// <summary>提交审核（草稿 / 已驳回 → 待审）。</summary>
    public void Submit()
    {
        Status = StudioMaterialStatus.PendingReview;
        SubmittedAt = DateTime.UtcNow;
    }

    /// <summary>领衔人通过 → 发布。</summary>
    public void Publish(Guid reviewerId, string reviewerName, string? comment)
    {
        Status = StudioMaterialStatus.Published;
        ReviewedByUserId = reviewerId;
        ReviewedByName = reviewerName;
        ReviewedAt = DateTime.UtcNow;
        ReviewComment = comment;
        PublishedAt = DateTime.UtcNow;
    }

    /// <summary>领衔人驳回。</summary>
    public void Reject(Guid reviewerId, string reviewerName, string comment)
    {
        Status = StudioMaterialStatus.Rejected;
        ReviewedByUserId = reviewerId;
        ReviewedByName = reviewerName;
        ReviewedAt = DateTime.UtcNow;
        ReviewComment = comment;
    }
}
