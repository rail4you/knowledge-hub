using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.UnifiedAuth;

/// <summary>
/// 建租户（学校）时先查统一账户：
/// 已存在同名/同 code/同域名的租户 → 提示“已占用，请申请开通本应用”，而不是重复建校。
/// 新建成功后自动登记到统一账户并开通本应用。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ITenantAppService), typeof(TenantAppService))]
public class UnifiedTenantAppService : TenantAppService
{
    private readonly IUnifiedAuthService _unified;

    public UnifiedTenantAppService(
        ITenantRepository tenantRepository,
        ITenantManager tenantManager,
        IDataSeeder dataSeeder,
        IDistributedEventBus distributedEventBus,
        ILocalEventBus localEventBus,
        IUnifiedAuthService unified)
        : base(tenantRepository, tenantManager, dataSeeder, distributedEventBus, localEventBus)
    {
        _unified = unified;
    }

    public override async Task<TenantDto> CreateAsync(TenantCreateDto input)
    {
        if (_unified.Enabled)
        {
            var lookup = await _unified.LookupTenantAsync(input.Name);
            if (lookup?.Conflict != null)
            {
                var candidates = (lookup.Candidates ?? lookup.Conflict.Candidates ?? new())
                    .Select(c => $"{c.Name}({c.Code})")
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                var hint = candidates.Count > 0 ? $"已存在：{string.Join("、", candidates)}。" : string.Empty;
                throw new UserFriendlyException(
                    $"租户「{input.Name}」已存在于统一账户。{hint}如需在本应用使用，请先在统一账户为本应用开通，而不是重复创建。");
            }
        }

        var dto = await base.CreateAsync(input);

        if (_unified.Enabled)
        {
            await _unified.RegisterTenantAsync(dto.Id, dto.Name);
            Logger.LogInformation("租户已登记到统一账户: {TenantId} {Name}", dto.Id, dto.Name);
        }
        return dto;
    }
}
