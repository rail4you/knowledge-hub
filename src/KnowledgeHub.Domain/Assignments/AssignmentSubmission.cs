using System;
using System.Collections.Generic;
using KnowledgeHub.Assignments.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Assignments;

/// <summary>
/// 学生作业提交（一名学生 × 一份作业 = 一条记录，多次提交以版本号区分）。
/// </summary>
public class AssignmentSubmission : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>所属作业</summary>
    public Guid AssignmentId { get; set; }

    /// <summary>学生用户 Id</summary>
    public Guid StudentId { get; set; }

    /// <summary>学生姓名（冗余，便于列表展示）</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>学号（冗余）</summary>
    public string? StudentNumber { get; set; }

    /// <summary>教学班 Id（冗余，便于按班统计）</summary>
    public Guid? ClassId { get; set; }

    /// <summary>提交状态</summary>
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Draft;

    /// <summary>学生补充说明</summary>
    public string? Note { get; set; }

    /// <summary>是否勾选学术诚信承诺</summary>
    public bool HonorPledge { get; set; }

    /// <summary>首次提交时间</summary>
    public DateTime? FirstSubmittedAt { get; set; }

    /// <summary>最近一次提交时间</summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>是否迟交（提交时间晚于截止时间）</summary>
    public bool IsLate { get; set; }

    /// <summary>提交版本号（每次提交 +1）</summary>
    public int Version { get; set; }

    /// <summary>总分（批阅后写入）</summary>
    public decimal? Score { get; set; }

    /// <summary>教师评语</summary>
    public string? Comment { get; set; }

    /// <summary>批阅人 Id</summary>
    public Guid? ReviewerId { get; set; }

    /// <summary>批阅人姓名（冗余）</summary>
    public string? ReviewerName { get; set; }

    /// <summary>批阅时间</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>教师是否允许该生重新提交</summary>
    public bool AllowResubmit { get; set; }

    /// <summary>本条提交的文件</summary>
    public ICollection<AssignmentFile> Files { get; set; }

    /// <summary>得分明细（按评分细则项）</summary>
    public ICollection<SubmissionScore> Scores { get; set; }

    private AssignmentSubmission()
    {
        Files = new List<AssignmentFile>();
        Scores = new List<SubmissionScore>();
    }

    public AssignmentSubmission(Guid id, Guid assignmentId, Guid studentId, string studentName) : base(id)
    {
        AssignmentId = assignmentId;
        StudentId = studentId;
        StudentName = studentName;
        Files = new List<AssignmentFile>();
        Scores = new List<SubmissionScore>();
    }

    /// <summary>记录一次提交（版本 +1）。</summary>
    public void Submit(DateTime submitTime, bool isLate)
    {
        Version += 1;
        Status = SubmissionStatus.Submitted;
        SubmittedAt = submitTime;
        FirstSubmittedAt ??= submitTime;
        IsLate = isLate;
    }

    /// <summary>记录批阅结果。</summary>
    public void Grade(decimal? score, string? comment, Guid reviewerId, string? reviewerName, DateTime reviewedAt)
    {
        Score = score;
        Comment = comment;
        ReviewerId = reviewerId;
        ReviewerName = reviewerName;
        ReviewedAt = reviewedAt;
        Status = SubmissionStatus.Graded;
    }
}
