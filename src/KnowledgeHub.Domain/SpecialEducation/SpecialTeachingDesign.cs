using System;
using KnowledgeHub.SpecialEducation;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.SpecialEducation;

/// <summary>智能教学设计方案（特教）：AI 生成 + 结构化存档 + 审核发布。</summary>
public class SpecialTeachingDesign : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CreatorUserId { get; set; }
    public SpecialEduCategory Category { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? ResourceId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public int Duration { get; set; } = 45;

    public string ObjectivesJson { get; set; } = "[]";
    public string KeyPointsJson { get; set; } = "[]";
    public string DifficultiesJson { get; set; } = "[]";
    public string SectionsJson { get; set; } = "[]";
    public string MethodsJson { get; set; } = "[]";
    public string ResourcesJson { get; set; } = "[]";
    public string AssessmentJson { get; set; } = "[]";
    public string HomeworkJson { get; set; } = "[]";
    /// <summary>板书设计（招标明确要求独立要素）。</summary>
    public string BoardDesignJson { get; set; } = "[]";
    public string SlidesOutlineJson { get; set; } = "[]";
    public string ActivitiesJson { get; set; } = "[]";
    public string AssessmentToolsJson { get; set; } = "[]";
    public string StandardBasis { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public string SourceInputJson { get; set; } = "{}";

    /// <summary>内容版本号：AI 生成为 v1，每次结构化编辑自动 +1。</summary>
    public int VersionNumber { get; set; } = 1;

    public SpecialEduPlanStatus Status { get; set; } = SpecialEduPlanStatus.Draft;
    public string? ReviewComment { get; set; }
    /// <summary>指派的审核教师（提交审核时指定）。</summary>
    public Guid? ReviewerUserId { get; set; }

    protected SpecialTeachingDesign() { }

    public SpecialTeachingDesign(Guid id, Guid? tenantId, Guid creatorUserId, SpecialEduCategory category) : base(id)
    {
        TenantId = tenantId;
        CreatorUserId = creatorUserId;
        Category = category;
    }
}
