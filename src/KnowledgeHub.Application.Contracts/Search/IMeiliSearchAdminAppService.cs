using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Search;

public interface IMeiliSearchAdminAppService : IApplicationService
{
    Task<MeiliDashboardDto> GetDashboardAsync(Guid? tenantId = null);
    Task<MeiliIndexStatsDto> GetIndexStatsAsync(string indexUid);
    Task<Dictionary<string, MeiliEmbedderDto>> GetEmbeddersAsync(string indexUid);
    Task<List<MeiliTaskDto>> GetRecentTasksAsync(int limit = 20);
    Task<List<MeiliDocumentGroupDto>> GetIndexDocumentsAsync(string indexUid, int limit = 200, Guid? tenantId = null);
    Task<List<MeiliIndexDto>> GetIndexesAsync();
    Task<List<PageIndexListItemDto>> GetPageIndexListAsync(Guid? tenantId = null);
}
