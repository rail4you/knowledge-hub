using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Conversion;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.HangfireJobs;

/// <summary>
/// 后台串行重处理：为"已审核通过的 Office 资源"预热 PDF 转换缓存。
/// 由 Hangfire RecurringJob 周期性触发；每轮只处理小批量（ReprocessBatchSize 个），
/// 转换通过 "reprocess" 并发闸门（默认 1）严格串行，不会打爆服务器。
/// </summary>
public class OfficeConversionReprocessJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<OfficeConversionOptions> _options;
    private readonly ILogger<OfficeConversionReprocessJob> _logger;

    public OfficeConversionReprocessJob(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<OfficeConversionOptions> options,
        ILogger<OfficeConversionReprocessJob> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        var conversion = scope.ServiceProvider.GetRequiredService<IOfficeConversionService>();
        var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter<IMultiTenant>>();

        // 关键：Hangfire job 在后台线程执行，不在 ABP 请求作用域内。
        // 必须用 UOW 包裹，EF Core DbContext 才不会被提前释放；且用独立 UOW
        // 避免跨租户数据过滤器状态残留。
        using (var uow = uowManager.Begin(requiresNew: true, isTransactional: false))
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<Resource, Guid>>();

            using (dataFilter.Disable())
            {
                var query = await repository.GetQueryableAsync();

                // 候选：已审核通过 + 有文件路径 + Office 扩展名。每轮只处理小批量。
                var batchSize = Math.Max(1, _options.Value.ReprocessBatchSize);
                var candidates = query
                    .Where(r => r.Status == ResourceStatus.SchoolApproved || r.Status == ResourceStatus.LeagueApproved)
                    .Where(r => !string.IsNullOrEmpty(r.FilePath))
                    .OrderBy(r => r.CreationTime)
                    .Take(batchSize)
                    .ToList();

                var done = 0;
                var failed = 0;
                var skipped = 0;
                foreach (var resource in candidates)
                {
                    if (!IsOfficeFile(resource.FilePath))
                    {
                        skipped++;
                        continue;
                    }

                    var fullPath = Path.Combine(fileStorage.RootPath, resource.FilePath);
                    if (!File.Exists(fullPath))
                    {
                        skipped++;
                        continue;
                    }

                    // 已存在有效缓存则跳过
                    if (conversion.HasValidCachedPdf(resource.Id.ToString(), fullPath))
                    {
                        skipped++;
                        continue;
                    }

                    _logger.LogInformation(
                        "[ReprocessOffice] 开始转换: {ResourceId} {Name} ({FilePath})",
                        resource.Id, resource.Name, resource.FilePath);

                    try
                    {
                        // 服务分组 "reprocess"，与预览（preview）并发闸门相互独立，默认各 1。
                        await conversion.ConvertToPdfAsync(resource.Id.ToString(), fullPath, "reprocess");
                        done++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogWarning(ex, "[ReprocessOffice] 转换失败: {ResourceId} {Name}", resource.Id, resource.Name);
                    }
                }

                if (done > 0 || failed > 0)
                {
                    _logger.LogInformation(
                        "[ReprocessOffice] 本轮完成: 成功 {Done}, 失败 {Failed}, 跳过 {Skipped}, 候选 {Total}",
                        done, failed, skipped, candidates.Count);
                }
            }

            await uow.CompleteAsync();
        }
    }

    private static bool IsOfficeFile(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext is ".pptx" or ".ppt" or ".docx" or ".doc" or ".xlsx" or ".xls";
    }
}
