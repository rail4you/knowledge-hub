using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace KnowledgeHub.Courses;

public class Chapter : Entity<Guid>
{
    public Guid CourseId { get; private set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    
    public ICollection<KnowledgeResource> KnowledgeResources { get; set; }
    
    private Chapter() { }
    
    public Chapter(Guid id, Guid courseId, string title, int sortOrder) : base(id)
    {
        CourseId = courseId;
        Title = title;
        SortOrder = sortOrder;
        KnowledgeResources = new List<KnowledgeResource>();
    }
}
