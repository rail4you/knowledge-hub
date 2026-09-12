using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using KnowledgeHub.Settings;
using Volo.Abp;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
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

    public AiQuotaService(
        IRepository<AiUsageRecord, Guid> repository,
        ICurrentUser currentUser,
        ISettingProvider settingProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _settingProvider = settingProvider;
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

        if (!limit.HasValue) return;

        var (startUtc, endUtc) = GetBeijingTodayRangeUtc();
        var query = await _repository.GetQueryableAsync();
        var used = query.Count(x =>
            x.UserId == userId &&
            x.FeatureGroup == featureGroup &&
            x.CreationTime >= startUtc &&
            x.CreationTime < endUtc);

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

        foreach (var group in groups)
        {
            int? limit = null;
            foreach (var role in roles)
            {
                if (quotas.TryGetValue(role, out var byGroup)
                    && byGroup.TryGetValue(group, out var v)
                    && v.HasValue)
                {
                    limit = limit.HasValue ? Math.Min(limit.Value, v.Value) : v.Value;
                }
            }

            if (!limit.HasValue)
            {
                result[group] = null;
                continue;
            }

            var used = query.Count(x =>
                x.UserId == userId.Value &&
                x.FeatureGroup == group &&
                x.CreationTime >= startUtc &&
                x.CreationTime < endUtc);
            result[group] = Math.Max(0, limit.Value - used);
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
