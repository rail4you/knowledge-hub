using System;
using Volo.Abp.Identity;
using Volo.Abp.ObjectExtending;
using Volo.Abp.Threading;

namespace KnowledgeHub;

public static class KnowledgeHubDtoExtensions
{
    private static readonly OneTimeRunner OneTimeRunner = new OneTimeRunner();

    public static void Configure()
    {
        OneTimeRunner.Run(() =>
        {
            ObjectExtensionManager.Instance
                .AddOrUpdateProperty<IdentityUserCreateDto, Guid?>("TenantId")
                .AddOrUpdateProperty<IdentityUserUpdateDto, Guid?>("TenantId");
        });
    }
}
