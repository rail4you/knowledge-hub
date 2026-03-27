using System;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Uow;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.TenantManagement.EntityFrameworkCore;
using Volo.Abp.Studio;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Courses;
using KnowledgeHub.EntityFrameworkCore.Courses;
using KnowledgeHub.Alliance;
using KnowledgeHub.EntityFrameworkCore.Alliance;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;

namespace KnowledgeHub.EntityFrameworkCore;

[DependsOn(
    typeof(KnowledgeHubDomainModule),
    typeof(AbpPermissionManagementEntityFrameworkCoreModule),
    typeof(AbpSettingManagementEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(AbpBackgroundJobsEntityFrameworkCoreModule),
    typeof(AbpAuditLoggingEntityFrameworkCoreModule),
    typeof(AbpFeatureManagementEntityFrameworkCoreModule),
    typeof(AbpIdentityEntityFrameworkCoreModule),
    typeof(AbpOpenIddictEntityFrameworkCoreModule),
    typeof(AbpTenantManagementEntityFrameworkCoreModule),
    typeof(BlobStoringDatabaseEntityFrameworkCoreModule)
    )]
public class KnowledgeHubEntityFrameworkCoreModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        KnowledgeHubEfCoreEntityExtensionMappings.Configure();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<KnowledgeHubDbContext>(options =>
        {
                /* Remove "includeAllEntities: true" to create
                 * default repositories only for aggregate roots */
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        // Register IDocumentIndexRepository
        context.Services.AddScoped<IDocumentIndexRepository, EfCoreDocumentIndexRepository>();
        Console.WriteLine("=== KnowledgeHubEntityFrameworkCoreModule: IDocumentIndexRepository registered ===");

        // Register custom Course repository
        context.Services.AddScoped<ICourseRepository, EfCoreCourseRepository>();
        Console.WriteLine("=== KnowledgeHubEntityFrameworkCoreModule: ICourseRepository registered ===");

        // Register Alliance repositories
        context.Services.AddScoped<IAllianceRepository, EfCoreAllianceRepository>();
        context.Services.AddScoped<IAllianceMemberRepository, EfCoreAllianceMemberRepository>();
        context.Services.AddScoped<IAllianceAuditRepository, EfCoreAllianceAuditRepository>();
        Console.WriteLine("=== KnowledgeHubEntityFrameworkCoreModule: Alliance repositories registered ===");

        if (AbpStudioAnalyzeHelper.IsInAnalyzeMode)
        {
            return;
        }

        Configure<AbpDbContextOptions>(options =>
        {
            /* The main point to change your DBMS.
             * See also KnowledgeHubDbContextFactory for EF Core tooling. */

            options.UseNpgsql();

        });
    }
}
