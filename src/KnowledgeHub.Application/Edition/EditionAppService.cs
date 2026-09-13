using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Install.Dto;
using KnowledgeHub.Install;
using KnowledgeHub.Edition;
using KnowledgeHub.Settings;
using KnowledgeHub.Features;
using KnowledgeHub.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.Application.Edition;

public class EditionAppService : ApplicationService, IEditionAppService
{
    private readonly IInstallStatusService _installStatusService;
    private readonly ILicenseValidator _licenseValidator;
    private readonly IEditionConfigService _editionConfig;
    private readonly KnowledgeHubDbContext _dbContext;
    private readonly IInstallAccessGuard _installAccessGuard;

    public EditionAppService(
        IInstallStatusService installStatusService,
        ILicenseValidator licenseValidator,
        IEditionConfigService editionConfig,
        KnowledgeHubDbContext dbContext,
        IInstallAccessGuard installAccessGuard)
    {
        _installStatusService = installStatusService;
        _licenseValidator = licenseValidator;
        _editionConfig = editionConfig;
        _dbContext = dbContext;
        _installAccessGuard = installAccessGuard;
    }

    public async Task<EditionDto> GetCurrentEditionAsync()
    {
        var edition = await _editionConfig.GetEditionAsync();
        var maxTenantCount = await _editionConfig.GetMaxTenantCountAsync();
        var isAllianceEnabled = await _editionConfig.IsAllianceEnabledAsync();
        var isTwoLevelApprovalEnabled = await _editionConfig.IsTwoLevelApprovalEnabledAsync();
        var isSpecialEducationEnabled = await _editionConfig.IsSpecialEducationEnabledAsync();

        return new EditionDto
        {
            Edition = edition,
            MaxTenantCount = maxTenantCount,
            IsAllianceEnabled = isAllianceEnabled,
            IsTwoLevelApprovalEnabled = isTwoLevelApprovalEnabled,
            IsSpecialEducationEnabled = isSpecialEducationEnabled
        };
    }

    [Authorize]
    [UnitOfWork]
    public virtual async Task UpgradeToStandardAsync(EditionUpgradeInputDto input)
    {
        // 版本升级会写入宿主 Feature/Setting 并创建租户，仅宿主管理员可执行。
        if (CurrentTenant.Id.HasValue)
        {
            throw new AbpAuthorizationException("仅宿主管理员可升级版本。");
        }

        // 与安装入口一致：本机或携带正确安装令牌，防止匿名越权开启付费能力。
        if (!_installAccessGuard.IsAccessAllowed(input.InstallToken))
        {
            throw new AbpAuthorizationException("升级入口已受保护：请从服务器本机访问，或在请求中提供正确的安装令牌。");
        }

        if (!await _installStatusService.IsInstalledAsync())
        {
            throw new UserFriendlyException("系统未安装");
        }

        var currentEdition = await _editionConfig.GetEditionAsync();
        if (currentEdition == KnowledgeHubEditions.Standard)
        {
            throw new UserFriendlyException("已经是标准版");
        }

        if (!_licenseValidator.Validate(input.LicenseKey))
        {
            throw new UserFriendlyException("许可证无效");
        }

        await UpdateSettingsToStandardAsync(input.LicenseKey);
        await UpdateFeaturesToStandardAsync();
        await CreateDefaultTenantAsync();
    }

    private async Task UpdateSettingsToStandardAsync(string licenseKey)
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE ""AbpSettings"" SET ""Value"" = {0} WHERE ""Name"" = {1} AND ""ProviderName"" = {2}",
            KnowledgeHubEditions.Standard, KnowledgeHubSettings.InstalledEdition, "H");
        
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE ""AbpSettings"" SET ""Value"" = {0} WHERE ""Name"" = {1} AND ""ProviderName"" = {2}",
            licenseKey, KnowledgeHubSettings.LicenseKey, "H");
    }

    private async Task UpdateFeaturesToStandardAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE ""AbpFeatureValues"" SET ""Value"" = {0} WHERE ""Name"" = {1} AND ""ProviderName"" = {2}",
            "-1", KnowledgeHubFeatures.MaxTenantCount, "H");
        
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE ""AbpFeatureValues"" SET ""Value"" = {0} WHERE ""Name"" = {1} AND ""ProviderName"" = {2}",
            "true", KnowledgeHubFeatures.Alliance, "H");
        
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE ""AbpFeatureValues"" SET ""Value"" = {0} WHERE ""Name"" = {1} AND ""ProviderName"" = {2}",
            "true", KnowledgeHubFeatures.TwoLevelApproval, "H");

        await _dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE ""AbpFeatureValues"" SET ""Value"" = {0} WHERE ""Name"" = {1} AND ""ProviderName"" = {2}",
            "true", KnowledgeHubFeatures.SpecialEducation, "H");
    }

    private async Task CreateDefaultTenantAsync()
    {
        var result = await _dbContext.Database.SqlQueryRaw<int>(
            @"SELECT COUNT(*) FROM ""AbpTenants"" WHERE ""Name"" = {0}", "Default").ToListAsync();
        
        if (result.FirstOrDefault() == 0)
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
                @"INSERT INTO ""AbpTenants"" (""Id"", ""Name"", ""DisplayName"", ""IsActive"", ""CreationTime"", ""CreatorId"", ""LastModificationTime"", ""LastModifierId"", ""IsDeleted"", ""DeleterId"", ""DeletionTime"")
                  VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})",
                Guid.NewGuid(), "Default", "默认租户", true, DateTime.UtcNow, null, null, null, false, null, null);
        }
    }
}