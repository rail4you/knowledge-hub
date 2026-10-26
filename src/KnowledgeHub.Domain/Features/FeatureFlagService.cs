using System.Threading.Tasks;
using KnowledgeHub.Features;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Features;

namespace KnowledgeHub.Features;

/// <summary>
/// 产品能力开关（原「版本 / 授权」体系的功能开关部分）。
///
/// <para>
/// 平台的增值能力（联盟协同 / 两级审核 / 特教模块 / 租户数量上限）统一由 ABP Feature 管理，
/// 由宿主管理员在「功能管理」中按租户或全局配置。这里只做一层薄封装，
/// 避免各 AppService 直接拼 Feature 名称。
/// </para>
///
/// <para>
/// 历史背景：项目早期有「安装向导 + 许可证密钥 + 标准版升级」的授权体系，
/// 该体系已整体移除；Feature 开关保留，不再依赖任何许可证。
/// </para>
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>允许创建的租户数量上限（-1 表示不限）。</summary>
    Task<int> GetMaxTenantCountAsync();

    /// <summary>是否启用多校联盟协同。</summary>
    Task<bool> IsAllianceEnabledAsync();

    /// <summary>是否启用两级审核（院校审核 → 联盟审核）。</summary>
    Task<bool> IsTwoLevelApprovalEnabledAsync();

    /// <summary>是否启用特殊教育扩展模块。</summary>
    Task<bool> IsSpecialEducationEnabledAsync();
}

public class FeatureFlagService : IFeatureFlagService, ITransientDependency
{
    private readonly IFeatureChecker _featureChecker;

    public FeatureFlagService(IFeatureChecker featureChecker)
    {
        _featureChecker = featureChecker;
    }

    public async Task<int> GetMaxTenantCountAsync()
    {
        var value = await _featureChecker.GetOrNullAsync(KnowledgeHubFeatures.MaxTenantCount) ?? "1";
        return int.TryParse(value, out var count) ? count : 1;
    }

    public async Task<bool> IsAllianceEnabledAsync()
    {
        return await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.Alliance);
    }

    public async Task<bool> IsTwoLevelApprovalEnabledAsync()
    {
        return await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.TwoLevelApproval);
    }

    public async Task<bool> IsSpecialEducationEnabledAsync()
    {
        return await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.SpecialEducation);
    }
}
