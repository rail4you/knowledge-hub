using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.KnowledgeGraph;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace KnowledgeHub.EntityFrameworkCore.KnowledgeGraph;

public class EfCoreKnowledgeGraphRepository
    : EfCoreRepository<KnowledgeHubDbContext, KnowledgeNode, Guid>,
      IKnowledgeGraphRepository
{
    public EfCoreKnowledgeGraphRepository(
        IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<KnowledgeNode>> GetByCourseIdAsync(Guid courseId)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.Where(x => x.CourseId == courseId).ToListAsync();
    }

    public async Task<KnowledgeNode?> GetWithRelationsAsync(Guid nodeId)
    {
        return await (await GetDbSetAsync()).FirstOrDefaultAsync(n => n.Id == nodeId);
    }

    public async Task<List<KnowledgeRelation>> GetRelationsByNodeIdAsync(Guid nodeId)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.Set<KnowledgeRelation>()
            .Where(r => r.SourceNodeId == nodeId || r.TargetNodeId == nodeId)
            .ToListAsync();
    }

    public async Task<List<KnowledgeRelation>> GetRelationsByCourseIdAsync(Guid courseId)
    {
        var dbContext = await GetDbContextAsync();
        var nodeIds = await dbContext.Set<KnowledgeNode>()
            .Where(n => n.CourseId == courseId)
            .Select(n => n.Id)
            .ToListAsync();
        return await dbContext.Set<KnowledgeRelation>()
            .Where(r => nodeIds.Contains(r.SourceNodeId) || nodeIds.Contains(r.TargetNodeId))
            .ToListAsync();
    }
}
