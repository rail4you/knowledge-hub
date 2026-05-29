using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.KnowledgeGraph.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.KnowledgeGraph;

public interface IKnowledgeGraphAppService : IApplicationService
{
    Task<KnowledgeGraphDto> GetGraphAsync(Guid courseId);
    Task<KnowledgeGraphDto> GetMicroMajorGraphAsync(Guid microMajorId, string microMajorName, List<Guid> courseIds);
    Task<KnowledgeGraphDto> GetResourceGraphAsync(Guid resourceId);
    Task<KnowledgeNodeDto> GetNodeAsync(Guid nodeId);
    Task<List<KnowledgeRelationDto>> GetRelationsAsync(Guid nodeId);
    Task<LearningPathDto> GetRecommendedPathAsync(Guid courseId);
    Task BuildGraphAsync(Guid courseId);
}
