using System;
using System.Threading.Tasks;
using KnowledgeHub.Features;
using KnowledgeHub.Schools;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Features;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace KnowledgeHub.Organizations;

/// <summary>单位有效性检查结果。</summary>
public class OrganizationValidityInfo
{
    public bool IsValid { get; set; }

    public Guid? TenantId { get; set; }

    public string? OrganizationName { get; set; }

    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    /// <summary>不可用原因（用于前端提示）。</summary>
    public string? Reason { get; set; }

    /// <summary>到期后是否允许只读使用。</summary>
    public bool ReadOnlyAfterExpiry { get; set; } = true;
}

/// <summary>
/// 单位（学校 / 企业）有效期门禁。
///
/// <para>
/// 与既有 <c>AccountValidity</c>（用户级权限熔断）<b>并存但不相同</b>：
/// 这里是<b>组织级</b>硬门禁 —— 到期后仍可读，但禁止一切写操作。
/// </para>
///
/// <para>
/// 调用点：
/// </para>
/// <list type="bullet">
/// <item><description>HTTP 层：<c>OrganizationValidityFilter</c> 统一拦截写请求（POST/PUT/DELETE/PATCH）。</description></item>
/// <item><description>异步任务：Hangfire job 执行时显式调用 <see cref="EnsureWritableForTenantAsync"/> 复核。</description></item>
/// </list>
/// </summary>
public interface IOrganizationValidityChecker
{
    /// <summary>检查某学校租户当前是否有效（未设置有效期视为长期有效）。</summary>
    Task<OrganizationValidityInfo> CheckTenantAsync(Guid? tenantId);

    /// <summary>检查当前上下文（学校 + 企业用户所在企业）是否允许写操作，不允许则抛业务异常。</summary>
    Task EnsureWritableAsync();

    /// <summary>指定租户的写前复核（异步任务 / 后台服务用）。</summary>
    Task EnsureWritableForTenantAsync(Guid tenantId);
}

public class OrganizationValidityChecker : IOrganizationValidityChecker, ITransientDependency
{
    private readonly IRepository<School, Guid> _schoolRepository;
    private readonly IFeatureChecker _featureChecker;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public OrganizationValidityChecker(
        IRepository<School, Guid> schoolRepository,
        IFeatureChecker featureChecker,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _schoolRepository = schoolRepository;
        _featureChecker = featureChecker;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<OrganizationValidityInfo> CheckTenantAsync(Guid? tenantId)
    {
        if (!tenantId.HasValue)
        {
            // 宿主上下文（平台侧）不受单位有效期限制
            return new OrganizationValidityInfo { IsValid = true };
        }

        var school = await _schoolRepository.FirstOrDefaultAsync(x => x.TenantId == tenantId.Value);
        if (school == null)
        {
            // 未建档的租户不阻断（避免误伤新建租户）
            return new OrganizationValidityInfo { IsValid = true, TenantId = tenantId };
        }

        var now = _clock.Now;
        var isValid = school.IsValidAt(now);

        return new OrganizationValidityInfo
        {
            IsValid = isValid,
            TenantId = tenantId,
            OrganizationName = school.Name,
            ValidFrom = school.ValidFrom,
            ValidTo = school.ValidTo,
            ReadOnlyAfterExpiry = school.ReadOnlyAfterExpiry,
            Reason = isValid
                ? null
                : $"单位「{school.Name}」已过有效期（至 {school.ValidTo:yyyy-MM-dd}），当前仅可查看，"
                  + "新建 / 编辑 / 删除 / 发布 / 审核 / 授权等操作已停用。请联系平台管理员续期。"
        };
    }

    public async Task EnsureWritableAsync()
    {
        // 功能开关：默认关闭，开启后才执行门禁（便于灰度与回退）
        var enabled = await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.OrganizationValidity);
        if (!enabled)
        {
            return;
        }

        var info = await CheckTenantAsync(_currentTenant.Id);
        if (!info.IsValid)
        {
            throw new BusinessException("KnowledgeHub:Organization:Expired")
                .WithData("tenantId", info.TenantId)
                .WithData("organizationName", info.OrganizationName)
                .WithData("validTo", info.ValidTo)
                .WithData("message", info.Reason);
        }
    }

    public async Task EnsureWritableForTenantAsync(Guid tenantId)
    {
        var enabled = await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.OrganizationValidity);
        if (!enabled)
        {
            return;
        }

        // 后台任务可能不在目标租户上下文，这里显式切换以确保开关按目标租户读取
        using (_currentTenant.Change(tenantId))
        {
            await EnsureWritableAsync();
        }
    }
}
