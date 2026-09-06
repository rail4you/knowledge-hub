using System;
using KnowledgeHub.SpecialEducation;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.SpecialEducation;

/// <summary>IEP 教学实施方案：学生现状 + 长/短期目标 + 策略 + 评估 + 家校协同，支持版本迭代。</summary>
public class IepPlan : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentUserId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public SpecialEduCategory Category { get; set; }
    public Guid? CourseId { get; set; }

    /// <summary>学生现状分析：评估数据（手动表单）+ 障碍类型 + 发展水平 + 选课关联快照。</summary>
    public string ProfileJson { get; set; } = "{}";
    public string LongTermGoalsJson { get; set; } = "[]";
    public string ShortTermGoalsJson { get; set; } = "[]";
    public string StrategiesJson { get; set; } = "[]";
    public string EvaluationJson { get; set; } = "[]";
    public string HomeSchoolJson { get; set; } = "[]";
    public string LegalBasis { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public string SourceInputJson { get; set; } = "{}";

    public int VersionNumber { get; set; } = 1;
    public Guid? ParentVersionId { get; set; }

    public SpecialEduPlanStatus Status { get; set; } = SpecialEduPlanStatus.Draft;
    public string? ReviewComment { get; set; }
    /// <summary>指派的审核教师（提交审核时指定）。</summary>
    public Guid? ReviewerUserId { get; set; }

    protected IepPlan() { }

    public IepPlan(Guid id, Guid? tenantId, Guid studentUserId, SpecialEduCategory category) : base(id)
    {
        TenantId = tenantId;
        StudentUserId = studentUserId;
        Category = category;
    }
}
