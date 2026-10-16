using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Resources.Media;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Edition;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Resources;

/// <summary>
/// 资源任务实现：合并媒体处理、文档索引、视频索引三类独立任务，按资源聚合成嵌套表格，
/// 以资源分页；每个任务可独立重试。
/// </summary>
[Authorize(KnowledgeHubPermissions.Resources.Default)]
public class ResourceTaskAppService : KnowledgeHubAppService, IResourceTaskAppService
{
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceMediaJob, Guid> _mediaJobRepository;
    private readonly IRepository<DocumentIndexingJob, Guid> _documentJobRepository;
    private readonly IRepository<VideoIndexingJob, Guid> _videoJobRepository;
    private readonly IRepository<ResourceArtifact, Guid> _artifactRepository;
    private readonly ResourceTaskRetryService _taskRetryService;
    private readonly IEditionConfigService _editionConfigService;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IMeiliSearchService _meiliSearchService;
    private readonly ILogger<ResourceTaskAppService> _logger;

    public ResourceTaskAppService(
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceMediaJob, Guid> mediaJobRepository,
        IRepository<DocumentIndexingJob, Guid> documentJobRepository,
        IRepository<VideoIndexingJob, Guid> videoJobRepository,
        IRepository<ResourceArtifact, Guid> artifactRepository,
        ResourceTaskRetryService taskRetryService,
        IEditionConfigService editionConfigService,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        IMeiliSearchService meiliSearchService,
        ILogger<ResourceTaskAppService> logger)
    {
        _resourceRepository = resourceRepository;
        _mediaJobRepository = mediaJobRepository;
        _documentJobRepository = documentJobRepository;
        _videoJobRepository = videoJobRepository;
        _artifactRepository = artifactRepository;
        _taskRetryService = taskRetryService;
        _editionConfigService = editionConfigService;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _meiliSearchService = meiliSearchService;
        _logger = logger;
    }

    public async Task<PagedResultDto<ResourceTaskGroupDto>> GetGroupedListAsync(GetResourceTaskGroupsInput input)
    {
        using var _ = DisableTenantFilterForHost();

        var resourceQuery = await _resourceRepository.GetQueryableAsync();
        if (input.ResourceId.HasValue)
        {
            resourceQuery = resourceQuery.Where(r => r.Id == input.ResourceId.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var kw = input.Filter.Trim();
            resourceQuery = resourceQuery.Where(r => r.Name.Contains(kw));
        }
        if (input.ResourceStatus.HasValue)
        {
            resourceQuery = resourceQuery.Where(r => r.Status == input.ResourceStatus.Value);
        }

        var matchedResourceIds = resourceQuery.Select(r => r.Id);

        var mediaQ = (await _mediaJobRepository.GetQueryableAsync()).Where(j => matchedResourceIds.Contains(j.ResourceId));
        var docQ = (await _documentJobRepository.GetQueryableAsync()).Where(j => matchedResourceIds.Contains(j.ResourceId));
        var vidQ = (await _videoJobRepository.GetQueryableAsync()).Where(j => matchedResourceIds.Contains(j.ResourceId));

        // 按归一化任务状态过滤（各任务表枚举不同，分别映射）
        var taskStatus = input.TaskStatus?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(taskStatus))
        {
            switch (taskStatus)
            {
                case "pending":
                    mediaQ = mediaQ.Where(j => j.Status == ResourceMediaJobStatus.Pending);
                    docQ = docQ.Where(j => j.Status == IndexingJobStatus.Pending);
                    vidQ = vidQ.Where(j => j.Status == VideoIndexingJobStatus.Pending);
                    break;
                case "running":
                    mediaQ = mediaQ.Where(j => j.Status == ResourceMediaJobStatus.Running);
                    docQ = docQ.Where(j => j.Status == IndexingJobStatus.Parsing || j.Status == IndexingJobStatus.Indexing);
                    vidQ = vidQ.Where(j => j.Status == VideoIndexingJobStatus.Parsing
                        || j.Status == VideoIndexingJobStatus.Analyzing
                        || j.Status == VideoIndexingJobStatus.Indexing);
                    break;
                case "success":
                    mediaQ = mediaQ.Where(j => j.Status == ResourceMediaJobStatus.Completed);
                    docQ = docQ.Where(j => j.Status == IndexingJobStatus.Completed);
                    vidQ = vidQ.Where(j => j.Status == VideoIndexingJobStatus.Completed);
                    break;
                case "partial":
                    mediaQ = mediaQ.Where(j => j.Status == ResourceMediaJobStatus.PartialFailed);
                    docQ = docQ.Where(j => false);
                    vidQ = vidQ.Where(j => false);
                    break;
                case "failed":
                    mediaQ = mediaQ.Where(j => j.Status == ResourceMediaJobStatus.Failed || j.Status == ResourceMediaJobStatus.PartialFailed);
                    docQ = docQ.Where(j => j.Status == IndexingJobStatus.Failed);
                    vidQ = vidQ.Where(j => j.Status == VideoIndexingJobStatus.Failed);
                    break;
                case "cancelled":
                    mediaQ = mediaQ.Where(j => j.Status == ResourceMediaJobStatus.Cancelled);
                    docQ = docQ.Where(j => j.Status == IndexingJobStatus.Cancelled);
                    vidQ = vidQ.Where(j => j.Status == VideoIndexingJobStatus.Cancelled);
                    break;
            }
        }

        // 每张表按资源取最近任务时间，合并后以资源为组分页
        var latest = new Dictionary<Guid, DateTime>();
        var mediaLatest = await AsyncExecuter.ToListAsync(
            mediaQ.GroupBy(j => j.ResourceId).Select(g => new { ResourceId = g.Key, Latest = g.Max(x => x.CreationTime) }));
        foreach (var x in mediaLatest)
        {
            if (!latest.TryGetValue(x.ResourceId, out var t) || x.Latest > t)
            {
                latest[x.ResourceId] = x.Latest;
            }
        }
        var docLatest = await AsyncExecuter.ToListAsync(
            docQ.GroupBy(j => j.ResourceId).Select(g => new { ResourceId = g.Key, Latest = g.Max(x => x.CreationTime) }));
        foreach (var x in docLatest)
        {
            if (!latest.TryGetValue(x.ResourceId, out var t) || x.Latest > t)
            {
                latest[x.ResourceId] = x.Latest;
            }
        }
        var vidLatest = await AsyncExecuter.ToListAsync(
            vidQ.GroupBy(j => j.ResourceId).Select(g => new { ResourceId = g.Key, Latest = g.Max(x => x.CreationTime) }));
        foreach (var x in vidLatest)
        {
            if (!latest.TryGetValue(x.ResourceId, out var t) || x.Latest > t)
            {
                latest[x.ResourceId] = x.Latest;
            }
        }

        var totalCount = latest.Count;
        var pageIds = latest.OrderByDescending(kv => kv.Value)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .Select(kv => kv.Key)
            .ToList();

        if (pageIds.Count == 0)
        {
            return new PagedResultDto<ResourceTaskGroupDto>(totalCount, new List<ResourceTaskGroupDto>());
        }

        var resources = await AsyncExecuter.ToListAsync(resourceQuery.Where(r => pageIds.Contains(r.Id)));
        var resourceMap = resources.ToDictionary(r => r.Id);

        var mediaJobs = await AsyncExecuter.ToListAsync(
            mediaQ.Where(j => pageIds.Contains(j.ResourceId)).OrderByDescending(j => j.CreationTime));
        var docJobs = await AsyncExecuter.ToListAsync(
            docQ.Where(j => pageIds.Contains(j.ResourceId)).OrderByDescending(j => j.CreationTime));
        var vidJobs = await AsyncExecuter.ToListAsync(
            vidQ.Where(j => pageIds.Contains(j.ResourceId)).OrderByDescending(j => j.CreationTime));

        // 失联 Completed 检测：DB 标记为 Completed 但 MeiliSearch videos 索引里一条文档都没有——
        // 多发生在 MeiliSearch 重建 / 迁移 / 清空后，DB 状态未跟着变，导致重试按钮永远不可点。
        // （只有 Status=Completed 且 TotalEvents>0 才需要排查——重置后的 Pending/Indexing 自然可重试。）
        var orphanedVideoResourceIds = await DetectOrphanedVideoResourcesAsync(vidJobs);

        var artifacts = await AsyncExecuter.ToListAsync(
            (await _artifactRepository.GetQueryableAsync()).Where(a => pageIds.Contains(a.ResourceId)));
        var artifactLookup = artifacts.ToLookup(a => (a.ResourceId, a.ResourceVersionId));

        var tasks = new List<ResourceTaskDto>();

        foreach (var j in mediaJobs)
        {
            var versionKey = j.ResourceVersionId ?? j.ResourceId;
            tasks.Add(new ResourceTaskDto
            {
                Id = j.Id,
                ResourceId = j.ResourceId,
                Kind = "media",
                Status = NormalizeMediaStatus(j.Status),
                StatusValue = (int)j.Status,
                Progress = j.Progress,
                Message = j.ProgressMessage,
                ErrorMessage = j.ErrorMessage,
                RetryCount = j.RetryCount,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt,
                CreationTime = j.CreationTime,
                ResourceVersionId = j.ResourceVersionId,
                CanRetry = j.Status is ResourceMediaJobStatus.Failed or ResourceMediaJobStatus.PartialFailed,
                Artifacts = artifactLookup[(j.ResourceId, versionKey)].Select(MapArtifact).ToList()
            });
        }

        foreach (var j in docJobs)
        {
            tasks.Add(new ResourceTaskDto
            {
                Id = j.Id,
                ResourceId = j.ResourceId,
                Kind = "document-index",
                Status = NormalizeDocumentStatus(j.Status),
                StatusValue = (int)j.Status,
                Progress = j.Progress,
                Message = j.ProcessedPages.HasValue && j.TotalPages.HasValue
                    ? $"{j.ProcessedPages}/{j.TotalPages} 页"
                    : null,
                ErrorMessage = j.ErrorMessage,
                RetryCount = j.RetryCount,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt,
                CreationTime = j.CreationTime,
                ResourceVersionId = j.ResourceVersionId,
                CanRetry = j.Status == IndexingJobStatus.Failed
            });
        }

        foreach (var j in vidJobs)
        {
            tasks.Add(new ResourceTaskDto
            {
                Id = j.Id,
                ResourceId = j.ResourceId,
                Kind = "video-index",
                Status = NormalizeVideoStatus(j.Status),
                StatusValue = (int)j.Status,
                Progress = j.Progress,
                Message = j.ProcessedEvents.HasValue && j.TotalEvents.HasValue
                    ? $"{j.ProcessedEvents}/{j.TotalEvents} 事件"
                    : null,
                ErrorMessage = j.ErrorMessage,
                RetryCount = j.RetryCount,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt,
                CreationTime = j.CreationTime,
                ResourceVersionId = j.ResourceVersionId,
                // 原本只允许 Failed 重试。2026-10-10 事件后发现：Completed 状言”下
                // MeiliSearch videos 索引里仍可能一条文档都没有（重建 / 清空的偏差）。
                // 失联 Completed 也允许重试，避免重试按钮永远不可点。
                CanRetry = j.Status == VideoIndexingJobStatus.Failed
                    || (j.Status == VideoIndexingJobStatus.Completed && orphanedVideoResourceIds.Contains(j.ResourceId))
            });
        }

        var twoLevel = await _editionConfigService.IsTwoLevelApprovalEnabledAsync();
        var taskLookup = tasks.ToLookup(t => t.ResourceId);

        var groups = new List<ResourceTaskGroupDto>();
        foreach (var resourceId in pageIds)
        {
            resourceMap.TryGetValue(resourceId, out var resource);
            var groupTasks = taskLookup[resourceId].OrderByDescending(t => t.CreationTime).ToList();
            var latestTask = groupTasks.FirstOrDefault();
            var status = resource?.Status ?? ResourceStatus.Draft;

            groups.Add(new ResourceTaskGroupDto
            {
                ResourceId = resourceId,
                ResourceName = resource?.Name,
                TenantId = resource?.TenantId,
                ResourceStatus = status,
                MediaStatus = resource?.MediaStatus ?? ResourceMediaStatus.None,
                VisibleToStudents = twoLevel
                    ? status == ResourceStatus.LeagueApproved
                    : status is ResourceStatus.SchoolApproved or ResourceStatus.LeagueApproved,
                TaskCount = groupTasks.Count,
                FailedCount = groupTasks.Count(t => t.Status is "failed" or "partial"),
                LatestTaskStatus = latestTask?.Status,
                LatestCreationTime = latest[resourceId],
                Tasks = groupTasks
            });
        }

        return new PagedResultDto<ResourceTaskGroupDto>(totalCount, groups);
    }

    public async Task RetryAsync(RetryTrackingTaskInput input)
    {
        await _taskRetryService.RetryAsync(input.Kind, input.TaskId);
    }

    private static ResourceArtifactDto MapArtifact(ResourceArtifact a) => new()
    {
        Kind = a.Kind,
        Variant = a.Variant,
        FilePath = a.FilePath,
        State = a.State,
        ErrorMessage = a.ErrorMessage,
        SizeBytes = a.SizeBytes,
        GeneratedAt = a.GeneratedAt
    };

    private IDisposable? DisableTenantFilterForHost()
    {
        return _currentTenant.Id == null ? _dataFilter.Disable<IMultiTenant>() : null;
    }

    private static string NormalizeMediaStatus(ResourceMediaJobStatus status) => status switch
    {
        ResourceMediaJobStatus.Pending => "pending",
        ResourceMediaJobStatus.Running => "running",
        ResourceMediaJobStatus.Completed => "success",
        ResourceMediaJobStatus.PartialFailed => "partial",
        ResourceMediaJobStatus.Failed => "failed",
        ResourceMediaJobStatus.Cancelled => "cancelled",
        _ => "pending"
    };

    private static string NormalizeDocumentStatus(IndexingJobStatus status) => status switch
    {
        IndexingJobStatus.Pending => "pending",
        IndexingJobStatus.Parsing => "running",
        IndexingJobStatus.Indexing => "running",
        IndexingJobStatus.Completed => "success",
        IndexingJobStatus.Failed => "failed",
        IndexingJobStatus.Cancelled => "cancelled",
        _ => "pending"
    };

    private static string NormalizeVideoStatus(VideoIndexingJobStatus status) => status switch
    {
        VideoIndexingJobStatus.Pending => "pending",
        VideoIndexingJobStatus.Parsing => "running",
        VideoIndexingJobStatus.Analyzing => "running",
        VideoIndexingJobStatus.Indexing => "running",
        VideoIndexingJobStatus.Completed => "success",
        VideoIndexingJobStatus.Failed => "failed",
        VideoIndexingJobStatus.Cancelled => "cancelled",
        _ => "pending"
    };

    /// <summary>
    /// 检查 <c>videos</c> 索引中是否存在已完成视频的轨迹事件，返回“DB Completed 但 MeiliSearch 一条文档都没有”的 ResourceId 集合。
    /// 主要检测 <see cref="VideoIndexingJobStatus.Completed"/> 的“表面成功”——如 MeiliSearch 重建/迁移/清空后
    /// DB 状态未同步造成的偏差，避免重试按钮永远不可点。
    /// </summary>
    /// <remarks>
    /// 只查 <c>videos</c> 索引；documents 索引是同一种偏差但严重性低（文档重生成可避免“AI 跳不出原句”），后续可作为扩展。
    /// 探查失败一律按“存在”处理：不造成误判可重试，只会让偏差情形下重试按钮维持禁用。
    /// </remarks>
    private async Task<HashSet<Guid>> DetectOrphanedVideoResourcesAsync(IReadOnlyList<VideoIndexingJob> vidJobs)
    {
        var candidateIds = vidJobs
            .Where(j => j.Status == VideoIndexingJobStatus.Completed && (j.TotalEvents ?? 0) > 0)
            .Select(j => j.ResourceId)
            .Distinct()
            .ToList();

        if (candidateIds.Count == 0) return new HashSet<Guid>();

        try
        {
            var probes = await Task.WhenAll(candidateIds.Select(async rid =>
            {
                try
                {
                    var result = await _meiliSearchService.SearchAsync(new SearchQueryDto
                    {
                        Query = "",
                        IndexName = "videos",
                        ResourceId = rid,
                        MaxResultCount = 1,
                        SkipCount = 0,
                    });
                    return (rid, hasData: result.TotalCount > 0);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MeiliSearch videos 探活失败，resourceId={ResourceId}", rid);
                    return (rid, hasData: true); // 探活失败保守按“有文档”处理，避免误开重试
                }
            }));

            return probes.Where(p => !p.hasData).Select(p => p.rid).ToHashSet();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DetectOrphanedVideoResourcesAsync 执行失败，本次不启用 Completed 重试。");
            return new HashSet<Guid>();
        }
    }
}
