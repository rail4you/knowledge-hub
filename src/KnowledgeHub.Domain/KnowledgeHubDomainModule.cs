using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KnowledgeHub.Localization;
using KnowledgeHub.MultiTenancy;
using KnowledgeHub.Permissions;
using KnowledgeHub.Accounts;
using System;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.BlobStoring.Database;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Caching;
using Volo.Abp.PermissionManagement;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub;

[DependsOn(
    typeof(KnowledgeHubDomainSharedModule),
    typeof(AbpAuditLoggingDomainModule),
    typeof(AbpCachingModule),
    typeof(AbpBackgroundJobsDomainModule),
    typeof(AbpFeatureManagementDomainModule),
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpPermissionManagementDomainOpenIddictModule),
    typeof(AbpSettingManagementDomainModule),
    typeof(AbpEmailingModule),
    typeof(AbpIdentityDomainModule),
    typeof(AbpOpenIddictDomainModule),
    typeof(AbpTenantManagementDomainModule),
    typeof(BlobStoringDatabaseDomainModule)
    )]
public class KnowledgeHubDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = MultiTenancyConsts.IsEnabled;
        });

        // 权限缓存自愈兜底：
        // 授权行的失效依赖事件通知（EntityChangedEvent → TenantAwarePermissionGrantCacheItemInvalidator），
        // 少数路径（批量删除、跨作用域写入、旧版本遗留键）可能漏发，导致「后台已取消权限，前台菜单还在」。
        // 给 PermissionGrantCacheItem 加一个较短的绝对过期时间，让脏缓存最多存活 1 分钟自动失效。
        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.CacheConfigurators.Add(cacheName =>
                cacheName == typeof(PermissionGrantCacheItem).FullName
                    ? new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60),
                    }
                    : null);
        });

        // 启动期自动为所有租户重新种子角色权限，
        // 避免老租户因为数据种子只跑过一次而长期缺失权限。
        context.Services.AddHostedService<RolePermissionEnsureHostedService>();

        // 账号有效期自动熔断后台任务：周期性扫描到期账号并收回编辑/管理权限
        context.Services.AddHostedService<AccountValidityCheckHostedService>();



#if DEBUG
        context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
#endif
    }
}
