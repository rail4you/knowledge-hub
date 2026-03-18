using System;
using System.ComponentModel.DataAnnotations;
using KnowledgeHub.Users;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.ObjectExtending;
using Volo.Abp.Threading;

namespace KnowledgeHub.EntityFrameworkCore;

public static class KnowledgeHubEfCoreEntityExtensionMappings
{
    private static readonly OneTimeRunner OneTimeRunner = new OneTimeRunner();

    public static void Configure()
    {
        KnowledgeHubGlobalFeatureConfigurator.Configure();
        KnowledgeHubModuleExtensionConfigurator.Configure();

        OneTimeRunner.Run(() =>
        {
            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, UserRoleType>(
                    "RoleType",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasConversion<int>();
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, Guid?>(
                    "SchoolId",
                    (entityBuilder, propertyBuilder) =>
                    {
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "EmployeeNumber",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(50);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "Department",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(100);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "Major",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(100);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "Course",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(200);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "Title",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(50);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "StudentNumber",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(50);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "Grade",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(50);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "ClassName",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(50);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "ManagementScope",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(500);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "CompanyName",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(200);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "UnifiedSocialCreditCode",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(18);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "Position",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(50);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "Industry",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(100);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "PartnerSchool",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(500);
                    }
                );

            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, string>(
                    "Remark",
                    (entityBuilder, propertyBuilder) =>
                    {
                        propertyBuilder.HasMaxLength(500);
                    }
                );
        });
    }
}
