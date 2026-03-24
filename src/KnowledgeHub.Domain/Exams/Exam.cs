using System;
using System.Collections.Generic;
using KnowledgeHub.Exams.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Exams;

public class Exam : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ExamType Type { get; set; } = ExamType.Quiz;
    public int DurationMinutes { get; set; } = 60;
    public int TotalScore { get; set; }
    public int PassingScore { get; set; } = 60;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    public ICollection<ExamExercise> ExamExercises { get; set; }
    
    public Exam()
    {
        ExamExercises = new List<ExamExercise>();
    }
    
    public Exam(Guid id, Guid courseId, string title) : base(id)
    {
        CourseId = courseId;
        Title = title;
        ExamExercises = new List<ExamExercise>();
    }
    
    public void AddExercise(Guid exerciseId, int score, int sortOrder)
    {
        ExamExercises.Add(new ExamExercise(Guid.NewGuid(), Id, exerciseId, score, sortOrder));
        TotalScore += score;
    }
}
