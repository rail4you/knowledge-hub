using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Courses;

public class KnowledgeResource : FullAuditedEntity<Guid>
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string ImportanceLevel { get; set; } = "normal";
    public int Difficulty { get; set; } = 1;
    public int SortOrder { get; set; }
    public string? Tags { get; set; }
    
    public Guid? ParentId { get; set; }
    public Guid? ResourceId { get; set; }
    public float[]? Embedding { get; set; }
    
    public KnowledgeResource? Parent { get; set; }
    public ICollection<KnowledgeResource> Children { get; set; }
    
    private KnowledgeResource() 
    { 
        Children = new List<KnowledgeResource>();
    }
    
    public KnowledgeResource(Guid id, Guid courseId, string name) : base(id)
    {
        CourseId = courseId;
        Name = name;
        Children = new List<KnowledgeResource>();
    }
    
    public void SetEmbedding(float[] embedding)
    {
        Embedding = embedding;
    }
}
