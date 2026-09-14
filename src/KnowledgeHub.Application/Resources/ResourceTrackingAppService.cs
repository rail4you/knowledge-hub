using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Alliance;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Edition;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.Media;
using KnowledgeHub.Search.Indexing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Resources;

/// <summary>
/// 资源全链路任务跟踪实现：把分散在媒体任务表、索引任务表、两级审核表中的记录
/// 聚合成一条时间轴；每个失败任务可独立重试（媒体走 ResourceMediaJobManager，
/// 索引走重置状态 + 重新入队）。
/// </summary>
[Authorize(KnowledgeHubPermissions.Resources.Default)]
public class ResourceTrackingAppService : KnowledgeHubAppService, IResourceTrackingAppService
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceVersionRepository _versionRepository;
    private readonly IRepository<ResourceMediaJob, Guid> _mediaJobRepository;
    private readonly IRepository<ResourceArtifact, Guid> _artifactRepository;
    private readonly IRepository<DocumentIndexingJob, Guid> _documentJobRepository;
    private readonly IRepository<VideoIndexingJob, Guid> _videoJobRepository;
    private readonly IResourceAuditRepository _auditRepository;
    private readonly IRepository<AllianceAudit, Guid> _allianceAuditRepository;
    private readonly IEditionConfigService _editionConfigService;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public ResourceTrackingAppService(
        IResourceRepository resourceRepository,
        IResourceVersionRepository versionRepository,
        IRepository<ResourceMediaJob, Guid> mediaJobRepository,
        IRepository<ResourceArtifact, Guid> artifactRepository,
        IRepository<DocumentIndexingJob, Guid> documentJobRepository,
        IRepository<VideoIndexingJob, Guid> videoJobRepository,
        IResourceAuditRepository auditRepository,
        IRepository<AllianceAudit, Guid> allianceAuditRepository,
        IEditionConfigService editionConfigService,
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter)
    {
        _resourceRepository = resourceRepository;
        _versionRepository = versionRepository;
        _mediaJobRepository = mediaJobRepository;
        _artifactRepository = artifactRepository;
        _documentJobRepository = documentJobRepository;
        _videoJobRepository = videoJobRepository;
        _auditRepository = auditRepository;
        _allianceAuditRepository = allianceAuditRepository;
        _editionConfigService = editionConfigService;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
    }

    public async Task<PagedResultDto<ResourceTrackingResourceDto>> GetListAsync(GetTrackingResourcesInput input)
    {
        using var _ = DisableTenantFilterForHost();

        var query = await _resourceRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var kw = input.Filter.Trim();
            query = query.Where(r => r.Name.Contains(kw));
        }

        if (input.Status.HasValue)
        {
            query = query.Where(r => r.Status == input.Status.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(r => r.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var dtos = items.Select(r => new ResourceTrackingResourceDto
        {
            Id = r.Id,
            Name = r.Name,
            ResourceType = r.ResourceType,
            FileExtension = r.FileExtension,
            Status = r.Status,
            MediaStatus = r.MediaStatus,
            CurrentVersion = r.CurrentVersion,
            CreationTime = r.CreationTime,
            LastModificationTime = r.LastModificationTime,
            TenantId = r.TenantId
        }).ToList();

        await FillTenantNamesAsync(dtos);

        return new PagedResultDto<ResourceTrackingResourceDto>(totalCount, dtos);
    }

    public async Task<ResourceTrackingTimelineDto> GetTimelineAsync(Guid resourceId)
    {
        using var _ = DisableTenantFilterForHost();

        var resource = await _resourceRepository.GetAsync(resourceId);

        var versions = await _versionRepository.GetListAsync(v => v.ResourceId == resourceId);
        var mediaJobs = await _mediaJobRepository.GetListAsync(j => j.ResourceId == resourceId);
        var documentJobs = await _documentJobRepository.GetListAsync(j => j.ResourceId == resourceId);
        var videoJobs = await _videoJobRepository.GetListAsync(j => j.ResourceId == resourceId);
        var artifacts = await _artifactRepository.GetListAsync(a => a.ResourceId == resourceId);
        var schoolAudits = await _auditRepository.GetAuditsAsync(resourceId);
        var leagueAudits = await _allianceAuditRepository.GetListAsync(a => a.ResourceId == resourceId);

        var twoLevel = await _editionConfigService.IsTwoLevelApprovalEnabledAsync();

        var dto = new ResourceTrackingTimelineDto
        {
            ResourceId = resource.Id,
            ResourceName = resource.Name,
            ResourceType = resource.ResourceType,
            FileExtension = resource.FileExtension,
            Status = resource.Status,
            MediaStatus = resource.MediaStatus,
            CurrentVersion = resource.CurrentVersion,
            TenantId = resource.TenantId,
            CreationTime = resource.CreationTime,
            VisibleToStudents = twoLevel
                ? resource.Status == ResourceStatus.LeagueApproved
                : resource.Status is ResourceStatus.SchoolApproved or ResourceStatus.LeagueApproved
        };

        // 补充租户名。
        dto.TenantName = await ResolveTenantNameAsync(resource.TenantId);

        // 1. 上传
        var latestVersion = versions.OrderByDescending(v => v.Version).FirstOrDefault();
        dto.Steps.Add(new ResourceTrackingStepDto
        {
            Key = "upload",
            Title = "上传资源",
            Description = $"{resource.OriginalFileName ?? resource.FilePath}（当前 V{resource.CurrentVersion}，共 {versions.Count} 个版本）",
            Status = "success",
            Time = latestVersion?.CreationTime ?? resource.CreationTime
        });

        // 2. 生成预览 / 媒体处理
        var previewStep = BuildMediaStep(mediaJobs, artifacts);
        dto.Steps.Add(previewStep);

        // 3. 院校审核
        dto.Steps.Add(BuildSchoolAuditStep(resource, schoolAudits));

        // 4. 生成索引（文档解析 / 视频时间轴）
        dto.Steps.Add(BuildIndexStep(resource, documentJobs, videoJobs));

        // 5. 联盟审核
        dto.Steps.Add(BuildLeagueAuditStep(resource, leagueAudits));

        // 6. 学生端可见
        dto.Steps.Add(BuildPublishStep(resource, dto.VisibleToStudents));

        return dto;
    }

    // ───────────────────────── 步骤构建 ─────────────────────────

    private ResourceTrackingStepDto BuildMediaStep(
        List<ResourceMediaJob> jobs,
        List<ResourceArtifact> artifacts)
    {
        var step = new ResourceTrackingStepDto
        {
            Key = "preview",
            Title = "生成预览",
            Description = "缩略图 / 预览 PDF"
        };

        if (jobs.Count == 0)
        {
            step.Status = "pending";
            step.Description = "发布（提交审核）后自动生成";
            return step;
        }

        var artifactLookup = artifacts.ToLookup(a => (a.ResourceId, a.ResourceVersionId));
        foreach (var job in jobs.OrderBy(j => j.CreationTime))
        {
            var versionKey = job.ResourceVersionId ?? job.ResourceId;
            var task = new ResourceTrackingTaskDto
            {
                Id = job.Id,
                Kind = "media",
                Title = "媒体处理（缩略图/预览）",
                Status = NormalizeMediaStatus(job.Status),
                Progress = job.Progress,
                Message = job.ProgressMessage,
                ErrorMessage = job.ErrorMessage,
                RetryCount = job.RetryCount,
                StartedAt = job.StartedAt,
                CompletedAt = job.CompletedAt,
                CreationTime = job.CreationTime,
                ResourceVersionId = job.ResourceVersionId,
                CanRetry = job.Status is ResourceMediaJobStatus.Failed or ResourceMediaJobStatus.PartialFailed,
                Artifacts = artifactLookup[(job.ResourceId, versionKey)]
                    .Select(a => new ResourceTrackingArtifactDto
                    {
                        Kind = a.Kind == ResourceArtifactKind.PreviewPdf ? "preview-pdf" : "thumbnail",
                        Variant = a.Variant,
                        State = a.State == ResourceArtifactState.Ready ? "ready" : "failed",
                        ErrorMessage = a.ErrorMessage,
                        SizeBytes = a.SizeBytes,
                        GeneratedAt = a.GeneratedAt
                    })
                    .ToList()
            };
            step.Tasks.Add(task);
        }

        ApplyStepAggregate(step, jobs.Select(j => NormalizeMediaStatus(j.Status)).ToList());
        step.Time = jobs.Min(j => j.CreationTime);
        return step;
    }

    private static ResourceTrackingStepDto BuildSchoolAuditStep(Resource resource, List<ResourceAudit> audits)
    {
        var step = new ResourceTrackingStepDto
        {
            Key = "school-audit",
            Title = "院校审核",
            Description = "一级审核"
        };

        var latest = audits.OrderByDescending(a => a.CreationTime).FirstOrDefault();
        if (latest == null)
        {
            step.Status = resource.Status == ResourceStatus.PendingReview ? "running" : "pending";
            step.Description = "待院校审核";
            return step;
        }

        step.Time = latest.CreationTime;
        step.Status = latest.Status == AuditStatus.Approved ? "success"
            : latest.Status == AuditStatus.Rejected ? "failed"
            : "running";
        step.ErrorMessage = latest.Status == AuditStatus.Rejected ? latest.Comment : null;
        step.Description = string.IsNullOrWhiteSpace(latest.Comment)
            ? (latest.Status == AuditStatus.Approved ? "院校审核通过" : "院校审核驳回")
            : latest.Comment;
        return step;
    }

    private static ResourceTrackingStepDto BuildIndexStep(
        Resource resource,
        List<DocumentIndexingJob> documentJobs,
        List<VideoIndexingJob> videoJobs)
    {
        var step = new ResourceTrackingStepDto
        {
            Key = "index",
            Title = "生成索引",
            Description = "文档解析 / 视频时间轴"
        };

        foreach (var job in documentJobs.OrderBy(j => j.CreationTime))
        {
            step.Tasks.Add(new ResourceTrackingTaskDto
            {
                Id = job.Id,
                Kind = "document-index",
                Title = "文档索引",
                Status = NormalizeDocumentStatus(job.Status),
                Progress = job.Progress,
                Message = job.ProcessedPages.HasValue && job.TotalPages.HasValue
                    ? $"{job.ProcessedPages}/{job.TotalPages} 页"
                    : null,
                ErrorMessage = job.ErrorMessage,
                RetryCount = job.RetryCount,
                StartedAt = job.StartedAt,
                CompletedAt = job.CompletedAt,
                CreationTime = job.CreationTime,
                ResourceVersionId = job.ResourceVersionId,
                CanRetry = job.Status == IndexingJobStatus.Failed
            });
        }

        foreach (var job in videoJobs.OrderBy(j => j.CreationTime))
        {
            step.Tasks.Add(new ResourceTrackingTaskDto
            {
                Id = job.Id,
                Kind = "video-index",
                Title = "视频索引",
                Status = NormalizeVideoStatus(job.Status),
                Progress = job.Progress,
                Message = job.ProcessedEvents.HasValue && job.TotalEvents.HasValue
                    ? $"{job.ProcessedEvents}/{job.TotalEvents} 事件"
                    : null,
                ErrorMessage = job.ErrorMessage,
                RetryCount = job.RetryCount,
                StartedAt = job.StartedAt,
                CompletedAt = job.CompletedAt,
                CreationTime = job.CreationTime,
                ResourceVersionId = job.ResourceVersionId,
                CanRetry = job.Status == VideoIndexingJobStatus.Failed
            });
        }

        if (step.Tasks.Count == 0)
        {
            step.Status = "pending";
            step.Description = "院校审核通过后自动启动";
            return step;
        }

        ApplyStepAggregate(step, step.Tasks.Select(t => t.Status).ToList());
        step.Time = step.Tasks.Min(t => t.CreationTime);
        return step;
    }

    private static ResourceTrackingStepDto BuildLeagueAuditStep(Resource resource, List<AllianceAudit> audits)
    {
        var step = new ResourceTrackingStepDto
        {
            Key = "league-audit",
            Title = "联盟审核",
            Description = "二级审核"
        };

        var latest = audits.OrderByDescending(a => a.CreationTime).FirstOrDefault();
        if (latest == null)
        {
            step.Status = resource.Status == ResourceStatus.SchoolApproved ? "running" : "pending";
            step.Description = "待联盟审核";
            return step;
        }

        step.Time = latest.CreationTime;
        step.Status = latest.Status == AuditStatus.Approved ? "success"
            : latest.Status == AuditStatus.Rejected ? "failed"
            : "running";
        step.ErrorMessage = latest.Status == AuditStatus.Rejected ? latest.Comment : null;
        step.Description = string.IsNullOrWhiteSpace(latest.Comment)
            ? (latest.Status == AuditStatus.Approved
                ? $"联盟审核通过（{latest.ApproverTenantName}）"
                : "联盟审核驳回")
            : $"{latest.ApproverTenantName}：{latest.Comment}";
        return step;
    }

    private static ResourceTrackingStepDto BuildPublishStep(Resource resource, bool visible)
    {
        return new ResourceTrackingStepDto
        {
            Key = "published",
            Title = "学生端可见",
            Description = visible ? "资源已发布，学生端可见" : "联盟审核通过后学生端可见",
            Status = visible ? "success"
                : resource.Status == ResourceStatus.Rejected ? "failed"
                : "pending",
            Time = visible ? resource.LastModificationTime : null
        };
    }

    private static void ApplyStepAggregate(ResourceTrackingStepDto step, List<string> taskStatuses)
    {
        if (taskStatuses.Count == 0)
        {
            step.Status = "pending";
            return;
        }

        if (taskStatuses.Any(s => s == "failed"))
        {
            step.Status = "failed";
        }
        else if (taskStatuses.Any(s => s == "running"))
        {
            step.Status = "running";
        }
        else if (taskStatuses.Any(s => s == "pending"))
        {
            step.Status = taskStatuses.Any(s => s == "success") ? "running" : "pending";
        }
        else
        {
            step.Status = "success";
        }

        var progressValues = step.Tasks.Where(t => t.Progress > 0).Select(t => t.Progress).ToList();
        step.Progress = progressValues.Count > 0 ? progressValues.Max() : null;
        step.ErrorMessage ??= step.Tasks.FirstOrDefault(t => !string.IsNullOrEmpty(t.ErrorMessage))?.ErrorMessage;
    }

    // ───────────────────────── 工具方法 ─────────────────────────

    private IDisposable? DisableTenantFilterForHost()
    {
        // host（无当前租户）需要跨租户查看；租户管理员保持过滤器，仅看本租户。
        return _currentTenant.Id == null ? _dataFilter.Disable<IMultiTenant>() : null;
    }

    private async Task FillTenantNamesAsync(List<ResourceTrackingResourceDto> dtos)
    {
        var tenantIds = dtos.Where(d => d.TenantId.HasValue).Select(d => d.TenantId!.Value).Distinct().ToList();
        if (tenantIds.Count == 0)
        {
            return;
        }

        var tenants = await _tenantRepository.GetListAsync();
        var map = tenants.ToDictionary(t => t.Id, t => t.Name);
        foreach (var dto in dtos)
        {
            if (dto.TenantId.HasValue && map.TryGetValue(dto.TenantId.Value, out var name))
            {
                dto.TenantName = name;
            }
        }
    }

    private async Task<string?> ResolveTenantNameAsync(Guid? tenantId)
    {
        if (!tenantId.HasValue)
        {
            return null;
        }

        var tenants = await _tenantRepository.GetListAsync();
        return tenants.FirstOrDefault(t => t.Id == tenantId.Value)?.Name;
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
}
