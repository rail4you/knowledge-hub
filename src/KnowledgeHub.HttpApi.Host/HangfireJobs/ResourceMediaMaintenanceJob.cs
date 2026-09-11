using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using KnowledgeHub.Resources.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.HangfireJobs;

/// <summary>
/// 资源媒体处理维护任务（RecurringJob）：
/// 1) 回填：为「有文件但尚无任何媒体任务」的历史资源补建任务（遗留数据统一纳入流水线）；
/// 2) 清理：删除已不存在资源的遗留生成物（converted/、thumbnails/）与过期临时文件（_tmp/）。
/// </summary>
public class ResourceMediaMaintenanceJob
{
    private const int BackfillBatchSize = 20;
    private const int OrphanScanLimit = 2000;
    private static readonly string[] GeneratedDirs = { "converted", "thumbnails" };

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ResourceMediaMaintenanceJob> _logger;

    public ResourceMediaMaintenanceJob(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ResourceMediaMaintenanceJob> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        try
        {
            await BackfillMissingJobsAsync(scope);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ResourceMediaMaintenance] 回填失败");
        }

        try
        {
            await CleanupOrphanFilesAsync(scope);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ResourceMediaMaintenance] 清理失败");
        }
    }

    private async Task BackfillMissingJobsAsync(IServiceScope scope)
    {
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter<IMultiTenant>>();
        var resourceRepository = scope.ServiceProvider.GetRequiredService<IRepository<Resource, Guid>>();
        var versionRepository = scope.ServiceProvider.GetRequiredService<IRepository<ResourceVersion, Guid>>();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IRepository<ResourceMediaJob, Guid>>();
        var manager = scope.ServiceProvider.GetRequiredService<ResourceMediaJobManager>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        using var uow = uowManager.Begin(requiresNew: true, isTransactional: false);
        using (dataFilter.Disable())
        {
            var resourceQuery = await resourceRepository.GetQueryableAsync();
            var jobQuery = await jobRepository.GetQueryableAsync();

            var candidates = await resourceQuery
                .Where(r => !string.IsNullOrEmpty(r.FilePath) && !jobQuery.Any(j => j.ResourceId == r.Id))
                .OrderBy(r => r.CreationTime)
                .Take(BackfillBatchSize)
                .ToListAsync();

            if (candidates.Count == 0)
            {
                await uow.CompleteAsync();
                return;
            }

            foreach (var resource in candidates)
            {
                using (currentTenant.Change(resource.TenantId))
                {
                    var version = await versionRepository.FirstOrDefaultAsync(x =>
                        x.ResourceId == resource.Id && x.IsCurrentVersion);
                    await manager.EnqueueAsync(resource.Id, version?.Id);
                }
            }

            _logger.LogInformation("[ResourceMediaMaintenance] 回填媒体任务 {Count} 个", candidates.Count);
        }

        await uow.CompleteAsync();
    }

    private async Task CleanupOrphanFilesAsync(IServiceScope scope)
    {
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter<IMultiTenant>>();
        var resourceRepository = scope.ServiceProvider.GetRequiredService<IRepository<Resource, Guid>>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        using var uow = uowManager.Begin(requiresNew: true, isTransactional: false);
        var root = fileStorage.RootPath;
        var known = new Dictionary<Guid, bool>();
        var removed = 0;

        foreach (var dirName in GeneratedDirs)
        {
            var dir = Path.Combine(root, dirName);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir).Take(OrphanScanLimit))
            {
                var guidToken = Path.GetFileName(file).Split('_', '.')[0];
                if (!Guid.TryParse(guidToken, out var resourceId))
                {
                    continue;
                }

                if (!known.TryGetValue(resourceId, out var exists))
                {
                    using (dataFilter.Disable())
                    {
                        exists = await resourceRepository.AnyAsync(x => x.Id == resourceId);
                    }
                    known[resourceId] = exists;
                }

                if (!exists)
                {
                    TryDelete(file);
                    removed++;
                }
            }
        }

        // 过期临时文件（视频压缩产物）
        var tmpDir = Path.Combine(root, "_tmp");
        if (Directory.Exists(tmpDir))
        {
            foreach (var file in Directory.EnumerateFiles(tmpDir))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-24))
                    {
                        TryDelete(file);
                        removed++;
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        await uow.CompleteAsync();

        if (removed > 0)
        {
            _logger.LogInformation("[ResourceMediaMaintenance] 清理孤儿生成物 {Count} 个", removed);
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ResourceMediaMaintenance] 删除失败: {Path}", path);
        }
    }
}
