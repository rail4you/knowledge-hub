using System;
using System.Collections.Generic;
using KnowledgeHub.Courses.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

public class Course : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Major { get; set; }
    public string? Semester { get; set; }
    public int? Credits { get; set; }
    public int? SemesterHours { get; set; }
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public int Difficulty { get; set; } = 1;
    public Guid? TeacherId { get; set; }
    public Guid? CategoryId { get; set; }
    
    public ICollection<Chapter> Chapters { get; set; }
    
    public Course()
    {
        Chapters = new List<Chapter>();
    }
    
    public Course(Guid id, string title) : base(id)
    {
        Title = title;
        Chapters = new List<Chapter>();
    }
    
    public void AddChapter(string title, int sortOrder)
    {
        Chapters.Add(new Chapter(Guid.NewGuid(), Id, title, sortOrder));
    }
    
    public void SetStatus(CourseStatus status)
    {
        Status = status;
    }
}
