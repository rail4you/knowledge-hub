using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Assignments;

/// <summary>
/// 作业得分明细（一条提交 × 一个评分细则项）。
/// 细则名称与满分在此做快照，避免教师后续修改作业细则影响历史成绩。
/// </summary>
public class SubmissionScore : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid SubmissionId { get; set; }

    /// <summary>评分细则 Id（可能已被删除，故可空）</summary>
    public Guid? CriterionId { get; set; }

    /// <summary>细则名称快照</summary>
    public string CriterionName { get; set; } = string.Empty;

    /// <summary>该项满分快照</summary>
    public decimal MaxScore { get; set; }

    /// <summary>该项得分</summary>
    public decimal Score { get; set; }

    /// <summary>该项评语</summary>
    public string? Comment { get; set; }

    /// <summary>排序</summary>
    public int SortOrder { get; set; }

    private SubmissionScore()
    {
    }

    public SubmissionScore(
        Guid id,
        Guid submissionId,
        Guid? criterionId,
        string criterionName,
        decimal maxScore,
        decimal score,
        int sortOrder = 0,
        string? comment = null) : base(id)
    {
        SubmissionId = submissionId;
        CriterionId = criterionId;
        CriterionName = criterionName;
        MaxScore = maxScore;
        Score = score;
        SortOrder = sortOrder;
        Comment = comment;
    }
}
