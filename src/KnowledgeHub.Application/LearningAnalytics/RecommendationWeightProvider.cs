using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 推荐三路权重读取/写入。
/// 优先级：**ABP Setting（可在运行时一键应用，带缓存自动失效）** → appsettings → 默认 0.5/0.3/0.2。
/// 读取失败时静默回退，避免分析链路因配置异常中断。
/// </summary>
public class RecommendationWeightProvider : ITransientDependency
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecommendationWeightProvider> _logger;

    public RecommendationWeightProvider(
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        IConfiguration configuration,
        ILogger<RecommendationWeightProvider> logger)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RecommendationWeights> GetAsync()
    {
        var fallback = RecommendationWeights.FromConfiguration(_configuration);
        try
        {
            var content = await ReadAsync(KnowledgeHubSettings.RecWeightContent, fallback.Content);
            var collab = await ReadAsync(KnowledgeHubSettings.RecWeightCollab, fallback.Collab);
            var fit = await ReadAsync(KnowledgeHubSettings.RecWeightFit, fallback.Fit);

            var sum = content + collab + fit;
            if (sum <= 0)
            {
                return fallback;
            }

            // 统一归一化，避免设置项被写成不合法的组合
            return new RecommendationWeights(
                Math.Round(content / sum, 4),
                Math.Round(collab / sum, 4),
                Math.Round(fit / sum, 4));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取推荐权重设置失败，回退配置文件");
            return fallback;
        }
    }

    private async Task<double> ReadAsync(string name, double fallback)
    {
        var raw = await _settingProvider.GetOrNullAsync(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0, 1)
            : fallback;
    }

    /// <summary>写入全局设置（保存即生效，ISettingProvider 缓存会自动失效）。</summary>
    public async Task<RecommendationWeights> SetAsync(RecommendationWeights weights)
    {
        var sum = weights.Content + weights.Collab + weights.Fit;
        if (sum <= 0)
        {
            throw new Volo.Abp.UserFriendlyException("权重之和必须大于 0");
        }

        var normalized = new RecommendationWeights(
            Math.Round(weights.Content / sum, 4),
            Math.Round(weights.Collab / sum, 4),
            Math.Round(weights.Fit / sum, 4));

        await _settingManager.SetGlobalAsync(KnowledgeHubSettings.RecWeightContent, normalized.Content.ToString(CultureInfo.InvariantCulture));
        await _settingManager.SetGlobalAsync(KnowledgeHubSettings.RecWeightCollab, normalized.Collab.ToString(CultureInfo.InvariantCulture));
        await _settingManager.SetGlobalAsync(KnowledgeHubSettings.RecWeightFit, normalized.Fit.ToString(CultureInfo.InvariantCulture));

        _logger.LogInformation("推荐权重已更新：{Label}", normalized.Label);
        return normalized;
    }
}
