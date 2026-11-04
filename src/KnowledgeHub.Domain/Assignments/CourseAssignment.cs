using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeHub.Assignments.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Assignments;

/// <summary>
/// 课程作业（教师发布 → 学生提交 → 教师批阅）。
///
/// 与「考试」的区别：考试是题库组卷 + 在线答题，本实体是「作业」——
/// 教师发布一份带说明 / 附件 / 评分细则的作业，学生上传文件（PDF / 图片 / ZIP），
/// 教师逐份批阅并给出得分明细与评语。
/// </summary>
public class CourseAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>所属课程</summary>
    public Guid CourseId { get; set; }

    /// <summary>可选：关联章节</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>作业标题，如「第3次 多元函数微分」</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>作业要求正文</summary>
    public string? Description { get; set; }

    /// <summary>个人 / 小组</summary>
    public AssignmentType Type { get; set; } = AssignmentType.Individual;

    /// <summary>满分</summary>
    public decimal MaxScore { get; set; } = 100m;

    /// <summary>截止时间</summary>
    public DateTime Deadline { get; set; }

    /// <summary>是否允许迟交</summary>
    public bool AllowLate { get; set; } = true;

    /// <summary>迟交扣分百分比（0~100），批阅时由教师选择是否自动扣除</summary>
    public decimal LatePenaltyPercent { get; set; }

    /// <summary>允许的文件扩展名（逗号分隔，小写，不含点）</summary>
    public string AllowedFileTypes { get; set; } = "pdf,doc,docx,xls,xlsx,ppt,pptx,zip,rar,7z,png,jpg,jpeg,txt,md";

    /// <summary>单个文件大小上限（MB）</summary>
    public int MaxFileSizeMB { get; set; } = 20;

    /// <summary>最多可上传文件数</summary>
    public int MaxFileCount { get; set; } = 5;

    /// <summary>是否允许截止前重复提交（以最新版为准）</summary>
    public bool AllowResubmit { get; set; } = true;

    /// <summary>作业状态</summary>
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Draft;

    /// <summary>发布人（任课教师）</summary>
    public Guid TeacherId { get; set; }

    /// <summary>发布时间</summary>
    public DateTime? PublishTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime? ClosedTime { get; set; }

    /// <summary>评分细则项</summary>
    public ICollection<AssignmentCriterion> Criteria { get; set; }

    /// <summary>作业文件（要求附件 / 学生提交件 / 教师批注版）</summary>
    public ICollection<AssignmentFile> Files { get; set; }

    /// <summary>学生提交</summary>
    public ICollection<AssignmentSubmission> Submissions { get; set; }

    private CourseAssignment()
    {
        Criteria = new List<AssignmentCriterion>();
        Files = new List<AssignmentFile>();
        Submissions = new List<AssignmentSubmission>();
    }

    public CourseAssignment(Guid id, Guid courseId, string title) : base(id)
    {
        CourseId = courseId;
        Title = title;
        Criteria = new List<AssignmentCriterion>();
        Files = new List<AssignmentFile>();
        Submissions = new List<AssignmentSubmission>();
    }

    /// <summary>发布作业：草稿 → 已发布。</summary>
    public void Publish(DateTime publishTime)
    {
        Status = AssignmentStatus.Published;
        PublishTime = publishTime;
    }

    /// <summary>结束作业：不再接受提交。</summary>
    public void Close(DateTime closedTime)
    {
        Status = AssignmentStatus.Closed;
        ClosedTime = closedTime;
    }

    /// <summary>重置为草稿（撤回发布）。</summary>
    public void ResetToDraft()
    {
        Status = AssignmentStatus.Draft;
        PublishTime = null;
    }

    /// <summary>按评分细则项设置满分合计（未配置细则时保持传入值）。</summary>
    public void SyncMaxScoreFromCriteria()
    {
        if (Criteria.Count > 0)
        {
            MaxScore = Criteria.Sum(x => x.MaxScore);
        }
    }
}
