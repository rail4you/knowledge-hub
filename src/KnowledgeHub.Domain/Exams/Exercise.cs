using System;
using KnowledgeHub.Exams.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Exams;

public class Exercise : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    /// <summary>
    /// 专业维度（资源库 / 课程的专业限制）。可由所属课程的主专业带出；
    /// 挂进资源库时必须与该库主建专业一致。
    /// </summary>
    public Guid? MajorId { get; set; }
    /// <summary>
    /// 所属资源库（多校共建）。非空表示该习题归属某个资源库。
    /// </summary>
    public Guid? LibraryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; } = ExerciseType.SingleChoice;
    public string? Options { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string? AnswerExplanation { get; set; }
    public string? QuestionAnalysis { get; set; }
    public int Difficulty { get; set; } = 1;
    public int Score { get; set; } = 1;
    public bool IsAiGenerated { get; set; }
    
    private Exercise() { }
    
    public Exercise(Guid id, Guid courseId, string title, string questionContent, ExerciseType type, string answer) : base(id)
    {
        CourseId = courseId;
        Title = title;
        QuestionContent = questionContent;
        Type = type;
        Answer = answer;
    }
}
