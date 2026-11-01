using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Search;

public interface IMeiliSearchAdminAppService : IApplicationService
{
    Task<MeiliDashboardDto> GetDashboardAsync(Guid? tenantId = null);

    /// <summary>
    /// 重灌已审核资源的文档索引（V2.0）。
    ///
    /// <para>
    /// 索引结构新增了 <c>libraryId</c> / <c>visibility</c> 字段，旧文档缺少这两个字段会走
    /// 「NOT EXISTS 放行」的兼容分支；重灌一次后按库可见性精确裁剪。仅处理已审核资源。
    /// </para>
    /// </summary>
    /// <param name="tenantId">只重灌指定租户；为空则处理当前上下文可见范围（host 为全部）。</param>
    /// <param name="batchSize">每次入队条数上限，避免一次性压满队列。</param>
    Task<int> ReindexAllApprovedAsync(Guid? tenantId = null, int batchSize = 200);
    Task<MeiliIndexStatsDto> GetIndexStatsAsync(string indexUid);
    Task<Dictionary<string, MeiliEmbedderDto>> GetEmbeddersAsync(string indexUid);
    Task<List<MeiliTaskDto>> GetRecentTasksAsync(int limit = 20);
    Task<List<MeiliDocumentGroupDto>> GetIndexDocumentsAsync(string indexUid, int limit = 200, Guid? tenantId = null);
    Task<List<MeiliIndexDto>> GetIndexesAsync();

    /// <summary>
    /// 获取文档的高频词汇（用于热门词 / 词云展示）。
    /// </summary>
    Task<List<HotWordDto>> GetHotWordsAsync(Guid resourceId, int count = 30);
}
