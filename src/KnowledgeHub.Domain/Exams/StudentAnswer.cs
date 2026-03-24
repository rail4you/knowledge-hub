using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Exams;

public class StudentAnswer : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentExamId { get; set; }
    public Guid ExerciseId { get; set; }
    public string? Content { get; set; }
    public int? Score { get; set; }
    public string? Feedback { get; set; }
    public bool? IsCorrect { get; set; }
    public bool IsAiGraded { get; set; }
    
    private StudentAnswer() { }
    
    public StudentAnswer(Guid id, Guid studentExamId, Guid exerciseId) : base(id)
    {
        StudentExamId = studentExamId;
        ExerciseId = exerciseId;
    }
    
    public void SetAnswer(string content)
    {
        Content = content;
    }
    
    public void SetGrade(int? score, string? feedback, bool? isCorrect, bool isAiGraded)
    {
        Score = score;
        Feedback = feedback;
        IsCorrect = isCorrect;
        IsAiGraded = isAiGraded;
    }
}
