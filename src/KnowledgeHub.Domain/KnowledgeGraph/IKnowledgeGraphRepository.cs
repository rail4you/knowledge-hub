using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.KnowledgeGraph;

public interface IKnowledgeGraphRepository : IRepository<KnowledgeNode, Guid>
{
    Task<List<KnowledgeNode>> GetByCourseIdAsync(Guid courseId);
    Task<KnowledgeNode?> GetWithRelationsAsync(Guid nodeId);
    Task<List<KnowledgeRelation>> GetRelationsByNodeIdAsync(Guid nodeId);
    Task<List<KnowledgeRelation>> GetRelationsByCourseIdAsync(Guid courseId);
}
