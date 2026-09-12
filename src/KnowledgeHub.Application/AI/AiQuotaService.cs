using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using KnowledgeHub.Settings;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Security.Claims;
using Volo.Abp.Settings;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// AI 每日配额：按（角色 × 功能分组）限制每人每天调用次数。
/// 配置存设置 Ai.Quotas（JSON），取用户所有角色中最严的那个；未配置=不限。
/// 按北京时间自然日统计（含进行中+成功+失败调用，防刷重试）。
/// </summary>
public interface IAiQuotaService
{
    /// <summary>检查当前用户配额，超限抛中文友好异常。</summary>
    Task CheckAsync(string featureGroup);

    /// <summary>后台/转调场景用显式身份检查。</summary>
    Task CheckAsync(Guid userId, string[] roles, string featureGroup);

    /// <summary>当前用户各分组今日剩余次数（null=不限）。</summary>
    Task<Dictionary<string, int?>> GetMyRemainingAsync();

    /// <summary>原始配额配置（管理页用）。</summary>
    Task<Dictionary<string, Dictionary<string, int?>>> GetQuotasAsync();
}

public class AiQuotaService : IAiQuotaService, ITransientDependency
{
    private readonly IRepository<AiUsageRecord, Guid> _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ISettingProvider _settingProvider;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public AiQuotaService(
        IRepository<AiUsageRecord, Guid> repository,
        ICurrentUser currentUser,
        ISettingProvider settingProvider,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _repository = repository;
        _currentUser = currentUser;
        _settingProvider = settingProvider;
        _asyncExecuter = asyncExecuter;
    }

    public Task CheckAsync(string featureGroup)
    {
        var userId = _currentUser.Id ?? throw new AbpAuthorizationException();
        var roles = _currentUser.Roles ?? Array.Empty<string>();
        return CheckAsync(userId, roles, featureGroup);
    }

    public async Task CheckAsync(Guid userId, string[] roles, string featureGroup)
    {
        var quotas = await GetQuotasAsync();
        var limit = ResolveLimit(quotas, roles, featureGroup);
        if (!limit.HasValue) return;

        var (startUtc, endUtc) = GetBeijingTodayRangeUtc();
        var query = await _repository.GetQueryableAsync();
        var used = await _asyncExecuter.CountAsync(query.Where(x =>
            x.UserId == userId &&
            x.FeatureGroup == featureGroup &&
            x.CreationTime >= startUtc &&
            x.CreationTime < endUtc));

        if (used >= limit.Value)
        {
            throw new UserFriendlyException(
                $"今日{AiFeatureGroups.Label(featureGroup)}已达上限（{limit.Value}次/天），明天再来吧。");
        }
    }

    public async Task<Dictionary<string, int?>> GetMyRemainingAsync()
    {
        var result = new Dictionary<string, int?>();
        var groups = new[]
        {
            AiFeatureGroups.CareerGuidance, AiFeatureGroups.LessonPlan, AiFeatureGroups.CaseAnalysis,
            AiFeatureGroups.ExerciseGenerate, AiFeatureGroups.Chat,
        };
        var userId = _currentUser.Id;
        if (!userId.HasValue) return groups.ToDictionary(g => g, _ => (int?)null);

        var quotas = await GetQuotasAsync();
        var roles = _currentUser.Roles ?? Array.Empty<string>();
        var (startUtc, endUtc) = GetBeijingTodayRangeUtc();
        var query = await _repository.GetQueryableAsync();

        // 一次分组统计，避免按分组多次往返数据库
        var counts = await _asyncExecuter.ToListAsync(query
            .Where(x => x.UserId == userId.Value && x.CreationTime >= startUtc && x.CreationTime < endUtc)
            .GroupBy(x => x.FeatureGroup)
            .Select(g => new { Group = g.Key, Count = g.Count() }));
        var usedByGroup = counts.ToDictionary(x => x.Group, x => x.Count);

        foreach (var group in groups)
        {
            var limit = ResolveLimit(quotas, roles, group);
            result[group] = limit.HasValue
                ? Math.Max(0, limit.Value - usedByGroup.GetValueOrDefault(group))
                : null;
        }

        return result;
    }

    public async Task<Dictionary<string, Dictionary<string, int?>>> GetQuotasAsync()
    {
        try
        {
            var json = await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.AiQuotas);
            if (string.IsNullOrWhiteSpace(json)) json = KnowledgeHubSettings.DefaultAiQuotas;
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int?>>>(json);
            return parsed ?? new Dictionary<string, Dictionary<string, int?>>();
        }
        catch
        {
            return new Dictionary<string, Dictionary<string, int?>>();
        }
    }

    /// <summary>取用户所有角色中最严的配额；未配置返回 null（不限）。</summary>
    private static int? ResolveLimit(
        Dictionary<string, Dictionary<string, int?>> quotas,
        string[] roles,
        string featureGroup)
    {
        int? limit = null;
        foreach (var role in roles)
        {
            if (quotas.TryGetValue(role, out var byGroup)
                && byGroup.TryGetValue(featureGroup, out var v)
                && v.HasValue)
            {
                limit = limit.HasValue ? Math.Min(limit.Value, v.Value) : v.Value;
            }
        }
        return limit;
    }

    /// <summary>北京时间今日 00:00~24:00 对应的 UTC 区间（CreationTime 按 UTC 存）。</summary>
    public static (DateTime StartUtc, DateTime EndUtc) GetBeijingTodayRangeUtc()
    {
        TimeZoneInfo beijing;
        try
        {
            beijing = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        }
        catch
        {
            beijing = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }

        var beijingNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, beijing);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(beijingNow.Date, beijing);
        return (startUtc, startUtc.AddDays(1));
    }
}
