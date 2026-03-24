using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Learning;

public class LearningProgress : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? ResourceId { get; set; }
    
    public decimal Progress { get; set; }
    public string? LastPosition { get; set; }
    public TimeSpan TimeSpent { get; set; }
    public DateTime LastAccessAt { get; set; }
    
    private LearningProgress() { }
    
    public LearningProgress(Guid id, Guid studentId, Guid courseId) : base(id)
    {
        StudentId = studentId;
        CourseId = courseId;
        LastAccessAt = DateTime.UtcNow;
    }
    
    public void UpdateProgress(decimal progress, string? position, TimeSpan additionalTime)
    {
        Progress = Math.Min(100, progress);
        LastPosition = position;
        TimeSpent = TimeSpent.Add(additionalTime);
        LastAccessAt = DateTime.UtcNow;
    }
}
