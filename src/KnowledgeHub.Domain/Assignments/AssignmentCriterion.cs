using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Assignments;

/// <summary>
/// 作业评分细则项，如「解题过程 40 / 结果正确 40 / 规范表达 20」。
/// 教师发布作业时配置，学生提交页展示，批阅时按项给分。
/// </summary>
public class AssignmentCriterion : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid AssignmentId { get; set; }

    /// <summary>细则名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>该项满分</summary>
    public decimal MaxScore { get; set; }

    /// <summary>排序</summary>
    public int SortOrder { get; set; }

    /// <summary>补充说明</summary>
    public string? Description { get; set; }

    private AssignmentCriterion()
    {
    }

    public AssignmentCriterion(Guid id, Guid assignmentId, string name, decimal maxScore, int sortOrder = 0) : base(id)
    {
        AssignmentId = assignmentId;
        Name = name;
        MaxScore = maxScore;
        SortOrder = sortOrder;
    }
}
