using System;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Learning;

public class StudentExerciseRecord : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid ExerciseId { get; set; }
    public string? StudentAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public bool HasViewedAnswer { get; set; }
    public DateTime? ViewedAt { get; set; }
    public SelfAssessment SelfAssessment { get; set; } = SelfAssessment.None;
    public TimeSpan TimeSpent { get; set; }
    public DateTime? CompletedAt { get; set; }

    private StudentExerciseRecord() { }

    public StudentExerciseRecord(Guid id, Guid studentId, Guid courseId, Guid exerciseId) : base(id)
    {
        StudentId = studentId;
        CourseId = courseId;
        ExerciseId = exerciseId;
    }

    public void SetAnswer(string? answer, bool? isCorrect)
    {
        StudentAnswer = answer;
        IsCorrect = isCorrect;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkAnswerViewed()
    {
        HasViewedAnswer = true;
        ViewedAt = DateTime.UtcNow;
    }

    public void SetSelfAssessment(SelfAssessment assessment)
    {
        SelfAssessment = assessment;
    }

    public void AddTimeSpent(TimeSpan additionalTime)
    {
        TimeSpent += additionalTime;
    }
}
