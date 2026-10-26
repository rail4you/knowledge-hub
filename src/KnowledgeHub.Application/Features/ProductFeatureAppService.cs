using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using KnowledgeHub.Features.Dtos;
using Microsoft.AspNetCore.Authorization;

namespace KnowledgeHub.Features;

/// <summary>
/// 产品能力开关查询实现。
///
/// <para>
/// 值来自 ABP Feature（宿主管理员在「功能管理」中配置），与租户上下文相关：
/// 租户侧读取到的就是该租户被授予的能力。匿名可访问，避免登录页因未登录而拿不到开关。
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
public class ProductFeatureAppService : KnowledgeHubAppService, IProductFeatureAppService
{
    private readonly IFeatureFlagService _featureFlags;

    public ProductFeatureAppService(IFeatureFlagService featureFlags)
    {
        _featureFlags = featureFlags;
    }

    [AllowAnonymous]
    public virtual async Task<ProductFeatureDto> GetAsync()
    {
        return new ProductFeatureDto
        {
            MaxTenantCount = await _featureFlags.GetMaxTenantCountAsync(),
            IsAllianceEnabled = await _featureFlags.IsAllianceEnabledAsync(),
            IsTwoLevelApprovalEnabled = await _featureFlags.IsTwoLevelApprovalEnabledAsync(),
            IsSpecialEducationEnabled = await _featureFlags.IsSpecialEducationEnabledAsync()
        };
    }
}
