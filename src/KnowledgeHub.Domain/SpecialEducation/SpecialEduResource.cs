using System;
using KnowledgeHub.SpecialEducation;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.SpecialEducation;

/// <summary>多模态课程资源：文本/图片描述/音频脚本/视频脚本 + 特教专用素材。</summary>
public class SpecialEduResource : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CreatorUserId { get; set; }
    public Guid? TeachingDesignId { get; set; }
    public Guid? IepPlanId { get; set; }
    public Guid? CourseId { get; set; }

    public string Title { get; set; } = string.Empty;
    public SpecialEduCategory Category { get; set; }
    public string Modality { get; set; } = SpecialEduResourceModality.Text;
    public string ContentJson { get; set; } = "{}";
    public string RawJson { get; set; } = string.Empty;
    public string SourceInputJson { get; set; } = "{}";

    public SpecialEduPlanStatus Status { get; set; } = SpecialEduPlanStatus.Draft;
    public string? ReviewComment { get; set; }
    /// <summary>指派的审核教师（提交审核时指定）。</summary>
    public Guid? ReviewerUserId { get; set; }

    protected SpecialEduResource() { }

    public SpecialEduResource(Guid id, Guid? tenantId, Guid creatorUserId, SpecialEduCategory category, string modality) : base(id)
    {
        TenantId = tenantId;
        CreatorUserId = creatorUserId;
        Category = category;
        Modality = modality;
    }
}
