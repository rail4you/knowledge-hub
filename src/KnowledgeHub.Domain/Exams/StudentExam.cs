using System;
using System.Collections.Generic;
using KnowledgeHub.Exams.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Exams;

public class StudentExam : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ExamId { get; set; }
    
    public ExamStatus Status { get; set; } = ExamStatus.NotStarted;
    public DateTime? StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }
    public int? TotalScore { get; set; }
    public bool? IsPassed { get; set; }
    
    public ICollection<StudentAnswer> Answers { get; set; }
    
    public StudentExam()
    {
        Answers = new List<StudentAnswer>();
    }
    
    public StudentExam(Guid id, Guid studentId, Guid examId) : base(id)
    {
        StudentId = studentId;
        ExamId = examId;
        Answers = new List<StudentAnswer>();
    }
    
    public void Start()
    {
        Status = ExamStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }
    
    public void Submit()
    {
        Status = ExamStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
    }
    
    public void SetGrade(int score, bool isPassed)
    {
        TotalScore = score;
        IsPassed = isPassed;
        Status = ExamStatus.Graded;
        GradedAt = DateTime.UtcNow;
    }
}
