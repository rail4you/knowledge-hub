using KnowledgeHub.Resources;
using KnowledgeHub.EntityFrameworkCore;
using KnowledgeHub.Application.Search;
using KnowledgeHub.Application.Search.LiteParse;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Resources.Conversion;
using Microsoft.Extensions.Configuration;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Account;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub;

 [DependsOn(
    typeof(KnowledgeHubDomainModule),
    typeof(KnowledgeHubApplicationContractsModule),
    typeof(KnowledgeHubEntityFrameworkCoreModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpAccountApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule)
    )]
public class KnowledgeHubApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        context.Services.Configure<OfficeConversionOptions>(
            configuration.GetSection("OfficeConversion"));

        context.Services.AddSingleton<IDocumentExtractionService, LiteParseDocumentExtractionService>();
        context.Services.AddSingleton<ILiteParseExtractionService, LiteParseDocumentExtractionService>();
        context.Services.AddSingleton<Practicums.PracticumChatConnectionManager>();
        context.Services.AddTransient<TeachingAgents.TeachingAgentContextBuilder>();
        context.Services.AddTransient<IOfficeConversionService, GotenbergConversionService>();
    }
}
