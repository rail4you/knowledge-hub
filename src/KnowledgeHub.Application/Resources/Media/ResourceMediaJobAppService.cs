using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Resources.Media;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 资源媒体处理任务管理接口实现（后台任务跟踪/重试/取消）。
/// </summary>
[Authorize(KnowledgeHubPermissions.Resources.Default)]
public class ResourceMediaJobAppService : KnowledgeHubAppService, IResourceMediaJobAppService
{
    private readonly IRepository<ResourceMediaJob, Guid> _jobRepository;
    private readonly IRepository<ResourceArtifact, Guid> _artifactRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly ResourceMediaJobManager _manager;

    public ResourceMediaJobAppService(
        IRepository<ResourceMediaJob, Guid> jobRepository,
        IRepository<ResourceArtifact, Guid> artifactRepository,
        IRepository<Resource, Guid> resourceRepository,
        ResourceMediaJobManager manager)
    {
        _jobRepository = jobRepository;
        _artifactRepository = artifactRepository;
        _resourceRepository = resourceRepository;
        _manager = manager;
    }

    public async Task<PagedResultDto<ResourceMediaJobDto>> GetListAsync(GetResourceMediaJobsInput input)
    {
        var query = await _jobRepository.GetQueryableAsync();

        if (input.ResourceId.HasValue)
        {
            query = query.Where(x => x.ResourceId == input.ResourceId.Value);
        }
        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var kw = input.Filter.Trim();
            var resourceQuery = await _resourceRepository.GetQueryableAsync();
            var matchedIds = resourceQuery.Where(r => r.Name.Contains(kw)).Select(r => r.Id);
            query = query.Where(x => matchedIds.Contains(x.ResourceId));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var jobs = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var dtos = jobs.Select(MapToDto).ToList();
        await FillDetailsAsync(dtos, jobs);

        return new PagedResultDto<ResourceMediaJobDto>(totalCount, dtos);
    }

    public async Task<ResourceMediaJobDto?> GetByResourceIdAsync(Guid resourceId)
    {
        var job = await _jobRepository.FirstOrDefaultAsync(x =>
            x.ResourceId == resourceId &&
            (x.Status == ResourceMediaJobStatus.Pending || x.Status == ResourceMediaJobStatus.Running))
            ?? await _jobRepository.FirstOrDefaultAsync(x => x.ResourceId == resourceId);
        if (job == null)
        {
            return null;
        }

        var dto = MapToDto(job);
        await FillDetailsAsync(new List<ResourceMediaJobDto> { dto }, new List<ResourceMediaJob> { job });
        return dto;
    }

    public async Task RetryAsync(Guid id)
    {
        await _manager.RetryAsync(id);
    }

    public async Task RetryAllFailedAsync()
    {
        var failed = await _jobRepository.GetListAsync(x =>
            x.Status == ResourceMediaJobStatus.Failed || x.Status == ResourceMediaJobStatus.PartialFailed);
        foreach (var job in failed)
        {
            await _manager.RetryAsync(job.Id);
        }
    }

    public async Task CancelAsync(Guid id)
    {
        var job = await _jobRepository.FindAsync(id);
        if (job == null)
        {
            return;
        }

        job.Status = ResourceMediaJobStatus.Cancelled;
        job.ProgressMessage = "已取消";
        job.CompletedAt = DateTime.UtcNow;
        await _jobRepository.UpdateAsync(job);
    }

    private static ResourceMediaJobDto MapToDto(ResourceMediaJob job)
    {
        return new ResourceMediaJobDto
        {
            Id = job.Id,
            ResourceId = job.ResourceId,
            ResourceVersionId = job.ResourceVersionId,
            Status = job.Status,
            Progress = job.Progress,
            ProgressMessage = job.ProgressMessage,
            ErrorMessage = job.ErrorMessage,
            RetryCount = job.RetryCount,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            CreationTime = job.CreationTime
        };
    }

    private async Task FillDetailsAsync(List<ResourceMediaJobDto> dtos, List<ResourceMediaJob> jobs)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        // 资源名称
        var resourceIds = jobs.Select(j => j.ResourceId).Distinct().ToList();
        var resources = await _resourceRepository.GetListAsync(r => resourceIds.Contains(r.Id));
        var nameMap = resources.ToDictionary(r => r.Id, r => r.Name);
        foreach (var dto in dtos)
        {
            if (nameMap.TryGetValue(dto.ResourceId, out var name))
            {
                dto.ResourceName = name;
            }
        }

        // 生成物
        var artifacts = await _artifactRepository.GetListAsync(a => resourceIds.Contains(a.ResourceId));
        var lookup = artifacts.ToLookup(a => (a.ResourceId, a.ResourceVersionId));
        foreach (var dto in dtos)
        {
            var versionId = dto.ResourceVersionId ?? dto.ResourceId;
            dto.Artifacts = lookup[(dto.ResourceId, versionId)]
                .Select(a => new ResourceArtifactDto
                {
                    Kind = a.Kind,
                    Variant = a.Variant,
                    FilePath = a.FilePath,
                    State = a.State,
                    ErrorMessage = a.ErrorMessage,
                    SizeBytes = a.SizeBytes,
                    GeneratedAt = a.GeneratedAt
                })
                .ToList();
        }
    }
}
