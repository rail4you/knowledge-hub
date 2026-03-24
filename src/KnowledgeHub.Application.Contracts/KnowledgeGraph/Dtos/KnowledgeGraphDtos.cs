using System;
using System.Collections.Generic;
using KnowledgeHub.KnowledgeGraph.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.KnowledgeGraph.Dtos;

public class KnowledgeGraphDto
{
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public List<KnowledgeNodeDto> Nodes { get; set; } = new();
    public List<KnowledgeRelationDto> Relations { get; set; } = new();
}

public class KnowledgeNodeDto : EntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ImportanceLevel { get; set; } = "normal";
    public Guid? KnowledgeResourceId { get; set; }
    public int MasteryLevel { get; set; }
}

public class KnowledgeRelationDto
{
    public Guid Id { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public RelationType Type { get; set; }
    public decimal Weight { get; set; }
    public string? Description { get; set; }
}

public class LearningPathDto
{
    public Guid CourseId { get; set; }
    public List<LearningPathNodeDto> Nodes { get; set; } = new();
    public int EstimatedMinutes { get; set; }
    public int TotalNodes { get; set; }
    public int CompletedNodes { get; set; }
}

public class LearningPathNodeDto
{
    public Guid NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsCompleted { get; set; }
    public List<Guid> PrerequisiteNodeIds { get; set; } = new();
}
