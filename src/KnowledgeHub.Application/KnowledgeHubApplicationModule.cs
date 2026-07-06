using KnowledgeHub.Resources;
using KnowledgeHub.EntityFrameworkCore;
using KnowledgeHub.Application.Search;
using KnowledgeHub.Application.Contracts.Search;
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
        context.Services.AddSingleton<NpoiDocumentParserService>();
        context.Services.AddSingleton<OpenDataLoaderService>();
        context.Services.AddSingleton<PdfTextExtractorService>();
        context.Services.AddSingleton<IDocumentExtractionService>(sp =>
        {
            var npoi = sp.GetRequiredService<NpoiDocumentParserService>();
            var pdf = sp.GetRequiredService<PdfTextExtractorService>();
            return new CompositeDocumentExtractionService(npoi, pdf);
        });
        context.Services.AddSingleton<Practicums.PracticumChatConnectionManager>();
        context.Services.AddTransient<TeachingAgents.TeachingAgentContextBuilder>();
    }
}
