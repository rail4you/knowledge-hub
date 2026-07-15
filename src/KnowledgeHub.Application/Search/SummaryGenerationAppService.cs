using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Search;

/// <summary>
/// 文档 AI 摘要生成的运维入口。
/// - GenerateForResourceAsync：单个资源入队
/// - BatchGenerateAsync：按 BatchSize（默认 20，硬上限 100）批量入队
///   - 默认跳过已有 Summary 的资源
///   - 默认只处理审核通过的资源
///   - 支持按 ResourceType / ResourceIds 过滤
/// </summary>
[Authorize(KnowledgeHubPermissions.Search.ManageIndex)]
public class SummaryGenerationAppService : KnowledgeHubAppService, ISummaryGenerationAppService
{
    private const int MaxBatchSize = 100;

    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<SummaryGenerationAppService> _logger;

    public SummaryGenerationAppService(
        IRepository<Resource, Guid> resourceRepository,
        IBackgroundJobManager backgroundJobManager,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        ILogger<SummaryGenerationAppService> logger)
    {
        _resourceRepository = resourceRepository;
        _backgroundJobManager = backgroundJobManager;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    public async Task<GenerateSummaryResultDto> GenerateForResourceAsync(GenerateSummaryInputDto input)
    {
        if (input.ResourceId == Guid.Empty)
        {
            throw new UserFriendlyException("ResourceId is required");
        }

        Resource? resource;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            resource = await _resourceRepository.FindAsync(input.ResourceId);
        }
        if (resource == null)
        {
            throw new UserFriendlyException($"Resource not found: {input.ResourceId}");
        }

        var tenantId = resource.TenantId;
        var jobId = await _backgroundJobManager.EnqueueAsync(new DocumentSummaryGenerationJobArgs
        {
            ResourceId = input.ResourceId,
            TenantId = tenantId
        });

        _logger.LogInformation(
            "Enqueued summary generation job {JobId} for resource {ResourceId}",
            jobId, input.ResourceId);

        return new GenerateSummaryResultDto
        {
            EnqueuedCount = 1,
            SkippedCount = 0,
            JobIds = new List<string> { jobId }
        };
    }

    public async Task<BatchGenerateSummaryResultDto> BatchGenerateAsync(BatchGenerateSummaryInputDto input)
    {
        if (input == null)
        {
            throw new UserFriendlyException("Input is required");
        }

        var batchSize = Math.Clamp(input.BatchSize <= 0 ? 20 : input.BatchSize, 1, MaxBatchSize);

        List<Resource> candidates;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _resourceRepository.GetQueryableAsync();

            // 显式列表优先
            if (input.ResourceIds != null && input.ResourceIds.Count > 0)
            {
                var idSet = new HashSet<Guid>(input.ResourceIds);
                query = query.Where(r => idSet.Contains(r.Id));
            }

            // 类型过滤
            if (input.ResourceType.HasValue)
            {
                var t = input.ResourceType.Value;
                query = query.Where(r => r.ResourceType == t);
            }

            // 跳过已有摘要
            if (input.SkipExisting)
            {
                query = query.Where(r => string.IsNullOrEmpty(r.Summary));
            }

            // 仅审核通过
            if (input.OnlyApproved)
            {
                query = query.Where(r =>
                    r.Status == ResourceStatus.SchoolApproved ||
                    r.Status == ResourceStatus.LeagueApproved);
            }

            // 限制本次处理的批大小
            query = query.OrderBy(r => r.CreationTime).Take(batchSize);

            candidates = await AsyncExecuter.ToListAsync(query);
        }

        var enqueued = new List<Guid>();
        var skipped = new List<Guid>();

        foreach (var resource in candidates)
        {
            try
            {
                await _backgroundJobManager.EnqueueAsync(new DocumentSummaryGenerationJobArgs
                {
                    ResourceId = resource.Id,
                    TenantId = resource.TenantId
                });
                enqueued.Add(resource.Id);
                _logger.LogInformation(
                    "Enqueued summary generation job for resource {ResourceId} (batch)",
                    resource.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to enqueue summary job for resource {ResourceId}, skipping",
                    resource.Id);
                skipped.Add(resource.Id);
            }
        }

        // 已被 SkipExisting 过滤掉的资源也算 skipped（来自显式列表时方便追踪）
        if (input.ResourceIds != null && input.ResourceIds.Count > 0 && input.SkipExisting)
        {
            var enqueuedSet = new HashSet<Guid>(enqueued);
            var candidateIds = candidates.Select(c => c.Id).ToHashSet();
            foreach (var id in input.ResourceIds)
            {
                if (!enqueuedSet.Contains(id) && !candidateIds.Contains(id))
                {
                    skipped.Add(id);
                }
            }
        }

        var message = $"Enqueued {enqueued.Count}, skipped {skipped.Count} (batchSize={batchSize}).";

        return new BatchGenerateSummaryResultDto
        {
            EnqueuedCount = enqueued.Count,
            SkippedCount = skipped.Count,
            EnqueuedResourceIds = enqueued,
            SkippedResourceIds = skipped,
            Message = message
        };
    }
}