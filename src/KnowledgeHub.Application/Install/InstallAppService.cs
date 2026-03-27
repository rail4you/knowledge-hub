using System;
using System.Threading.Tasks;
using KnowledgeHub.Install.Dto;
using KnowledgeHub.Install;
using Volo.Abp;
using Volo.Abp.Application.Services;
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

    public InstallAppService(
        IInstallStatusService installStatusService,
        ILicenseValidator licenseValidator,
        IdentityUserManager identityUserManager)
    {
        _installStatusService = installStatusService;
        _licenseValidator = licenseValidator;
        _identityUserManager = identityUserManager;
    }

    public async Task<InstallStatusDto> GetStatusAsync()
    {
        var isInstalled = await _installStatusService.IsInstalledAsync();
        
        return new InstallStatusDto
        {
            IsInstalled = isInstalled,
            CurrentEdition = isInstalled ? "Standard" : "Basic"
        };
    }

    [UnitOfWork]
    public virtual async Task InstallAsync(InstallInputDto input)
    {
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
    }
}

public interface IInstallAppService : IApplicationService
{
    Task<InstallStatusDto> GetStatusAsync();
    Task InstallAsync(InstallInputDto input);
}