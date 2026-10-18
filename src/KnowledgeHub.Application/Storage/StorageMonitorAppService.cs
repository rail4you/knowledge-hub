using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using KnowledgeHub.Resources.Oss;
using KnowledgeHub.Settings;
using KnowledgeHub.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Storage;

/// <summary>
/// 存储容量监控（host 全局管理员）。
///
/// 统计口径（页面上会明示，避免把几个数混为一谈）：
///  1. 租户用量  = 资源库在册资源（Resource.FileSize 汇总，软删除不计），可精确到租户；
///  2. 本地占用  = Storage:RootPath 目录递归扫描，含缩略图/预览/转换产物/未清理分片，
///     不区分租户（目录按日期分桶），只反映服务器磁盘真实压力；
///  3. OSS 用量  = 桶级统计（GetBucketStat），同样不区分租户，反映对象存储账单。
/// 配额只对 (1) 生效；(2)(3) 是全局水位，避免给出"没超配额但磁盘满了"的假象。
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize(KnowledgeHubPermissions.StorageMonitor.Default)]
public class StorageMonitorAppService : KnowledgeHubAppService, IStorageMonitorAppService
{
    private const int RecentDays = 30;

    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISettingManager _settingManager;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IFileStorageService _fileStorage;
    private readonly IOssCredentialProvider _ossCredentials;

    public StorageMonitorAppService(
        IRepository<Resource, Guid> resourceRepository,
        ITenantRepository tenantRepository,
        ISettingManager settingManager,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter,
        IFileStorageService fileStorage,
        IOssCredentialProvider ossCredentials)
    {
        _resourceRepository = resourceRepository;
        _tenantRepository = tenantRepository;
        _settingManager = settingManager;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
        _fileStorage = fileStorage;
        _ossCredentials = ossCredentials;
    }

    public async Task<StorageOverviewDto> GetOverviewAsync()
    {
        EnsureHost();

        var cutoff = Clock.Now.AddDays(-RecentDays);
        var totals = await QueryTotalsAsync(cutoff);
        var (localBytes, localFiles, localOk) = await ScanLocalStorageAsync();
        var oss = await QueryOssAsync();

        var tenants = await _tenantRepository.GetListAsync();

        var dto = new StorageOverviewDto
        {
            LocalUsedBytes = localBytes,
            LocalFileCount = localFiles,
            LocalAvailable = localOk,
            LocalRootPath = _fileStorage.RootPath,
            OssAvailable = oss.Available,
            OssBucketName = oss.Bucket,
            OssUsedBytes = oss.Bytes,
            OssObjectCount = oss.ObjectCount,
            OssError = oss.Error,
            TotalResourceBytes = totals.UsedBytes,
            TotalResourceFiles = totals.FileCount,
            Recent30dBytes = totals.RecentBytes,
            Recent30dFiles = totals.RecentFiles,
            TenantCount = tenants.Count,
            TenantsInUse = totals.PerTenant.Count(x => x.Value.UsedBytes > 0),
            DefaultQuotaBytes = await GetDefaultQuotaBytesAsync(),
            WarnPercent = await GetWarnPercentAsync(),
            StatisticsNote =
                "租户用量按资源库在册资源统计（软删除不计），配额只对该口径生效；"
                + "「本地上传目录」与「OSS 桶」是全局水位，不区分租户，用于判断磁盘/账单压力。",
            GeneratedAt = Clock.Now,
        };

        return dto;
    }

    public async Task<PagedResultDto<TenantStorageUsageDto>> GetTenantListAsync(GetStorageMonitorListDto input)
    {
        EnsureHost();

        var cutoff = Clock.Now.AddDays(-RecentDays);
        var totals = await QueryTotalsAsync(cutoff);
        var defaultQuota = await GetDefaultQuotaBytesAsync();
        var warnPercent = await GetWarnPercentAsync();

        var tenants = await _tenantRepository.GetListAsync();

        var rows = new List<TenantStorageUsageDto>(tenants.Count);
        foreach (var tenant in tenants)
        {
            totals.PerTenant.TryGetValue(tenant.Id, out var usage);

            // 租户级配额：未配置（0/缺失）时回退全局默认值。
            var quota = await GetTenantQuotaBytesAsync(tenant.Id);
            if (quota <= 0)
            {
                quota = defaultQuota;
            }

            var percent = quota > 0 ? usage.UsedBytes * 100d / quota : 0d;

            rows.Add(new TenantStorageUsageDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                FileCount = usage.FileCount,
                UsedBytes = usage.UsedBytes,
                QuotaBytes = quota,
                UsagePercent = Math.Round(percent, 2),
                Status = ResolveStatus(usage.UsedBytes, quota, percent, warnPercent),
                Recent30dBytes = usage.RecentBytes,
                Recent30dFiles = usage.RecentFiles,
                LastUploadTime = usage.LastUploadTime,
            });
        }

        // 关键字 / 状态过滤
        var keyword = input.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            rows = rows.Where(r => r.TenantName.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var statusFilter = input.StatusFilter?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "all")
        {
            rows = statusFilter switch
            {
                "unused" => rows.Where(r => r.UsedBytes == 0).ToList(),
                "normal" => rows.Where(r => r.Status == "normal").ToList(),
                "warn" => rows.Where(r => r.Status == "warn").ToList(),
                "exceeded" => rows.Where(r => r.Status == "exceeded").ToList(),
                _ => rows,
            };
        }

        rows = ApplySorting(rows, input.Sorting);

        var totalCount = rows.Count;
        var page = rows
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount <= 0 ? 20 : input.MaxResultCount)
            .ToList();

        return new PagedResultDto<TenantStorageUsageDto>(totalCount, page);
    }

    public async Task<TenantStorageUsageDto> UpdateQuotaAsync(UpdateStorageQuotaDto input)
    {
        EnsureHost();

        if (input.QuotaBytes < 0)
        {
            throw new UserFriendlyException("配额不能为负数，填 0 表示不限。");
        }

        await _settingManager.SetForTenantAsync(input.TenantId, KnowledgeHubSettings.StorageQuotaBytes, input.QuotaBytes.ToString());

        // 直接回查该租户那一行，前端就地刷新，无需整表重载
        var page = await GetTenantListAsync(new GetStorageMonitorListDto { MaxResultCount = 500 });
        var row = page.Items.FirstOrDefault(x => x.TenantId == input.TenantId);
        if (row == null)
        {
            throw new UserFriendlyException("未找到该租户。");
        }

        return row;
    }

    public async Task<StorageOverviewDto> UpdateDefaultAsync(UpdateDefaultQuotaDto input)
    {
        EnsureHost();

        if (input.QuotaBytes < 0)
        {
            throw new UserFriendlyException("默认配额不能为负数。");
        }

        if (input.WarnPercent is < 1 or > 100)
        {
            throw new UserFriendlyException("预警阈值必须在 1-100 之间。");
        }

        await _settingManager.SetGlobalAsync(KnowledgeHubSettings.DefaultStorageQuotaBytes, input.QuotaBytes.ToString());
        await _settingManager.SetGlobalAsync(KnowledgeHubSettings.StorageWarnPercent, input.WarnPercent.ToString());

        return await GetOverviewAsync();
    }

    /// <summary>
    /// 汇总资源库在册文件。必须关闭多租户过滤才能看到所有租户的数据，
    /// 同时 IDataFilter 的软删除过滤保持开启 —— 即"在册"口径。
    /// </summary>
    private async Task<(long UsedBytes, long FileCount, long RecentBytes, int RecentFiles, Dictionary<Guid, TenantUsage> PerTenant)>
        QueryTotalsAsync(DateTime cutoff)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _resourceRepository.GetQueryableAsync();

            var grouped = await _asyncExecuter.ToListAsync(
                queryable
                    .Where(r => r.TenantId != null)
                    .GroupBy(r => r.TenantId!.Value)
                    .Select(g => new TenantUsage
                    {
                        TenantId = g.Key,
                        UsedBytes = g.Sum(x => x.FileSize ?? 0),
                        FileCount = g.LongCount(),
                        RecentBytes = g.Where(x => x.CreationTime >= cutoff).Sum(x => x.FileSize ?? 0),
                        RecentFiles = g.Count(x => x.CreationTime >= cutoff),
                        LastUploadTime = g.Max(x => x.CreationTime),
                    }));

            return (
                grouped.Sum(x => x.UsedBytes),
                grouped.Sum(x => x.FileCount),
                grouped.Sum(x => x.RecentBytes),
                grouped.Sum(x => x.RecentFiles),
                grouped.ToDictionary(x => x.TenantId)
            );
        }
    }

    private class TenantUsage
    {
        public Guid TenantId { get; set; }
        public long UsedBytes { get; set; }
        public long FileCount { get; set; }
        public long RecentBytes { get; set; }
        public int RecentFiles { get; set; }
        public DateTime LastUploadTime { get; set; }
    }

    /// <summary>
    /// 递归统计本地上传目录。用显式栈逐层枚举而不是 SearchOption.AllDirectories：
    /// 某个子目录无权限时不会中断整次统计。
    /// </summary>
    private Task<(long Bytes, long Files, bool Ok)> ScanLocalStorageAsync()
    {
        return Task.Run(() =>
        {
            var root = _fileStorage.RootPath;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return (0L, 0L, false);
            }

            long bytes = 0;
            long files = 0;
            var ok = false;
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        try
                        {
                            bytes += new FileInfo(file).Length;
                            files++;
                        }
                        catch
                        {
                            // 文件可能刚好被清理/移动，跳过
                        }
                    }

                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        // 跳过符号链接，避免环路导致死循环
                        if (new DirectoryInfo(sub).LinkTarget is null)
                        {
                            stack.Push(sub);
                        }
                    }

                    ok = true;
                }
                catch
                {
                    // 无权限等，跳过该目录
                }
            }

            return (bytes, files, ok);
        });
    }

    /// <summary>查询 OSS 桶用量；未配置或无权限时降级为不可用，不影响其他指标。</summary>
    private async Task<(bool Available, string Bucket, long Bytes, long ObjectCount, string? Error)> QueryOssAsync()
    {
        try
        {
            var options = await _ossCredentials.GetOptionsAsync();
            if (!options.IsConfigured)
            {
                return (false, "", 0, 0, "OSS 未配置（管理页 → 对象存储设置）。");
            }

            var client = _ossCredentials.CreateClient(options);
            var stat = await Task.Run(() => client.GetBucketStat(options.BucketName));
            return (true, options.BucketName, (long)stat.Storage, (long)stat.ObjectCount, null);
        }
        catch (Exception ex)
        {
            return (false, "", 0, 0, ex.Message);
        }
    }

    private async Task<long> GetDefaultQuotaBytesAsync()
    {
        var raw = await _settingManager.GetOrNullGlobalAsync(KnowledgeHubSettings.DefaultStorageQuotaBytes);
        return long.TryParse(raw, out var value) ? value : 0;
    }

    private async Task<int> GetWarnPercentAsync()
    {
        var raw = await _settingManager.GetOrNullGlobalAsync(KnowledgeHubSettings.StorageWarnPercent);
        return int.TryParse(raw, out var value) ? value : 80;
    }

    /// <summary>取租户级配额；未单独设置时返回 0，由调用方回退默认值。</summary>
    private async Task<long> GetTenantQuotaBytesAsync(Guid tenantId)
    {
        // 参数顺序：(name, tenantId, fallbackToGlobal) —— 不回退全局，
        // 因为“未单独设置”与“等于默认值”在这里必须可区分。
        var raw = await _settingManager.GetOrNullForTenantAsync(
            KnowledgeHubSettings.StorageQuotaBytes,
            tenantId,
            false);

        return long.TryParse(raw, out var value) ? value : 0;
    }

    private static string ResolveStatus(long usedBytes, long quotaBytes, double percent, int warnPercent)
    {
        if (usedBytes == 0)
        {
            return "unused";
        }

        if (quotaBytes <= 0)
        {
            return "unlimited";
        }

        if (percent >= 100)
        {
            return "exceeded";
        }

        return percent >= warnPercent ? "warn" : "normal";
    }

    /// <summary>白名单排序：只允许按已知字段排序，避免把任意表达式透传给 EF。</summary>
    private static List<TenantStorageUsageDto> ApplySorting(List<TenantStorageUsageDto> rows, string? sorting)
    {
        var key = (sorting ?? "").Trim().ToLowerInvariant();
        var desc = key.EndsWith("desc", StringComparison.Ordinal);
        if (key.EndsWith("desc", StringComparison.Ordinal))
        {
            key = key[..^4].Trim();
        }

        return key switch
        {
            "tenantname" => desc
                ? rows.OrderByDescending(r => r.TenantName).ToList()
                : rows.OrderBy(r => r.TenantName).ToList(),
            "filecount" => desc
                ? rows.OrderByDescending(r => r.FileCount).ToList()
                : rows.OrderBy(r => r.FileCount).ToList(),
            "recent30dbytes" => desc
                ? rows.OrderByDescending(r => r.Recent30dBytes).ToList()
                : rows.OrderBy(r => r.Recent30dBytes).ToList(),
            "lastuploadtime" => desc
                ? rows.OrderByDescending(r => r.LastUploadTime).ToList()
                : rows.OrderBy(r => r.LastUploadTime).ToList(),
            // 默认（也是 usagepercent 的排序键）：已用容量降序，最耗空间的排前面
            _ => rows.OrderByDescending(r => r.UsedBytes).ThenBy(r => r.TenantName).ToList(),
        };
    }

    /// <summary>
    /// 该页面要跨租户统计，权限已是 host-only；这里再显式拦一道租户上下文，
    /// 防止将来有人误把它加进 GrantAllPoliciesMiddleware 注入列表导致越权。
    /// </summary>
    private void EnsureHost()
    {
        if (CurrentTenant.Id != null)
        {
            throw new AbpAuthorizationException("存储容量监控仅全局管理员可用。");
        }
    }
}
