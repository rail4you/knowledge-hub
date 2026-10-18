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
    
    /// <summary>
    /// 记录「资源学习进度」（视频/文档观看百分比的平均值）。
    /// 注意：课程是否完成只认「全部习题作答完毕」，不看资源进度，
    /// 因此这里不再顺带把状态置为 Completed（历史实现会，导致只看视频也能算完成）。
    /// </summary>
    public void SetResourceProgress(decimal progress)
    {
        Progress = Math.Min(100, Math.Max(0, progress));
    }

    /// <summary>兼容旧调用：等价于 SetResourceProgress（不再自动完成）</summary>
    public void UpdateProgress(decimal progress) => SetResourceProgress(progress);
}
