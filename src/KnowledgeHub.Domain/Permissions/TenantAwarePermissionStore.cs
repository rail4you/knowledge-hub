using System;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace KnowledgeHub.Permissions;

/// <summary>
/// 权限缓存键的租户隔离助手。
///
/// <para>
/// ABP 内置的 <see cref="PermissionGrantCacheItem"/> 用
/// <c>pn:{providerName},pk:{providerKey},n:{name}</c> 作为缓存键，**不含租户维度**。
/// 而 ABP 运行时解析「角色」授权时用的 providerKey 是**角色名**
/// （见 Volo.Abp.Authorization.Permissions.RolePermissionValueProvider：
/// 取 Claims 的 AbpClaimTypes.Role 值，即角色名）。
/// 角色名在多租户下并不唯一（每个租户都有 SchoolAdmin / Teacher / Student…），
/// 于是不同租户会共用同一个缓存键：先被访问的租户把结果写进缓存，
/// 其它租户直接命中这份「别人的」结果。
/// </para>
///
/// <para>
/// 表现：平台管理员在 A 租户取消了某角色的某项权限，A 租户的角色权限表里那一行确实被删掉了，
/// 但 A 租户用户登录后菜单/接口鉴权仍显示「已授权」——因为命中的是 B 租户写进共享缓存的结果。
/// 反向同样成立：B 租户本该有的权限会被 A 租户的结果覆盖掉。
/// </para>
///
/// <para>
/// 修复方式：把租户维度拼进缓存键的 providerKey 部分。格式仍保持 ABP 的
/// <c>pn:…,pk:…,n:…</c> 结构（permissionName 段不能被污染，
/// PermissionStore.GetPermissionNameFormCacheKeyOrNull 依赖它反查权限名）。
/// </para>
/// </summary>
public static class TenantAwarePermissionCacheKey
{
    /// <summary>宿主（host）作用域标识。</summary>
    public const string HostScope = "host";

    /// <summary>
    /// 给 providerKey 追加租户维度。角色名 / 用户键在多租户下必须隔离。
    /// </summary>
    public static string ScopeProviderKey(string providerKey, Guid? tenantId)
        => providerKey + "@" + (tenantId.HasValue ? tenantId.Value.ToString() : HostScope);

    /// <summary>构造与 <see cref="TenantAwarePermissionStore"/> 完全一致的缓存键。</summary>
    public static string Build(string name, string providerName, string providerKey, Guid? tenantId)
        => PermissionGrantCacheItem.CalculateCacheKey(
            name,
            providerName,
            ScopeProviderKey(providerKey, tenantId));
}

/// <summary>
/// 多租户感知的权限存储，替换 ABP 内置的 <see cref="PermissionStore"/>。
///
/// <para>
/// 唯一改动：缓存键加入租户维度，消除「同名角色的授权结果跨租户串味」。
/// 授权数据的读取仍然走 ABP 原生的 <c>PermissionGrantRepository</c>（自带租户过滤器），
/// 语义与 ABP 完全一致。
/// </para>
/// </summary>
[ExposeServices(typeof(IPermissionStore))]
[Dependency(ReplaceServices = true)]
public class TenantAwarePermissionStore : PermissionStore
{
    protected ICurrentTenant CurrentTenant { get; }

    public TenantAwarePermissionStore(
        IPermissionGrantRepository permissionGrantRepository,
        IDistributedCache<PermissionGrantCacheItem> cache,
        IPermissionDefinitionManager permissionDefinitionManager,
        ICurrentTenant currentTenant)
        : base(permissionGrantRepository, cache, permissionDefinitionManager)
    {
        CurrentTenant = currentTenant;
    }

    protected override string CalculateCacheKey(string name, string providerName, string providerKey)
    {
        return base.CalculateCacheKey(
            name,
            providerName,
            TenantAwarePermissionCacheKey.ScopeProviderKey(providerKey, CurrentTenant.Id));
    }
}

/// <summary>
/// 授权行变更时，清理「租户感知」的权限缓存键。
///
/// <para>
/// ABP 自带的 <see cref="PermissionGrantCacheItemInvalidator"/> 清理的是不带租户维度的旧键，
/// 对本项目替换后的缓存键无效，必须由本类负责清理，否则运营改完权限要等缓存过期才生效。
/// </para>
/// </summary>
public class TenantAwarePermissionGrantCacheItemInvalidator :
    ILocalEventHandler<EntityChangedEventData<PermissionGrant>>,
    ITransientDependency
{
    protected IDistributedCache<PermissionGrantCacheItem> Cache { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public TenantAwarePermissionGrantCacheItemInvalidator(
        IDistributedCache<PermissionGrantCacheItem> cache,
        ICurrentTenant currentTenant)
    {
        Cache = cache;
        CurrentTenant = currentTenant;
    }

    public virtual async Task HandleEventAsync(EntityChangedEventData<PermissionGrant> eventData)
    {
        // 用授权行自身的 TenantId 计算键，而不是当前租户：
        // 事件触发时的环境租户未必等于该行的租户（例如跨租户后台任务）。
        var cacheKey = TenantAwarePermissionCacheKey.Build(
            eventData.Entity.Name,
            eventData.Entity.ProviderName,
            eventData.Entity.ProviderKey,
            eventData.Entity.TenantId);

        using (CurrentTenant.Change(eventData.Entity.TenantId))
        {
            await Cache.RemoveAsync(cacheKey, considerUow: true);
        }
    }
}
