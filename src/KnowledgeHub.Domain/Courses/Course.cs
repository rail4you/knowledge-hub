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
    public Guid? MajorId { get; set; }
    /// <summary>
    /// 所属资源库（多校共建）。非空表示该课程归属某个资源库，
    /// 其专业维度须落在该库的主建专业上；合作学校通过库成员关系获得可见性。
    /// </summary>
    public Guid? LibraryId { get; set; }
    public string? Semester { get; set; }
    public int? Credits { get; set; }
    public int? SemesterHours { get; set; }
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public int Difficulty { get; set; } = 1;
    /// <summary>是否推荐课程：学生端「推荐课程」Tab 只显示开启此开关的已发布课程</summary>
    public bool IsRecommended { get; set; }
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
