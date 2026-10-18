using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Storage;

/// <summary>存储总览（host 全局管理员）。</summary>
public class StorageOverviewDto
{
    /// <summary>本地上传目录实际占用（字节，递归扫描）。</summary>
    public long LocalUsedBytes { get; set; }

    /// <summary>本地上传目录文件数。</summary>
    public long LocalFileCount { get; set; }

    /// <summary>本地上传目录物理路径。</summary>
    public string LocalRootPath { get; set; } = string.Empty;

    /// <summary>本地扫描是否成功（目录不存在/无权限时为 false）。</summary>
    public bool LocalAvailable { get; set; }

    /// <summary>OSS 是否已配置且可查询。</summary>
    public bool OssAvailable { get; set; }

    public string OssBucketName { get; set; } = string.Empty;

    /// <summary>OSS 桶已用容量（字节）；未配置或查询失败时为 0。</summary>
    public long OssUsedBytes { get; set; }

    public long OssObjectCount { get; set; }

    /// <summary>OSS 查询失败原因（成功时为空）。</summary>
    public string? OssError { get; set; }

    /// <summary>资源库在册文件总大小（字节，按 Resource.FileSize 汇总）。</summary>
    public long TotalResourceBytes { get; set; }

    public long TotalResourceFiles { get; set; }

    /// <summary>近 30 天新增资源占用（字节）。</summary>
    public long Recent30dBytes { get; set; }

    public int Recent30dFiles { get; set; }

    /// <summary>租户总数 / 已在用（容量大于 0）租户数。</summary>
    public int TenantCount { get; set; }

    public int TenantsInUse { get; set; }

    /// <summary>默认单租户配额（字节）。</summary>
    public long DefaultQuotaBytes { get; set; }

    /// <summary>配额预警阈值（百分比）。</summary>
    public int WarnPercent { get; set; }

    /// <summary>统计口径说明（前端直接展示，避免误解）。</summary>
    public string StatisticsNote { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; }
}

/// <summary>单租户存储用量。</summary>
public class TenantStorageUsageDto
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    /// <summary>在册资源文件数。</summary>
    public long FileCount { get; set; }

    /// <summary>在册资源占用（字节）。</summary>
    public long UsedBytes { get; set; }

    /// <summary>配额（字节）；0 = 不限。</summary>
    public long QuotaBytes { get; set; }

    /// <summary>使用率（百分比）。配额为 0（不限）时为 0。</summary>
    public double UsagePercent { get; set; }

    /// <summary>normal=正常 / warn=接近配额 / exceeded=已超配额 / unlimited=不限额。</summary>
    public string Status { get; set; } = "normal";

    /// <summary>近 30 天新增占用（字节）。</summary>
    public long Recent30dBytes { get; set; }

    public int Recent30dFiles { get; set; }

    /// <summary>最近一次上传时间。</summary>
    public DateTime? LastUploadTime { get; set; }
}

public class GetStorageMonitorListDto : PagedAndSortedResultRequestDto
{
    public GetStorageMonitorListDto()
    {
        MaxResultCount = 20;
    }

    /// <summary>按租户名模糊过滤。</summary>
    public string? Keyword { get; set; }

    /// <summary>按状态过滤：all / normal / warn / exceeded / unused。</summary>
    public string? StatusFilter { get; set; }
}

public class UpdateStorageQuotaDto
{
    public Guid TenantId { get; set; }

    /// <summary>配额（字节）；0 = 不限。</summary>
    public long QuotaBytes { get; set; }
}

public class UpdateDefaultQuotaDto
{
    /// <summary>默认单租户配额（字节）。</summary>
    public long QuotaBytes { get; set; }

    /// <summary>预警阈值（1-100）。</summary>
    public int WarnPercent { get; set; } = 80;
}

/// <summary>
/// 存储容量监控（host 全局管理员）：每租户文件容量 / 使用率 / 配额，
/// 以及本地上传目录与 OSS 桶的总占用。仅 host 上下文可用。
/// </summary>
public interface IStorageMonitorAppService : IApplicationService
{
    Task<StorageOverviewDto> GetOverviewAsync();

    Task<PagedResultDto<TenantStorageUsageDto>> GetTenantListAsync(GetStorageMonitorListDto input);

    /// <summary>设置单租户配额（写租户级 Setting，0 = 不限）。</summary>
    Task<TenantStorageUsageDto> UpdateQuotaAsync(UpdateStorageQuotaDto input);

    /// <summary>设置默认配额与预警阈值（写全局 Setting）。</summary>
    Task<StorageOverviewDto> UpdateDefaultAsync(UpdateDefaultQuotaDto input);
}
