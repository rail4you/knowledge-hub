using System;
using System.Threading.Tasks;
using KnowledgeHub.Install.Dto;
using KnowledgeHub.Install;
using KnowledgeHub.Settings;
using KnowledgeHub.Features;
using KnowledgeHub.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using TenantManagement = Volo.Abp.TenantManagement;

namespace KnowledgeHub.Application.Install;

public class InstallAppService : ApplicationService, IInstallAppService
{
    private readonly IInstallStatusService _installStatusService;
    private readonly ILicenseValidator _licenseValidator;
    private readonly IdentityUserManager _identityUserManager;
    private readonly KnowledgeHubDbContext _dbContext;
    private readonly IRepository<TenantManagement.Tenant, Guid> _tenantRepository;
    private readonly IInstallAccessGuard _installAccessGuard;

    public InstallAppService(
        IInstallStatusService installStatusService,
        ILicenseValidator licenseValidator,
        IdentityUserManager identityUserManager,
        KnowledgeHubDbContext dbContext,
        IRepository<TenantManagement.Tenant, Guid> tenantRepository,
        IInstallAccessGuard installAccessGuard)
    {
        _installStatusService = installStatusService;
        _licenseValidator = licenseValidator;
        _identityUserManager = identityUserManager;
        _dbContext = dbContext;
        _tenantRepository = tenantRepository;
        _installAccessGuard = installAccessGuard;
    }

    // 安装状态需在登录前可查（安装向导使用）
    [AllowAnonymous]
    public async Task<InstallStatusDto> GetStatusAsync()
    {
        var isInstalled = await _installStatusService.IsInstalledAsync();
        
        return new InstallStatusDto
        {
            IsInstalled = isInstalled,
            CurrentEdition = isInstalled ? "Standard" : "Basic"
        };
    }

    // 安装需在登录前执行；访问控制由 InstallAccessGuard（安装令牌/回环）负责，见方法内校验。
    [AllowAnonymous]
    [UnitOfWork]
    public virtual async Task InstallAsync(InstallInputDto input)
    {
        // 安全：安装入口默认仅允许本机（回环）或携带正确安装令牌的请求，防止公网匿名抢注管理员。
        if (!_installAccessGuard.IsAccessAllowed(input.InstallToken))
        {
            throw new AbpAuthorizationException("安装入口已受保护：请从服务器本机访问，或在请求中提供正确的安装令牌。");
        }

        if (await _installStatusService.IsInstalledAsync())
        {
            throw new UserFriendlyException("系统已安装");
        }

        if (input.Edition == "Standard" && !_licenseValidator.Validate(input.LicenseKey))
        {
            throw new UserFriendlyException("许可证无效");
        }

        var adminUser = await _identityUserManager.FindByNameAsync(input.AdminUsername);
        if (adminUser == null)
        {
            adminUser = new IdentityUser(
                Guid.NewGuid(),
                input.AdminUsername,
                input.AdminEmail
            );
            await _identityUserManager.CreateAsync(adminUser, input.AdminPassword);
        }

        await SaveInstallSettingsAsync(input);
        await SaveFeatureValuesAsync(input.Edition);
        
        if (input.Edition == "Standard")
        {
            await CreateDefaultTenantAsync();
        }
    }

    private async Task SaveInstallSettingsAsync(InstallInputDto input)
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""AbpSettings"" (""Id"", ""Name"", ""Value"", ""ProviderName"", ""ProviderKey"")
              VALUES ({0}, {1}, {2}, {3}, {4})",
            Guid.NewGuid(), KnowledgeHubSettings.IsInstalled, "true", "H", (string?)null);
        
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""AbpSettings"" (""Id"", ""Name"", ""Value"", ""ProviderName"", ""ProviderKey"")
              VALUES ({0}, {1}, {2}, {3}, {4})",
            Guid.NewGuid(), KnowledgeHubSettings.InstalledEdition, input.Edition, "H", (string?)null);
        
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""AbpSettings"" (""Id"", ""Name"", ""Value"", ""ProviderName"", ""ProviderKey"")
              VALUES ({0}, {1}, {2}, {3}, {4})",
            Guid.NewGuid(), KnowledgeHubSettings.LicenseKey, input.LicenseKey ?? "", "H", (string?)null);
    }

    private async Task SaveFeatureValuesAsync(string edition)
    {
        var maxTenantCount = edition == "Standard" ? "-1" : "1";
        var allianceEnabled = edition == "Standard" ? "true" : "false";
        var twoLevelApproval = edition == "Standard" ? "true" : "false";

        await _dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""AbpFeatureValues"" (""Id"", ""Name"", ""Value"", ""ProviderName"", ""ProviderKey"")
              VALUES ({0}, {1}, {2}, {3}, {4})",
            Guid.NewGuid(), KnowledgeHubFeatures.MaxTenantCount, maxTenantCount, "H", (string?)null);
        
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""AbpFeatureValues"" (""Id"", ""Name"", ""Value"", ""ProviderName"", ""ProviderKey"")
              VALUES ({0}, {1}, {2}, {3}, {4})",
            Guid.NewGuid(), KnowledgeHubFeatures.Alliance, allianceEnabled, "H", (string?)null);
        
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""AbpFeatureValues"" (""Id"", ""Name"", ""Value"", ""ProviderName"", ""ProviderKey"")
              VALUES ({0}, {1}, {2}, {3}, {4})",
            Guid.NewGuid(), KnowledgeHubFeatures.TwoLevelApproval, twoLevelApproval, "H", (string?)null);

        await _dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""AbpFeatureValues"" (""Id"", ""Name"", ""Value"", ""ProviderName"", ""ProviderKey"")
              VALUES ({0}, {1}, {2}, {3}, {4})",
            Guid.NewGuid(), KnowledgeHubFeatures.SpecialEducation, "false", "H", (string?)null);
    }

    private async Task CreateDefaultTenantAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""AbpTenants"" (""Id"", ""Name"", ""DisplayName"", ""IsActive"", ""CreationTime"", ""CreatorId"", ""LastModificationTime"", ""LastModifierId"", ""IsDeleted"", ""DeleterId"", ""DeletionTime"")
              VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})",
            Guid.NewGuid(), "Default", "默认租户", true, DateTime.UtcNow, null, null, null, false, null, null);
    }
}