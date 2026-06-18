using System;
using System.Collections.Generic;
using KnowledgeHub.TenantInfos.Enums;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.TenantInfos;

/// <summary>
/// 租户信息 — 与 ABP Tenant 一对一关联，存储专业资料库的展示信息。
/// </summary>
public class TenantInfo : FullAuditedAggregateRoot<Guid>
{
    public const int MaxNameLength = 256;
    public const int MaxDescriptionLength = 4000;
    public const int MaxCoverImagesLength = 4000;
    public const int MaxTalentTrainingPlanLength = 8000;
    public const int MaxProfessionalTeachingStandardsLength = 8000;
    public const int MaxSpecialProjectsLength = 4000;

    /// <summary>关联的 ABP Tenant Id</summary>
    public Guid TenantId { get; set; }

    /// <summary>租户类型：专业/项目</summary>
    public TenantType Type { get; set; } = TenantType.Professional;

    /// <summary>显示名称（如"市场营销"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述文字</summary>
    public string? Description { get; set; }

    /// <summary>封面图片 URL 列表（JSON 数组），用于轮播</summary>
    public string? CoverImages { get; set; }

    /// <summary>人才培养方案（富文本/HTML）</summary>
    public string? TalentTrainingPlan { get; set; }

    /// <summary>专业教学标准（富文本/HTML）</summary>
    public string? ProfessionalTeachingStandards { get; set; }

    /// <summary>特色项目列表（JSON 数组，每项含名称和描述）</summary>
    public string? SpecialProjects { get; set; }

    protected TenantInfo() { }

    public TenantInfo(
        Guid id,
        Guid tenantId,
        string name,
        TenantType type = TenantType.Professional)
        : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        Type = type;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: MaxNameLength);
    }

    public void SetDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            Description = null;
            return;
        }
        Description = Check.Length(description.Trim(), nameof(description), maxLength: MaxDescriptionLength);
    }

    public void SetCoverImages(string? coverImagesJson)
    {
        if (string.IsNullOrWhiteSpace(coverImagesJson))
        {
            CoverImages = null;
            return;
        }
        CoverImages = Check.Length(coverImagesJson.Trim(), nameof(CoverImages), maxLength: MaxCoverImagesLength);
    }

    public void SetTalentTrainingPlan(string? plan)
    {
        if (string.IsNullOrWhiteSpace(plan))
        {
            TalentTrainingPlan = null;
            return;
        }
        TalentTrainingPlan = Check.Length(plan.Trim(), nameof(TalentTrainingPlan), maxLength: MaxTalentTrainingPlanLength);
    }

    public void SetProfessionalTeachingStandards(string? standards)
    {
        if (string.IsNullOrWhiteSpace(standards))
        {
            ProfessionalTeachingStandards = null;
            return;
        }
        ProfessionalTeachingStandards = Check.Length(standards.Trim(), nameof(ProfessionalTeachingStandards), maxLength: MaxProfessionalTeachingStandardsLength);
    }

    public void SetSpecialProjects(string? projectsJson)
    {
        if (string.IsNullOrWhiteSpace(projectsJson))
        {
            SpecialProjects = null;
            return;
        }
        SpecialProjects = Check.Length(projectsJson.Trim(), nameof(SpecialProjects), maxLength: MaxSpecialProjectsLength);
    }
}
