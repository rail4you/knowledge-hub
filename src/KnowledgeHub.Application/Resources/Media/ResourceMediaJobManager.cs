using System;
using System.Threading.Tasks;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 媒体处理任务的创建与入队（上传/换版本/回滚/索引完成后调用）。
/// 同一资源版本已有 Pending/Running 任务时默认去重，避免重复处理。
/// </summary>
public class ResourceMediaJobManager : ITransientDependency
{
    private readonly IRepository<ResourceMediaJob, Guid> _jobRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IResourceMediaJobQueue _queue;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ResourceMediaJobManager(
        IRepository<ResourceMediaJob, Guid> jobRepository,
        IRepository<Resource, Guid> resourceRepository,
        IResourceMediaJobQueue queue,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _jobRepository = jobRepository;
        _resourceRepository = resourceRepository;
        _queue = queue;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// 为资源版本创建并入队一个媒体处理任务，并把资源 MediaStatus 置为 Processing。
    /// </summary>
    /// <returns>任务 Id；若被去重返回已存在任务 Id。</returns>
    public async Task<Guid> EnqueueAsync(Guid resourceId, Guid? resourceVersionId, bool force = false)
    {
        var tenantId = _currentTenant.Id;

        ResourceMediaJob? job = null;
        if (!force)
        {
            job = await _jobRepository.FirstOrDefaultAsync(x =>
                x.ResourceId == resourceId &&
                x.ResourceVersionId == resourceVersionId &&
                (x.Status == ResourceMediaJobStatus.Pending || x.Status == ResourceMediaJobStatus.Running));
        }

        if (job == null)
        {
            job = new ResourceMediaJob(_guidGenerator.Create(), resourceId, resourceVersionId)
            {
                TenantId = tenantId,
                Status = ResourceMediaJobStatus.Pending,
                ProgressMessage = "排队中…"
            };
            await _jobRepository.InsertAsync(job);

            var jobId = job.Id;
            var uow = _unitOfWorkManager.Current;
            if (uow != null)
            {
                // 事务提交后再入队，避免 Hangfire 抢先执行查不到任务行
                uow.OnCompleted(async () => await _queue.EnqueueAsync(jobId, tenantId));
            }
            else
            {
                await _queue.EnqueueAsync(jobId, tenantId);
            }
        }

        var resource = await _resourceRepository.FindAsync(resourceId);
        if (resource != null && resource.MediaStatus != ResourceMediaStatus.Processing)
        {
            resource.MediaStatus = ResourceMediaStatus.Processing;
            await _resourceRepository.UpdateAsync(resource);
        }

        return job.Id;
    }

    /// <summary>重试：重置任务状态后重新入队（用于管理端"重新生成"）。</summary>
    public async Task RetryAsync(Guid jobId)
    {
        var job = await _jobRepository.FindAsync(jobId);
        if (job == null)
        {
            return;
        }

        job.Status = ResourceMediaJobStatus.Pending;
        job.Progress = 0;
        job.ProgressMessage = "重新排队…";
        job.ErrorMessage = null;
        job.StartedAt = null;
        job.CompletedAt = null;
        job.RetryCount++;
        await _jobRepository.UpdateAsync(job);

        var tenantId = job.TenantId;
        var uow = _unitOfWorkManager.Current;
        if (uow != null)
        {
            uow.OnCompleted(async () => await _queue.EnqueueAsync(job.Id, tenantId));
        }
        else
        {
            await _queue.EnqueueAsync(job.Id, tenantId);
        }
    }
}
