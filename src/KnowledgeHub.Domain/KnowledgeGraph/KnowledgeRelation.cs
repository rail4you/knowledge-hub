using System;
using KnowledgeHub.KnowledgeGraph.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.KnowledgeGraph;

public class KnowledgeRelation : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public RelationType Type { get; set; } = RelationType.Prerequisite;
    public decimal Weight { get; set; } = 1.0m;
    public string? Description { get; set; }
    
    private KnowledgeRelation() { }
    
    public KnowledgeRelation(Guid id, Guid sourceNodeId, Guid targetNodeId, RelationType type, decimal weight = 1.0m) : base(id)
    {
        SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId;
        Type = type;
        Weight = weight;
    }
}
