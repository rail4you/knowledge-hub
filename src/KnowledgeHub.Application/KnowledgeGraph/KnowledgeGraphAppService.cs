using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.KnowledgeGraph.Dtos;
using KnowledgeHub.KnowledgeGraph.Enums;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.KnowledgeGraph;

[AllowAnonymous]
public class KnowledgeGraphAppService : KnowledgeHubAppService, IKnowledgeGraphAppService
{
    private readonly IRepository<KnowledgeNode, Guid> _nodeRepository;
    private readonly IRepository<KnowledgeRelation, Guid> _relationRepository;
    private readonly IKnowledgeGraphRepository _knowledgeGraphRepository;

    public KnowledgeGraphAppService(
        IRepository<KnowledgeNode, Guid> nodeRepository,
        IRepository<KnowledgeRelation, Guid> relationRepository,
        IKnowledgeGraphRepository knowledgeGraphRepository)
    {
        _nodeRepository = nodeRepository;
        _relationRepository = relationRepository;
        _knowledgeGraphRepository = knowledgeGraphRepository;
    }

    public async Task<KnowledgeGraphDto> GetGraphAsync(Guid courseId)
    {
        var nodes = await _knowledgeGraphRepository.GetByCourseIdAsync(courseId);
        var relations = await _knowledgeGraphRepository.GetRelationsByCourseIdAsync(courseId);

        return new KnowledgeGraphDto
        {
            CourseId = courseId,
            CourseName = string.Empty,
            Nodes = nodes.Select(MapToNodeDto).ToList(),
            Relations = relations.Select(MapToRelationDto).ToList()
        };
    }

    public async Task<KnowledgeNodeDto> GetNodeAsync(Guid nodeId)
    {
        var node = await _nodeRepository.GetAsync(nodeId);
        return MapToNodeDto(node);
    }

    public async Task<List<KnowledgeRelationDto>> GetRelationsAsync(Guid nodeId)
    {
        var relations = await _knowledgeGraphRepository.GetRelationsByNodeIdAsync(nodeId);
        return relations.Select(MapToRelationDto).ToList();
    }

    public Task<LearningPathDto> GetRecommendedPathAsync(Guid courseId)
    {
        // Basic implementation: return nodes sorted by prerequisite relationships
        throw new NotImplementedException("Learning path recommendation requires more advanced algorithm");
    }

    public async Task BuildGraphAsync(Guid courseId)
    {
        // Trigger graph building from existing course data (chapters, resources, exercises)
        // This is a placeholder - graph building logic would extract knowledge points from course content
        var existingNodes = await _knowledgeGraphRepository.GetByCourseIdAsync(courseId);
        if (existingNodes.Count > 0)
        {
            return; // Already built
        }

        // TODO: Implement automatic graph building from course materials
        // For now, graph is built manually or through admin tools
    }

    /// <summary>
    /// Get knowledge graph for a micro-major (aggregates graphs from all courses in the micro-major)
    /// </summary>
    public async Task<KnowledgeGraphDto> GetMicroMajorGraphAsync(Guid microMajorId, string microMajorName, List<Guid> courseIds)
    {
        var allNodes = new List<KnowledgeNodeDto>();
        var allRelations = new List<KnowledgeRelationDto>();

        // Add micro-major root node
        var rootNodeId = Guid.NewGuid();
        allNodes.Add(new KnowledgeNodeDto
        {
            Id = rootNodeId,
            Name = microMajorName,
            Description = "微专业根节点",
            ImportanceLevel = "high"
        });

        foreach (var courseId in courseIds)
        {
            var courseNodes = await _knowledgeGraphRepository.GetByCourseIdAsync(courseId);
            var courseRelations = await _knowledgeGraphRepository.GetRelationsByCourseIdAsync(courseId);

            // Add course node and connect to root
            var courseNodeId = Guid.NewGuid();
            allNodes.Add(new KnowledgeNodeDto
            {
                Id = courseNodeId,
                Name = $"课程:{courseId}",
                ImportanceLevel = "high"
            });

            allRelations.Add(new KnowledgeRelationDto
            {
                Id = Guid.NewGuid(),
                SourceNodeId = rootNodeId,
                TargetNodeId = courseNodeId,
                Type = RelationType.Contains,
                Weight = 1.0m
            });

            // Map course nodes with offset IDs to avoid conflicts, connect to course node
            var nodeIdMap = new Dictionary<Guid, Guid>();
            foreach (var node in courseNodes)
            {
                var mappedId = Guid.NewGuid();
                nodeIdMap[node.Id] = mappedId;
                allNodes.Add(new KnowledgeNodeDto
                {
                    Id = mappedId,
                    Name = node.Name,
                    Description = node.Description,
                    ImportanceLevel = "normal"
                });

                allRelations.Add(new KnowledgeRelationDto
                {
                    Id = Guid.NewGuid(),
                    SourceNodeId = courseNodeId,
                    TargetNodeId = mappedId,
                    Type = RelationType.Contains,
                    Weight = 1.0m
                });
            }

            // Remap relations
            foreach (var rel in courseRelations)
            {
                if (nodeIdMap.TryGetValue(rel.SourceNodeId, out var sourceId) &&
                    nodeIdMap.TryGetValue(rel.TargetNodeId, out var targetId))
                {
                    allRelations.Add(new KnowledgeRelationDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = sourceId,
                        TargetNodeId = targetId,
                        Type = rel.Type,
                        Weight = rel.Weight,
                        Description = rel.Description
                    });
                }
            }
        }

        return new KnowledgeGraphDto
        {
            Nodes = allNodes,
            Relations = allRelations
        };
    }

    /// <summary>
    /// Get the knowledge graph context for a specific resource (素材)
    /// Shows which knowledge points and courses reference this resource
    /// </summary>
    public async Task<KnowledgeGraphDto> GetResourceGraphAsync(Guid resourceId)
    {
        var query = await _nodeRepository.GetQueryableAsync();
        var nodes = query.Where(x => x.KnowledgeResourceId == resourceId).ToList();

        var resultNodes = new List<KnowledgeNodeDto>();
        var resultRelations = new List<KnowledgeRelationDto>();

        // Add resource as central node
        var centerNodeId = Guid.NewGuid();
        resultNodes.Add(new KnowledgeNodeDto
        {
            Id = centerNodeId,
            Name = $"素材:{resourceId}",
            ImportanceLevel = "high"
        });

        foreach (var node in nodes)
        {
            var mappedId = Guid.NewGuid();
            resultNodes.Add(new KnowledgeNodeDto
            {
                Id = mappedId,
                Name = node.Name,
                Description = node.Description,
                ImportanceLevel = "normal"
            });

            resultRelations.Add(new KnowledgeRelationDto
            {
                Id = Guid.NewGuid(),
                SourceNodeId = centerNodeId,
                TargetNodeId = mappedId,
                Type = RelationType.References,
                Weight = 1.0m,
                Description = "素材关联知识点"
            });
        }

        return new KnowledgeGraphDto
        {
            Nodes = resultNodes,
            Relations = resultRelations
        };
    }

    private static KnowledgeNodeDto MapToNodeDto(KnowledgeNode node)
    {
        return new KnowledgeNodeDto
        {
            Id = node.Id,
            Name = node.Name,
            Description = node.Description,
            ImportanceLevel = "normal",
            KnowledgeResourceId = node.KnowledgeResourceId,
            MasteryLevel = 0
        };
    }

    private static KnowledgeRelationDto MapToRelationDto(KnowledgeRelation relation)
    {
        return new KnowledgeRelationDto
        {
            Id = relation.Id,
            SourceNodeId = relation.SourceNodeId,
            TargetNodeId = relation.TargetNodeId,
            Type = relation.Type,
            Weight = relation.Weight,
            Description = relation.Description
        };
    }
}
