using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Permissions;
using KnowledgeHub.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// AI 使用管理（模型管理页“AI 使用管理”Tab）：Key 维护、调用记录、配额配置。
/// </summary>
[Authorize(KnowledgeHubPermissions.AI.ManageTasks)]
public class AiManagementAppService : KnowledgeHubAppService, IAiManagementAppService
{
    private readonly ISettingManager _settingManager;
    private readonly ISettingProvider _settingProvider;
    private readonly IQwenCredentialProvider _credentials;
    private readonly IConfiguration _configuration;
    private readonly IRepository<AiUsageRecord, Guid> _usageRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IAiQuotaService _quotaService;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataFilter _dataFilter;

    public AiManagementAppService(
        ISettingManager settingManager,
        ISettingProvider settingProvider,
        IQwenCredentialProvider credentials,
        IConfiguration configuration,
        IRepository<AiUsageRecord, Guid> usageRepository,
        ITenantRepository tenantRepository,
        IAiQuotaService quotaService,
        IAsyncQueryableExecuter asyncExecuter,
        IHttpClientFactory httpClientFactory,
        IDataFilter dataFilter)
    {
        _settingManager = settingManager;
        _settingProvider = settingProvider;
        _credentials = credentials;
        _configuration = configuration;
        _usageRepository = usageRepository;
        _tenantRepository = tenantRepository;
        _quotaService = quotaService;
        _asyncExecuter = asyncExecuter;
        _httpClientFactory = httpClientFactory;
        _dataFilter = dataFilter;
    }

    public async Task<AiManagementStatusDto> GetStatusAsync()
    {
        string? key = null;
        try
        {
            key = await _credentials.GetApiKeyAsync();
        }
        catch
        {
            // 未配置：状态页照常展示空 Key
        }

        return new AiManagementStatusDto
        {
            HasApiKey = !string.IsNullOrWhiteSpace(key),
            MaskedApiKey = MaskKey(key),
            TextModel = _configuration["Qwen:Model"] ?? "qwen-flash",
            VisionModel = _configuration["Qwen:VisionModel"] ?? "qwen3-vl-flash",
            VideoFps = _configuration.GetValue("Qwen:VideoFps", 0.2),
            Pricing = IAiUsageTracker.Pricing
                .Where(p => p.Key != "default")
                .Select(p => new AiModelPriceDto
                {
                    Model = p.Key,
                    InputPerMillion = p.Value.In,
                    OutputPerMillion = p.Value.Out,
                })
                .ToList(),
        };
    }

    public async Task UpdateApiKeyAsync(UpdateAiApiKeyDto input)
    {
        var key = (input.ApiKey ?? "").Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new UserFriendlyException("API Key 不能为空。");
        }

        // AI 服务按租户配置：Key 写入当前租户，只影响本租户
        var tenantId = RequireTenant();
        await _settingManager.SetForTenantAsync(tenantId, KnowledgeHubSettings.QwenApiKey, key);

        // 换 Key 即生效：清掉本租户的凭证缓存（静态 HttpClient 不绑 Key，无需重启）
        _credentials.ClearCache();
    }

    /// <summary>零 token 验证：调 compatible-mode 的 /models 列表接口，只验 Key 有效性。</summary>
    public async Task<bool> TestConnectionAsync()
    {
        string key;
        try
        {
            key = await _credentials.GetApiKeyAsync();
        }
        catch
        {
            return false;
        }

        try
        {
            var baseUrl = (_configuration["Qwen:BaseUrl"]
                ?? "https://dashscope.aliyuncs.com/compatible-mode/v1").TrimEnd('/');
            var client = _httpClientFactory.CreateClient("AiManagement");
            client.Timeout = TimeSpan.FromSeconds(15);
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<PagedResultDto<AiUsageRecordDto>> GetUsageRecordsAsync(GetAiUsageRecordsInput input)
    {
        // host（平台）需要查看全部租户的调用情况：关闭多租户查询过滤器，手动按上下文限定
        using var tenantFilter = _dataFilter.Disable<IMultiTenant>();
        var query = await ApplyFiltersAsync(input);

        var totalCount = await _asyncExecuter.CountAsync(query);
        var items = await _asyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(Math.Clamp(input.MaxResultCount, 1, 200)));

        var tenantIds = items.Where(x => x.TenantId.HasValue).Select(x => x.TenantId!.Value).Distinct().ToList();
        var tenantNames = new Dictionary<Guid, string>();
        if (tenantIds.Count > 0)
        {
            var tenants = await _tenantRepository.GetListAsync();
            foreach (var t in tenants)
            {
                if (tenantIds.Contains(t.Id)) tenantNames[t.Id] = t.Name;
            }
        }

        return new PagedResultDto<AiUsageRecordDto>(
            totalCount,
            items.Select(x => MapDto(x, tenantNames)).ToList());
    }

    public async Task<AiUsageSummaryDto> GetUsageSummaryAsync(GetAiUsageRecordsInput input)
    {
        // 只取聚合所需列（不拉取含长文本 ErrorMessage 的完整实体），在内存中汇总
        using var tenantFilter = _dataFilter.Disable<IMultiTenant>();
        var query = await ApplyFiltersAsync(input);
        var rows = await _asyncExecuter.ToListAsync(query.Select(x => new
        {
            x.Status,
            x.InputTokens,
            x.OutputTokens,
            x.EstimatedCost,
        }));

        if (rows.Count == 0) return new AiUsageSummaryDto();

        return new AiUsageSummaryDto
        {
            TotalCount = rows.Count,
            SuccessCount = rows.Count(x => x.Status == AiUsageStatus.Completed),
            FailedCount = rows.Count(x => x.Status == AiUsageStatus.Failed),
            TotalInputTokens = rows.Sum(x => (long)x.InputTokens),
            TotalOutputTokens = rows.Sum(x => (long)x.OutputTokens),
            TotalEstimatedCost = Math.Round(rows.Sum(x => x.EstimatedCost), 4),
        };
    }

    public async Task<AiQuotasDto> GetQuotasAsync()
    {
        return new AiQuotasDto { Quotas = await _quotaService.GetQuotasAsync() };
    }

    public async Task UpdateQuotasAsync(AiQuotasDto input)
    {
        var quotas = input.Quotas ?? new Dictionary<string, Dictionary<string, int?>>();
        // 校验：次数非负、上限封顶，防止误填
        foreach (var (role, byGroup) in quotas)
        {
            if (string.IsNullOrWhiteSpace(role) || byGroup == null) continue;
            foreach (var (group, limit) in byGroup)
            {
                if (limit.HasValue && (limit.Value < 0 || limit.Value > 100000))
                {
                    throw new UserFriendlyException($"配额数值非法：{role}/{group}。");
                }
            }
        }

        // 配额按租户配置：写入当前租户，只影响本租户
        var tenantId = RequireTenant();
        await _settingManager.SetForTenantAsync(
            tenantId, KnowledgeHubSettings.AiQuotas, JsonSerializer.Serialize(quotas));
    }

    /// <summary>AI 配置为租户级，host 上下文不允许修改。</summary>
    private Guid RequireTenant()
    {
        if (!CurrentTenant.Id.HasValue)
        {
            throw new UserFriendlyException("AI 服务按租户配置，请使用租户管理员账号操作。");
        }
        return CurrentTenant.Id.Value;
    }

    // ============= helpers =============

    private async Task<IQueryable<AiUsageRecord>> ApplyFiltersAsync(GetAiUsageRecordsInput input)
    {
        // 调用方已关闭多租户过滤器：租户上下文手动限定本租户；host 上下文查看全部租户
        var query = await _usageRepository.GetQueryableAsync();
        if (CurrentTenant.Id.HasValue)
        {
            var tenantId = CurrentTenant.Id.Value;
            query = query.Where(x => x.TenantId == tenantId);
        }

        if (input.StartTime.HasValue)
        {
            var s = input.StartTime.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(input.StartTime.Value, DateTimeKind.Utc)
                : input.StartTime.Value.ToUniversalTime();
            query = query.Where(x => x.CreationTime >= s);
        }
        if (input.EndTime.HasValue)
        {
            var e = input.EndTime.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(input.EndTime.Value, DateTimeKind.Utc)
                : input.EndTime.Value.ToUniversalTime();
            query = query.Where(x => x.CreationTime < e);
        }
        if (!string.IsNullOrWhiteSpace(input.FeatureGroup))
        {
            query = query.Where(x => x.FeatureGroup == input.FeatureGroup);
        }
        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var kw = input.Filter.Trim();
            query = query.Where(x =>
                (x.UserName != null && x.UserName.Contains(kw)) ||
                x.Feature.Contains(kw) ||
                x.Model.Contains(kw));
        }
        return query;
    }

    private static AiUsageRecordDto MapDto(AiUsageRecord x, Dictionary<Guid, string> tenantNames)
    {
        return new AiUsageRecordDto
        {
            Id = x.Id,
            TenantId = x.TenantId,
            TenantName = x.TenantId.HasValue && tenantNames.TryGetValue(x.TenantId.Value, out var tn) ? tn : null,
            UserId = x.UserId,
            UserName = x.UserName,
            Roles = x.Roles,
            FeatureGroup = x.FeatureGroup,
            FeatureGroupName = AiFeatureGroups.Label(x.FeatureGroup),
            Feature = x.Feature,
            Model = x.Model,
            Status = x.Status,
            StatusName = x.Status switch
            {
                AiUsageStatus.Running => "进行中",
                AiUsageStatus.Completed => "成功",
                AiUsageStatus.Failed => "失败",
                _ => "未知",
            },
            InputTokens = x.InputTokens,
            OutputTokens = x.OutputTokens,
            IsEstimated = x.IsEstimated,
            EstimatedCost = x.EstimatedCost,
            ErrorMessage = x.ErrorMessage,
            CreationTime = x.CreationTime,
        };
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        var k = key.Trim();
        if (k.Length <= 8) return "****";
        return new string('*', Math.Min(k.Length - 4, 24)) + k[^4..];
    }
}
