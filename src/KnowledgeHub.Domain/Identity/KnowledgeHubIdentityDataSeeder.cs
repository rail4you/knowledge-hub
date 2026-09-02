using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Identity;

/// <summary>
/// 覆盖 ABP 默认的 <see cref="IdentityDataSeeder"/>，落实“admin 仅宿主可用”约束：
/// - 宿主（TenantId == null）时按 ABP 默认行为创建全局 admin（admin@abp.io / 1q2w3E*）；
/// - 租户（TenantId != null）时，禁止自动创建用户名为 admin 的租户级 admin。
///   原因：历史种子导致每个租户都会产生一个与宿主同名同密码的 qidi/admin，登录时虽经租户过滤，
///   仍能在租户 tab 下用 admin/1q2w3E* 登录成功（实际命中租户 admin），与“全局管理员仅全局 tab 可用”的产品约束冲突。
///   租户的初始管理员应通过 TenantUserAppService 以 school_admin / qidi-admin 等具名账号创建，NotNull admin。
/// 若已存在历史残留的租户 admin（AbpUsers.TenantId != null AND UserName='admin'），
/// 本类不会主动删除，但后续的 SignInManager 与 LoginModel 会禁止其登录；建议配合 SQL 清理：
///   UPDATE "AbpUsers" SET "IsActive"=false, "IsDeleted"=true WHERE "UserName"='admin' AND "TenantId" IS NOT NULL AND "IsDeleted"=false;
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IIdentityDataSeeder), typeof(IdentityDataSeeder), typeof(KnowledgeHubIdentityDataSeeder))]
public class KnowledgeHubIdentityDataSeeder : IdentityDataSeeder, ITransientDependency
{
    public KnowledgeHubIdentityDataSeeder(
        IGuidGenerator guidGenerator,
        IIdentityRoleRepository roleRepository,
        IIdentityUserRepository userRepository,
        Microsoft.AspNetCore.Identity.ILookupNormalizer lookupNormalizer,
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        ICurrentTenant currentTenant,
        IOptions<Microsoft.AspNetCore.Identity.IdentityOptions> identityOptions)
        : base(guidGenerator, roleRepository, userRepository, lookupNormalizer, userManager, roleManager, currentTenant, identityOptions)
    {
    }

    public override async Task<IdentityDataSeedResult> SeedAsync(string adminEmail, string adminPassword, Guid? tenantId = null, string? adminUserName = null)
    {
        if (tenantId.HasValue)
        {
            var normalizedAdminName = adminUserName ?? "admin";
            if (string.Equals(normalizedAdminName.Trim(), "admin", StringComparison.OrdinalIgnoreCase))
            {
                // 租户上下文禁止自动创建 admin，保持租户内无同名宿主账号。
                // 仍返回空结果，让上层认为 seeding 已完成，避免反复重试。
                return new IdentityDataSeedResult();
            }
        }

        return await base.SeedAsync(adminEmail, adminPassword, tenantId, adminUserName);
    }
}
