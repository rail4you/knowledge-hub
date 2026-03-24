using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.KnowledgeGraph;

public class KnowledgeNode : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Metadata { get; set; }
    public float[]? Embedding { get; set; }
    
    private KnowledgeNode() { }
    
    public KnowledgeNode(Guid id, Guid courseId, string name) : base(id)
    {
        CourseId = courseId;
        Name = name;
    }
    
    public void SetEmbedding(float[] embedding)
    {
        Embedding = embedding;
    }
    
    public void SetMetadata(string metadata)
    {
        Metadata = metadata;
    }
}
