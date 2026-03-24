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
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; } = ExerciseType.SingleChoice;
    public string? Options { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string? AnswerExplanation { get; set; }
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
