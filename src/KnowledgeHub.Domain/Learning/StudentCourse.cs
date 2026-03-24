using System;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Learning;

public class StudentCourse : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public StudentCourseStatus Status { get; set; } = StudentCourseStatus.Enrolled;
    public DateTime EnrolledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal Progress { get; set; }
    
    private StudentCourse() { }
    
    public StudentCourse(Guid id, Guid studentId, Guid courseId) : base(id)
    {
        StudentId = studentId;
        CourseId = courseId;
        EnrolledAt = DateTime.UtcNow;
        Status = StudentCourseStatus.Enrolled;
    }
    
    public void StartLearning()
    {
        Status = StudentCourseStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }
    
    public void Complete()
    {
        Status = StudentCourseStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Progress = 100;
    }
    
    public void Drop()
    {
        Status = StudentCourseStatus.Dropped;
    }
    
    public void UpdateProgress(decimal progress)
    {
        Progress = Math.Min(100, progress);
        if (Progress >= 100)
        {
            Complete();
        }
    }
}
